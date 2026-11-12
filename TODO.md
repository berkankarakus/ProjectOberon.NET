# TODO

The deterministic validation gate is `bash scripts/verify-dotnet.sh`. The
external `RISC-work.img` flow remains a separate integration requirement.

## Compiler

- [ ] [P0] Lower external procedure calls through RSC import fixups and add an executable imported-call fixture.
- [ ] [P0] Generate type descriptors and pointer fixup tables for RSC output, then validate them through module loading.
- [ ] [P1] Complete aggregate field, array-element and pointer-dereference lowering with record/array execution fixtures.
- [ ] [P1] Complete record and open-array parameter lowering for all supported forms and cover value/VAR combinations.
- [ ] [P1] Implement `IS` type-test lowering with positive, negative and incompatible-type cases.
- [ ] [P1] Load imported constants, variables and type metadata from SMB files and verify compiler diagnostics for missing exports.

## Integration

- [ ] [P0] Add a controlled `RISC-work.img` fixture to CI for boot and input tests.
- [ ] [P1] Add end-to-end compiler fixtures covering pointers, records, arrays and imports.
- [ ] [P1] Add an SDL frontend smoke test that does not require an interactive session.
