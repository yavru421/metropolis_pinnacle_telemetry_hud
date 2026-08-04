"""
db_session.py — Unified Serverless DuckDB In-Process Connection Factory.

Architecture:
1. Purely serverless, in-process embedded DuckDB architecture. Zero network servers or daemons.
2. Primary connection is strictly in-process (:memory:).
3. Base databases (agent_memory, mind, st_codex) are attached READ_ONLY locally with exponential retry backoff.
4. Decoupled Live JSONL Engine: excavator.ssr and excavator.intel_signals views stream telemetry
   on-the-fly directly from append-only flat files (out/*.jsonl) using read_json_auto, guaranteeing 100% lock-free reads.
5. Ephemeral Writes: execute_ephemeral_write() manages short-lived Open -> Write -> Commit -> Close lifecycles.
"""

import os
import sys
import glob
import time
import logging
import duckdb

logger = logging.getLogger(__name__)

EXCAVATOR_JSONL_GLOB = r'C:/Users/John/Pictures/Screenshots/screenshot_excavator/out/*.jsonl'
EXCAVATOR_DB  = r'C:\Users\John\Pictures\Screenshots\screenshot_excavator\out\excavator.duckdb'
ST_CODEX      = r'C:\Users\John\.gemini\config\st_codex.duckdb'
AGENT_MEM     = r'C:\Users\John\.gemini\config\agent_memory.duckdb'
MIND_DB       = r'C:\Users\John\.gemini\config\mind.duckdb'

DATABASES = [
    ('st_codex', ST_CODEX),
    ('agent_memory', AGENT_MEM),
    ('mind', MIND_DB),
]

import json

def emit_hud_signal(channel: str, detail: str) -> None:
    signal_file = r"C:\Users\John\.gemini\config\hud_signal.json"
    history_file = r"C:\Users\John\.gemini\config\hud_history.log"
    time_str = time.strftime("%H:%M:%S")
    payload = {
        "timestamp": time_str,
        "channel": channel.upper(),
        "detail": detail
    }
    try:
        with open(signal_file, "w", encoding="utf-8") as f:
            json.dump(payload, f, indent=2)
        with open(history_file, "a", encoding="utf-8") as f:
            f.write(f"[{time_str}] {channel.upper()}: {detail}\n")
    except Exception:
        pass

    try:
        stmt = "INSERT INTO telemetry_events (event_type, event_data, source) VALUES (?, ?, ?);"
        execute_ephemeral_write(MIND_DB, [stmt], [[channel.upper(), detail, "hud_emitter"]])
    except Exception:
        pass


def _attach_with_retry(con: duckdb.DuckDBPyConnection, db_path: str, alias: str, max_retries: int = 8, initial_delay: float = 0.05) -> bool:
    """
    Attempts to ATTACH a database file as READ_ONLY with exponential backoff retries.
    Handles lock contention during parallel agent queries.
    """
    if not os.path.exists(db_path):
        return False

    safe_path = db_path.replace('\\', '/')
    delay = initial_delay

    for attempt in range(1, max_retries + 1):
        try:
            con.execute(f"ATTACH '{safe_path}' AS {alias} (READ_ONLY)")
            return True
        except Exception as err:
            err_str = str(err).lower()
            if ("lock" in err_str or "could not set lock" in err_str or "permission" in err_str or "busy" in err_str) and attempt < max_retries:
                time.sleep(delay)
                delay = min(delay * 2, 1.6)
            else:
                logger.warning(f"ATTACH {alias} failed on attempt {attempt}: {err}")
                return False
    return False


