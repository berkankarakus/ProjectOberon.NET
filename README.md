# C# emulator

This directory contains the incremental C# implementation of the RISC5
emulator. The C++ implementation remains the reference implementation while
the port is developed and compared against it.

The first slice contains:

- RISC5 CPU instruction execution and flags
- ROM and RAM memory bus
- MMIO timer, UART, keyboard, mouse, GPIO and LED devices
- SPI/SD disk access and disk-image I/O
- a command-line runner
- behavior tests for the CPU core

Build and run the tests:

```sh
dotnet build dotnet/ProjectOberon.Cli/ProjectOberon.Cli.csproj
dotnet run --project dotnet/ProjectOberon.Emulator.Tests/ProjectOberon.Emulator.Tests.csproj
```

Run the CLI:

```sh
dotnet run --project dotnet/ProjectOberon.Cli/ProjectOberon.Cli.csproj -- \
  --rom RISC5Verilog/prom.mem \
  --disk RISC-work.img \
  --read-only \
  --steps 100000
```

The C# and C++ runners use the same ROM and disk image. Their final CPU state
is compared during development to catch instruction, MMIO, timer and SPI
compatibility regressions.

`--trace N` prints the first `N` instructions with the program counter and
selected registers, which is useful when learning the boot ROM and debugging
differences between the two implementations.

List the files in an Oberon disk image without booting the emulator:

```sh
dotnet run --project dotnet/ProjectOberon.FsCli/ProjectOberon.FsCli.csproj -- \
  --disk RISC-work.img
```

The filesystem reader follows `FileDir.Mod` and `Kernel.Mod`: it understands
the 256 MB filesystem offset, 1024-byte logical sectors, and the directory
B-tree.

Inspect a compiled Oberon module using the layout read by `Modules.Mod`:

```sh
dotnet run --project dotnet/ProjectOberon.RscCli/ProjectOberon.RscCli.csproj -- \
  --disk RISC-work.img \
  --module Oberon.rsc
```

Trace the compiled modules whose header sectors are touched during boot:

```sh
dotnet run --project dotnet/ProjectOberon.Cli/ProjectOberon.Cli.csproj -- \
  --rom RISC5Verilog/prom.mem \
  --disk RISC-work.img \
  --read-only \
  --steps 100000 \
  --boot-trace
```
