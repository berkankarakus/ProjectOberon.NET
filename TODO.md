# TODO

The deterministic validation gate is `bash scripts/verify-dotnet.sh`. The
external `RISC-work.img` flow remains a separate integration requirement.

## Compiler

- [x] [P0] Complete external RSC call lowering for aggregate record parameters and remaining `VAR` forms; scalar, array, pointer and record layouts are covered.
- [x] [P0] Generate local record type descriptors and module pointer root tables for RSC output; validate descriptor loading, type fixups and shifted global data reads and writes.
- [x] [P0] Lower imported scalar variable reads and writes through RSC data fixups.
- [x] [P0] Preserve imported record base metadata in the compiler type graph.
- [ ] [P0] Emit and link imported record-base descriptors in RSC output.
- [x] [P0] Parse recursive pointer-field declarations and include their module root references in RSC output.
- [ ] [P1] Complete aggregate field, array-element and pointer-dereference lowering with record/array execution fixtures.
- [x] [P1] Complete record parameter lowering and cover record/open-array value and VAR combinations.
- [x] [P1] Implement local exact-pointer `IS` lowering with NIL handling.
- [x] [P1] Extend local `IS` lowering for record extensions and extension-level descriptor lookups.
- [ ] [P1] Extend `IS` lowering for imported descriptors and non-matching runtime tags.
- [x] [P1] Complete imported nested array/record type-reference metadata; record fields, array elements and pointer targets are preserved.

## Integration

- [ ] [P0] Add a controlled `RISC-work.img` fixture to CI for boot and input tests.
- [ ] [P1] Add end-to-end compiler fixtures covering pointers, records, arrays and imports.
- [ ] [P1] Add an SDL frontend smoke test that does not require an interactive session.
