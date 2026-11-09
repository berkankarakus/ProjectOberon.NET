namespace ProjectOberon.Emulator;

public abstract record OberonCompilerExpression;

public sealed record OberonCompilerLiteralExpression(
    OberonCompilerType Type,
    long Value) : OberonCompilerExpression;

public sealed record OberonCompilerNameExpression(
    string Name,
    OberonCompilerObject? Symbol) : OberonCompilerExpression;

public sealed record OberonCompilerUnaryExpression(
    OberonCompilerTokenKind Operator,
    OberonCompilerExpression Operand) : OberonCompilerExpression;

public sealed record OberonCompilerBinaryExpression(
    OberonCompilerTokenKind Operator,
    OberonCompilerExpression Left,
    OberonCompilerExpression Right) : OberonCompilerExpression;

public abstract record OberonCompilerStatement;

public sealed record OberonCompilerAssignmentStatement(
    OberonCompilerNameExpression Target,
    OberonCompilerExpression Value) : OberonCompilerStatement;

public sealed record OberonCompilerCallStatement(
    OberonCompilerNameExpression Procedure,
    IReadOnlyList<OberonCompilerExpression> Arguments) : OberonCompilerStatement;

public sealed record OberonCompilerReturnStatement(
    OberonCompilerExpression? Value) : OberonCompilerStatement;

public sealed record OberonCompilerStatementSequence(
    IReadOnlyList<OberonCompilerStatement> Statements);
