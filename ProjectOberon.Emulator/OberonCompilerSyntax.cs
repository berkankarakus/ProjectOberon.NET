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

public sealed record OberonCompilerCallExpression(
    OberonCompilerNameExpression Procedure,
    IReadOnlyList<OberonCompilerExpression> Arguments) : OberonCompilerExpression;

public sealed record OberonCompilerFieldExpression(
    OberonCompilerExpression Receiver,
    OberonCompilerObject Field) : OberonCompilerExpression;

public sealed record OberonCompilerIndexExpression(
    OberonCompilerExpression Receiver,
    OberonCompilerExpression Index,
    OberonCompilerType ElementType) : OberonCompilerExpression;

public sealed record OberonCompilerDereferenceExpression(
    OberonCompilerExpression Receiver,
    OberonCompilerType ElementType) : OberonCompilerExpression;

public sealed record OberonCompilerUnaryExpression(
    OberonCompilerTokenKind Operator,
    OberonCompilerExpression Operand) : OberonCompilerExpression;

public sealed record OberonCompilerBinaryExpression(
    OberonCompilerTokenKind Operator,
    OberonCompilerExpression Left,
    OberonCompilerExpression Right) : OberonCompilerExpression;

public abstract record OberonCompilerStatement;

public sealed record OberonCompilerAssignmentStatement(
    OberonCompilerExpression Target,
    OberonCompilerExpression Value) : OberonCompilerStatement;

public sealed record OberonCompilerCallStatement(
    OberonCompilerNameExpression Procedure,
    IReadOnlyList<OberonCompilerExpression> Arguments) : OberonCompilerStatement;

public sealed record OberonCompilerReturnStatement(
    OberonCompilerExpression? Value) : OberonCompilerStatement;

public sealed record OberonCompilerIfBranch(
    OberonCompilerExpression Condition,
    OberonCompilerStatementSequence Body);

public sealed record OberonCompilerIfStatement(
    IReadOnlyList<OberonCompilerIfBranch> Branches,
    OberonCompilerStatementSequence? ElseBody) : OberonCompilerStatement;

public sealed record OberonCompilerWhileStatement(
    OberonCompilerExpression Condition,
    OberonCompilerStatementSequence Body) : OberonCompilerStatement;

public sealed record OberonCompilerRepeatStatement(
    OberonCompilerStatementSequence Body,
    OberonCompilerExpression Condition) : OberonCompilerStatement;

public sealed record OberonCompilerStatementSequence(
    IReadOnlyList<OberonCompilerStatement> Statements);
