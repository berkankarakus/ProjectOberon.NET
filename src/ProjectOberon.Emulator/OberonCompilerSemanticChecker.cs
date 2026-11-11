namespace ProjectOberon.Emulator;

public sealed class OberonCompilerSemanticChecker
{
    private readonly List<OberonCompilerDiagnostic> _diagnostics = [];
    private readonly OberonCompilerType _booleanType;

    public OberonCompilerSemanticChecker(OberonCompilerType booleanType)
    {
        ArgumentNullException.ThrowIfNull(booleanType);
        _booleanType = booleanType;
    }

    public IReadOnlyList<OberonCompilerDiagnostic> Check(
        OberonCompilerStatementSequence? moduleBody,
        IReadOnlyList<OberonCompilerProcedureDefinition> procedures)
    {
        ArgumentNullException.ThrowIfNull(procedures);

        if (moduleBody is not null) CheckStatements(moduleBody, expectedReturnType: null, procedureName: null);
        foreach (var procedure in procedures) CheckProcedure(procedure);
        return _diagnostics;
    }

    private void CheckProcedure(OberonCompilerProcedureDefinition procedure)
    {
        var returnType = procedure.Symbol.Type?.Base;
        CheckStatements(procedure.Body, returnType, procedure.Symbol.Name);
        if (returnType?.Form != OberonCompilerTypeForm.None
            && OberonCompilerControlFlowAnalyzer.CanCompleteNormally(procedure.Body))
        {
            AddDiagnostic($"function '{procedure.Symbol.Name}' may complete without returning a value");
        }

        foreach (var nested in procedure.NestedProcedures) CheckProcedure(nested);
    }

    private void CheckStatements(
        OberonCompilerStatementSequence? sequence,
        OberonCompilerType? expectedReturnType,
        string? procedureName)
    {
        if (sequence is null) return;

        foreach (var statement in sequence.Statements)
        {
            switch (statement)
            {
                case OberonCompilerAssignmentStatement assignment:
                    CheckAssignment(assignment);
                    break;
                case OberonCompilerCallStatement call:
                    CheckCall(call.Procedure, call.Arguments, valueRequired: false);
                    break;
                case OberonCompilerReturnStatement result:
                    CheckReturn(result, expectedReturnType, procedureName);
                    break;
                case OberonCompilerIfStatement conditional:
                    foreach (var branch in conditional.Branches)
                    {
                        CheckBoolean(branch.Condition, "IF condition must be BOOLEAN");
                        CheckStatements(branch.Body, expectedReturnType, procedureName);
                    }

                    CheckStatements(conditional.ElseBody, expectedReturnType, procedureName);
                    break;
                case OberonCompilerWhileStatement loop:
                    CheckBoolean(loop.Condition, "WHILE condition must be BOOLEAN");
                    CheckStatements(loop.Body, expectedReturnType, procedureName);
                    break;
                case OberonCompilerRepeatStatement repeat:
                    CheckStatements(repeat.Body, expectedReturnType, procedureName);
                    CheckBoolean(repeat.Condition, "UNTIL condition must be BOOLEAN");
                    break;
                case OberonCompilerForStatement loop:
                    CheckFor(loop, expectedReturnType, procedureName);
                    break;
                case OberonCompilerCaseStatement conditional:
                    CheckCase(conditional, expectedReturnType, procedureName);
                    break;
            }
        }
    }

    private void CheckFor(
        OberonCompilerForStatement loop,
        OberonCompilerType? expectedReturnType,
        string? procedureName)
    {
        if (loop.Control.Symbol?.Type?.Form != OberonCompilerTypeForm.Integer)
            AddDiagnostic("FOR control variable must be INTEGER");
        if (!IsWritable(loop.Control))
            AddDiagnostic("FOR control variable is not writable");
        CheckInteger(loop.Start, "FOR start must be INTEGER");
        CheckInteger(loop.End, "FOR end must be INTEGER");
        CheckInteger(loop.Step, "FOR step must be INTEGER");
        if (loop.Step is OberonCompilerIntegerLiteralExpression { Value: 0 })
            AddDiagnostic("FOR step must not be zero");
        CheckStatements(loop.Body, expectedReturnType, procedureName);
    }

