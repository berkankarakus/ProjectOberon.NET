namespace ProjectOberon.Compiler;

public sealed partial class OberonCompilerParser
{
    private OberonCompilerStatementSequence ParseStatementSequence()
    {
        var statements = new List<OberonCompilerStatement>();
        while (_current.Kind is not (OberonCompilerTokenKind.End
            or OberonCompilerTokenKind.Else
            or OberonCompilerTokenKind.Elsif
            or OberonCompilerTokenKind.Until
            or OberonCompilerTokenKind.Bar
            or OberonCompilerTokenKind.EndOfText))
        {
            if (_current.Kind == OberonCompilerTokenKind.Semicolon)
            {
                Next();
                continue;
            }

            var statement = ParseStatement();
            if (statement is not null)
            {
                statements.Add(statement);
            }
            if (_current.Kind == OberonCompilerTokenKind.Semicolon)
            {
                Next();
            }
            else if (_current.Kind is not (OberonCompilerTokenKind.End
                or OberonCompilerTokenKind.Else
                or OberonCompilerTokenKind.Elsif
                or OberonCompilerTokenKind.Until
                or OberonCompilerTokenKind.Bar
                or OberonCompilerTokenKind.EndOfText))
            {
                AddDiagnostic("; expected between statements");
                RecoverTo(OberonCompilerTokenKind.Semicolon,
                    OberonCompilerTokenKind.End,
                    OberonCompilerTokenKind.EndOfText);
                if (_current.Kind == OberonCompilerTokenKind.Semicolon)
                {
                    Next();
                }
            }
        }

        return new OberonCompilerStatementSequence(statements);
    }

    private OberonCompilerStatement? ParseStatement()
    {
        if (_current.Kind == OberonCompilerTokenKind.If)
        {
            return ParseIfStatement();
        }

        if (_current.Kind == OberonCompilerTokenKind.While)
        {
            return ParseWhileStatement();
        }

        if (_current.Kind == OberonCompilerTokenKind.Repeat)
        {
            return ParseRepeatStatement();
        }

        if (_current.Kind == OberonCompilerTokenKind.For)
        {
            return ParseForStatement();
        }

        if (_current.Kind == OberonCompilerTokenKind.Case)
        {
            return ParseCaseStatement();
        }

        if (_current.Kind == OberonCompilerTokenKind.Return)
        {
            Next();
            var value = _current.Kind == OberonCompilerTokenKind.Semicolon
                ? null
                : ParseRuntimeExpression();
            return new OberonCompilerReturnStatement(value);
        }

        if (_current.Kind != OberonCompilerTokenKind.Identifier)
        {
            AddDiagnostic("statement expected");
            Next();
            return null;
        }

        var token = _current;
        Next();
        var expression = ParseRuntimeNameOrCall(token);
        if (_current.Kind == OberonCompilerTokenKind.Becomes)
        {
            Next();
            return new OberonCompilerAssignmentStatement(expression, ParseRuntimeExpression());
        }

        if (expression is OberonCompilerCallExpression call)
        {
            ValidateProcedureCall(call.Procedure);
            return new OberonCompilerCallStatement(call.Procedure, call.Arguments);
        }

        if (expression is OberonCompilerNameExpression name)
        {
            ValidateProcedureCall(name);
            return new OberonCompilerCallStatement(name, []);
        }

        AddDiagnostic("procedure call expected");
        return null;
    }

    private OberonCompilerIfStatement ParseIfStatement()
    {
        Next();
        var branches = new List<OberonCompilerIfBranch>
        {
            ParseIfBranch()
        };

        while (_current.Kind == OberonCompilerTokenKind.Elsif)
        {
            Next();
            branches.Add(ParseIfBranch());
        }

        OberonCompilerStatementSequence? elseBody = null;
        if (_current.Kind == OberonCompilerTokenKind.Else)
        {
            Next();
            elseBody = ParseStatementSequence();
        }

        Expect(OberonCompilerTokenKind.End, "END expected after IF");
        return new OberonCompilerIfStatement(branches, elseBody);
    }

    private OberonCompilerIfBranch ParseIfBranch()
    {
        var condition = ParseRuntimeExpression();
        Expect(OberonCompilerTokenKind.Then, "THEN expected after IF condition");
        return new OberonCompilerIfBranch(condition, ParseStatementSequence());
    }

