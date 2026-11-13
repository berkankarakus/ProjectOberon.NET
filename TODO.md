# TODO

The deterministic validation gate is `bash scripts/verify-dotnet.sh`. The
external `RISC-work.img` flow remains a separate integration requirement. The
current ABI also cannot carry the length of a nested dynamic open-array
selector; the semantic checker rejects that form with a deterministic error.
Compiler roadmap batches 51-75 are complete; the unchecked integration item
below requires an external disk artifact.

## Compiler

- [x] [P0] Complete external RSC call lowering for aggregate record parameters and remaining `VAR` forms; scalar, array, pointer and record layouts are covered.
- [x] [P0] Generate local record type descriptors and module pointer root tables for RSC output; validate descriptor loading, type fixups and shifted global data reads and writes.
- [x] [P0] Lower imported scalar variable reads and writes through RSC data fixups.
- [x] [P0] Preserve imported record base metadata in the compiler type graph.
- [x] [P0] Emit and link imported record-base descriptors in RSC output; descriptor emission, runtime execution and multi-level extension tests are covered.
- [x] [P0] Parse recursive pointer-field declarations and include their module root references in RSC output.
- [x] [P1] Complete aggregate field, array-element and pointer-dereference expression lowering; aggregate expressions use their address as the value representation.
- [x] [P1] Lower aggregate function results when their address is required by assignment or parameter passing.
- [x] [P1] Complete record parameter lowering and cover record/open-array value and VAR combinations.
- [x] [P1] Implement local exact-pointer `IS` lowering with NIL handling.
- [x] [P1] Extend local `IS` lowering for record extensions and extension-level descriptor lookups.
- [x] [P1] Diagnose imported `IS` expressions before code emission when no local descriptor is available.
- [x] [P1] Extend `IS` lowering for imported descriptors and non-matching runtime tags.
- [x] [P1] Complete imported nested array/record type-reference metadata; record fields, array elements and pointer targets are preserved.
- [x] [P1] Preserve imported record-valued function result sizes in SMB metadata and compiler symbols.
- [x] [P1] Complete recursive SMB type reference resolution before importing nested field metadata.

## Integration

- [ ] [P0] Provide a CI-owned compatible `RISC-work.img` artifact for boot and input tests; ROM/disk binaries are intentionally excluded from this repository.
- [x] [P1] Add end-to-end compiler fixtures covering pointers, records, arrays and imports; the deterministic RSC emulator suite covers these paths.
- [x] [P1] Add an SDL frontend smoke test that does not require an interactive session; UI logic, input, framebuffer and editor routing are covered without opening a window.