    private void CheckInteger(OberonCompilerExpression expression, string message)
    {
        if (CheckExpression(expression)?.Form != OberonCompilerTypeForm.Integer)
            AddDiagnostic(message);
    }

    private void CheckCase(
        OberonCompilerCaseStatement conditional,
        OberonCompilerType? expectedReturnType,
        string? procedureName)
    {
        var selectorType = CheckExpression(conditional.Selector);
        if (selectorType?.Form is not (OberonCompilerTypeForm.Byte
            or OberonCompilerTypeForm.Character
            or OberonCompilerTypeForm.Integer))
        {
            AddDiagnostic("CASE selector must be an integer or character");
        }

        foreach (var arm in conditional.Arms)
            CheckStatements(arm.Body, expectedReturnType, procedureName);
        CheckStatements(conditional.ElseBody, expectedReturnType, procedureName);
    }

    private void CheckAssignment(OberonCompilerAssignmentStatement assignment)
    {
        var targetType = CheckExpression(assignment.Target);
        var valueType = CheckExpression(assignment.Value);

        if (!IsWritable(assignment.Target))
        {
            AddDiagnostic("assignment target is not writable");
            return;
        }

        if (targetType is not null && valueType is not null
            && !AreAssignable(targetType, valueType))
        {
            AddDiagnostic($"cannot assign {TypeName(valueType)} to {TypeName(targetType)}");
        }
    }

    private void CheckReturn(
        OberonCompilerReturnStatement result,
        OberonCompilerType? expectedReturnType,
        string? procedureName)
    {
        var valueType = result.Value is null ? null : CheckExpression(result.Value);
        var isProcedure = expectedReturnType is null
            || expectedReturnType.Form == OberonCompilerTypeForm.None;
        if (isProcedure)
        {
            if (result.Value is not null)
                AddDiagnostic(procedureName is null
                    ? "RETURN value is not allowed in a module body"
                    : $"procedure '{procedureName}' cannot return a value");
            return;
        }

        if (valueType is null)
        {
            AddDiagnostic($"function '{procedureName}' must return a value");
            return;
        }

        var expectedType = expectedReturnType!;
        if (!AreAssignable(expectedType, valueType))
            AddDiagnostic($"function '{procedureName}' returns {TypeName(valueType)}, expected {TypeName(expectedType)}");
    }

    private OberonCompilerType? CheckExpression(OberonCompilerExpression expression) => expression switch
    {
        OberonCompilerLiteralExpression literal => literal.Type,
        OberonCompilerNameExpression name => name.Symbol?.Type,
        OberonCompilerCallExpression call => CheckCall(call.Procedure, call.Arguments, valueRequired: true),
        OberonCompilerFieldExpression field => CheckField(field),
        OberonCompilerIndexExpression index => CheckIndex(index),
        OberonCompilerDereferenceExpression dereference => CheckDereference(dereference),
        OberonCompilerUnaryExpression unary => CheckUnary(unary),
        OberonCompilerBinaryExpression binary => CheckBinary(binary),
        _ => null
    };

    private OberonCompilerType? CheckField(OberonCompilerFieldExpression field)
    {
        var receiverType = CheckExpression(field.Receiver);
        if (receiverType?.Form != OberonCompilerTypeForm.Record)
            AddDiagnostic("field receiver must be a RECORD");
        return field.Field.Type;
    }

    private OberonCompilerType? CheckIndex(OberonCompilerIndexExpression index)
    {
        var receiverType = CheckExpression(index.Receiver);
        var indexType = CheckExpression(index.Index);
        if (receiverType?.Form != OberonCompilerTypeForm.Array)
            AddDiagnostic("array index requires an ARRAY receiver");
        if (indexType?.Form != OberonCompilerTypeForm.Integer)
            AddDiagnostic("array index must be INTEGER");
        return index.ElementType;
    }

    private OberonCompilerType? CheckDereference(OberonCompilerDereferenceExpression dereference)
    {
        var receiverType = CheckExpression(dereference.Receiver);
        if (receiverType?.Form != OberonCompilerTypeForm.Pointer)
            AddDiagnostic("pointer dereference requires a POINTER receiver");
        return dereference.ElementType;
    }

