using System;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using FlowCommand.Core.Actions;
using FlowCommand.Core.Pipeline;
using FlowCommand.Core.Util;
using FlowCommand.Platform;

namespace FlowCommand.UI
{
    internal sealed class HistoryItem
    {
        public string Icon { get; init; } = "";
        public string Text { get; init; } = "";
        public Brush Brush { get; init; } = Brushes.Gray;
    }

    /// <summary>
    /// Keyboard-first command palette. Wispr Flow pastes the transcript into <see cref="InputBox"/>;
    /// this window never touches the microphone. Enter runs, Esc cancels/clears/closes.
    /// </summary>
    public partial class PaletteWindow : Window
    {
        private enum State { Idle, Interpreting, AwaitingConfirmation, Executing }
        private enum StatusKind { Info, Ok, Warn, Error, Muted }

        private const int MaxHistory = 6;

        private readonly ServiceHost _host;
        private readonly DispatcherTimer _autoTimer = new DispatcherTimer();
        private readonly ObservableCollection<HistoryItem> _history = new ObservableCollection<HistoryItem>();

        private State _state = State.Idle;
        private CancellationTokenSource? _cts;
        private Interpretation? _pending;
        private IntPtr _previousForeground = IntPtr.Zero;
        private bool _pasteArmed;
        private int _lastLength;

        internal PaletteWindow(ServiceHost host)
        {
            _host = host;
            InitializeComponent();
            HistoryList.ItemsSource = _history;

            DataObject.AddPastingHandler(InputBox, OnPaste);
            InputBox.TextChanged += OnTextChanged;
            InputBox.PreviewTextInput += OnPreviewTextInput;
            PreviewKeyDown += OnPreviewKeyDown;
            Activated += (s, e) => UpdateFooter(true);
            Deactivated += (s, e) => UpdateFooter(false);
            Header.MouseLeftButtonDown += (s, e) =>
            {
                if (e.ButtonState == MouseButtonState.Pressed) DragMove();
            };
            _autoTimer.Tick += OnAutoTimer;
            ApplyConfig();
        }

        private IntPtr OwnHandle => new WindowInteropHelper(this).EnsureHandle();

        // ---------------------------------------------------------------- show / hide
        public void ShowPalette()
        {
            var own = OwnHandle;
            var fg = NativeMethods.GetForegroundWindow();
            if (fg != IntPtr.Zero && fg != own) _previousForeground = fg;

            ApplyConfig();
            Reposition();
            if (!IsVisible) Show();
            WindowState = WindowState.Normal;
            FocusInput();
        }

        public void HidePalette()
        {
            _autoTimer.Stop();
            _cts?.Cancel();
            _pending = null;
            ConfirmPanel.Visibility = Visibility.Collapsed;
            if (_state != State.Executing) SetState(State.Idle);
            Hide();

            var prev = _previousForeground;
            if (prev != IntPtr.Zero && NativeMethods.IsWindow(prev) && !NativeMethods.IsIconic(prev))
                NativeMethods.SetForegroundWindow(prev);
        }

        private void FocusInput()
        {
            WindowTools.ForceForeground(OwnHandle);
            Activate();
            InputBox.Focus();
            Keyboard.Focus(InputBox);
            InputBox.CaretIndex = InputBox.Text.Length;
        }

        private void Reposition()
        {
            var wa = SystemParameters.WorkArea;
            Left = wa.Left + (wa.Width - Width) / 2;
            Top = wa.Top + wa.Height * 0.16;
        }

        private void ApplyConfig()
        {
            ModeBadge.Visibility = _host.Config.DryRun ? Visibility.Visible : Visibility.Collapsed;
            UpdateFooter(IsActive);
        }

        private void UpdateFooter(bool focused)
        {
            var hk = _host.Config.TriggerDescription();
            FooterHint.Text = focused
                ? $"Enter run \u00B7 Esc cancel/close \u00B7 {hk} toggle"
                : $"Not focused \u2014 dictated text won't arrive here. Press {hk} or click the palette.";
            FooterHint.Foreground = (Brush)FindResource(focused ? "MutedBrush" : "WarnBrush");
        }

        // ---------------------------------------------------------------- input handling
        private string Mode => _host.Config.AutoSubmitMode;

