namespace ProjectOberon.Compiler;

internal static class OberonCompilerBranchConditions
{
    public static OberonCompilerTokenKind ReverseComparison(
        OberonCompilerTokenKind operation) => operation switch
    {
        OberonCompilerTokenKind.Equal => OberonCompilerTokenKind.Equal,
        OberonCompilerTokenKind.NotEqual => OberonCompilerTokenKind.NotEqual,
        OberonCompilerTokenKind.Less => OberonCompilerTokenKind.Greater,
        OberonCompilerTokenKind.LessOrEqual => OberonCompilerTokenKind.GreaterOrEqual,
        OberonCompilerTokenKind.Greater => OberonCompilerTokenKind.Less,
        OberonCompilerTokenKind.GreaterOrEqual => OberonCompilerTokenKind.LessOrEqual,
        _ => throw new ArgumentOutOfRangeException(nameof(operation))
    };

    public static (ConditionCode Condition, bool Invert) ComparisonBranch(
        OberonCompilerTokenKind operation) => operation switch
    {
        OberonCompilerTokenKind.Equal => (ConditionCode.Eq, false),
        OberonCompilerTokenKind.NotEqual => (ConditionCode.Eq, true),
        OberonCompilerTokenKind.Less => (ConditionCode.Lt, false),
        OberonCompilerTokenKind.LessOrEqual => (ConditionCode.Le, false),
        OberonCompilerTokenKind.Greater => (ConditionCode.Le, true),
        OberonCompilerTokenKind.GreaterOrEqual => (ConditionCode.Lt, true),
        _ => throw new ArgumentOutOfRangeException(nameof(operation))
    };
}
