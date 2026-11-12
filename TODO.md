# TODO

## Compiler

- [ ] Lower external procedure calls through RSC import fixups.
- [ ] Generate type descriptors and pointer fixup tables for RSC output.
- [ ] Complete aggregate field, array-element and pointer-dereference lowering.
- [ ] Complete record and open-array parameter lowering for all supported forms.
- [ ] Implement `IS` type-test lowering.
- [ ] Load imported constants, variables and type metadata from SMB files.

## Integration

- [ ] Add a controlled `RISC-work.img` fixture to CI for boot and input tests.
- [ ] Add end-to-end compiler fixtures covering pointers, records, arrays and imports.
- [ ] Add an SDL frontend smoke test that does not require an interactive session.