    private OberonCompilerWhileStatement ParseWhileStatement()
    {
        Next();
        var condition = ParseRuntimeExpression();
        Expect(OberonCompilerTokenKind.Do, "DO expected after WHILE condition");
        var body = ParseStatementSequence();
        Expect(OberonCompilerTokenKind.End, "END expected after WHILE");
        return new OberonCompilerWhileStatement(condition, body);
    }

    private OberonCompilerRepeatStatement ParseRepeatStatement()
    {
        Next();
        var body = ParseStatementSequence();
        Expect(OberonCompilerTokenKind.Until, "UNTIL expected after REPEAT body");
        return new OberonCompilerRepeatStatement(body, ParseRuntimeExpression());
    }

    private OberonCompilerForStatement ParseForStatement()
    {
        Next();
        var controlToken = _current;
        var control = controlToken.Kind == OberonCompilerTokenKind.Identifier
            ? ResolveRuntimeName(controlToken)
            : new OberonCompilerNameExpression(string.Empty, null);
        if (controlToken.Kind == OberonCompilerTokenKind.Identifier)
        {
            Next();
        }
        else AddDiagnostic("FOR control variable expected");
        Expect(OberonCompilerTokenKind.Becomes, ":= expected after FOR control variable");
        var start = ParseRuntimeExpression();
        Expect(OberonCompilerTokenKind.To, "TO expected in FOR statement");
        var end = ParseRuntimeExpression();
        OberonCompilerExpression step = new OberonCompilerIntegerLiteralExpression(_symbols.IntegerType, 1);
        if (_current.Kind == OberonCompilerTokenKind.By)
        {
            Next();
            step = ParseRuntimeExpression();
        }

        Expect(OberonCompilerTokenKind.Do, "DO expected in FOR statement");
        var body = ParseStatementSequence();
        Expect(OberonCompilerTokenKind.End, "END expected after FOR statement");
        return new OberonCompilerForStatement(control, start, end, step, body);
    }

    private OberonCompilerCaseStatement ParseCaseStatement()
    {
        Next();
        var selector = ParseRuntimeExpression();
        Expect(OberonCompilerTokenKind.Of, "OF expected in CASE statement");
        var arms = new List<OberonCompilerCaseArm>();
        while (_current.Kind is not (OberonCompilerTokenKind.Else
            or OberonCompilerTokenKind.End
            or OberonCompilerTokenKind.EndOfText))
        {
            var labels = new List<OberonCompilerCaseLabel>();
            while (true)
            {
                var start = ParseCaseLabelValue();
                var end = start;
                if (_current.Kind == OberonCompilerTokenKind.Range)
                {
                    Next();
                    end = ParseCaseLabelValue();
                    if (start > end)
                    {
                        AddDiagnostic("CASE label range must be ascending");
                    }
                }

                labels.Add(new OberonCompilerCaseLabel(start, end));

                if (_current.Kind != OberonCompilerTokenKind.Comma)
                {
                    break;
                }
                Next();
            }

            Expect(OberonCompilerTokenKind.Colon, ": expected after CASE labels");
            arms.Add(new OberonCompilerCaseArm(labels, ParseStatementSequence()));
            if (_current.Kind != OberonCompilerTokenKind.Bar)
            {
                break;
            }
            Next();
        }

        OberonCompilerStatementSequence? elseBody = null;
        if (_current.Kind == OberonCompilerTokenKind.Else)
        {
            Next();
            elseBody = ParseStatementSequence();
        }

        Expect(OberonCompilerTokenKind.End, "END expected after CASE statement");
        return new OberonCompilerCaseStatement(selector, arms, elseBody);
    }

    private long ParseCaseLabelValue()
    {
        var label = ParseConstantExpression();
        if (label.Type.Form is OberonCompilerTypeForm.Byte
            or OberonCompilerTypeForm.Character
            or OberonCompilerTypeForm.Integer)
        {
            return label.IntegerValue;
        }

        AddDiagnostic("CASE labels must be integer constants");
        return 0;
    }

    private IReadOnlyList<OberonCompilerExpression> ParseArgumentList()
    {
        Next();
        var arguments = new List<OberonCompilerExpression>();
        while (_current.Kind != OberonCompilerTokenKind.RightParenthesis
            && _current.Kind != OberonCompilerTokenKind.EndOfText)
        {
            arguments.Add(ParseRuntimeExpression());
            if (_current.Kind != OberonCompilerTokenKind.Comma)
            {
                break;
            }
            Next();
        }

        Expect(OberonCompilerTokenKind.RightParenthesis, ") expected after argument list");
        return arguments;
    }

}
