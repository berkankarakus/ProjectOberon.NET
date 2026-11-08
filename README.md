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
`OberonKernel` now owns the shared 1024-byte logical-sector mapping and sector
allocation map used by both the kernel port and the filesystem reader.
`OberonDiskLayout`, `OberonFileHeaderCodec` and `OberonDirectoryPageCodec`
centralize the disk-sector, file-header and directory-page binary layouts used
by the read-only and persistent paths.
`OberonFiles`/`OberonRider` provide the `Files.Mod` read path and rider
serialization helpers (`ReadInt`, `ReadString`, `ReadNum` and their write-side
memory representation). `OberonFileDirectory` now ports the `FileDir.Mod`
directory B-tree search, insertion/split, deletion/rebalancing and rename
operations. The Kernel-backed `OberonFiles` path now persists headers, data
pages and extension index pages, and rebuilds the sector allocation map when a
disk is reopened.
`OberonText`, `OberonTextBuffer`, `OberonTextPiece` and `OberonTextTypes`
provide the first `Texts.Mod` editing slice: piece-based insertion/deletion,
range copying, style attributes, change notifications and sequential readers.
`OberonTextScanner` continues that slice with Oberon names, strings, decimal
and hexadecimal integers, real numbers, character tokens and source line
tracking.
`OberonTextWriter` creates styled buffers and supports line breaks, padded
integers and hexadecimal output for future editor and tool ports.
`OberonFont` reads the original `.Scn.Fnt` raster format, and
`OberonDisplay.DrawGlyph`/`DrawText` render those glyphs into the emulated
framebuffer. Display pixel, pattern/block and font-raster operations are kept
in focused source files behind the same display API.
`OberonTextFrame` adds the first `TextFrames.Mod` slice with explicit line
breaks, width-based wrapping and line-by-line framebuffer rendering. Its
layout, viewport, rendering and caret/hit-testing code are kept in focused
source files while preserving the same frame API. It also maps caret
positions and mouse coordinates to the laid-out text.
`OberonTextCodec` stores and restores styled text using the original Oberon
TextBlock run format and can create/load `OberonFile` instances from it.
`OberonFiles.OpenText` and `RegisterText` connect that codec to the persistent
Oberon filesystem, while keeping read-only backends read-only.
`OberonTextEditor` adds cursor movement, selection, insertion, backspace/delete
and mouse-to-cursor editing operations over a text frame.
`OberonTextFrame` can also render selection highlights and the caret into the
framebuffer.
`OberonModuleLoader` ports the first `Modules.Mod` runtime layer: recursive
RSC import loading, module caching, key-conflict and cycle detection, command
lookup, and reference-counted freeing. Load exceptions, module metadata and
recursive loading operations are kept in focused source files.
`OberonSymbolModuleReader` reads `.smb` headers and exported symbol metadata;
the loader can register those symbols as external, host-owned modules.
`OberonModuleMemoryLoader` materializes that module graph in emulator RAM,
writes module descriptors and tables, and applies the `Modules.Mod` BL, data
and type-descriptor fixup chains. The CPU boot path is not wired to invoke
the host loader yet; direct module startup is available through
`OberonModuleRuntime` and the CLI’s `--start-module` option.
`OberonModuleRuntime.BindExternal` can bind an external module to existing
emulated data/code addresses and export offsets; it deliberately does not
invent executable stubs for host-only C# implementations.

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

An experimental host text editor can be opened over the framebuffer with an
Oberon screen font:

```sh
dotnet run --project dotnet/ProjectOberon.SdlUi/ProjectOberon.SdlUi.csproj -- \
  --editor notes.txt \
  --font systools/Oberon10.Scn.Fnt
```

Editor mode reads and writes the selected file as Latin-1 text. SDL text input
inserts characters, arrow/Home/End and Up/Down move the caret, Shift extends a selection,
Ctrl-A selects all, Ctrl-C/Ctrl-X/Ctrl-V copy, cut and paste through the SDL
clipboard, Backspace/Delete edit it, Ctrl-Z/Ctrl-Y undo and redo edits, the
left mouse button places or drags the selection, and Ctrl-S saves.
PageUp/PageDown scroll the text surface, and caret movement keeps the active line visible. The
emulated machine continues running in the background, but editor mode displays
the host text surface; word wrapping is width-based and horizontal scrolling
is not implemented yet.

