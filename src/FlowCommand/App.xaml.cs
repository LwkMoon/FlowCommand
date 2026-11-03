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
        private Mutex? _mutex;
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
                MessageBox.Show("FlowCommand is already running (look for its tray icon).", "FlowCommand");
                _mutex = null;
                Shutdown();
                return;
            }

            DispatcherUnhandledException += (s, args) =>
            {
                _host?.Log.Error("Unhandled UI exception", args.Exception);
                MessageBox.Show("Unexpected error: " + args.Exception.Message + "\nSee the log from the tray menu.", "FlowCommand");
                args.Handled = true;
            };

            _host = new ServiceHost();
            _palette = new PaletteWindow(_host);
            _hotkeys = new HotkeyService();
            _hotkeys.Pressed += () => OnTrigger(false);
            _doubleTap = new DoubleTapKeyListener();
            _doubleTap.DoubleTapped += () => OnTrigger(true);
            _tray = new TrayService(_host, ShowPalette, ShowSettings, OnConfigReloaded, ExitApp);

            var problem = ApplyTriggers();
            if (problem != null)
            {
                _tray.Notify("Trigger problem", problem);
                ShowSettings();
            }

            if (_host.ConfigError != null)
                _tray.Notify("Config problem", _host.ConfigError + " Using defaults until fixed.");
            if (!_host.HasApiKey())
                ShowSettings();
            else if (problem == null)
                _tray.Notify("FlowCommand is running", $"To open the command palette: {_host.Config.TriggerDescription()}.");
        }

        /// <summary>(Re)applies the configured triggers. Returns a problem description, or null when all is well.</summary>
        private string? ApplyTriggers()
        {
            var cfg = _host!.Config;
            string? problem = null;

            _hotkeys!.Unregister();
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
            try { _mutex?.ReleaseMutex(); } catch { }
            _mutex?.Dispose();
            base.OnExit(e);
        }
    }
}