    private OberonCompilerType? CheckUnary(OberonCompilerUnaryExpression unary)
    {
        var operandType = CheckExpression(unary.Operand);
        if (unary.Operator == OberonCompilerTokenKind.Not)
        {
            if (operandType?.Form != OberonCompilerTypeForm.Boolean)
                AddDiagnostic("boolean operand expected after ~");
            return operandType;
        }

        if (!IsNumeric(operandType)) AddDiagnostic("numeric operand expected");
        return operandType;
    }

    private OberonCompilerType? CheckBinary(OberonCompilerBinaryExpression binary)
    {
        var leftType = CheckExpression(binary.Left);
        var rightType = CheckExpression(binary.Right);
        if (binary.Operator == OberonCompilerTokenKind.In)
        {
            if (leftType?.Form is not (OberonCompilerTypeForm.Byte
                or OberonCompilerTypeForm.Character
                or OberonCompilerTypeForm.Integer))
            {
                AddDiagnostic("IN left operand must be an integer");
            }

            if (rightType?.Form != OberonCompilerTypeForm.Set)
                AddDiagnostic("IN right operand must be a SET");
            return _booleanType;
        }

        if (IsRelation(binary.Operator))
        {
            if (leftType is not null && rightType is not null
                && !AreComparable(leftType, rightType))
            {
                AddDiagnostic("incompatible types in comparison");
            }

            return _booleanType;
        }

        if (binary.Operator is OberonCompilerTokenKind.And or OberonCompilerTokenKind.Or)
        {
            if (leftType?.Form != OberonCompilerTypeForm.Boolean
                || rightType?.Form != OberonCompilerTypeForm.Boolean)
            {
                AddDiagnostic("boolean operands expected");
            }

            return leftType;
        }

        if (leftType?.Form == OberonCompilerTypeForm.Set
            && rightType?.Form == OberonCompilerTypeForm.Set
            && binary.Operator is OberonCompilerTokenKind.Plus
                or OberonCompilerTokenKind.Minus
                or OberonCompilerTokenKind.Multiply)
        {
            return leftType;
        }

        if (!IsNumeric(leftType) || !IsNumeric(rightType))
            AddDiagnostic("numeric operands expected");
        return leftType;
    }

    private OberonCompilerType? CheckCall(
        OberonCompilerNameExpression procedure,
        IReadOnlyList<OberonCompilerExpression> arguments,
        bool valueRequired)
    {
        var symbol = procedure.Symbol;
        if (symbol?.Class is not (OberonCompilerObjectClass.Procedure or OberonCompilerObjectClass.Function))
        {
            AddDiagnostic($"'{procedure.Name}' is not callable");
            foreach (var argument in arguments) CheckExpression(argument);
            return null;
        }

        var procedureType = symbol.Type;
        if (valueRequired && procedureType?.Base?.Form == OberonCompilerTypeForm.None)
            AddDiagnostic($"procedure '{procedure.Name}' cannot be used as a value");

        var parameters = procedureType?.Parameters ?? [];
        if (arguments.Count != parameters.Count)
        {
            AddDiagnostic($"procedure '{procedure.Name}' expects {parameters.Count} arguments, got {arguments.Count}");
        }

        var count = Math.Min(arguments.Count, parameters.Count);
        for (var index = 0; index < count; index++)
            CheckArgument(procedure.Name, parameters[index], arguments[index]);
        for (var index = count; index < arguments.Count; index++) CheckExpression(arguments[index]);

        return procedureType?.Base;
    }

    private void CheckArgument(
        string procedureName,
        OberonCompilerObject parameter,
        OberonCompilerExpression argument)
    {
        var actualType = CheckExpression(argument);
        var expectedType = parameter.Type;
        if (actualType is null || expectedType is null) return;

        if (expectedType.Form == OberonCompilerTypeForm.Array && expectedType.Length < 0)
        {
            var isCharacterString = expectedType.Base?.Form == OberonCompilerTypeForm.Character
                && actualType.Form == OberonCompilerTypeForm.String;
            var isCompatibleArray = actualType.Form == OberonCompilerTypeForm.Array
                && actualType.Base is not null
                && AreAssignable(expectedType.Base!, actualType.Base);
            if (!isCharacterString && !isCompatibleArray)
            {
                AddDiagnostic($"argument for '{parameter.Name}' must be an ARRAY");
            }
        }
        else if (!AreAssignable(expectedType, actualType))
        {
            AddDiagnostic($"argument {parameter.Name} of '{procedureName}' expects {TypeName(expectedType)}");
        }

        if (OberonCompilerTypeRules.IsAggregate(expectedType) && !IsAddressable(argument))
            AddDiagnostic($"aggregate argument '{parameter.Name}' of '{procedureName}' must be addressable");

        if (!parameter.ReadOnly && !IsAddressable(argument))
            AddDiagnostic($"VAR argument '{parameter.Name}' of '{procedureName}' must be addressable");
    }

