namespace FlowCommand.Core.Util;

public sealed record MatchResult(int Index, IReadOnlyList<int> Ambiguous)
{
    public bool Found => Index >= 0;
    public bool IsAmbiguous => Ambiguous.Count > 1;
    public static readonly MatchResult None = new(-1, Array.Empty<int>());
}

/// <summary>Forgiving name lookup ("Opera GX" == "operagx" == "Opera GX Browser"). Letters, digits, "+" and "#" count; everything else is ignored.</summary>
public static class NameMatcher
{
    public static string Normalize(string? s)
    {
        if (string.IsNullOrEmpty(s)) return "";
        var chars = s.Where(c => char.IsLetterOrDigit(c) || c == '+' || c == '#').Select(char.ToLowerInvariant).ToArray();
        return new string(chars);
    }

    /// <param name="namesPerItem">For each candidate item, all names/aliases it answers to.</param>
    public static MatchResult Find(string? query, IReadOnlyList<IReadOnlyList<string>> namesPerItem)
    {
        var q = Normalize(query);
        if (q.Length == 0) return MatchResult.None;

        var norm = namesPerItem
            .Select(l => l.Select(Normalize).Where(n => n.Length > 0).ToList())
            .ToList();

        var exact = Indices(norm, n => n == q);
        if (exact.Count > 0) return Result(exact);

        var partial = Indices(norm, n => (q.Length >= 3 && n.Contains(q)) || (n.Length >= 4 && q.Contains(n)));
        if (partial.Count > 1)
        {
            var starts = Indices(norm, n => n.StartsWith(q, StringComparison.Ordinal)).Intersect(partial).ToList();
            if (starts.Count == 1) return Result(starts);
        }
        return Result(partial);
    }

    private static List<int> Indices(List<List<string>> norm, Func<string, bool> pred)
    {
        var res = new List<int>();
        for (var i = 0; i < norm.Count; i++)
            if (norm[i].Any(pred)) res.Add(i);
        return res;
    }

    private static MatchResult Result(List<int> idx) => idx.Count switch
    {
        0 => MatchResult.None,
        1 => new MatchResult(idx[0], idx),
        _ => new MatchResult(-1, idx),
    };
}