        private void OnPaste(object sender, DataObjectPastingEventArgs e)
        {
            if (_state != State.Idle) { e.CancelCommand(); return; }
            _pasteArmed = true;
        }

        /// <summary>Hides the palette so a screenshot doesn't capture it. The next double-tap brings it back.</summary>
        private void HideForCapture() => Hide();

        private void OnPreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            // When a typing key (the backtick) is the trigger, the double-tap itself types characters.
            // Commands never need a backtick, so keep them out of the box (also stops auto-repeat leaking in as it opens).
            var cfg = _host.Config;
            var key = TriggerKeys.Parse(cfg.DoubleTapKey) ?? TriggerKeys.Default;
            if (cfg.DoubleTapEnabled && key.ProducesText && e.Text == "`")
            {
                e.Handled = true;
                return;
            }
            OnManualEdit();
        }

        private void OnManualEdit()
        {
            // Typing by hand in "paste" mode means the user is editing: wait for Enter.
            if (Mode == "paste") { _autoTimer.Stop(); _pasteArmed = false; }
        }

        private void OnTextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            var len = InputBox.Text.Length;
            var added = len - _lastLength;
            _lastLength = len;
            Placeholder.Visibility = len == 0 ? Visibility.Visible : Visibility.Collapsed;

            if (_state != State.Idle) return;
            if (len == 0) { _autoTimer.Stop(); _pasteArmed = false; return; }

            var cfg = _host.Config;
            var arm = Mode == "idle" ||
                      (Mode == "paste" && (_pasteArmed || added >= cfg.PasteThresholdChars));
            if (!arm) return;