The emulator library also contains a UI-independent viewer core based on
Oberon `Viewers.Mod`. `OberonViewerSystem` models tracks and vertically split
viewer regions, and exposes open/close/change, hit-testing, input dispatch and
broadcast operations. Menu drawing and SDL integration remain separate so the
viewer behavior can be tested without a window.

`OberonMenuViewer` builds on that core with separate menu and main child frames,
routes pointer input using the Oberon menu boundary, and propagates restore,
resize and suspend transitions to the child handlers. Frame messages, child
state, routing and layout are kept in focused source files.

`OberonTextFrameViewer` attaches the existing text frame/editor to the main
child frame. It renders through the viewer viewport and keeps selection, caret,
mouse placement and scrolling consistent when the viewer is resized.
The shared `OberonTextViewportController` also keeps scrolling and cursor
visibility behavior consistent with the SDL host editor.

The viewer system also exposes typed Oberon input/control messages, focus
transfer, keyboard dispatch and control broadcasting. A focused text viewer
consumes character input through the same editor used by the host SDL surface.

The SDL host editor routes text input, keyboard commands and scaled mouse
tracking through this typed viewer message path. Its bridge is testable without
opening an SDL window.

`OberonEventLoop` preserves the Oberon loop ordering for escape, set-star,
focused characters and mouse tracking. It is used by the host editor; the
normal emulator path continues to feed the emulated keyboard and mouse MMIO
devices directly. SDL constants, event layouts and native bindings are kept
behind focused source files in the SDL project.

List the files in an Oberon disk image without booting the emulator:

```sh
dotnet run --project dotnet/ProjectOberon.FsCli/ProjectOberon.FsCli.csproj -- \
  --disk RISC-work.img
```

The filesystem reader follows `FileDir.Mod` and `Kernel.Mod`: it understands
the 256 MB filesystem offset, 1024-byte logical sectors, and the directory
B-tree.

The filesystem CLI can also inspect and modify an existing image. Listing and
`--cat` are read-only by default; mutations require the explicit `--writable`
flag:

```sh
dotnet run --project dotnet/ProjectOberon.FsCli/ProjectOberon.FsCli.csproj -- \
  --disk RISC-work.img --cat System.rsc > System.rsc

dotnet run --project dotnet/ProjectOberon.FsCli/ProjectOberon.FsCli.csproj -- \
  --disk RISC-work.img --put Demo.Mod Demo.Mod --writable
dotnet run --project dotnet/ProjectOberon.FsCli/ProjectOberon.FsCli.csproj -- \
  --disk RISC-work.img --rename Demo.Mod Demo2.Mod --writable
dotnet run --project dotnet/ProjectOberon.FsCli/ProjectOberon.FsCli.csproj -- \
  --disk RISC-work.img --delete Demo2.Mod --writable
```

`--put` refuses to overwrite an existing Oberon file. Use a copy of the disk
image when experimenting with mutating operations.

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

Start a compiled module directly from the disk image using the C# module
loader. This prepares its RAM image, module table, stack and body entry point
before executing the requested number of instructions:

```sh
dotnet run --project dotnet/ProjectOberon.Cli/ProjectOberon.Cli.csproj -- \
  --disk RISC-work.img \
  --start-module Oberon \
  --steps 1000
```

To start at a named exported command instead of the module body entry point,
add `--command`:

```sh
dotnet run --project dotnet/ProjectOberon.Cli/ProjectOberon.Cli.csproj -- \
  --disk RISC-work.img \
  --start-module System \
  --command Directory \
  --steps 1000
```

The command must be present in the module's RSC command table.

The reference `RISC-work.img` keeps `Kernel`, `Files` and `Modules` as
kernel-linked modules and stores only their `.smb` symbol files. A direct
`--start-module Oberon` run therefore requires host-backed bindings for those
core modules; the CLI reports the missing binding instead of treating it as a
normal RSC file.
