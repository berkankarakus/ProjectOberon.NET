using ProjectOberon.Compiler;

internal static class OberonCompilerControlFlowAnalyzerTests
{
    public static void Run()
    {
        EmptySequencesCompleteNormally();
        ReturnDoesNotCompleteNormally();
        IfRequiresACompletingPath();
        RepeatFollowsItsBody();
    }

    private static void EmptySequencesCompleteNormally()
    {
        TestAssertions.True(OberonCompilerControlFlowAnalyzer.CanCompleteNormally(null));
        TestAssertions.True(OberonCompilerControlFlowAnalyzer.CanCompleteNormally(
            new OberonCompilerStatementSequence([])));
    }

    private static void ReturnDoesNotCompleteNormally()
    {
        var sequence = new OberonCompilerStatementSequence([
            new OberonCompilerReturnStatement(null)]);

        TestAssertions.False(OberonCompilerControlFlowAnalyzer.CanCompleteNormally(sequence));
    }

    private static void IfRequiresACompletingPath()
    {
        var condition = new OberonCompilerNameExpression("condition", null);
        var returning = new OberonCompilerStatementSequence([
            new OberonCompilerReturnStatement(null)]);
        var assignment = new OberonCompilerStatementSequence([
            new OberonCompilerAssignmentStatement(
                new OberonCompilerNameExpression("target", null),
                new OberonCompilerNameExpression("value", null))]);

        var allPathsReturn = new OberonCompilerIfStatement(
            [new OberonCompilerIfBranch(condition, returning)], returning);
        var branchCanContinue = new OberonCompilerIfStatement(
            [new OberonCompilerIfBranch(condition, assignment)], returning);

        TestAssertions.False(OberonCompilerControlFlowAnalyzer.CanCompleteNormally(
            new OberonCompilerStatementSequence([allPathsReturn])));
        TestAssertions.True(OberonCompilerControlFlowAnalyzer.CanCompleteNormally(
            new OberonCompilerStatementSequence([branchCanContinue])));
    }

    private static void RepeatFollowsItsBody()
    {
        var condition = new OberonCompilerNameExpression("condition", null);
        var returning = new OberonCompilerStatementSequence([
            new OberonCompilerReturnStatement(null)]);
        var assignment = new OberonCompilerStatementSequence([
            new OberonCompilerAssignmentStatement(
                new OberonCompilerNameExpression("target", null),
                new OberonCompilerNameExpression("value", null))]);

        TestAssertions.False(OberonCompilerControlFlowAnalyzer.CanCompleteNormally(
            new OberonCompilerStatementSequence([
                new OberonCompilerRepeatStatement(returning, condition)])));
        TestAssertions.True(OberonCompilerControlFlowAnalyzer.CanCompleteNormally(
            new OberonCompilerStatementSequence([
                new OberonCompilerRepeatStatement(assignment, condition)])));
    }
}
