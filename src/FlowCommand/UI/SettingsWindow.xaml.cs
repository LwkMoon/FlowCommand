using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using FlowCommand.Core.Actions;
using FlowCommand.Core.Ai;
using FlowCommand.Core.Config;
using FlowCommand.Core.Util;
using FlowCommand.Platform;

namespace FlowCommand.UI
{
    public partial class SettingsWindow : Window
    {
        private readonly ServiceHost _host;
        private readonly Func<string?> _applyTriggers;
        private readonly Dictionary<string, CheckBox> _actionBoxes = new Dictionary<string, CheckBox>();
        private string _currentProvider = ProviderCatalog.Local;
        private bool _loading;

        internal SettingsWindow(ServiceHost host, Func<string?> applyTriggers)
        {
            _host = host;
            _applyTriggers = applyTriggers;
            InitializeComponent();
            Load();
        }

        private string SelectedProvider() =>
            (ProviderBox.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? ProviderCatalog.Local;

        private void Load()
        {
            _loading = true;
            var c = _host.Config;

            var idx = ProviderCatalog.Names.ToList().IndexOf(ProviderCatalog.Normalize(c.Provider));
            _currentProvider = idx >= 0 ? ProviderCatalog.Names[idx] : ProviderCatalog.Local;
            ProviderBox.SelectedIndex = idx >= 0 ? idx : 0;
            ModelBox.Text = c.Model;

            foreach (var k in TriggerKeys.All)
                DoubleTapKeyBox.Items.Add(new ComboBoxItem { Content = k.Display, Tag = k.Id });
            var selected = (TriggerKeys.Parse(c.DoubleTapKey) ?? TriggerKeys.Default).Id;
            DoubleTapKeyBox.SelectedIndex = Math.Max(0, TriggerKeys.All.ToList().FindIndex(k => k.Id == selected));
            DoubleTapBox.IsChecked = c.DoubleTapEnabled;
            IgnoreTextBox.IsChecked = c.IgnoreWhenTextBoxFocused;
            UnknownBox.IsChecked = c.OpenWhenFocusUnknown;
            HotkeyBox.Text = c.Hotkey;
            ModeBox.SelectedIndex = c.AutoSubmitMode == "idle" ? 1 : c.AutoSubmitMode == "off" ? 2 : 0;
            DelayBox.Text = c.AutoSubmitDelayMs.ToString();
            DryRunBox.IsChecked = c.DryRun;
            ConfirmModerateBox.IsChecked = c.ConfirmModerateActions;
            ConfirmAllBox.IsChecked = c.ConfirmAllActions;
            ContextBox.IsChecked = c.UseConversationContext;
            ReclaimBox.IsChecked = c.ReclaimFocusAfterAction;
            LogTranscriptsBox.IsChecked = c.LogTranscripts;
            Loaded += async (s, e) =>
            {
                StartupBox.IsChecked = await StartupRegistration.IsEnabledAsync();
                WisprStatus.Text = await Task.Run(() => WisprFlowDetector.IsRunning())
                    ? "Wispr Flow appears to be running on this PC."
                    : "Wispr Flow was not detected. That is fine: typing, Windows voice typing or any other dictation tool works too.";
            };
            WisprStatus.Text = "Checking...";
            var version = Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "";
            AboutText.Text = $"FlowCommand {version}. Free, open source (MIT licence). Not affiliated with Wispr, Google, Anthropic or Microsoft. " +
                "FlowCommand has no ads and no telemetry. Offline mode sends nothing anywhere; online AI modes send the text of each command to the provider you chose.";
            DataLocationText.Text = "Your settings and saved keys are stored at: " + AppPaths.ConfigDir +
                (PackageIdentity.IsPackaged ? " (removed automatically when you uninstall FlowCommand)." : ". Delete that folder to remove them when uninstalling.");

            foreach (var a in ActionCatalog.All.Where(x => !x.Meta))
            {
                var cb = new CheckBox
                {
                    Content = $"{a.Name}  [{a.Risk}]  - {a.Description}",
                    IsChecked = c.EnabledActions.Contains(a.Name),
                    Margin = new Thickness(0, 2, 0, 2),
                    ToolTip = a.Description,
                };
                _actionBoxes[a.Name] = cb;
                ActionsPanel.Children.Add(cb);
            }
            UpdateProviderNote();
            RefreshKeyState();
            _loading = false;
        }

        private void OnProviderChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!IsInitialized || ModelBox == null) return;
            var next = SelectedProvider();
            if (next == _currentProvider) return;