def setup_jsonl_excavator_view(con: duckdb.DuckDBPyConnection) -> None:
    """
    Constructs in-memory excavator schema and live streaming views over flat append-only JSONL files.
    """
    try:
        con.execute("CREATE SCHEMA IF NOT EXISTS excavator;")
        jsonl_files = glob.glob(EXCAVATOR_JSONL_GLOB)
        if jsonl_files:
            con.execute(f"""
                CREATE OR REPLACE VIEW excavator.ssr AS 
                SELECT * FROM read_json_auto('{EXCAVATOR_JSONL_GLOB}')
                WHERE lower(COALESCE(content.raw_ocr, '')) NOT LIKE '%intruder%'
                  AND lower(COALESCE(content.raw_ocr, '')) NOT LIKE '%repeater%'
                  AND lower(COALESCE(content.raw_ocr, '')) NOT LIKE '%burp%'
                  AND lower(COALESCE(content.raw_ocr, '')) NOT LIKE '%openclaw%'
                  AND lower(COALESCE(content.raw_ocr, '')) NOT LIKE '%fuzz%';
            """)
            con.execute("""
                CREATE OR REPLACE VIEW excavator.intel_signals AS
                SELECT 
                    identity.source_file,
                    time.fs_timestamp,
                    signals.intelligence_density,
                    context.primary_category,
                    content.raw_ocr
                FROM excavator.ssr
                ORDER BY signals.intelligence_density DESC;
            """)
        else:
            con.execute("""
                CREATE TABLE IF NOT EXISTS excavator.ssr (
                    identity STRUCT(source_file VARCHAR, sha256 VARCHAR, phash VARCHAR),
                    time STRUCT(fs_timestamp TIMESTAMP),
                    content STRUCT(raw_ocr VARCHAR, normalized_ocr VARCHAR),
                    context STRUCT(primary_category VARCHAR),
                    signals STRUCT(intelligence_density DOUBLE)
                );
                CREATE VIEW IF NOT EXISTS excavator.intel_signals AS SELECT * FROM excavator.ssr;
            """)
    except Exception as e:
        logger.error(f"Failed to setup live JSONL excavator view: {e}")


def init_all_schemas(max_retries: int = 5, initial_delay: float = 0.05) -> None:
    """
    Ensures mandatory tables exist across databases (mind, agent_memory, st_codex).
    Creates tables via zero-lock execute_ephemeral_write:
    - mind.duckdb: corrections, telemetry_events, solutions
    - agent_memory.duckdb: transcripts
    - st_codex.duckdb: codex_files, codex_sections
    """
    # 1. mind.duckdb
    mind_stmts = [
        """
        CREATE TABLE IF NOT EXISTS corrections (
            id VARCHAR DEFAULT (uuid()),
            timestamp TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
            correction_text VARCHAR,
            original_prompt VARCHAR,
            execution_output VARCHAR,
            metadata VARCHAR
        );
        """,
        """
        CREATE TABLE IF NOT EXISTS telemetry_events (
            id VARCHAR DEFAULT (uuid()),
            timestamp TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
            event_type VARCHAR,
            event_data VARCHAR,
            source VARCHAR,
            metadata VARCHAR
        );
        """,
        """
        CREATE TABLE IF NOT EXISTS solutions (
            id VARCHAR DEFAULT (uuid()),
            timestamp TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
            task_description VARCHAR,
            solution_summary VARCHAR,
            code_diff VARCHAR,
            files_modified VARCHAR,
            metadata VARCHAR
        );
        """,
        """
        CREATE TABLE IF NOT EXISTS slash_command_events (
            id VARCHAR DEFAULT (uuid()),
            timestamp TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
            conversation_id VARCHAR,
            slash_command VARCHAR,
            raw_prompt VARCHAR,
            outcome_status VARCHAR,
            execution_duration_ms INTEGER,
            metadata VARCHAR
        );
        """,
        """
        CREATE VIEW IF NOT EXISTS v_slash_command_audit AS
        SELECT s.timestamp, s.slash_command, s.conversation_id, s.outcome_status, s.raw_prompt
        FROM slash_command_events s
        ORDER BY s.timestamp DESC;
        """,
        """
        CREATE VIEW IF NOT EXISTS v_unified_timeline AS
        SELECT ts AS timestamp, activity_type, target, domain FROM (
            SELECT
                sol.timestamp                             AS ts,
                'solution'                                AS activity_type,
                sol.solution_summary                      AS target,
                sol.domain                                AS domain
            FROM mind.solutions sol
            UNION ALL
            SELECT
                c.timestamp                               AS ts,
                'correction'                              AS activity_type,
                c.original_prompt                         AS target,
                'cognitive_mind'                          AS domain
            FROM mind.corrections c
            UNION ALL
            SELECT
                sc.timestamp                              AS ts,
                'slash_command'                           AS activity_type,
                sc.slash_command || ' | ' || sc.raw_prompt AS target,
                'telemetry'                               AS domain
            FROM mind.slash_command_events sc
        )
        ORDER BY timestamp DESC;
        """
    ]
    try:
        execute_ephemeral_write(MIND_DB, mind_stmts, max_retries=max_retries, initial_delay=initial_delay)
    except Exception as e:
        logger.warning(f"Failed to auto-initialize mind.duckdb schema: {e}")

    # 2. agent_memory.duckdb
    agent_mem_stmts = [
        """
        CREATE TABLE IF NOT EXISTS transcripts (
            id VARCHAR DEFAULT (uuid()),
            timestamp TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
            conversation_id VARCHAR,
            step_index INTEGER,
            role VARCHAR,
            content VARCHAR,
            metadata VARCHAR
        );
        """
    ]
    try:
        execute_ephemeral_write(AGENT_MEM, agent_mem_stmts, max_retries=max_retries, initial_delay=initial_delay)
    except Exception as e:
        logger.warning(f"Failed to auto-initialize agent_memory.duckdb schema: {e}")

    # 3. st_codex.duckdb
    st_codex_stmts = [
        """
        CREATE TABLE IF NOT EXISTS codex_files (
            filename VARCHAR PRIMARY KEY,
            last_modified TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
            file_hash VARCHAR,
            content VARCHAR
        );
        """,
        """
        CREATE TABLE IF NOT EXISTS codex_sections (
            id VARCHAR DEFAULT (uuid()),
            filename VARCHAR,
            header VARCHAR,
            content VARCHAR,
            last_modified TIMESTAMP DEFAULT CURRENT_TIMESTAMP
        );
        """
    ]
    try:
        execute_ephemeral_write(ST_CODEX, st_codex_stmts, max_retries=max_retries, initial_delay=initial_delay)
    except Exception as e:
        logger.warning(f"Failed to auto-initialize st_codex.duckdb schema: {e}")


