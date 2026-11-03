namespace FlowCommand.Core.Util;

/// <summary>
/// Pure timing logic for "double-tap a key". Feed it key-down/key-up events for the target key and
/// <see cref="OtherKey"/> for any other physical key. Auto-repeat while held is ignored.
/// </summary>
public sealed class DoubleTapDetector
{
    private long _lastTap = -1;
    private bool _isDown;

    public DoubleTapDetector(int intervalMs) => IntervalMs = intervalMs;

    public int IntervalMs { get; set; }

    /// <returns>true when this key-down completes a double tap.</returns>
    public bool KeyDown(long nowMs)
    {
        if (_isDown) return false;              // auto-repeat of a held key
        _isDown = true;
        if (_lastTap >= 0 && nowMs - _lastTap <= IntervalMs)
        {
            _lastTap = -1;                       // a third tap starts a fresh sequence
            return true;
        }
        _lastTap = nowMs;
        return false;
    }

    public void KeyUp() => _isDown = false;

    /// <summary>Any other physical key between the taps cancels the sequence.</summary>
    public void OtherKey() => _lastTap = -1;
}
