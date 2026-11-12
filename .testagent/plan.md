# Test-generation plan

1. Inventory static source/test pairings and use the existing compiler/RSC test
   suites as the target locations.
2. Add focused regression tests for remaining nested imported metadata and
   external-call combinations.
3. Implement only the production changes required by a failing regression,
   keeping unsupported paths explicit instead of silently lowering them.
4. Run the executable test project after each coherent batch and commit the
   batch locally.
5. Run `bash scripts/verify-dotnet.sh`, review assertions, refresh CodeGraph,
   and record final status before reporting.

## Planned batch groups

- SMB reader/model: nested type metadata, malformed input boundaries.
- Compiler symbols/parser: imported type construction and assignability.
- Code generation: aggregate selectors, pointer/array address paths.
- RSC: external record/array/pointer and `VAR` combinations.
- Final validation: static pairing review, CodeGraph sync, full verification.
