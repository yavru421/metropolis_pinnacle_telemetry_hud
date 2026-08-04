using System;
using System.Windows;

namespace MetropolisHUD
{
    public partial class SettingsWindow : Window
    {
        private readonly MainWindow _mainHud;

        public SettingsWindow(MainWindow mainHud)
        {
            InitializeComponent();
            _mainHud = mainHud;

            SliderSettingsBadgeSize.Value = _mainHud.CurrentBadgeFontSize;
            TxtSettingsBadgeSize.Text = $"{_mainHud.CurrentBadgeFontSize:F0}pt";
            ChkCollapseLogs.IsChecked = _mainHud.IsLogStreamCollapsed;
        }

        private void SliderSettingsBadgeSize_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (TxtSettingsBadgeSize == null) return;
            double newSize = e.NewValue;
            TxtSettingsBadgeSize.Text = $"{newSize:F0}pt";
            _mainHud?.SetBadgeFontSize(newSize);
        }

        private void ChkCollapseLogs_Changed(object sender, RoutedEventArgs e)
        {
            if (ChkCollapseLogs == null || _mainHud == null) return;
            _mainHud.SetLogStreamCollapsed(ChkCollapseLogs.IsChecked == true);
        }

        private void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            _mainHud?.SaveCurrentConfig();
            Close();
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
