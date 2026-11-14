# Performance workflow

The repository keeps performance checks local and deterministic. They are not part
of the normal test run and do not require a CI job.

## .NET benchmark

```sh
bash scripts/benchmark-dotnet.sh
```

Set `PROJECT_OBERON_BENCHMARK_ITERATIONS` to change the loop size. The command
reports elapsed time, operations per second, and managed allocations for memory,
CPU, and compiler workloads. Compare runs on the same machine and build mode;
the output is a measurement aid, not a cross-machine score.

## SDL benchmark

The SDL benchmark uses the dummy video driver and exits after a fixed frame count:

```sh
PROJECT_OBERON_ROM=/path/to/prom.mem \
PROJECT_OBERON_DISK=/path/to/RISC-work.img \
bash scripts/benchmark-sdl.sh
```

`PROJECT_OBERON_SDL_BENCHMARK_FRAMES` and
`PROJECT_OBERON_SDL_BENCHMARK_STEPS` control the workload. A compatible ROM and
disk image are intentionally supplied by the caller and are never downloaded by
the benchmark.

## Trace collection

For a running .NET process, collect a short trace with:

```sh
dotnet-trace collect --process-id <pid> --duration 00:00:15 \
  --output oberon-ui.nettrace
```

Open the trace in PerfView or speedscope. For a repeatable local run, first use
the SDL frontend with `--frames` so the process has a bounded lifetime. Keep
trace files outside the repository unless they are deliberately being retained
as an investigation artifact.

## Review rules

1. Run the full local verification script before and after an optimization.
2. Keep code-size and runtime-step baselines in
   `OberonCompilerOptimizationBaselineTests`.
3. Prefer allocation-free changes on emulator/display hot paths, but retain the
   original boundary behavior for unaligned and wrapped memory accesses.
4. Do not add unsafe code or a benchmark package for a change that does not show
   a measurable improvement.

## Compiler peephole boundary

The compiler performs peephole-like reductions while emitting expressions and
branches. A generic post-emission deletion pass is intentionally not enabled:
branch labels store instruction indexes and removing an instruction would need
to rewrite every label and fixup. The current source-level folds preserve those
indexes and have golden fixtures, so a later post-emission pass should only be
added together with a label-aware representation and before/after measurements.
