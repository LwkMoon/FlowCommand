using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using FlowCommand.Core.Config;
using FlowCommand.Core.Util;
using FlowCommand.Platform;
using FlowCommand.UI;

namespace FlowCommand
{
    public partial class App : Application
    {
        private const string ActivateEventName = @"Local\FlowCommand.Activate";

        private Mutex? _mutex;
        private EventWaitHandle? _activate;
        private RegisteredWaitHandle? _activateWait;
        private bool _paused;
        private WelcomeWindow? _welcome;
        private ServiceHost? _host;
        private HotkeyService? _hotkeys;
        private DoubleTapKeyListener? _doubleTap;
        private TrayService? _tray;
        private PaletteWindow? _palette;
        private SettingsWindow? _settings;

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            _mutex = new Mutex(true, @"Local\FlowCommand.SingleInstance", out var created);
            if (!created)
            {
                // Already running (it lives in the tray): ask the running copy to show its window instead of failing.
                SignalRunningInstance();
                _mutex.Dispose();
                _mutex = null;
                Shutdown();
                return;
            }

            _activate = new EventWaitHandle(false, EventResetMode.AutoReset, ActivateEventName);
            _activateWait = ThreadPool.RegisterWaitForSingleObject(
                _activate, (state, timedOut) => Dispatcher.BeginInvoke(new Action(OnActivateRequested)), null, Timeout.Infinite, false);

            DispatcherUnhandledException += (s, args) =>
            {
                _host?.Log.Error("Unhandled UI exception", args.Exception);
                MessageBox.Show("Unexpected error: " + args.Exception.Message + "\nSee the log from the tray menu.", "FlowCommand");
                args.Handled = true;
            };
            AppDomain.CurrentDomain.UnhandledException += (s, args) =>
                _host?.Log.Error("Fatal exception", args.ExceptionObject as Exception);
            TaskScheduler.UnobservedTaskException += (s, args) =>
            {
                _host?.Log.Error("Unobserved task exception", args.Exception);
                args.SetObserved();
            };

            _host = new ServiceHost();
            _palette = new PaletteWindow(_host);
            _hotkeys = new HotkeyService();
            _hotkeys.Pressed += () => OnTrigger(false);
            _doubleTap = new DoubleTapKeyListener();
            _doubleTap.DoubleTapped += () => OnTrigger(true);
            _tray = new TrayService(_host, ShowPalette, ShowSettings, OnConfigReloaded, ExitApp, () => _paused, SetPaused);

            var problem = ApplyTriggers();

            if (_host.ConfigError != null)
                _tray.Notify("Config problem", _host.ConfigError + " Using defaults until fixed.");

            if (!_host.Config.FirstRunCompleted && _host.ConfigError == null)
            {
                ShowWelcome();      // first start: explain the product and let the person choose, before anything else
                return;
            }

            if (problem != null)
            {
                _tray.Notify("Trigger problem", problem);
                ShowSettings();
            }
            else if (!_host.IsReady())
            {
                ShowSettings();     // an online provider is selected but no key is saved yet
            }
            else
            {
                _tray.Notify("FlowCommand is running", $"To open the command palette: {_host.Config.TriggerDescription()}.");
            }
        }

        private static void SignalRunningInstance()
        {
            try
            {
                using (var ev = EventWaitHandle.OpenExisting(ActivateEventName)) ev.Set();
            }
            catch (Exception) { /* the first copy is still starting; nothing to do */ }
        }

        private void OnActivateRequested()
        {
            if (_host == null) return;
            if (_welcome != null) { _welcome.Activate(); return; }
            ShowSettings();
        }

        private void SetPaused(bool paused)
        {
            _paused = paused;
            var problem = ApplyTriggers();
            _tray?.SetPaused(paused);
            _tray?.Notify(
                paused ? "FlowCommand is paused" : "FlowCommand is listening again",
                paused
                    ? "It is not listening for the trigger key or hotkey. The tray icon still opens the palette."
                    : problem ?? $"To open the command palette: {_host!.Config.TriggerDescription()}.");
        }

