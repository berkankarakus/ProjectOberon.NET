namespace ProjectOberon.Emulator;

internal static class OberonCompilerTypeRules
{
    public static bool IsScalar(OberonCompilerType? type) => type?.Form is
        OberonCompilerTypeForm.Byte or OberonCompilerTypeForm.Boolean
        or OberonCompilerTypeForm.Character or OberonCompilerTypeForm.Integer;

    public static bool IsByteSized(OberonCompilerType? type) => type?.Form is
        OberonCompilerTypeForm.Byte or OberonCompilerTypeForm.Boolean
        or OberonCompilerTypeForm.Character;
}
