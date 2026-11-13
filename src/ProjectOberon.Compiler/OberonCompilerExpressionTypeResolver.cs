namespace ProjectOberon.Compiler;

internal static class OberonCompilerExpressionTypeResolver
{
    public static OberonCompilerType? Resolve(OberonCompilerExpression expression)
    {
        ArgumentNullException.ThrowIfNull(expression);

        switch (expression)
        {
            case OberonCompilerLiteralExpression literal:
                return literal.Type;
            case OberonCompilerNameExpression name:
                return name.Symbol?.Type;
            case OberonCompilerCallExpression call:
                return call.Procedure.Symbol?.Type?.Base;
            case OberonCompilerFieldExpression field:
                return field.Field.Type;
            case OberonCompilerIndexExpression index:
                return index.ElementType;
            case OberonCompilerDereferenceExpression dereference:
                return dereference.ElementType;
            case OberonCompilerUnaryExpression unary:
                return Resolve(unary.Operand);
            case OberonCompilerBinaryExpression binary:
                return Resolve(binary.Left);
            default:
                return null;
        }
    }
}
