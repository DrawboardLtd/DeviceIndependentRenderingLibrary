# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Fork relationship

This repo is a **private fork** of an upstream repo of the same name (`DIR.Lib`). It is for internal drawboard use and must **not** publish to nuget.org — only the upstream repo does.

When syncing from upstream: copy code changes but skip nuget publish CI jobs, and do not overwrite LICENSE, README title, or `RepositoryUrl` in `DIR.Lib.csproj`. This fork may also carry extra tests not present upstream.

## Common commands

Run from the repo root. CI (`.github/workflows/dotnet.yml`) uses `ubuntu-latest` with `dotnet 10.0.x`.

```bash
dotnet restore
dotnet build -c Release
dotnet test src/DIR.Lib.Tests                   # all tests
dotnet test src/DIR.Lib.Tests --filter "FullyQualifiedName~RenderText_HelloWorld"   # single test
```

### Regenerating render baselines

`RenderAcceptanceTests` compares output against BMPs in `src/DIR.Lib.Tests/Baselines/`. To regenerate them after an intentional rendering change:

```bash
DIR_LIB_UPDATE_BASELINES=1 dotnet test src/DIR.Lib.Tests
```

The test harness writes updates back to the source `Baselines/` directory (not just `bin/`). Review and commit the diffed BMPs.

## Architecture

Single library project (`src/DIR.Lib`) + test project (`src/DIR.Lib.Tests`). Target: `net10.0`, `Nullable=enable`, `AllowUnsafeBlocks=true`, `IsAotCompatible=true`. Central package management via `src/Directory.Packages.props`.

The library is the **shared foundation** consumed by platform packages (SDL3+Vulkan, Console). It deliberately has no platform-specific code; platform bridges live downstream and provide mappings like `SdlInputMapping` / `ConsoleInputMapping` to `InputKey`.

### Core abstraction: `Renderer<TSurface>`

`Renderer<TSurface>` is the abstract base for all rendering backends. `TSurface` is the platform surface type (e.g. Vulkan image, `RgbaImage`). Only `RgbaImageRenderer` lives in this library — it's what tests and headless scenarios use. GPU and terminal renderers are downstream.

When adding a primitive to `Renderer<TSurface>`, provide a default virtual implementation where reasonable (see `FillRectangles`) so backends can opt into batching without breaking.

### Glyph pipeline

`ManagedFontRasterizer` is a pure-managed, AOT-compatible rasterizer backed by the `SharpAstro.Fonts` NuGet package. It produces `GlyphBitmap` (raw RGBA with bearing/advance) and `SdfGlyphBitmap` (signed-distance-field for GPU sampling). COLRv1 color glyphs are handled natively inside the rasterizer — there is no separate COLR renderer class anymore. Glyphs carry `IsColored` to distinguish COLR/CBDT output from alpha-only monochrome output. Cmap lookup strategy for PDF subset fonts is driven by `GlyphMapHint` (Auto / EmbeddedSubset / CharCodeIsGID / Unicode).

Color/subset/COLR fixtures live at `src/DIR.Lib.Tests/Fonts/` (`Noto-COLRv1.ttf`, `BabelStoneXiangqiColour.ttf`, `Merida.ttf`, and several `*_subset.ttf` PDF-embedding fixtures).

The test project additionally references `SharpAstro.FreeTypeBindings` as a ground-truth reference for hinting comparisons — **the library itself has no FreeType dependency** after Phase 12.

### Widget / input layer

- `IWidget` — minimal shared interface (`HandleKeyDown`, `HandleMouseWheel`) usable from both pixel and terminal UIs.
- `IPixelWidget` / `PixelWidgetBase<TSurface>` — adds hit testing + click dispatch. Widgets call `RegisterClickable` during render; the tracker walks regions in reverse for hit tests, producing a `HitResult` discriminated union (`TextInputHit`, `ButtonHit`, `ListItemHit`, `SlotHit<T>`, `SliderHit`).
- `PixelLayout` + `PixelDockStyle` — dock-based layout (Top/Bottom/Left/Right/Fill) consumed during render.
- `TextInputState` + `TextInputRenderer` — single-line input state machine (cursor, selection, undo) with async `OnCommit` and a renderer that works against any `Renderer<T>`.

### Cross-frame coordination

- `SignalBus` — thread-safe, typed, deferred bus. Widgets `Post<T>` during event handling; hosts `Subscribe<T>` at startup. Delivery happens when the host calls `ProcessPending` (convention: once per frame, after input, before render). Not thread-safe for subscription itself.
- `BackgroundTaskTracker` — collects `Task`s; `ProcessCompletions(logger)` is called each frame and returns `true` if anything completed (signalling a redraw). `DrainAsync` at shutdown.

## Conventions

- `InternalsVisibleTo("DIR.Lib.Tests")` is set — tests can reach internals; keep public surface intentional.
- Keep the library AOT-friendly: no dynamic code gen, no reflection-heavy patterns. Generic `TSurface` erasure is by design.
- Tests use xUnit v3 + Shouldly. Acceptance tests that depend on optional fonts check `File.Exists` and silently return if missing, rather than failing.
