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

The first source-level Oberon port slice is available in the emulator library:
`OberonDisplay` implements the `Display.Mod` raster operations against the
emulated framebuffer, and `OberonKeyboard` implements the `Input.Mod` Set 2
state machine for break, extended, Shift and Ctrl sequences. The original
`.Mod` modules remain the executable reference while these C# slices grow.

`--trace N` prints the first `N` instructions with the program counter and
selected registers, which is useful when learning the boot ROM and debugging
differences between the two implementations.

Build and run the C# SDL2 frontend:

```sh
dotnet run --project dotnet/ProjectOberon.SdlUi/ProjectOberon.SdlUi.csproj -- \
  --rom RISC5Verilog/prom.mem \
  --disk RISC-work.img
```

The C# UI preserves the C++ controls: `F9` pause/resume, `F10` single-step,
`Escape` quit, keyboard forwarding, mouse input, framebuffer scaling and
vertical flip control. SDL text input is forwarded to the emulated UART receive
queue, while physical key events are translated to the Oberon Set 2 keyboard
codes. It uses the system SDL2 library through a small P/Invoke layer, so no
NuGet SDL wrapper is required.

Install SDL2 if it is not already available:

```sh
brew install sdl2                 # macOS
sudo apt install libsdl2-2.0-0    # Debian/Ubuntu runtime
```

The SDL frontend also supports `--read-only`, `--steps-per-frame N`,
`--scale N` and `--no-flip-y`.

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

Generate a Graphviz dependency graph for all compiled modules:

```sh
dotnet run --project dotnet/ProjectOberon.RscCli/ProjectOberon.RscCli.csproj -- \
  --disk RISC-work.img --graph > oberon-modules.dot
dot -Tsvg oberon-modules.dot > oberon-modules.svg
```

Map compiled modules to their extracted Oberon sources and commands:

```sh
dotnet run --project dotnet/ProjectOberon.RscCli/ProjectOberon.RscCli.csproj -- \
  --disk RISC-work.img --source-map --root .
```

The source map also reports source procedure line numbers and compares
exported `PROCEDURE name*` declarations with the commands stored in the RSC
module.

Disassemble the code for a compiled command and connect it to the source line:

```sh
dotnet run --project dotnet/ProjectOberon.RscCli/ProjectOberon.RscCli.csproj -- \
  --disk RISC-work.img \
  --module System.rsc \
  --disassemble \
  --command Directory \
  --count 40 \
  --root .
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

Generate a graph combining boot order and module imports:

```sh
dotnet run --project dotnet/ProjectOberon.Cli/ProjectOberon.Cli.csproj -- \
  --rom RISC5Verilog/prom.mem \
  --disk RISC-work.img \
  --read-only \
  --steps 1000000 \
  --boot-graph > oberon-boot.dot
```
