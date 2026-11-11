namespace ProjectOberon.Emulator;

internal static class OberonCompilerTypeRules
{
    public static bool IsScalar(OberonCompilerType? type) => type?.Form is
        OberonCompilerTypeForm.Byte or OberonCompilerTypeForm.Boolean
        or OberonCompilerTypeForm.Character or OberonCompilerTypeForm.Integer
        or OberonCompilerTypeForm.Real or OberonCompilerTypeForm.Pointer
        or OberonCompilerTypeForm.Set;

    public static bool IsByteSized(OberonCompilerType? type) => type?.Form is
        OberonCompilerTypeForm.Byte or OberonCompilerTypeForm.Boolean
        or OberonCompilerTypeForm.Character;

    public static bool IsAggregate(OberonCompilerType? type) => type?.Form is
        OberonCompilerTypeForm.Array or OberonCompilerTypeForm.Record;
}