            if (!_loading)
            {
                var oldDefault = ProviderCatalog.DefaultModel(_currentProvider);
                if (string.IsNullOrWhiteSpace(ModelBox.Text) || ModelBox.Text.Trim() == oldDefault)
                    ModelBox.Text = ProviderCatalog.DefaultModel(next);
            }
            _currentProvider = next;
            KeyBox.Clear();
            TestResult.Text = "";
            UpdateProviderNote();
            RefreshKeyState();
        }

        private void UpdateProviderNote()
        {
            OnlineAiPanel.Visibility = ProviderCatalog.UsesApiKey(_currentProvider) ? Visibility.Visible : Visibility.Collapsed;
            if (_currentProvider == ProviderCatalog.Local)
            {
                ProviderNote.Text = "Offline mode understands common commands (open apps, folders, websites and Settings pages, volume and media keys, screenshots, closing or switching windows) using rules on this PC. " +
                    "Nothing is sent anywhere and no account is needed. For free-form wording, choose Google Gemini or Anthropic Claude and add your own API key.";
                return;
            }
            ProviderNote.Text = _currentProvider == ProviderCatalog.Gemini
                ? "Create a key in Google AI Studio (aistudio.google.com). Privacy note: under Google's Gemini API terms, content sent on the free (unpaid) tier may be used to improve Google's products and may be reviewed by people; paid use with billing enabled is not. Don't send sensitive or personal information on the free tier, and check Google's current terms. Only your dictated text and a few recent commands are sent, never audio."
                : "Anthropic API use is billed per use and is separate from a Claude.ai subscription. Create a key at console.anthropic.com. Only your dictated text and a few recent commands are sent, never audio.";
        }

        private void RefreshKeyState()
        {
            var envVars = ProviderCatalog.EnvVars(_currentProvider);
            var envName = envVars.FirstOrDefault(v => !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(v)));
            var label = ProviderCatalog.DisplayName(_currentProvider);

