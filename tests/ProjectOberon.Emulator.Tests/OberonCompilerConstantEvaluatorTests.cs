using ProjectOberon.Compiler;

internal static class OberonCompilerConstantEvaluatorTests
{
    public static void Run()
    {
        var symbols = new OberonCompilerSymbols();

        var valid = new OberonCompilerBinaryExpression(
            OberonCompilerTokenKind.Multiply,
            new OberonCompilerIntegerLiteralExpression(symbols.IntegerType, 7),
            new OberonCompilerIntegerLiteralExpression(symbols.IntegerType, 6));
        TestAssertions.True(OberonCompilerConstantEvaluator.TryEvaluateInteger(valid, out var validValue));
        TestAssertions.Equal(42, validValue);

        var overflow = new OberonCompilerBinaryExpression(
            OberonCompilerTokenKind.Plus,
            new OberonCompilerIntegerLiteralExpression(symbols.IntegerType, int.MaxValue),
            new OberonCompilerIntegerLiteralExpression(symbols.IntegerType, 1));
        TestAssertions.False(OberonCompilerConstantEvaluator.TryEvaluateInteger(overflow, out _));

        var divideByZero = new OberonCompilerBinaryExpression(
            OberonCompilerTokenKind.Divide,
            new OberonCompilerIntegerLiteralExpression(symbols.IntegerType, 1),
            new OberonCompilerIntegerLiteralExpression(symbols.IntegerType, 0));
        TestAssertions.False(OberonCompilerConstantEvaluator.TryEvaluateInteger(divideByZero, out _));

        var boolean = new OberonCompilerBinaryExpression(
            OberonCompilerTokenKind.And,
            new OberonCompilerIntegerLiteralExpression(symbols.BooleanType, 1),
            new OberonCompilerIntegerLiteralExpression(symbols.BooleanType, 0));
        TestAssertions.True(OberonCompilerConstantEvaluator.TryEvaluateBoolean(boolean, out var booleanValue));
        TestAssertions.False(booleanValue);
    }
}
