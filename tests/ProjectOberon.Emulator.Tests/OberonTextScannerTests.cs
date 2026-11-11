using ProjectOberon.Emulator;

internal static partial class OberonTextTests
{
    private static void ScansOberonTokens()
    {
        var scanner = new OberonText("  VAR x := 2AH;\r\nvalue := -3.5E+1; \"ok\" !").OpenScanner();

        var varToken = scanner.Scan();
        TestAssertions.Equal(OberonScannerTokenKind.Name, varToken.Kind);
        TestAssertions.Equal("VAR", varToken.Lexeme);
        TestAssertions.Equal(0, varToken.Line);

        TestAssertions.Equal("x", scanner.Scan().Lexeme);
        TestAssertions.Equal(OberonScannerTokenKind.Character, scanner.Scan().Kind);
        TestAssertions.Equal(OberonScannerTokenKind.Character, scanner.Scan().Kind);
        var integer = scanner.Scan();
        TestAssertions.Equal(OberonScannerTokenKind.Integer, integer.Kind);
        TestAssertions.Equal(42, integer.IntegerValue);

        TestAssertions.Equal(OberonScannerTokenKind.Character, scanner.Scan().Kind);
        var real = scanner.Scan();
        TestAssertions.Equal(1, real.Line);
        TestAssertions.Equal(OberonScannerTokenKind.Name, real.Kind);
        TestAssertions.Equal("value", real.Lexeme);
        TestAssertions.Equal(OberonScannerTokenKind.Character, scanner.Scan().Kind);
        TestAssertions.Equal(OberonScannerTokenKind.Character, scanner.Scan().Kind);
        var number = scanner.Scan();
        TestAssertions.Equal(OberonScannerTokenKind.Real, number.Kind);
        TestAssertions.Equal(-35d, number.RealValue!.Value);
        TestAssertions.Equal(OberonScannerTokenKind.Character, scanner.Scan().Kind);
        var text = scanner.Scan();
        TestAssertions.Equal(OberonScannerTokenKind.String, text.Kind);
        TestAssertions.Equal("ok", text.StringValue);
        TestAssertions.Equal(OberonScannerTokenKind.Character, scanner.Scan().Kind);
        TestAssertions.Equal(OberonScannerTokenKind.EndOfText, scanner.Scan().Kind);

        TestAssertions.Equal(
            OberonScannerTokenKind.Invalid,
            new OberonText("\"unterminated").OpenScanner().Scan().Kind);
        TestAssertions.Equal(
            OberonScannerTokenKind.Invalid,
            new OberonText("999999999999999999999999").OpenScanner().Scan().Kind);
    }
}
