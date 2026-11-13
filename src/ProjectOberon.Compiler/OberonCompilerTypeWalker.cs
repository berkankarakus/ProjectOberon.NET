namespace ProjectOberon.Compiler;

internal static class OberonCompilerTypeWalker
{
    public static void VisitPointerLocations(
        OberonCompilerType? type,
        long offset,
        Action<long> visitor
    )
    {
        ArgumentNullException.ThrowIfNull(visitor);

        VisitPointerLocationsCore(type, offset, visitor);
    }

    private static void VisitPointerLocationsCore(
        OberonCompilerType? type,
        long offset,
        Action<long> visitor
    )
    {
        if (type is null)
        {
            return;
        }

        switch (type.Form)
        {
            case OberonCompilerTypeForm.Pointer:
            case OberonCompilerTypeForm.Nil:
                visitor(offset);
                return;
            case OberonCompilerTypeForm.Record:
                if (type.Base is not null)
                {
                    VisitPointerLocationsCore(type.Base, offset, visitor);
                }

                foreach (var field in type.Fields)
                {
                    VisitPointerLocationsCore(
                        field.Type,
                        checked(offset + field.Value),
                        visitor);
                }

                return;
            case OberonCompilerTypeForm.Array:
                if (type.Base is null || type.Length < 0)
                {
                    return;
                }

                for (var index = 0; index < type.Length; index++)
                {
                    VisitPointerLocationsCore(
                        type.Base,
                        checked(offset + (long)index * type.Base.Size),
                        visitor);
                }

                return;
            default:
                return;
        }
    }
}
