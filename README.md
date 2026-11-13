# ProjectOberon.NET

This repository contains the C#/.NET implementation of the Project Oberon
RISC5 emulator, runtime ports and Oberon compiler slices. It is an independent
implementation with its own history, build, tests and SDL2 frontend.

ROM and disk images are runtime inputs and are intentionally kept outside the
source repository. Supply compatible files with `--rom` and `--disk` when
running the emulator or its inspection tools.

The first slice contains:

- RISC5 CPU instruction execution and flags
- ROM and RAM memory bus
- MMIO timer, UART, keyboard, mouse, GPIO and LED devices
- SPI/SD disk access and disk-image I/O
- a command-line runner
- behavior tests for the CPU core

Build and run the deterministic test suite:

```sh
bash scripts/verify-dotnet.sh
```

The script builds the Release solution and runs the test runner without the
external disk fixture. Build artifacts are written to the user temporary
directory instead of the repository; set `PROJECT_OBERON_DOTNET_ARTIFACTS` to
override that location.

The boot/input integration test requires a compatible `RISC-work.img`. Set
`PROJECT_OBERON_DISK` to its path, or run the deterministic unit suite without
the external disk test:

```sh
PROJECT_OBERON_DISK=/path/to/RISC-work.img \
  dotnet run --project tests/ProjectOberon.Emulator.Tests/ProjectOberon.Emulator.Tests.csproj

  dotnet run --project tests/ProjectOberon.Emulator.Tests/ProjectOberon.Emulator.Tests.csproj -- \
  --skip-disk-tests
```

Run the CLI:

```sh
dotnet run --project src/ProjectOberon.Cli/ProjectOberon.Cli.csproj -- \
  --rom RISC5Verilog/prom.mem \
  --disk RISC-work.img \
  --read-only \
  --steps 100000
```

The runner accepts standard Project Oberon RISC5 ROM and disk images. Its
final CPU state, MMIO, timer and SPI behavior are covered by the included
deterministic tests.

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
and applies the `Modules.Mod` BL, data and type-descriptor fixup chains.
`OberonCoreHostBinding` composes the host-backed `Kernel`, `Files` and
`Modules` modules for a compatible disk image; SMB procedure and variable
values are preserved as sparse word-offset entry tables so imported RSC fixups
resolve to the correct byte addresses. Direct module startup is available
through `OberonModuleRuntime` and the CLI’s `--start-module` option.
`OberonModuleRuntime.BindExternal` can bind an external module to existing
emulated data/code addresses and export offsets; it deliberately does not
invent executable stubs for host-only C# implementations.

`--trace N` prints the first `N` instructions with the program counter and
selected registers, which is useful when learning the boot ROM and debugging
differences between the two implementations.

Build and run the C# SDL2 frontend:

```sh
dotnet run --project src/ProjectOberon.SdlUi/ProjectOberon.SdlUi.csproj -- \
  --rom RISC5Verilog/prom.mem \
  --disk RISC-work.img
```

The UI provides `F9` pause/resume, `F10` single-step,
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
`--scale N`, `--no-flip-y`, `--log FILE` and the shared module startup options
`--start-module NAME`, `--initialize`, `--boot` and `--command NAME`.

When diagnosing input or startup behavior, pass `--log FILE`. The log records
boot/startup state, SDL keyboard, mouse-button and text-input events, and
periodic CPU/frame progress without changing the emulated disk.

For example, start the `Oberon` event loop after initializing its imported
modules through the same host-backed boot session used by the CLI:

```sh
dotnet run --project src/ProjectOberon.SdlUi/ProjectOberon.SdlUi.csproj -- \
  --disk RISC-work.img \
  --read-only \
  --start-module Oberon \
  --boot
```

When `--start-module` is omitted, the SDL frontend keeps its original ROM boot
behavior.

An experimental host text editor can be opened over the framebuffer with an
Oberon screen font:

```sh
dotnet run --project src/ProjectOberon.SdlUi/ProjectOberon.SdlUi.csproj -- \
  --editor notes.txt \
  --font systools/Oberon10.Scn.Fnt
```

For a larger/bolder host presentation, use `apptools/Oberon12b.Scn.Fnt` or
`apptools/Oberon16.Scn.Fnt`. Editor mode renders a two-pixel colored caret and
a host mouse cursor over the text surface; normal emulated Oberon mode keeps
the original framebuffer cursor and font unchanged.

Editor mode reads and writes the selected file as Latin-1 text. SDL text input
inserts characters, arrow/Home/End and Up/Down move the caret, Shift extends a selection,
Ctrl-A selects all, Ctrl-C/Ctrl-X/Ctrl-V copy, cut and paste through the SDL
clipboard, Backspace/Delete edit it, Ctrl-Z/Ctrl-Y undo and redo edits, the
left mouse button places or drags the selection, and Ctrl-S saves.
PageUp/PageDown scroll the text surface, and caret movement keeps the active line visible. Long host
lines use a horizontal viewport that follows the caret. The emulated machine
continues running in the background, but editor mode displays the host text
surface without wrapping long lines.

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