    private void CheckBoolean(OberonCompilerExpression expression, string message)
    {
        if (CheckExpression(expression)?.Form != OberonCompilerTypeForm.Boolean)
            AddDiagnostic(message);
    }

    private static bool IsWritable(OberonCompilerExpression expression) => expression switch
    {
        OberonCompilerNameExpression { Symbol.Class: OberonCompilerObjectClass.Variable } => true,
        OberonCompilerNameExpression { Symbol.Class: OberonCompilerObjectClass.Parameter } => true,
        OberonCompilerFieldExpression => true,
        OberonCompilerIndexExpression index => IsAddressable(index.Receiver),
        OberonCompilerDereferenceExpression => true,
        _ => false
    };

    private static bool IsAddressable(OberonCompilerExpression expression) => expression switch
    {
        OberonCompilerNameExpression { Symbol.Class: OberonCompilerObjectClass.Variable } => true,
        OberonCompilerNameExpression
        {
            Symbol.Class: OberonCompilerObjectClass.Parameter,
            Symbol.ReadOnly: false
        } => true,
        OberonCompilerNameExpression
        {
            Symbol.Class: OberonCompilerObjectClass.Parameter,
            Symbol.Type.Form: OberonCompilerTypeForm.Array,
            Symbol.Type.Length: < 0
        } => true,
        OberonCompilerNameExpression
        {
            Symbol.Class: OberonCompilerObjectClass.Parameter,
            Symbol.Type: { } type
        } when OberonCompilerTypeRules.IsAggregate(type) => true,
        OberonCompilerFieldExpression => true,
        OberonCompilerDereferenceExpression => true,
        OberonCompilerIndexExpression index => IsAddressable(index.Receiver),
        _ => false
    };

    private static bool AreAssignable(OberonCompilerType expected, OberonCompilerType actual)
    {
        if (ReferenceEquals(expected, actual)) return true;
        if (expected.Form == OberonCompilerTypeForm.Pointer
            && actual.Form == OberonCompilerTypeForm.Nil) return true;
        if (IsNumeric(expected) && IsNumeric(actual)) return true;
        return expected.Form == OberonCompilerTypeForm.Array
            && expected.Base?.Form == OberonCompilerTypeForm.Character
            && actual.Form == OberonCompilerTypeForm.String;
    }

    private static bool AreComparable(OberonCompilerType left, OberonCompilerType right) =>
        ReferenceEquals(left, right)
        || left.Form == OberonCompilerTypeForm.Pointer && right.Form == OberonCompilerTypeForm.Nil
        || left.Form == OberonCompilerTypeForm.Nil && right.Form == OberonCompilerTypeForm.Pointer
        || IsNumeric(left) && IsNumeric(right);

    private static bool IsNumeric(OberonCompilerType? type) => type?.Form is
        OberonCompilerTypeForm.Byte or OberonCompilerTypeForm.Character
        or OberonCompilerTypeForm.Integer or OberonCompilerTypeForm.Real;

    private static bool IsRelation(OberonCompilerTokenKind operation) => operation is
        OberonCompilerTokenKind.Equal or OberonCompilerTokenKind.NotEqual
        or OberonCompilerTokenKind.Less or OberonCompilerTokenKind.LessOrEqual
        or OberonCompilerTokenKind.Greater or OberonCompilerTokenKind.GreaterOrEqual;

    private static string TypeName(OberonCompilerType type) => type.Form.ToString().ToUpperInvariant();

    private void AddDiagnostic(string message) =>
        _diagnostics.Add(new OberonCompilerDiagnostic(message, 0, 1, 1));
}
