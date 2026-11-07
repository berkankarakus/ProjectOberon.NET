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