            if (_host.Secrets.Has(_currentProvider))
                KeyState.Text = $"A {label} key is saved (encrypted with Windows DPAPI for your account). Type here only to replace it.";
            else if (envName != null)
                KeyState.Text = $"Using the {envName} environment variable. Type a key here to store one securely instead.";
            else
                KeyState.Text = $"No {label} key yet. Paste one above and press Save.";
        }

        private void OnRemoveKey(object sender, RoutedEventArgs e)
        {
            if (!ProviderCatalog.UsesApiKey(_currentProvider)) return;
            _host.Secrets.Delete(_currentProvider);
            KeyBox.Clear();
            RefreshKeyState();
            ShowTest("Saved key removed.", false);
        }

        private async void OnTest(object sender, RoutedEventArgs e)
        {
            TestButton.IsEnabled = false;
            ShowTest(SelectedProvider() == ProviderCatalog.Local ? "Checking..." : "Contacting the AI...", null);
            try
            {
                var provider = SelectedProvider();
                var typed = KeyBox.Password.Trim();
                Func<string?> key = () => typed.Length > 0 ? typed : _host.GetApiKey(provider);

                var temp = _host.Config.Clone();
                temp.Provider = provider;
                temp.Model = string.IsNullOrWhiteSpace(ModelBox.Text) ? ProviderCatalog.DefaultModel(provider) : ModelBox.Text.Trim();

                const string sample = "open my downloads folder";
                var ai = AiProviderFactory.Create(temp, ServiceHost.Http, key);
                var req = new AiRequest(temp.Model, PromptBuilder.BuildSystemPrompt(temp),
                    PromptBuilder.BuildUserMessage(sample, Array.Empty<Core.Pipeline.ConversationTurn>()),
                    PromptBuilder.BuildTool(temp), CommandText: sample);

                var raw = await ai.GetPlanJsonAsync(req, CancellationToken.None);
                var v = new PlanValidator(temp.EnabledActions).Validate(raw);
                ShowTest(v.Ok
                    ? $"Works. \u201C{sample}\u201D \u2192 {v.Plan!.Describe()}. Nothing was executed."
                    : $"The AI answered, but the answer failed validation: {v.Error}", v.Ok);
            }
            catch (AiProviderException ex) { ShowTest(ex.Message, false); }
            catch (Exception ex) { ShowTest("Test failed: " + ex.Message, false); }
            finally { TestButton.IsEnabled = true; }
        }

        private void ShowTest(string text, bool? ok)
        {
            TestResult.Text = text;
            TestResult.Foreground = ok == true ? Brushes.ForestGreen : ok == false ? Brushes.Firebrick : Brushes.Gray;
        }

        private void OnOpenConfig(object sender, RoutedEventArgs e)
        {
            try { Process.Start(new ProcessStartInfo(AppPaths.ConfigFile) { UseShellExecute = true }); }
            catch { MessageBox.Show("Couldn't open " + AppPaths.ConfigFile, "FlowCommand"); }
        }

        private static void OpenUrl(string url)
        {
            try { Process.Start(new ProcessStartInfo(url) { UseShellExecute = true }); }
            catch { MessageBox.Show("Couldn't open " + url, "FlowCommand"); }
        }

        private void OnPrivacy(object sender, RoutedEventArgs e) => OpenUrl(AppLinks.Privacy);
        private void OnSupport(object sender, RoutedEventArgs e) => OpenUrl(AppLinks.Support);

        private void OnOpenLogs(object sender, RoutedEventArgs e)
        {
            try
            {
                System.IO.Directory.CreateDirectory(AppPaths.LogDir);
                Process.Start(new ProcessStartInfo(AppPaths.LogDir) { UseShellExecute = true });
            }
            catch { MessageBox.Show("Couldn't open " + AppPaths.LogDir, "FlowCommand"); }
        }

        private async void OnDeleteData(object sender, RoutedEventArgs e)
        {
            var answer = MessageBox.Show(
                "This removes your FlowCommand settings, saved API keys and log from this PC, and turns off start-with-Windows. " +
                "FlowCommand keeps running with default settings and shows its welcome screen the next time it starts.\n\nContinue?",
                "Delete all my FlowCommand data", MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No);
            if (answer != MessageBoxResult.Yes) return;

            try
            {
                await StartupRegistration.SetAsync(false);
                _host.DeleteAllData();
                _applyTriggers();
                MessageBox.Show("Your FlowCommand data was removed.", "FlowCommand");
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Couldn't remove everything: " + ex.Message, "FlowCommand");
            }
        }

        private void OnCancel(object sender, RoutedEventArgs e) => Close();

        private async void OnSave(object sender, RoutedEventArgs e)
        {
            var hotkeyText = HotkeyBox.Text.Trim();
            string hotkeyDisplay = "";
            if (hotkeyText.Length > 0)
            {
                var hk = HotkeyParser.Parse(hotkeyText);
                if (!hk.Ok) { MessageBox.Show(hk.Error, "Extra hotkey"); return; }
                hotkeyDisplay = hk.Hotkey!.Display;
            }
            if (!int.TryParse(DelayBox.Text, out var delay)) { MessageBox.Show("Delay must be a number of milliseconds.", "Delay"); return; }
            if (DoubleTapBox.IsChecked != true && hotkeyDisplay.Length == 0)
            {
                MessageBox.Show("Turn on the key double-tap or set an extra hotkey, otherwise there's no way to open the palette (other than the tray icon).", "No trigger");
                return;
            }

            var c = _host.Config;
            var provider = SelectedProvider();
            c.Provider = provider;
            c.Model = string.IsNullOrWhiteSpace(ModelBox.Text) ? ProviderCatalog.DefaultModel(provider) : ModelBox.Text.Trim();
            c.DoubleTapEnabled = DoubleTapBox.IsChecked == true;
            c.DoubleTapKey = (DoubleTapKeyBox.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? TriggerKeys.Default.Id;
            c.IgnoreWhenTextBoxFocused = IgnoreTextBox.IsChecked == true;
            c.OpenWhenFocusUnknown = UnknownBox.IsChecked == true;
            c.Hotkey = hotkeyDisplay;
            c.AutoSubmitMode = ((ComboBoxItem)ModeBox.SelectedItem).Content.ToString() ?? "paste";
            c.AutoSubmitDelayMs = delay;
            c.DryRun = DryRunBox.IsChecked == true;
            c.ConfirmModerateActions = ConfirmModerateBox.IsChecked == true;
            c.ConfirmAllActions = ConfirmAllBox.IsChecked == true;
            c.UseConversationContext = ContextBox.IsChecked == true;
            c.ReclaimFocusAfterAction = ReclaimBox.IsChecked == true;
            c.LogTranscripts = LogTranscriptsBox.IsChecked == true;
            c.EnabledActions = _actionBoxes.Where(kv => kv.Value.IsChecked == true).Select(kv => kv.Key).ToList();

            try
            {
                if (ProviderCatalog.UsesApiKey(provider) && KeyBox.Password.Trim().Length > 0)
                    _host.Secrets.Set(provider, KeyBox.Password.Trim());
                _host.SaveConfig();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Couldn't save: " + ex.Message, "FlowCommand");
                return;
            }

            var startupMessage = await StartupRegistration.SetAsync(StartupBox.IsChecked == true);
            if (startupMessage != null) MessageBox.Show(startupMessage, "FlowCommand");

            var problem = _applyTriggers();
            if (problem != null) MessageBox.Show(problem + "\n\nYour other settings were saved.", "Trigger problem");
            Close();
        }
    }
}