`OberonTaskScheduler` ports the task-list portion of `Oberon.Mod`: task
creation, circular-list installation/removal, periodic dispatch, active/idle
state and the collector activity counter are available as a deterministic
host-side component with focused tests.

List the files in an Oberon disk image without booting the emulator:

```sh
dotnet run --project src/ProjectOberon.FsCli/ProjectOberon.FsCli.csproj -- \
  --disk RISC-work.img
```

The filesystem reader follows `FileDir.Mod` and `Kernel.Mod`: it understands
the 256 MB filesystem offset, 1024-byte logical sectors, and the directory
B-tree.

The filesystem CLI can also inspect and modify an existing image. Listing and
`--cat` are read-only by default; mutations require the explicit `--writable`
flag:

```sh
dotnet run --project src/ProjectOberon.FsCli/ProjectOberon.FsCli.csproj -- \
  --disk RISC-work.img --cat System.rsc > System.rsc

dotnet run --project src/ProjectOberon.FsCli/ProjectOberon.FsCli.csproj -- \
  --disk RISC-work.img --put Demo.Mod Demo.Mod --writable
dotnet run --project src/ProjectOberon.FsCli/ProjectOberon.FsCli.csproj -- \
  --disk RISC-work.img --rename Demo.Mod Demo2.Mod --writable
dotnet run --project src/ProjectOberon.FsCli/ProjectOberon.FsCli.csproj -- \
  --disk RISC-work.img --delete Demo2.Mod --writable
```

`--put` refuses to overwrite an existing Oberon file. Use a copy of the disk
image when experimenting with mutating operations.

Compile an Oberon source module and inspect the generated RISC5 instructions:

```sh
dotnet run --project src/ProjectOberon.CompilerCli/ProjectOberon.CompilerCli.csproj -- \
  path/to/Demo.Mod
```

Write the exported symbol file used by the module loader:

```sh
dotnet run --project src/ProjectOberon.CompilerCli/ProjectOberon.CompilerCli.csproj -- \
  path/to/Demo.Mod --symbols Demo.smb --no-disassemble
```

Emit a minimal RSC module for modules whose type descriptor and pointer-fixup
tables are not required:

```sh
dotnet run --project src/ProjectOberon.CompilerCli/ProjectOberon.CompilerCli.csproj -- \
  path/to/Demo.Mod --rsc Demo.rsc --no-disassemble
```

An external SMB module can be supplied for imported procedure resolution:

```sh
dotnet run --project src/ProjectOberon.CompilerCli/ProjectOberon.CompilerCli.csproj -- \
  path/to/Demo.Mod --import Files=Files.smb --no-disassemble
```

The current import slice preserves exported procedure arity, scalar and record
results, scalar parameters, fixed/open arrays, pointer targets, record sizes and
nested record fields. Qualified calls are validated against that metadata.
Imported external variables preserve their scalar, array, pointer and record
types as well. External scalar, array, pointer and aggregate record
calls—including `VAR` forms—are lowered into RSC program/data fixups.

The compiler CLI emits RSC type descriptors for local record types, type-fixup
chains for local record extensions, and pointer root tables for module data.
Global variables and strings are addressed after the descriptor section. RSC
descriptor support for imported record bases and imported descriptor-backed
variable offsets remains a separate compiler milestone.

The generated RSC currently supports scalar data, whole record/array copies and
character-array string payloads. The reader and writer reject duplicate import
or command names and validate command, entry, body and fixup offsets before the
module reaches the emulator memory loader. This keeps malformed artifacts as
compiler/loader errors instead of turning them into ambiguous runtime behavior.

Disk codecs apply the same strictness to names: directory and file-header
names must be ASCII and fit the Oberon field width, and truncated extension
pages are rejected before their addresses are read. Text/rider codecs reject
NUL or unrepresentable characters instead of silently replacing data.

Compiler limitations are currently explicit for aggregate field/array-element
and pointer-dereference loads, imported descriptor-backed variable offsets,
record-valued function result copying, imported-descriptor `IS` tests and
repeated or recursive SMB type references. These are planned compiler
milestones rather than silently accepted syntax.

Run the fast validation suite after changing compiler or RSC code:

```sh
dotnet build ProjectOberon.NET.slnx --disable-build-servers -m:1 -v:q
dotnet run --project tests/ProjectOberon.Emulator.Tests/ProjectOberon.Emulator.Tests.csproj -- \
  --skip-disk-tests
```

The suite includes compiler artifact round-trips, repeated-emission checks,
RSC boundary validation, runtime diagnostics and dependency-direction tests.