            _autoTimer.Interval = TimeSpan.FromMilliseconds(cfg.AutoSubmitDelayMs);
            _autoTimer.Stop();   // each new chunk of text restarts the wait, so partial transcripts aren't run
            _autoTimer.Start();
        }

        private void OnAutoTimer(object? sender, EventArgs e)
        {
            _autoTimer.Stop();
            _ = SubmitAsync();
        }

        private void OnPreviewKeyDown(object sender, KeyEventArgs e)
        {
            switch (e.Key)
            {
                case Key.Enter:
                    e.Handled = true;
                    if (_state == State.AwaitingConfirmation) _ = ConfirmAsync();
                    else if (_state == State.Idle) { _autoTimer.Stop(); _ = SubmitAsync(); }
                    break;
                case Key.Escape:
                    e.Handled = true;
                    HandleEscape();
                    break;
                case Key.Back:
                case Key.Delete:
                    OnManualEdit();
                    break;
            }
        }

        private void HandleEscape()
        {
            switch (_state)
            {
                case State.Interpreting:
                    _cts?.Cancel();
                    break;
                case State.Executing:
                    SetStatus("\u2026", "The action is already running and can't be cancelled.", StatusKind.Muted);
                    break;
                case State.AwaitingConfirmation:
                    _pending = null;
                    ConfirmPanel.Visibility = Visibility.Collapsed;
                    SetState(State.Idle);
                    SetStatus("\u2013", "Cancelled. Nothing was run.", StatusKind.Muted);
                    break;
                default:
                    if (InputBox.Text.Length > 0) { ClearInput(); SetStatus("\u2022", "Ready.", StatusKind.Muted); }
                    else HidePalette();
                    break;
            }
        }

        // ---------------------------------------------------------------- command flow
        private async Task SubmitAsync()
        {
            var text = InputBox.Text.Trim();
            if (text.Length == 0 || _state != State.Idle) return;

            _pasteArmed = false;
            SetState(State.Interpreting);
            SetStatus("\u2026", $"Understanding: \u201C{text}\u201D", StatusKind.Info);
            _cts?.Dispose();
            _cts = new CancellationTokenSource();

            try
            {
                var interp = await _host.Pipeline.InterpretAsync(text, _cts.Token);
                switch (interp.Kind)
                {
                    case OutcomeKind.Ready:
                        SetStatus("\u2026", interp.Message, StatusKind.Info);
                        await RunAsync(interp);
                        break;

                    case OutcomeKind.NeedsConfirmation:
                        _pending = interp;
                        SetState(State.AwaitingConfirmation);
                        ConfirmText.Text = "\u26A0 Confirm: " + interp.Message
                            + (interp.Preview != null ? "\n" + interp.Preview : "")
                            + "\nEnter = confirm \u00B7 Esc = cancel";
                        ConfirmPanel.Visibility = Visibility.Visible;
                        SetStatus("?", $"Understood: {interp.Plan!.Describe()}", StatusKind.Warn);
                        break;

                    case OutcomeKind.Clarification:
                        ClearInput();
                        SetStatus("?", interp.Message, StatusKind.Warn);
                        AddHistory("?", StatusKind.Warn, $"{text} \u2192 {interp.Message}");
                        SetState(State.Idle);
                        break;

                    case OutcomeKind.Unsupported:
                        ClearInput();
                        SetStatus("\u2717", interp.Message, StatusKind.Error);
                        AddHistory("\u2717", StatusKind.Error, $"{text} \u2192 {interp.Message}");
                        SetState(State.Idle);
                        break;

                    default: // Rejected / Unavailable: keep the text so Enter retries
                        SetStatus("\u2717", interp.Message, StatusKind.Error);
                        AddHistory("\u2717", StatusKind.Error, $"{text} \u2192 {interp.Message}");
                        SetState(State.Idle);
                        break;
                }
            }
            catch (OperationCanceledException)
            {
                SetStatus("\u2013", "Cancelled. Nothing was run.", StatusKind.Muted);
                SetState(State.Idle);
            }
            catch (Exception ex)
            {
                _host.Log.Error("Submit failed", ex);
                SetStatus("\u2717", "Unexpected error: " + ex.Message, StatusKind.Error);
                SetState(State.Idle);
            }

        }

        private async Task ConfirmAsync()
        {
            var p = _pending;
            _pending = null;
            ConfirmPanel.Visibility = Visibility.Collapsed;
            if (p != null) await RunAsync(p);
        }

        private async Task RunAsync(Interpretation interp)
        {
            SetState(State.Executing);
            SetStatus("\u2026", interp.Message, StatusKind.Info);

            ActionResult result;
            try
            {
                result = await _host.Pipeline.ExecuteAsync(interp, new ActionContext(_previousForeground, HideForCapture), CancellationToken.None);
            }
            catch (Exception ex)
            {
                _host.Log.Error("Execute failed", ex);
                result = ActionResult.Fail("Unexpected error: " + ex.Message);
            }

            string icon;
            StatusKind kind;
            if (result.DryRun) { icon = "\u25CC"; kind = StatusKind.Muted; }
            else if (!result.Success) { icon = "\u2717"; kind = StatusKind.Error; }
            else if (result.Verified) { icon = "\u2713"; kind = StatusKind.Ok; }
            else { icon = "!"; kind = StatusKind.Warn; }

            SetStatus(icon, result.Message, kind);
            AddHistory(icon, kind, $"{interp.UserText} \u2192 {result.Message}");
            ClearInput();
            SetState(State.Idle);

            if (_host.Config.ReclaimFocusAfterAction && !result.KeepFocus && IsVisible)
            {
                await Task.Delay(450);
                if (IsVisible && _state == State.Idle) { WindowState = WindowState.Normal; FocusInput(); }
            }
        }

        // ---------------------------------------------------------------- small helpers
        private void SetState(State s)
        {
            _state = s;
            InputBox.IsReadOnly = s != State.Idle;
        }

        private void ClearInput()
        {
            _pasteArmed = false;
            _autoTimer.Stop();
            InputBox.Text = "";
        }

        private void SetStatus(string icon, string text, StatusKind kind)
        {
            StatusIcon.Text = icon;
            StatusText.Text = text;
            StatusIcon.Foreground = (Brush)FindResource(kind switch
            {
                StatusKind.Ok => "OkBrush",
                StatusKind.Warn => "WarnBrush",
                StatusKind.Error => "ErrBrush",
                StatusKind.Info => "AccentBrush",
                _ => "MutedBrush",
            });
        }

        private void AddHistory(string icon, StatusKind kind, string text)
        {
            var brush = (Brush)FindResource(kind switch
            {
                StatusKind.Ok => "OkBrush",
                StatusKind.Warn => "WarnBrush",
                StatusKind.Error => "ErrBrush",
                StatusKind.Info => "AccentBrush",
                _ => "MutedBrush",
            });
            _history.Insert(0, new HistoryItem { Icon = icon, Text = text, Brush = brush });
            while (_history.Count > MaxHistory) _history.RemoveAt(_history.Count - 1);
        }
    }
}
