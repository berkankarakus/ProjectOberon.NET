using ProjectOberon.Emulator;

internal static class OberonCompilerScannerTests
{
    public static void Run()
    {
        ScansKeywordsAndOperators();
        ScansOberonLiterals();
        SkipsNestedCommentsAndTracksLocation();
        ReportsMalformedInput();
        ReportsLiteralOverflowAndMalformedHex();
    }

    private static void ScansKeywordsAndOperators()
    {
        var scanner = new OberonCompilerScanner("MODULE Demo; VAR x := 1..10; BEGIN x # 0 END. TO BY");

        TestAssertions.Equal(OberonCompilerTokenKind.Module, scanner.Scan().Kind);
        TestAssertions.Equal(OberonCompilerTokenKind.Identifier, scanner.Scan().Kind);
        TestAssertions.Equal(OberonCompilerTokenKind.Semicolon, scanner.Scan().Kind);
        TestAssertions.Equal(OberonCompilerTokenKind.Var, scanner.Scan().Kind);
        TestAssertions.Equal("x", scanner.Scan().Lexeme);
        TestAssertions.Equal(OberonCompilerTokenKind.Becomes, scanner.Scan().Kind);
        TestAssertions.Equal(1, scanner.Scan().IntegerValue);
        TestAssertions.Equal(OberonCompilerTokenKind.Range, scanner.Scan().Kind);
        TestAssertions.Equal(10, scanner.Scan().IntegerValue);
        TestAssertions.Equal(OberonCompilerTokenKind.Semicolon, scanner.Scan().Kind);
        TestAssertions.Equal(OberonCompilerTokenKind.Begin, scanner.Scan().Kind);
        TestAssertions.Equal(OberonCompilerTokenKind.Identifier, scanner.Scan().Kind);
        TestAssertions.Equal(OberonCompilerTokenKind.NotEqual, scanner.Scan().Kind);
        TestAssertions.Equal(0, scanner.Scan().IntegerValue);
        TestAssertions.Equal(OberonCompilerTokenKind.End, scanner.Scan().Kind);
        TestAssertions.Equal(OberonCompilerTokenKind.Period, scanner.Scan().Kind);
        TestAssertions.Equal(OberonCompilerTokenKind.To, scanner.Scan().Kind);
        TestAssertions.Equal(OberonCompilerTokenKind.By, scanner.Scan().Kind);
        TestAssertions.Equal(OberonCompilerTokenKind.EndOfText, scanner.Scan().Kind);
    }

    private static void ScansOberonLiterals()
    {
        var scanner = new OberonCompilerScanner("2AH 41X 1.5D-1 $41 42$");

        var integer = scanner.Scan();
        TestAssertions.Equal(OberonCompilerTokenKind.Integer, integer.Kind);
        TestAssertions.Equal(42, integer.IntegerValue);

        var character = scanner.Scan();
        TestAssertions.Equal(OberonCompilerTokenKind.Character, character.Kind);
        TestAssertions.Equal(0x41, character.CharacterValue);

        var real = scanner.Scan();
        TestAssertions.Equal(OberonCompilerTokenKind.Real, real.Kind);
        TestAssertions.Equal(0.15d, real.RealValue!.Value);

        var hexString = scanner.Scan();
        TestAssertions.Equal(OberonCompilerTokenKind.String, hexString.Kind);
        TestAssertions.Equal("AB", hexString.StringValue);
    }

    private static void SkipsNestedCommentsAndTracksLocation()
    {
        var scanner = new OberonCompilerScanner("(* outer (* inner *) done *)\r\nVAR");
        var token = scanner.Scan();

        TestAssertions.Equal(OberonCompilerTokenKind.Var, token.Kind);
        TestAssertions.Equal(1, token.Line);
        TestAssertions.Equal(0, token.Column);
        TestAssertions.Equal(0, scanner.Diagnostics.Count);
    }

    private static void ReportsMalformedInput()
    {
        var scanner = new OberonCompilerScanner("(* unfinished");
        TestAssertions.Equal(OberonCompilerTokenKind.EndOfText, scanner.Scan().Kind);
        TestAssertions.Equal(1, scanner.Diagnostics.Count);

        var invalid = new OberonCompilerScanner("\"unfinished").Scan();
        TestAssertions.Equal(OberonCompilerTokenKind.Invalid, invalid.Kind);
    }

    private static void ReportsLiteralOverflowAndMalformedHex()
    {
        var overflow = new OberonCompilerScanner("2147483648").Scan();
        var characterOverflow = new OberonCompilerScanner("100X").Scan();
        var malformedHex = new OberonCompilerScanner("$A$").Scan();
        var unterminatedHex = new OberonCompilerScanner("$41").Scan();

        TestAssertions.Equal(OberonCompilerTokenKind.Invalid, overflow.Kind);
        TestAssertions.Equal(OberonCompilerTokenKind.Invalid, characterOverflow.Kind);
        TestAssertions.Equal(OberonCompilerTokenKind.Invalid, malformedHex.Kind);
        TestAssertions.Equal(OberonCompilerTokenKind.Invalid, unterminatedHex.Kind);
    }
}
