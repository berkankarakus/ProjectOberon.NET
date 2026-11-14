namespace ProjectOberon.Compiler;

internal static class OberonCompilerConstantEvaluator
{
    public static bool TryEvaluateInteger(
        OberonCompilerExpression expression,
        out int value)
    {
        if (expression is OberonCompilerIntegerLiteralExpression literal
            && IsInteger(literal)
            && literal.Value is >= int.MinValue and <= int.MaxValue)
        {
            value = (int)literal.Value;
            return true;
        }

        if (expression is OberonCompilerUnaryExpression unary
            && TryEvaluateInteger(unary.Operand, out var operandValue))
        {
            switch (unary.Operator)
            {
                case OberonCompilerTokenKind.Plus:
                    value = operandValue;
                    return true;
                case OberonCompilerTokenKind.Minus:
                    var negated = -(long)operandValue;
                    if (negated is < int.MinValue or > int.MaxValue)
                    {
                        break;
                    }

                    value = (int)negated;
                    return true;
            }
        }

        if (expression is not OberonCompilerBinaryExpression binary
            || !TryEvaluateInteger(binary.Left, out var left)
            || !TryEvaluateInteger(binary.Right, out var right))
        {
            value = 0;
            return false;
        }

        long result;
        switch (binary.Operator)
        {
            case OberonCompilerTokenKind.Plus:
                result = (long)left + right;
                break;
            case OberonCompilerTokenKind.Minus:
                result = (long)left - right;
                break;
            case OberonCompilerTokenKind.Multiply:
                result = (long)left * right;
                break;
            case OberonCompilerTokenKind.Divide:
                if (right == 0)
                {
                    value = 0;
                    return false;
                }

                result = (long)left / right;
                break;
            case OberonCompilerTokenKind.Mod:
                if (right == 0)
                {
                    value = 0;
                    return false;
                }

                result = (long)left % right;
                break;
            case OberonCompilerTokenKind.And:
                result = left & right;
                break;
            case OberonCompilerTokenKind.Or:
                result = left | right;
                break;
            default:
                value = 0;
                return false;
        }

        if (result is < int.MinValue or > int.MaxValue)
        {
            value = 0;
            return false;
        }

        value = (int)result;
        return true;
    }

    private static bool IsInteger(OberonCompilerExpression expression) =>
        OberonCompilerExpressionTypeResolver.Resolve(expression)?.Form is
            OberonCompilerTypeForm.Byte or
            OberonCompilerTypeForm.Character or
            OberonCompilerTypeForm.Integer;
}
