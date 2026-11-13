internal static partial class SymbolModuleTests
{
    public static void Run()
    {
        ParsesBuiltinTypeReference();
        ParsesRecursivePointerTypeReference();
        ParsesExportedSymbol();
        ParsesTypeFixupsAndReexport();
        ParsesRecordAndProcedureTypes();
        RejectsUnsupportedVersion();
        RejectsTruncatedModule();
        RejectsInvalidTypeForm();
        RejectsUnknownTypeReference();
        RejectsNonAsciiStrings();
        RejectsTrailingBytes();
        RejectsInvalidModuleNames();
        RejectsDuplicateExports();
        RejectsOverlongNumbers();
    }
}