def get_unified_connection(max_retries: int = 5, initial_delay: float = 0.05) -> duckdb.DuckDBPyConnection:
    """
    Returns an in-process in-memory DuckDB connection with attached READ_ONLY databases
    and on-the-fly flat file JSONL streaming view for OCR signals.
    Auto-initializes missing schemas prior to connection.
    """
    init_all_schemas(max_retries=max_retries, initial_delay=initial_delay)

    con = duckdb.connect(':memory:')

    # Attach base databases READ_ONLY
    for alias, db_path in DATABASES:
        _attach_with_retry(con, db_path, alias, max_retries=max_retries, initial_delay=initial_delay)

    # Attach excavator: try READ_ONLY attachment, fallback to live JSONL streaming view
    attached_excavator = _attach_with_retry(con, EXCAVATOR_DB, 'excavator', max_retries=1, initial_delay=0.01)
    if not attached_excavator:
        setup_jsonl_excavator_view(con)

    return con


def execute_ephemeral_write(db_path: str, statements: list, params_list: list = None, max_retries: int = 5, initial_delay: float = 0.05) -> bool:
    """
    Manages an ephemeral write lifecycle on a target DuckDB file:
    Connect -> BEGIN TRANSACTION -> Execute -> COMMIT -> Immediate Close.
    Retries automatically with exponential backoff if transient lock collisions occur.
    """
    safe_path = db_path.replace('\\', '/')
    delay = initial_delay

    if isinstance(statements, str):
        statements = [statements]
    if params_list is None:
        params_list = [None] * len(statements)
    elif not isinstance(params_list, list):
        params_list = [params_list]

    for attempt in range(1, max_retries + 1):
        con = None
        try:
            con = duckdb.connect(safe_path)
            con.execute("BEGIN TRANSACTION;")
            for stmt, params in zip(statements, params_list):
                if params is not None:
                    con.execute(stmt, params)
                else:
                    con.execute(stmt)
            con.execute("COMMIT;")
            return True
        except Exception as err:
            err_str = str(err).lower()
            if con:
                try:
                    con.execute("ROLLBACK;")
                except Exception:
                    pass
            if attempt < max_retries and ("lock" in err_str or "could not set lock" in err_str or "permission" in err_str or "write" in err_str):
                time.sleep(delay)
                delay *= 2
            else:
                logger.error(f"Ephemeral write on {db_path} failed after {max_retries} attempts: {err}")
                raise err
        finally:
            if con:
                try:
                    con.close()
                except Exception:
                    pass
    return False


