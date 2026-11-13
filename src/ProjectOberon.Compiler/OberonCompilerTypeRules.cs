namespace ProjectOberon.Compiler;

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

    public static bool IsOpenArray(OberonCompilerType? type) =>
        type?.Form == OberonCompilerTypeForm.Array && type.Length < 0;

    public static bool IsBaseType(
        OberonCompilerType? type,
        OberonCompilerType baseType)
    {
        for (var current = type; current is not null; current = current.Base)
        {
            if (current == baseType)
            {
                return true;
            }
        }

        return false;
    }

    public static bool AreRelatedRecordTypes(
        OberonCompilerType left,
        OberonCompilerType right)
    {
        return IsBaseType(left, right) || IsBaseType(right, left);
    }
}
