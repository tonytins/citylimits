# CityLimits scorecard

Maintained by hand. Every status below was checked against the code, a
build, or a run on 2026-10-20, and each row names the file, command, or log
that proves it. Numbers here supersede estimates elsewhere. What comes next
is in the [roadmap](./ROADMAP.md).

When a change affects a row, update its status and evidence in the same
change. Statuses are `done`, `partial`, and `missing`.

## Summary

| Area | Done | Partial | Missing | Key numbers |
|---|---:|---:|---:|---|
| [Core](#core) | 8 | 0 | 0 | 0.25 s ticks, seed 2000, 128 by 128 map |
| [Map](#map-and-terrain) | 4 | 0 | 0 | 4 kinds of ground, one 256 by 960 atlas |
| [Zoning](#roads-and-zoning) | 0 | 0 | 5 | 0 tools that change the world |
| [Simulation](#simulation) | 1 | 1 | 4 | 1 month a tick, no growth yet |
| [Economy](#economy-and-services) | 0 | 0 | 5 | 0 funds |
| [Traffic](#traffic-and-commuting) | 0 | 0 | 4 | 0 trips |
| [Environment](#land-value-pollution-and-disasters) | 0 | 0 | 5 | 0 disasters |
| [Interface](#player-interface) | 1 | 1 | 4 | 5 tools with icons, 1 status bar |
| [Platform](#platform) | 5 | 0 | 2 | Raylib-cs 8.1.0, NLog 6.2.2, .NET 10 |
| [Reliability](#reliability) | 4 | 1 | 1 | 46 tests, all passing |
| [Documentation](#documentation) | 3 | 0 | 2 | 3 of 5 documents exist |
| [Known issues](#known-issues) | | | | 2 open, 2 resolved |

The game today opens a resizable window on a seeded map of water, grass,
trees, and rock, drawn from a tile sheet. The camera pans and zooms, the
clock runs the city a month at a time, the tool palette shows the icon of each
tool, the status bar is in place, and the tile under the mouse is highlighted
and read out. The tools do nothing yet, and nothing in the world changes as the
clock runs. Everything below that line is missing, and that is the point of the
table: it is the contract the work has to meet.

How the state was verified on 2026-10-20:

```sh
dotnet build CityLimits.slnx                              # 3 projects: 0 warnings, 0 errors
dotnet test  CityLimits.Tests/CityLimits.Tests.csproj     # 46 passed, 0 failed
dotnet run --project CityLimits -- --headless --ticks 5  # 5 ticks logged to the console
dotnet run --project CityLimits                           # window opened on the desktop; the map, the palette icons, and the tile readout were drawn
```

The game was run on the desktop, and the window was captured and measured: the
map area holds the atlas colours (61% clear ground, 6% water, 5% trees), and
the tool icons are on screen in their own colours. The headless mode needs no
display at all, so the simulation can be checked on a machine with no screen.

## Core

| Id | Target | Status | Evidence |
|---|---|---|---|
| CORE-1 | The game opens a window and closes cleanly | done | `CityLimits/Program.cs`: `InitWindow` / `BeginDrawing` / `EndDrawing` / `CloseWindow`; the run on 2026-10-20 reached the render loop and exited when the window closed |
| CORE-2 | The engine-free half of the game lives in a core library that no engine can reach | done | `CityLimits.Core/CityLimits.Core.csproj` references no package at all, and `CityLimits.csproj` references the core library, never the other way round |
| CORE-3 | The simulation advances in fixed-size ticks, independent of the frame rate | done | `CityLimits.Core/Simulation/GameClock.cs`: a 0.25 s tick with an accumulator; `GameLoop.Update` runs the ticks that are due |
| CORE-4 | Pause and speed control (1x, 2x, 3x) change how many ticks run per frame, not the tick size | done | `GameLoop.HandleInput`: space toggles pause, comma and period change the speed; the speed multiplies the time fed to the clock, so the tick stays 0.25 s |
| CORE-5 | The mouse position is known in world space, and the keyboard is read | done | `Camera.ScreenToWorld` and `Raylib.GetMousePosition`; the HUD shows the world position, and the keys pan, zoom, and pick tools |
| CORE-6 | A camera pans and zooms, clamped to the map | done | `CityLimits/Rendering/Camera.cs`: `Pan`, `ZoomAt`, and `ClampTo`; the run on 2026-10-20 showed the view clamped to the 128 by 128 map |
| CORE-7 | One seeded PRNG owned by the simulation makes a run reproducible | done | `CityLimits.Core/Randomness/DeterministicRandom.cs`; the simulation owns it, and `GameLoop` builds the map from the seed 2000 |
| CORE-8 | Settings persist to disk and a bad settings file is reported, not fatal | done | `CityLimits.Core/Settings/SettingsStore.cs`: `Load` falls back to the defaults with a warning, `TrySave` reports a failure; the game writes `settings.json` beside the executable when it ends |

## Map and terrain

| Id | Target | Status | Evidence |
|---|---|---|---|
| MAP-1 | A tile grid of a fixed size, stored as arrays | done | `CityLimits.Core/World/TileMap.cs`: a `TileKind` array behind a width and a height, with the tile count checked against the size |
| MAP-2 | Terrain is generated from the seed, and the same seed gives the same map | done | `CityLimits.Core/World/TileMapGenerator.cs`: value noise over four octaves; `TileMapGeneratorTests` proves the same seed gives the same map |
| MAP-3 | Terrain is rendered from a tile atlas with no per-frame allocation | done | `CityLimits/Rendering/MapRenderer.cs` draws each visible tile from `CityLimits/Assets/tiles.png` with `DrawTexturePro`; `CityLimits/Rendering/TileAtlas.cs` holds the tile indices, and `GameAssets` loads the sheet once at startup and unloads it at shutdown. The captured window on 2026-10-20 holds the atlas colours: 61% clear ground, 6% water, 5% trees |
| MAP-4 | The tile under the mouse is highlighted and readable | done | `MapRenderer.DrawHover` outlines the tile under the pointer, and `Hud.DrawTileReadout` names it: the tile's place and what can be done with it, from `CityLimits.Core/World/TileInfo.cs` |

## Roads and zoning

| Id | Target | Status | Evidence |
|---|---|---|---|
| ZONE-1 | Roads can be drawn and bulldozed, with a cost the player must be able to pay | missing | No tools, no money |
| ZONE-2 | Residential, commercial, and industrial tiles can be zoned, by dragging | missing | No zone types |
| ZONE-3 | A zoned tile counts as served only when it reaches the road network | missing | No road graph |
| ZONE-4 | Buildings are drawn from the tile's own state, not a second list of buildings | missing | No buildings |
| ZONE-5 | Bulldozing clears a tile and refunds part of its cost | missing | No bulldozer |

## Simulation

| Id | Target | Status | Evidence |
|---|---|---|---|
| SIM-1 | A tick advances the date and updates the world | partial | `CitySimulation.Tick` moves the calendar on by a month and counts the tick; nothing in the world changes yet |
| SIM-2 | Population, jobs, and migration between zones | missing | No population model |
| SIM-3 | Residential, commercial, and industrial demand exist and are readable | missing | No demand model |
| SIM-4 | Zones develop and decay, and abandoned zones come back | missing | No growth model |
| SIM-5 | Housing capacity follows the development level of a tile | missing | No development levels |
| SIM-6 | The same seed and the same actions give the same city after N ticks | done | `CitySimulationTests.TheSameSeedAndTheSameTicksGiveTheSameCity`: 100 ticks on two cities from the same seed agree on the date, the tick count, and the generator |

## Economy and services

| Id | Target | Status | Evidence |
|---|---|---|---|
| ECON-1 | Power plants feed a grid, and each tile is powered or unserved | missing | No power model |
| ECON-2 | Power demand follows population, and a brownout is visible | missing | No demand or brownout |
| ECON-3 | Taxes, a budget, and a deficit the player has to answer | missing | No economy |
| ECON-4 | Police, fire, and parks have a coverage effect | missing | No services |
| ECON-5 | A budget panel and a power overlay show what the simulation thinks | missing | No panels or overlays |

## Traffic and commuting

| Id | Target | Status | Evidence |
|---|---|---|---|
| TRAF-1 | A road graph is built from the tile map and rebuilt only when roads change | missing | No road graph |
| TRAF-2 | Traffic volumes per road tile come from commuting trips | missing | No trips |
| TRAF-3 | Congestion above a road's capacity feeds back into demand and land value | missing | No capacity model |
| TRAF-4 | A traffic overlay shows the volumes, and a small map is covered by a test | missing | No overlay, no test project |

## Land value, pollution, and disasters

| Id | Target | Status | Evidence |
|---|---|---|---|
| ENV-1 | Land value comes from desirability factors and has an overlay | missing | No land value model |
| ENV-2 | Pollution comes from industry and traffic and lowers land value | missing | No pollution model |
| ENV-3 | Fires start, spread, and are answered by the fire service | missing | No fire model |
| ENV-4 | Earthquakes and floods happen on a timer, with a warning first | missing | No disasters |
| ENV-5 | Damage, repair cost, and recovery exist | missing | No repair model |

## Player interface

| Id | Target | Status | Evidence |
|---|---|---|---|
| UI-1 | A tool palette with a hotkey for every tool | done | `CityLimits/UI/ToolPaletteView.cs`: five tools, the keys 1 to 5, and the icon of each tool drawn from `CityLimits/Assets/tools/`. The captured window on 2026-10-20 shows the icons in their own colours |
| UI-2 | A HUD with population, funds, date, speed, and the demand bars | partial | `CityLimits/UI/Hud.cs` shows the date, the tick count, the speed, the held tool, the mouse position, the frame rate, and the tile under the mouse. There is no population, no funds, and no demand yet |
| UI-3 | Panels for the budget, power, and the city score | missing | No panels; the tool palette is the only panel |
| UI-4 | A city score that rates the city the way the classic games do | missing | No scoring |
| UI-5 | A city saves to disk and reloads exactly, and a corrupt file is reported, not fatal | missing | No save format |
| UI-6 | A start screen and a new-city flow: name, seed, and map size | missing | No screen flow |

## Platform

| Id | Target | Status | Evidence |
|---|---|---|---|
| PLAT-1 | The solution builds with no new warnings | done | `dotnet build CityLimits.slnx` on 2026-10-20: 3 projects, 0 warnings, 0 errors (see BUG-1) |
| PLAT-2 | The engine is pinned to a known version | done | `CityLimits/CityLimits.csproj`: `Raylib-cs` `8.1.0` and `NLog` `6.2.2`; `CityLimits.Core.csproj` references no package |
| PLAT-3 | The native engine ships with the package, so no system install is needed | done | `~/.nuget/packages/raylib-cs/8.1.0/runtimes/`: `linux-x64/native/libraylib.so`, `win-x64/native/raylib.dll`, `win-x86/native/raylib.dll` |
| PLAT-4 | The game runs on Linux | done | `xvfb-run -a dotnet run --project CityLimits` on 2026-10-20: window created, shaders compiled, default font loaded, clean exit |
| PLAT-5 | The game builds and runs on Windows | missing | Not attempted; no Windows machine or build check yet |
| PLAT-6 | A release build can be handed to someone else | missing | No packaging, no version, no repository |
| PLAT-7 | The city can be run and checked without a display | done | `dotnet run --project CityLimits -- --headless --ticks 100` runs the simulation with no window and writes each tick to the console; `CityLimits/Game/HeadlessRun.cs` and `CityLimits/Game/GameLog.cs`, which sends NLog to the console and nowhere else |

## Reliability

| Id | Target | Status | Evidence |
|---|---|---|---|
| REL-1 | Nothing ends the player's session: the frame loop catches, reports, and keeps drawing | done | `Program.Main` catches every exception around the update and the draw, and `GameLoop.ReportError` puts the message on screen; the game keeps running |
| REL-2 | The world and the simulation never call the engine | done | `CityLimits.Core` references no package at all, and the game references the core library; no Raylib type appears in the core |
| REL-3 | Input, tile indices, map sizes, and files are validated where they enter | partial | Map sizes, tile counts, and the settings file are checked, and the tile indexer throws outside the map. The mouse position is turned into a tile by `MapRenderer.TryGetTileAt`, which reports when the pointer is off the map. The tools still take no action, so nothing is written from the mouse yet |
| REL-4 | A test project covers the engine-free logic, and the tests pass | done | `CityLimits.Tests` references `CityLimits.Core` and holds 46 xUnit tests over the generator, the map, the calendar, the clock, the simulation, the settings, and the tile rules. All 46 passed on 2026-10-20 |
| REL-5 | A determinism test proves the same seed gives the same city | done | `DeterministicRandomTests.TheSameSeedGivesTheSameSequence` and `CitySimulationTests.TheSameSeedAndTheSameTicksGiveTheSameCity` |
| REL-6 | CI builds and tests on every push | missing | The repository has no commits and no workflow file, so there is nowhere to run CI |

## Documentation

| Id | Target | Status | Evidence |
|---|---|---|---|
| DOC-1 | An agent and contributor guide matches the code | done | `AGENTS.md`, reframed for CityLimits and updated for the three-project layout on 2026-10-20 |
| DOC-2 | A roadmap says what comes next, with estimates and a history | done | `docs/ROADMAP.md`, written 2026-10-20 |
| DOC-3 | A scorecard says what is true today, with evidence | done | This file, written 2026-10-20 |
| DOC-4 | An architecture overview in `docs/` | missing | Not written yet; write it once the layout settles in New Dullsville |
| DOC-5 | A README for a person who has not seen the project | missing | Not written yet |

## Known issues

| Id | Issue | Status | Note |
|---|---|---|---|
| BUG-1 | A build over an existing `obj/` folder warns twice with MSB4011 | open | Cause: `obj/` held both `CityLimits.csproj.nuget.g.props` and a lowercase `citylimits.csproj.nuget.g.props`, so the same file was imported twice. Fix: delete `obj/` and restore, which gave 0 warnings on 2026-10-20. The project name's casing is the trigger; it has not recurred since the clean restore, but watch for it in CI |
| BUG-2 | The game's namespace is `HelloWorld` | resolved | `CityLimits/Program.cs` now declares `namespace CityLimits;`, and the other files use `CityLimits.Game`, `CityLimits.Rendering`, and `CityLimits.UI` |
| BUG-3 | The tree is under git but has no commits | open | The repository was initialised on 2026-10-20 on `main`, but nothing is committed, and there is no CI, no changelog, and no `README`. Until the first commit lands, every change is unrecoverable |
| BUG-4 | The test project references no project, so it cannot test the core library | resolved | `CityLimits.Tests/CityLimits.Tests.csproj` now has a `ProjectReference` to `CityLimits.Core/CityLimits.Core.csproj`, and the game project references the core library too |