# Backwards-compatibility alias
execute_micro_transaction = execute_ephemeral_write


# --- Mind DB Corrections Functions ---

def init_mind_corrections_schema(db_path: str = MIND_DB) -> bool:
    """
    Ensures the corrections table exists in mind.duckdb.
    """
    init_all_schemas()
    return True


def record_correction(correction_text: str, original_prompt: str, execution_output: str, metadata: str = None, db_path: str = MIND_DB) -> bool:
    """
    Persists a /correct re-execution event into mind.duckdb using lock-resilient ephemeral write.
    """
    stmt = """
    INSERT INTO corrections (correction_text, original_prompt, execution_output, metadata)
    VALUES (?, ?, ?, ?);
    """
    return execute_ephemeral_write(db_path, [stmt], [[correction_text, original_prompt, execution_output, metadata]])


def get_recent_corrections(limit: int = 10, db_path: str = MIND_DB) -> list:
    """
    Queries recent /correct re-execution events from mind.duckdb.
    """
    con = duckdb.connect(db_path, read_only=True)
    try:
        res = con.execute("SELECT timestamp, correction_text, original_prompt, execution_output FROM corrections ORDER BY timestamp DESC LIMIT ?", [limit]).fetchall()
        return res
    except Exception as e:
        logger.warning(f"Failed to query corrections: {e}")
        return []
    finally:
        con.close()


# --- New Ephemeral Write Logging Helpers ---

def log_session_output(conversation_id: str, step_index: int, role: str, content: str, metadata: str = None, db_path: str = AGENT_MEM) -> bool:
    """
    Logs session output text into agent_memory.transcripts using zero-lock execute_ephemeral_write.
    """
    stmt = """
    INSERT INTO transcripts (conversation_id, step_index, role, content, metadata)
    VALUES (?, ?, ?, ?, ?);
    """
    return execute_ephemeral_write(db_path, [stmt], [[conversation_id, step_index, role, content, metadata]])


def log_prompt_history(original_prompt: str, correction_text: str = None, execution_output: str = None, metadata: str = None, db_path: str = MIND_DB) -> bool:
    """
    Logs user prompt history and corrections into mind.corrections using zero-lock execute_ephemeral_write.
    """
    emit_hud_signal("DUCKDB", f"mind.corrections write: {(original_prompt or '')[:40]}")
    stmt = """
    INSERT INTO corrections (correction_text, original_prompt, execution_output, metadata)
    VALUES (?, ?, ?, ?);
    """
    return execute_ephemeral_write(db_path, [stmt], [[correction_text, original_prompt, execution_output, metadata]])


def log_execution_metrics(event_type: str, event_data: str, source: str = "agent", metadata: str = None, db_path: str = MIND_DB) -> bool:
    """
    Logs execution metrics and telemetry events into mind.telemetry_events using zero-lock execute_ephemeral_write.
    """
    emit_hud_signal("DUCKDB", f"mind.telemetry {event_type}: {(event_data or '')[:40]}")
    stmt = """
    INSERT INTO telemetry_events (event_type, event_data, source, metadata)
    VALUES (?, ?, ?, ?);
    """
    return execute_ephemeral_write(db_path, [stmt], [[event_type, event_data, source, metadata]])


