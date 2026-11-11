using System.Text.RegularExpressions;

namespace ProjectOberon.Emulator;

public sealed record OberonProcedure(string Name, bool Exported, int Line);

public static partial class OberonSourceAnalysis
{
    [GeneratedRegex(@"^\s*PROCEDURE\s+([A-Za-z][A-Za-z0-9]*)(\*)?")]
    private static partial Regex ProcedurePattern();

    public static IReadOnlyList<OberonProcedure> FindProcedures(string path)
    {
        var result = new List<OberonProcedure>();
        var lineNumber = 0;
        foreach (var line in File.ReadLines(path))
        {
            lineNumber++;
            var match = ProcedurePattern().Match(line);
            if (match.Success) result.Add(new OberonProcedure(match.Groups[1].Value, match.Groups[2].Success, lineNumber));
        }
        return result;
    }
}
