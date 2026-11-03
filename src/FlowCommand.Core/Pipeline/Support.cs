namespace FlowCommand.Core.Pipeline;

public interface ILog
{
    void Info(string message);
    void Warn(string message);
    void Error(string message, Exception? ex = null);
}

public sealed class NullLog : ILog
{
    public static readonly NullLog Instance = new();
    public void Info(string message) { }
    public void Warn(string message) { }
    public void Error(string message, Exception? ex = null) { }
}

public interface ISecretStore
{
    string? Get(string name);
    void Set(string name, string value);
    void Delete(string name);
    bool Has(string name);
}

public sealed record ConversationTurn(string UserText, string ActionSummary, string ResultSummary, DateTimeOffset At);

/// <summary>
/// Short-lived, in-memory only. Lets "actually, open the other browser instead" be interpreted.
/// Never written to disk; entries expire; cleared when the app exits.
/// </summary>
public sealed class ConversationMemory
{
    private readonly List<ConversationTurn> _turns = new();
    private readonly int _capacity;
    private readonly TimeSpan _ttl;
    private readonly Func<DateTimeOffset> _clock;

    public ConversationMemory(int capacity = 4, TimeSpan? ttl = null, Func<DateTimeOffset>? clock = null)
    {
        _capacity = capacity;
        _ttl = ttl ?? TimeSpan.FromMinutes(10);
        _clock = clock ?? (() => DateTimeOffset.UtcNow);
    }

    public void Add(string userText, string action, string result)
    {
        lock (_turns)
        {
            _turns.Add(new ConversationTurn(userText, action, result, _clock()));
            while (_turns.Count > _capacity) _turns.RemoveAt(0);
        }
    }

    public IReadOnlyList<ConversationTurn> Snapshot()
    {
        lock (_turns)
        {
            var cutoff = _clock() - _ttl;
            return _turns.Where(t => t.At >= cutoff).ToList();
        }
    }

    public void Clear() { lock (_turns) _turns.Clear(); }
}