def log_slash_command(slash_command: str, conversation_id: str = None, raw_prompt: str = None, outcome_status: str = "COMPLETED", execution_duration_ms: int = None, metadata: str = None, db_path: str = MIND_DB) -> bool:
    """
    Logs slash command invocations into mind.slash_command_events using zero-lock execute_ephemeral_write.
    """
    stmt = """
    INSERT INTO slash_command_events (slash_command, conversation_id, raw_prompt, outcome_status, execution_duration_ms, metadata)
    VALUES (?, ?, ?, ?, ?, ?);
    """
    return execute_ephemeral_write(db_path, [stmt], [[slash_command, conversation_id, raw_prompt, outcome_status, execution_duration_ms, metadata]])


# --- Convenience query templates ---

RECENT_SCREENSHOTS_SQL = """
    SELECT
        s.time.fs_timestamp                       AS ts,
        s.context.primary_category                AS domain,
        s.analysis.image_class                    AS img_class,
        SUBSTR(COALESCE(s.content.normalized_ocr, ''), 1, 100) AS ocr_preview
    FROM excavator.ssr s
    WHERE s.time.fs_timestamp >= CURRENT_TIMESTAMP - INTERVAL '{hours} HOURS'
    ORDER BY ts DESC
    LIMIT {limit}
"""

RECENT_CODEX_SQL = """
    SELECT
        f.last_modified   AS ts,
        f.filename        AS file,
        s.header          AS section
    FROM st_codex.codex_files f
    JOIN st_codex.codex_sections s USING (filename)
    WHERE f.last_modified >= CURRENT_TIMESTAMP - INTERVAL '{hours} HOURS'
    ORDER BY ts DESC
    LIMIT {limit}
"""

UNIFIED_TIMELINE_SQL = """
    SELECT ts, activity_type, target, domain FROM (
        SELECT
            s.time.fs_timestamp                       AS ts,
            'screenshot'                              AS activity_type,
            s.identity.source_file                    AS target,
            s.context.primary_category                AS domain
        FROM excavator.ssr s
        WHERE s.time.fs_timestamp >= CURRENT_TIMESTAMP - INTERVAL '{hours} HOURS'
        UNION ALL
        SELECT
            f.last_modified                           AS ts,
            'codex_file'                              AS activity_type,
            f.filename || ' | ' || s.header          AS target,
            'documentation'                           AS domain
        FROM st_codex.codex_files f
        JOIN st_codex.codex_sections s USING (filename)
        WHERE f.last_modified >= CURRENT_TIMESTAMP - INTERVAL '{hours} HOURS'
    )
    ORDER BY ts DESC
    LIMIT {limit}
"""


import stat


def save_immutable_artifact(target_path: str, content: str) -> str:
    """
    Writes content to target_path and immediately enforces OS-level ReadOnly permissions (stat.S_IREAD).
    """
    os.makedirs(os.path.dirname(target_path), exist_ok=True)
    with open(target_path, 'w', encoding='utf-8') as f:
        f.write(content)
    try:
        os.chmod(target_path, stat.S_IREAD)
    except Exception:
        pass
    return target_path


if __name__ == '__main__':
    # Self-test
    con = get_unified_connection()
    print('=== Pure Serverless Connection Factory Self-Test ===')
    for table in [
        'excavator.ssr',
        'st_codex.codex_files',
        'agent_memory.transcripts',
        'mind.solutions',
        'mind.corrections',
        'mind.telemetry_events',
    ]:
        try:
            res = con.execute(f'SELECT COUNT(*) FROM {table}').fetchone()
            print(f'  {table}: {res[0]} rows')
        except Exception as e:
            print(f'  {table}: failed ({e})')
    con.close()

    print('=== Testing mind.corrections helper ===')
    record_correction("Test correction", "Test original prompt", "Test execution output")
    recs = get_recent_corrections(limit=5)
    print(f"Recorded {len(recs)} corrections. Latest: {recs[0] if recs else 'None'}")

    print('=== Testing ephemeral logging helpers ===')
    log_session_output("conv_123", 1, "user", "Test message")
    log_prompt_history("Test prompt", "Correction text")
    log_execution_metrics("test_metric", "200ms", source="test")
    print('OK')