        private void ShowWelcome()
        {
            if (_welcome != null) { _welcome.Activate(); return; }
            _welcome = new WelcomeWindow(_host!, ApplyTriggers, ShowSettings);
            _welcome.Closed += (s, e) => _welcome = null;
            _welcome.Show();
            _welcome.Activate();
        }

        /// <summary>(Re)applies the configured triggers. Returns a problem description, or null when all is well.</summary>
        private string? ApplyTriggers()
        {
            var cfg = _host!.Config;
            string? problem = null;

            _hotkeys!.Unregister();
            if (_paused)
            {
                _doubleTap!.Stop();          // paused: no keyboard listener and no hotkey are active at all
                return null;
            }
            if (!string.IsNullOrWhiteSpace(cfg.Hotkey))
            {
                var parsed = HotkeyParser.Parse(cfg.Hotkey);
                if (!parsed.Ok) problem = parsed.Error;
                else if (!_hotkeys.Register(parsed.Hotkey!)) problem = $"Couldn't register '{cfg.Hotkey}' (another app probably uses it).";
            }

            _doubleTap!.Stop();
            var key = TriggerKeys.Parse(cfg.DoubleTapKey) ?? TriggerKeys.Default;
            if (cfg.DoubleTapEnabled && !_doubleTap.Start(key, cfg.DoubleTapIntervalMs))
                problem = (problem == null ? "" : problem + " ") + $"Couldn't start the listener for the {key.Display} key.";

            if (!cfg.DoubleTapEnabled && string.IsNullOrWhiteSpace(cfg.Hotkey))
                problem ??= "No trigger is enabled. Turn on the double-tap or set a hotkey in Settings (the tray icon still works).";
            return problem;
        }

        private void OnConfigReloaded()
        {
            var problem = ApplyTriggers();
            _tray?.Notify("Config reloaded", problem ?? _host!.ConfigError ?? "Applied.");
        }

        /// <param name="fromDoubleTap">The key double-tap is gated by focus; the optional hotkey always opens the palette.</param>
        private async void OnTrigger(bool fromDoubleTap)
        {
            try
            {
                if (_palette == null || _host == null) return;

                // Palette already focused: the same trigger closes it, no questions asked.
                if (_palette.IsVisible && _palette.IsActive) { _palette.HidePalette(); return; }

                var cfg = _host.Config;
                if (fromDoubleTap && cfg.IgnoreWhenTextBoxFocused)
                {
                    var decision = await DetectFocusAsync(cfg);
                    if (decision.Kind == FocusKind.TextInput)
                    {
                        _host.Log.Info($"Double-tap ignored: text box focused ({decision.Reason}).");
                        return;
                    }
                    if (decision.Kind == FocusKind.Unknown && !cfg.OpenWhenFocusUnknown)
                    {
                        _host.Log.Info($"Double-tap ignored: focus unclear ({decision.Reason}).");
                        return;
                    }
                }
                _palette.ShowPalette();
            }
            catch (Exception ex)
            {
                _host?.Log.Error("Trigger handling failed", ex);
            }
        }

        private static async Task<FocusDecision> DetectFocusAsync(AppConfig cfg)
        {
            var names = cfg.WebContentProcesses.ToArray();
            var work = Task.Run(() => TextFocusDetector.Detect(names));   // UI Automation belongs off the UI thread
            var finished = await Task.WhenAny(work, Task.Delay(700));
            return finished == work ? await work : new FocusDecision(FocusKind.Unknown, "focus check timed out");
        }

        private void ShowPalette() => _palette?.ShowPalette();

        private void ShowSettings()
        {
            if (_settings != null) { _settings.Activate(); return; }
            _settings = new SettingsWindow(_host!, ApplyTriggers);
            _settings.Closed += (s, e) => _settings = null;
            _settings.Show();
            _settings.Activate();
        }

        private void ExitApp()
        {
            _tray?.Dispose();
            _doubleTap?.Dispose();
            _hotkeys?.Dispose();
            Shutdown();
        }

        protected override void OnExit(ExitEventArgs e)
        {
            try { _activateWait?.Unregister(null); } catch { }
            _activate?.Dispose();
            try { _mutex?.ReleaseMutex(); } catch { }
            _mutex?.Dispose();
            base.OnExit(e);
        }
    }
}
