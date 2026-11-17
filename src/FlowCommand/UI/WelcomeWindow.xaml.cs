using System;
using System.Threading.Tasks;
using System.Windows;
using FlowCommand.Core.Ai;
using FlowCommand.Platform;

namespace FlowCommand.UI
{
    /// <summary>First-run screen: what FlowCommand is, how to use it, and the few choices that matter. Everything here can be changed later in Settings.</summary>
    public partial class WelcomeWindow : Window
    {
        private const string FallbackHotkey = "Ctrl+Alt+Space";

        private readonly ServiceHost _host;
        private readonly Func<string?> _applyTriggers;
        private readonly Action _openSettings;
        private bool _finished;

        internal WelcomeWindow(ServiceHost host, Func<string?> applyTriggers, Action openSettings)
        {
            _host = host;
            _applyTriggers = applyTriggers;
            _openSettings = openSettings;
            InitializeComponent();

            var c = host.Config;
            DoubleTapBox.IsChecked = c.DoubleTapEnabled;
            OnlineRadio.IsChecked = ProviderCatalog.UsesApiKey(c.Provider);
            OfflineRadio.IsChecked = OnlineRadio.IsChecked != true;
            WisprStatus.Text = "Checking whether Wispr Flow is running...";

            Loaded += async (s, e) =>
            {
                var wispr = await Task.Run(() => WisprFlowDetector.IsRunning());
                WisprStatus.Text = wispr
                    ? "Wispr Flow appears to be running on this PC."
                    : "Wispr Flow was not detected. That is fine: FlowCommand works with any way of entering text.";
                StartupBox.IsChecked = await StartupRegistration.IsEnabledAsync();
            };
        }

        private async void OnGetStarted(object sender, RoutedEventArgs e)
        {
            StartButton.IsEnabled = false;
            MoreButton.IsEnabled = false;
            await Apply(openSettingsAfter: OnlineRadio.IsChecked == true);
        }

        private async void OnMoreSettings(object sender, RoutedEventArgs e)
        {
            StartButton.IsEnabled = false;
            MoreButton.IsEnabled = false;
            await Apply(openSettingsAfter: true);
        }

        private async Task Apply(bool openSettingsAfter)
        {
            var c = _host.Config;
            c.DoubleTapEnabled = DoubleTapBox.IsChecked == true;
            if (!c.DoubleTapEnabled && string.IsNullOrWhiteSpace(c.Hotkey)) c.Hotkey = FallbackHotkey;

            var wantsOnline = OnlineRadio.IsChecked == true;
            if (wantsOnline && !ProviderCatalog.UsesApiKey(c.Provider))
            {
                c.Provider = ProviderCatalog.Gemini;
                c.Model = ProviderCatalog.DefaultModel(ProviderCatalog.Gemini);
            }
            else if (!wantsOnline)
            {
                c.Provider = ProviderCatalog.Local;
                c.Model = ProviderCatalog.DefaultModel(ProviderCatalog.Local);
            }
            c.FirstRunCompleted = true;

            var startupMessage = await StartupRegistration.SetAsync(StartupBox.IsChecked == true);
            try { _host.SaveConfig(); }
            catch (Exception ex)
            {
                MessageBox.Show("Couldn't save your choices: " + ex.Message, "FlowCommand");
                StartButton.IsEnabled = true;
                MoreButton.IsEnabled = true;
                return;
            }

            var problem = _applyTriggers();
            _finished = true;
            if (startupMessage != null) MessageBox.Show(startupMessage, "FlowCommand");
            if (problem != null) MessageBox.Show(problem, "FlowCommand");
            if (openSettingsAfter) _openSettings();
            Close();
        }

        protected override void OnClosed(EventArgs e)
        {
            // Closing the window without choosing keeps the defaults but never shows this screen again.
            if (!_finished)
            {
                try
                {
                    _host.Config.FirstRunCompleted = true;
                    _host.SaveConfig();
                }
                catch (Exception) { }
            }
            base.OnClosed(e);
        }
    }
}
