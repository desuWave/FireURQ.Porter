using System;
using System.Linq;
using System.Text.RegularExpressions;

namespace FireURQ.Runtime;

// The RegExpr API used by f2LinkParser. Match positions are Delphi string indexes.
internal sealed class TRegExpr : TObject
{
    private Regex? regex;
    private System.Text.RegularExpressions.Match? current;

    public string Expression
    {
        get => regex?.ToString() ?? string.Empty;
        set
        {
            regex = new Regex(value);
            SetMatch(null);
        }
    }

    public string[] Match { get; private set; } = Array.Empty<string>();
    public int[] MatchPos { get; private set; } = Array.Empty<int>();
    public int[] MatchLen { get; private set; } = Array.Empty<int>();

    public static TRegExpr Create() => new TRegExpr();

    public bool Exec(string input)
    {
        if (regex is null)
            throw new InvalidOperationException("Set Expression before calling Exec.");
        return SetMatch(regex.Match(input));
    }

    public bool ExecNext() => SetMatch(current?.NextMatch());

    private bool SetMatch(System.Text.RegularExpressions.Match? match)
    {
        current = match is { Success: true } ? match : null;
        var groups = current?.Groups;
        Match = groups?.Select(group => group.Value).ToArray() ?? Array.Empty<string>();
        MatchPos = groups?.Select(group => group.Success ? group.Index + 1 : 0).ToArray() ?? Array.Empty<int>();
        MatchLen = groups?.Select(group => group.Length).ToArray() ?? Array.Empty<int>();
        return current is not null;
    }
}
