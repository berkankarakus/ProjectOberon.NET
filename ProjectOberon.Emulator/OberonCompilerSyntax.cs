namespace ProjectOberon.Emulator;

public abstract record OberonCompilerExpression;

public abstract record OberonCompilerLiteralExpression(
    OberonCompilerType Type) : OberonCompilerExpression;

public sealed record OberonCompilerIntegerLiteralExpression(
    OberonCompilerType Type,
    long Value) : OberonCompilerLiteralExpression(Type);

public sealed record OberonCompilerRealLiteralExpression(
    OberonCompilerType Type,
    double Value) : OberonCompilerLiteralExpression(Type);

public sealed record OberonCompilerStringLiteralExpression(
    OberonCompilerType Type,
    string Value) : OberonCompilerLiteralExpression(Type);

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
