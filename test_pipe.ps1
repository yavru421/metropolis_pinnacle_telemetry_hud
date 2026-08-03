try {
    $pipe = New-Object System.IO.Pipes.NamedPipeClientStream(".", "MetropolisHUDPipe", [System.IO.Pipes.PipeDirection]::Out)
    $pipe.Connect(2000)
    $writer = New-Object System.IO.StreamWriter($pipe)
    $writer.AutoFlush = $true
    
    $payloads = @(
        '{"channel":"MCP","detail":"[IPC TEST] Sub-millisecond NamedPipe signal stream packet 1","timestamp":"19:33:45"}',
        '{"channel":"SKILLS","detail":"[IPC TEST] Skill dispatch telemetry packet 2","timestamp":"19:33:46"}',
        '{"channel":"THOUGHT","detail":"[IPC TEST] SequentialThinking thought trace packet 3","timestamp":"19:33:47"}'
    )

    foreach ($p in $payloads) {
        $writer.WriteLine($p)
        Write-Host "[PIPE TEST SUCCESS] Emitted: $p"
        Start-Sleep -Milliseconds 200
    }

    $writer.Close()
    $pipe.Close()
} catch {
    Write-Host "[PIPE TEST ERROR] $($_.Exception.Message)"
}