The same Release build and deterministic test flow can be run with:

```sh
bash scripts/verify-dotnet.sh
```

The project layout and dependency direction are documented in
[`docs/architecture.md`](docs/architecture.md) and enforced by the architecture
tests. Shared instruction/RSC contracts are kept in
`ProjectOberon.Contracts`; compiler implementation lives in
`ProjectOberon.Compiler` and does not reference the emulator runtime. RSC
serialization and validation live in `ProjectOberon.Rsc`.
SMB symbol-module parsing lives in `ProjectOberon.Symbols` so compiler and
runtime symbol consumers can share it without coupling to the emulator.

Inspect a compiled Oberon module using the layout read by `Modules.Mod`:

```sh
dotnet run --project src/ProjectOberon.RscCli/ProjectOberon.RscCli.csproj -- \
  --disk RISC-work.img \
  --module Oberon.rsc
```

Generate a Graphviz dependency graph for all compiled modules:

```sh
dotnet run --project src/ProjectOberon.RscCli/ProjectOberon.RscCli.csproj -- \
  --disk RISC-work.img --graph > oberon-modules.dot
dot -Tsvg oberon-modules.dot > oberon-modules.svg
```

Map compiled modules to their extracted Oberon sources and commands:

```sh
dotnet run --project src/ProjectOberon.RscCli/ProjectOberon.RscCli.csproj -- \
  --disk RISC-work.img --source-map --root .
```

The source map also reports source procedure line numbers and compares
exported `PROCEDURE name*` declarations with the commands stored in the RSC
module.

Disassemble the code for a compiled command and connect it to the source line:

```sh
dotnet run --project src/ProjectOberon.RscCli/ProjectOberon.RscCli.csproj -- \
  --disk RISC-work.img \
  --module System.rsc \
  --disassemble \
  --command Directory \
  --count 40 \
  --root .
```

Trace the compiled modules whose header sectors are touched during boot:

```sh
dotnet run --project src/ProjectOberon.Cli/ProjectOberon.Cli.csproj -- \
  --rom RISC5Verilog/prom.mem \
  --disk RISC-work.img \
  --read-only \
  --steps 100000 \
  --boot-trace
```

Generate a graph combining boot order and module imports:

```sh
dotnet run --project src/ProjectOberon.Cli/ProjectOberon.Cli.csproj -- \
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
dotnet run --project src/ProjectOberon.Cli/ProjectOberon.Cli.csproj -- \
  --disk RISC-work.img \
  --start-module Oberon \
  --steps 1000
```

To start at a named exported command instead of the module body entry point,
add `--command`:

```sh
dotnet run --project src/ProjectOberon.Cli/ProjectOberon.Cli.csproj -- \
  --disk RISC-work.img \
  --start-module System \
  --command Directory \
  --steps 1000
```

The command must be present in the module's RSC command table.

An Oberon `RISC-work.img` keeps `Kernel`, `Files` and `Modules` as
kernel-linked modules and stores only their `.smb` symbol files. The CLI loads
those symbol files and installs the host-backed bindings before starting an
ordinary RSC module. A read-only smoke run against a supplied image is:

```sh
dotnet run --project src/ProjectOberon.Cli/ProjectOberon.Cli.csproj -- \
  --disk RISC-work.img \
  --read-only \
  --start-module Oberon \
  --steps 100000
```

The command should print `started module Oberon` and finish with the CPU step
count; it does not modify the disk image.

Runtime failures from the CLI and SDL frontend are printed with the exception
type, message, CPU steps, PC, selected registers, flags and stack trace. This
makes a host-call or emulator fault actionable even when the SDL window closes.

For performance or hang investigation, the repository targets .NET 10 and the
cross-platform `dotnet-trace` tool can collect a managed trace:

```sh
dotnet-trace ps
dotnet-trace collect -p PID --duration 00:00:00:30 \
  -o artifacts/project-oberon.nettrace
dotnet-trace report artifacts/project-oberon.nettrace topN -n 20
```

Use the actual emulator PID from `dotnet-trace ps`. For a short-lived failure,
launch through `dotnet-trace collect -- ...` instead of attaching after start.
The `.nettrace` artifact can be opened in PerfView or converted to Speedscope
for visual inspection. Record the runtime version, OS, PID, UTC time window,
exact command and artifact path with the trace.

The emulator CLI's `--trace N` output includes `r12` (the Oberon module table),
`r13` (the active module data base), `r14` (the stack) and `r15` (the return
address). These registers are the first values to compare when a compiled
module's PC advances but its data or framebuffer does not change.

Compiler-produced minimal RSC files can be smoke-tested without a disk image
by using the test runner; the suite executes scalar and character-array module
bodies through the same memory loader used by the CLI.
