namespace ProjectOberon.Compiler;

public static class OberonCompilerControlFlowAnalyzer
{
    public static bool CanCompleteNormally(OberonCompilerStatementSequence? sequence)
    {
        if (sequence is null)
        {
            return true;
        }

        foreach (var statement in sequence.Statements)
        {
            if (!CanCompleteNormally(statement))
            {
                return false;
            }
        }

        return true;
    }

    private static bool CanCompleteNormally(OberonCompilerStatement statement) => statement switch
    {
        OberonCompilerReturnStatement => false,
        OberonCompilerIfStatement conditional => CanCompleteNormally(conditional),
        OberonCompilerRepeatStatement repeat => CanCompleteNormally(repeat.Body),
        _ => true
    };

    private static bool CanCompleteNormally(OberonCompilerIfStatement conditional)
    {
        if (conditional.ElseBody is null)
        {
            return true;
        }

        if (CanCompleteNormally(conditional.ElseBody))
        {
            return true;
        }

        return conditional.Branches.Any(branch => CanCompleteNormally(branch.Body));
    }
}
