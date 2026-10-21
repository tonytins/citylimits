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
| [Core](#core) | 8 | 0 | 0 | 0.25 s steps at Normal, seed 2000, 120 by 100 map |
| [Map](#map-and-terrain) | 4 | 0 | 0 | 4 kinds of ground, 16 shore and 17 wood drawings, one 256 by 960 atlas |
| [Zoning](#roads-and-zoning) | 5 | 0 | 0 | 5 tools that change the world, $10 a road, $50 a bridge, $100 a 3 by 3 zone, $1 to bulldoze |
| [Simulation](#simulation) | 7 | 0 | 0 | 16 phases a 48th of a year, 3 minutes a year at Normal, houses then 4 to 5 building levels, 3 built-in scenarios |
| [Economy](#economy-and-services) | 4 | 1 | 0 | $20 000 in the treasury, 7% tax, 700 and 2 000 tile plants, the budget panel, the overview map's six modes |
| [Traffic](#traffic-and-commuting) | 0 | 2 | 2 | 1 road network, trips of up to 30 tiles |
| [Environment](#land-value-pollution-and-disasters) | 0 | 1 | 4 | land value from distance and terrain, 0 disasters |
| [Interface](#player-interface) | 2 | 1 | 3 | 12 tools with icons, the top bar, the demand panel, the budget panel, the overview map |
| [Platform](#platform) | 7 | 0 | 2 | Raylib-cs 8.1.0, NLog 6.2.2, .NET 10, data mods |
| [Reliability](#reliability) | 6 | 0 | 0 | 489 tests, all passing, CI green |
| [Documentation](#documentation) | 3 | 0 | 2 | 3 of 5 documents exist |
| [Known issues](#known-issues) | | | | 1 open, 3 resolved |

The game today opens a resizable window on a seeded map laid out as in
Micropolis, with rivers, lakes, woods, and now and then an island, drawn
from a tile sheet with smoothed shores and wood edges. The camera pans and zooms, the
clock runs the city at Micropolis' pace, the tool palette shows the icon of each
tool, the status bar is in place, and the tile under the mouse is highlighted
and read out. The player can draw roads, zone land, and bulldoze, and the
city charges for the work and refuses what it cannot pay for. Zones grow
and decline with population, demand, and trips, as in Micropolis, and their
buildings are drawn from the tiles' own state. Power, taxes, the budget,
services, and an overview map with its overlays are in; traffic, crime,
pollution, disasters, and saving are still missing, and that is the point
of the table: it is the contract the work has to meet.

How the state was verified on 2026-10-20:

```sh
dotnet build CityLimits.slnx                              # 4 projects: 0 warnings, 0 errors
dotnet test  CityLimits.Tests/CityLimits.Tests.csproj     # 481 passed, 0 failed
dotnet run --project CityLimits -- --headless            # a year of steps, 12 months logged to the console
dotnet run --project CityLimits                           # window opened; roads, zones, buildings, the palette icons, and the tile readout were drawn
dotnet run --project CityLimits -- --seed 7               # window opened and the assets loaded (the screen was not inspected)
xvfb-run -a dotnet run --project CityLimits               # on a private display: roads drawn by xdotool came from the sheet (a crossroads, two T-junctions, corners, a closed loop)
                                                          # the chunk cache drew the same pixels as tile by tile, at two zoom levels, across chunk edges
```

The game was run on a virtual display and the window was captured and
measured: the map area holds the atlas colours, the three zone types
are drawn in their own colours, and every zoned tile carries a building of
15 by 15 pixels, the top development level, with the dark outline around it.
The palette shows the bulldozer picked out, and the notice line in the status
bar carries the refusal for the bulldozer's last click, which had nothing to
clear. The headless mode needs no display at all, so the simulation can be
checked on a machine with no screen, and it builds a small demo town first so
there is something to grow.

## Core

| Id | Target | Status | Evidence |
|---|---|---|---|
| CORE-1 | The game opens a window and closes cleanly | done | `CityLimits/Game/WindowedGame.cs`, run by the default command in `GameCommand.cs`: `InitWindow` / `BeginDrawing` / `EndDrawing` / `CloseWindow`; the run on 2026-10-20 reached the render loop and exited when the window closed |
| CORE-2 | The engine-free half of the game lives in a core library that no engine can reach | done | `CityLimits.Core/CityLimits.Core.csproj` references no package at all, and `CityLimits.csproj` references the core library, never the other way round |
| CORE-3 | The simulation advances in fixed-size ticks, independent of the frame rate | done | `CityLimits.Core/Simulation/GameClock.cs`: a 0.25 s step with an accumulator; `GameLoop.Update` runs the steps that are due, at most 16 a frame |
| CORE-4 | Pause and speed control change how many ticks run per frame, not the tick size | done | `GameSpeed`: Normal, Fast (5 times), and Super fast (50 times), as in Micropolis, multiply the time fed to the clock, so a step stays 0.25 s. On a private display on 2026-10-20, Normal ran 69 steps in 17 seconds and moved the date a month, and Super fast ran 811 steps in 4 seconds, about 200 a second |
| CORE-5 | The mouse position is known in world space, and the keyboard is read | done | `Camera.ScreenToWorld` and `Raylib.GetMousePosition`; the HUD shows the world position, and the keys pan, zoom, and pick tools |
| CORE-6 | A camera pans and zooms, clamped to the map | done | `CityLimits/Rendering/Camera.cs`: `Pan`, `ZoomAt`, and `ClampTo`; the run on 2026-10-20 showed the whole 120 by 100 map zoomed out, held inside the view |
| CORE-7 | One seeded PRNG owned by the simulation makes a run reproducible | done | `CityLimits.Core/Randomness/DeterministicRandom.cs`; the simulation owns it, and `GameLoop` builds the map from the seed 2000 |
| CORE-8 | Settings persist to disk and a bad settings file is reported, not fatal | done | `CityLimits.Core/Settings/SettingsStore.cs`: `Load` falls back to the defaults with a warning, `TrySave` reports a failure; the game writes `settings.json` beside the executable when it ends |

## Map and terrain

| Id | Target | Status | Evidence |
|---|---|---|---|
| MAP-1 | A tile grid of a fixed size, stored as arrays | done | `CityLimits.Core/World/TileMap.cs`: a `TileState` array behind a width and a height, with the tile count checked against the size. `TileState` holds the ground, the use, and the development level, so a tile carries its own story |
| MAP-2 | Terrain is generated from the seed, and the same seed gives the same map | done | `CityLimits.Core/World/TileMapGenerator.cs` lays the land out as Micropolis does: an island one map in ten, otherwise two wide rivers and a narrow one from a random start, up to ten lakes, then woods scattered by random walks and thinned twice. `TileMapGeneratorTests` proves the same seed gives the same map, about one map in ten is an island (20 of 200), rivers run off the map's edges, at most 1 tree in 200 is left outside a wood (92 of 53 835 over 20 seeds), and every size a scenario allows generates. Removing the thinning, the islands, or the rivers' run to the edge each failed a test. On a private display on 2026-10-20, seed 2000 showed a river with lakes and woods, and seed 4 an island |
| MAP-3 | Terrain is rendered from a tile atlas with no per-frame allocation | done | `CityLimits/Rendering/MapChunkCache.cs` draws the map into 32 by 32 tile render textures, redrawn only when `TileMap.TileChanged` reports a change, and a frame draws the visible chunks. On a private display on 2026-10-20, the cached map matched the tile-by-tile drawing pixel for pixel over 580 800 pixels, at two zoom levels and across chunk edges, and a cache that ignored changes was caught. `MapRenderer.DrawTile` draws each tile from `CityLimits/Assets/tiles.png` with `DrawTexturePro`; `CityLimits/Rendering/TileAtlas.cs` holds the tile indices, roads included, chosen from `RoadNetwork.ConnectionsAt`, and shores and wood edges chosen from `TerrainEdges.SidesAt`, which the screenshot on 2026-10-20 showed running the right way round, and `GameAssets` loads the sheet once at startup and unloads it at shutdown. The captured window on 2026-10-20 held the atlas colours |
| MAP-4 | The tile under the mouse is highlighted and readable | done | `MapRenderer.DrawHover` outlines the tile under the pointer, and `Hud.DrawTileReadout` names it: the tile's place and what can be done with it, from `CityLimits.Core/World/TileInfo.cs` |

## Roads and zoning

| Id | Target | Status | Evidence |
|---|---|---|---|
| ZONE-1 | Roads can be drawn and bulldozed, with a cost the player must be able to pay | done | `CityLimits.Core/World/CityBuilder.cs`: `LayRoad` charges `TileCosts.Road`, and the bulldozer clears it again. Over water a road is a bridge at $50 a tile, started from a road beside it, as in Micropolis. Work is done in strokes: `CityBuilder.Plan` previews a `BuildStroke` into a `BuildPlan` with its total cost, and `CityBuilder.Apply` applies all of it or refuses it whole with `BuildRefusal.NotEnoughFunds` or `OffTheMap`. `GameLoop.HandleStroke` puts the result on screen. `CityBuilderTests` and `BuildStrokeTests` cover the charge, the refusals, the skipped tiles, and the refund. On a private display on 2026-10-20, a diagonal road drag previewed and built a straight road of 11 tiles for $115, the money matched, and Escape cancelled a bulldozer stroke with nothing changed |
| ZONE-2 | Residential, commercial, and industrial zones can be laid, by dragging | done | As in Micropolis, a zone is 3 by 3 (`ZoneLayout`, `TileState.ZonePart`) and costs $100. A zone stroke lays a grid three tiles apart, the first zone centred where it started (`BuildStroke.Bounds`), and each zone goes down whole or is skipped (`CityBuilder.PlanZone`). Trees and rubble are cleared for $1 a tile. `CityBuilderTests` and `BuildStrokeTests` cover the size, the grid, the clearing, and the refusals. On a private display on 2026-10-20, a residential, a commercial, and an industrial zone were laid and previewed, and drawn from the sheet |
| ZONE-3 | A zone counts as served only when it reaches the road network | done | `RoadNetwork.IsZoneServed`: a road must lie on one of the 12 tiles along the zone's edges, just outside it; a road at a corner doesn't count, as in Micropolis. `RoadNetworkTests` covers each edge, the corners, and the map edge, and `CitySimulationTests` shows a zone with a road only at its corner never develops |
| ZONE-4 | Buildings are drawn from the tile's own state, not a second list of buildings | done | A zone's development lives on its centre tile, and each of its nine tiles is drawn from the sheet as its part of the zone's 3 by 3 drawing for that level and land-value class, or as a single house on an outer tile (`TileAtlas.IndexFor`, `TileMap.ZoneStateAt`). There is no list of buildings anywhere in the tree. On a private display on 2026-10-20, three zones grew into buildings at 3x speed |
| ZONE-5 | The bulldozer clears what it can, at Micropolis' price | done | `CityBuilder.PlanBulldozer`: $1 a tile for roads, bridges (back to water), trees, and rubble, and nothing refunded. A zone comes down only when its centre is in the stroke, turning all nine tiles to rubble for $1; its other tiles are refused with `BuildRefusal.PartOfAZone`. On a private display on 2026-10-20, an industrial zone was bulldozed through its centre into rubble for $1 |

## Simulation

| Id | Target | Status | Evidence |
|---|---|---|---|
| SIM-1 | A tick advances the date and updates the world | done | `CitySimulation.Tick` runs one phase of Micropolis' 16-phase cycle: phase 0 moves the calendar on by a 48th of a year (`CityCalendar`: 4 counts a month, from January 1900), and phases 1 to 8 scan the map in vertical bands, growing the served zones whose centres are in the band. `CitySimulationTests` and `CityCalendarTests` cover the count, the months, the year, the bands, and that each zone is scanned once a count in its own phase |
| SIM-2 | Population, jobs, and migration between zones | done | As in Micropolis: each zone's population comes from its spec, the census counts it, demand follows jobs and births, and residents move in and out through growth and decline. `CensusAndDemandTests` and `ZoneGrowthTests` cover them, and the `small-town` scenario grows twelve zones for ten years and checks the result |
| SIM-3 | Residential, commercial, and industrial demand exist and are readable | done | `CityCensus` counts each zone's population as the scan passes it and records the month; `CityDemand` moves each demand every second count from employment, births, the market, and labour, as Micropolis does, within ±2,000 and ±1,500. `CensusAndDemandTests` works two cities by hand (an empty one: +180, -600, +600; 160 residents with no jobs: -588, +600, +600), the limits, the timing, and the census. The HUD's demand panel and the headless run's monthly line show them; on a private display on 2026-10-20 the bars rose and fell from the middle line as the town grew |
| SIM-4 | Zones develop and decay, and abandoned zones come back | done | `ZoneGrowth.Visit`: a score from demand and the zone's worth, against a roll, grows or declines a zone a step at a time, as Micropolis does; a zone with no road declines. A zone that declined grows again when demand returns: in a ten-year run of `small-town` on 2026-10-20, residential zones filled with houses, emptied, and filled again as demand swung between -2,000 and +1,200, and buildings came where houses clustered. `ZoneGrowthTests` checks one-level steps, the density rule, declines without a road, commerce's land-value limit (a mutation that dropped it reached level 4 where the rule allows 1), and land-value classes |
| SIM-5 | Housing capacity follows the development level of a tile | done | As in Micropolis, a residential zone grows up to 8 single houses on its outer tiles, one person each, then 3 by 3 buildings of 16, 24, 32, and 40 by level; commercial and industrial buildings hold 1 to 5 and 1 to 4. The numbers are data in the zone's `ZoneSpec`, which a mod can change. `ZoneGrowthTests` covers the houses, the first house beside the road, the change to a building, every level, and a modded house limit |
| SIM-6 | The same seed and the same actions give the same city after N ticks | done | `CitySimulationTests.TheSameSeedAndTheSameTicksGiveTheSameCity`: 100 ticks on two cities from the same seed agree on the date, the tick count, and the generator |
| SIM-7 | A scenario file sets up a city, runs it with no window, and checks it | done | `CityLimits.Core/Scenarios/`: `ScenarioLoader` checks a file (unknown fields, exact enum names, every range, the first problem reported with its path), `ScenarioRunner` makes tool strokes and checks at their ticks and returns a format-1 `ScenarioReport`, and `DemoTown` is the fallback, defined in code and read through the same loader. `citylimits scenario` runs one by id or `--file`, with `--json`, the fallback, and exit codes 0 to 3. `BuiltInScenarioTests` runs `demo-town`, `bridges`, and `zones-and-bulldozer`, and pricing bridges at $40 on purpose failed `bridges` with "expected funds equal 19260, found 19390". `ScenarioLoaderTests` and `ScenarioRunnerTests` hold 55 more tests |

## Economy and services

| Id | Target | Status | Evidence |
|---|---|---|---|
| ECON-1 | Power plants feed a grid, and each tile is powered or unserved | done | Coal and nuclear plants (4 by 4, $3,000 and $5,000) and power lines ($5, $25 under water, crossing straight roads) can be built; their prices and capacities are a `PlantSpec` a mod can change. `PowerGrid` traces power at phase 11 from the plants through lines, zones, and plants, 700 tiles a coal plant and 2 000 a nuclear one, pooled over every plant as in Micropolis, and the scan records each zone's power on its centre (`TileState.Powered`); an unpowered zone scores -500 and never grows. `PowerGridTests`, `PowerLineTests`, and `PowerPlantTests` cover it; ignoring the capacity, the plants' own power, the -500 score, the need-power threshold, a crossing's direction, or nuclear capacity each failed a test. On a private display on 2026-10-20 two unpowered zones blinked the lightning bolt; a plant beside one powered it within two cycles, its readout lost "no power", and it grew a house, while the other kept blinking. The headless run logs 43 of 700 tiles powered for its demo town |
| ECON-2 | Power demand follows the grid, and a brownout is visible | done | As in Micropolis, every tile the trace powers draws on the plants' capacity, zones and lines alike, whether anyone lives there or not: 700 tiles a coal plant, about 50 zones with their lines; the owner chose this over demand by population. A brownout sends a message, shown on the notice line and logged, and the zones left dark blink the bolt (`PowerGridTests`). The power overlay comes with J4 |
| ECON-3 | Taxes, a budget, and a deficit the player has to answer | done | `CityLimits.Core/Economy/CityBudget.cs` works the budget out as Micropolis does: each count estimates the year's taxes (population times average land value over 120, times the tax rate and Easy's 1.4) and upkeep (road tiles times 0.7, $100 a station, at each funding level, roads paid first when money runs short), sets a 48th aside, and settles the year into the treasury, which can go below zero. The tax rate pushes demand by Micropolis' table, underfunded roads crumble, and the city warns of high taxes and underfunded roads. `BudgetTests` cover it, and dividing the tax as a fraction, paying fire before roads, dropping the tax push, letting crossings crumble, counting a bridge as one tile, not dividing the year by 48, the high-tax threshold, and a crumbled bridge leaving rubble each failed a test. The headless run logs each year: the demo town's first full year came to $1 in taxes against $6 of road upkeep. A year whose upkeep outran its taxes, or whose funding was cut, opens the budget panel by itself and pauses the game; on a private display on 2026-10-21 a road with no town opened it at the year's turn, tick 777, saying the $44 of upkeep was more than the $0 in taxes |
| ECON-4 | Police, fire, and parks have a coverage effect | partial | Fire and police stations can be built (3 by 3, $500, $100 a year, a `ServiceSpec` a mod can change), and `ServiceCoverage` works their cover out as Micropolis does: each station's funding effect on its 8 by 8 block, halved without power and again without a road, smoothed three times at phases 13 and 15. `ServiceTests` cover it, including Micropolis' figures for one station (218 on its block, 111 beside it); ignoring power or the road, spreading too far, not budgeting the stations, the need-station threshold, and never spreading fire each failed a test. A test caught a crash before it shipped: a station scanned before the first cycle had started. On a private display on 2026-10-21 both stations drew from the sheet in Micropolis' palette row, and an unpowered one blinked the bolt while one against a plant did not. Parks are in too: $10 a tile, as a rectangle, on clear ground or rubble, woods or one time in five a fountain chosen from the tile's place; a wooded park adds to land value as trees do, and a fountain counts as built (`ParkTests`; counting parks as built, fountains as natural, letting parks go on trees, giving every park the same look, and keeping the bulldozer off them each failed a test). On a private display a 40-tile park previewed and built the same, fountains and all, for $400. Hospitals and churches come as in Micropolis: one of each needed for every 256 residents, an empty residential zone becoming one a time in four when it would grow, and one too many turning back now and then; Micropolis' hospital-then-church quirk is fixed, as the owner chose (`CommunityBuildingTests`, where a 600-resident town gets one of each; one per 128 residents, the quirk, never reverting, growth never building them, and miscounting each failed a test). Their drawings were checked in the sheet, not yet seen in the running game. Nothing answers to coverage until crime and fires come with Deadwood |
| ECON-5 | A budget panel and a power overlay show what the simulation thinks | done | `CityLimits/UI/BudgetPanel.cs` shows the year as `CitySimulation.EstimateBudget` works it out now, last year as it was settled, and the tax rate and funding the player sets; on a private display on 2026-10-21 B and a click on the funds opened it, raising the tax twice and lowering road funding once showed 9% and 90%, its asks and pays followed, and Continue gave the speed back. `CityLimits/UI/OverviewMap.cs` is Micropolis' overview map: the whole city in the top-right corner with the view outlined, and modes for power, fire and police coverage, land value, and population density, read from `PowerGrid`'s centres, `ServiceCoverage`, and `CityMaps`. On a private display on 2026-10-21 each button switched the mode, a click and a drag on the map moved the view and its outline, a town against a coal plant showed its zones, stations, and plant red and its power line grey, the same town without the plant showed them all blue, and a fire station without a road read 109 on its block in the overlay's log, as `ServiceCoverage` gives it, drawn as the third band with the lower two around it |

## Traffic and commuting

| Id | Target | Status | Evidence |
|---|---|---|---|
| TRAF-1 | A road graph is built from the tile map and rebuilt only when roads change | partial | `CityLimits.Core/World/RoadNetwork.cs` is built from the tile map and is rebuilt only when the roads change: `CityBuilder` marks it dirty when a road is laid or cleared, and `RoadNetwork.EnsureCurrent` rebuilds it when it is asked and out of date. It holds the road tiles and who they serve, but it is not yet a graph of links and carries no volumes |
| TRAF-2 | Traffic volumes per road tile come from commuting trips | partial | The trips exist: `TripPlanner` drives up to 30 road tiles from a zone's edge, as Micropolis does, to a zone of a kind its spec lists, and remembers the route every other step. `TripTests` covers each outcome, the 30-tile reach, the dead end, the route, the seed, and modded destinations. Nothing lays traffic along the routes yet |
| TRAF-3 | Congestion above a road's capacity feeds back into demand and land value | missing | No capacity model |
| TRAF-4 | A traffic overlay shows the volumes, and a small map is covered by a test | missing | No overlay, no test project |

## Land value, pollution, and disasters

| Id | Target | Status | Evidence |
|---|---|---|---|
| ENV-1 | Land value comes from desirability factors and has an overlay | partial | `CityMaps.UpdateLandValue`, as Micropolis: closeness to the city's centre of mass and the trees and water nearby, from 1 to 250, on 2 by 2 blocks. Pollution and crime aren't in it yet, and there is no overlay. `CityMapsTests` works the values out by hand |
| ENV-2 | Pollution comes from industry and traffic and lowers land value | missing | No pollution model |
| ENV-3 | Fires start, spread, and are answered by the fire service | missing | No fire model |
| ENV-4 | Earthquakes and floods happen on a timer, with a warning first | missing | No disasters |
| ENV-5 | Damage, repair cost, and recovery exist | missing | No repair model |

## Player interface

| Id | Target | Status | Evidence |
|---|---|---|---|
| UI-1 | A tool palette with a hotkey for every tool | done | `CityLimits/UI/ToolPaletteView.cs` lays the palette out as Micropolis does: the name and price of the tool under the pointer, or the held one, over centred rows of icon buttons in Micropolis' order, with the held tool's highlighted icon, from `CityLimits/Assets/tools/`. The keys 1 to 9 pick the first nine of the twelve tools; the park and the two stations are picked from the palette. On a private display on 2026-10-20 the palette drew its five rows, clicking the commercial button held it and highlighted it, hovering the nuclear plant named it with its price, and with one icon hidden the button fell back to a labelled box; that check found and fixed `GameAssets.IconFor` never returning null for a missing icon |
| UI-2 | A HUD with population, funds, date, speed, and the demand bars | done | `CityLimits/UI/Hud.cs` draws the top bar after the owner's mock-up in `docs/design/`: a pause and play button, the month, speed buttons that light up to the speed, the city's name (`CitySimulation.Name`), an R, C, I demand meter, the funds, the population, the steps, and the frame rate. The meter opens `CityLimits/UI/DemandPanel.cs`, with each demand against its limit, the economy's pull and the tax's push at the last update, jobs per resident, workers per job, and the population's parts (`CensusAndDemandTests` and `CitySimulationTests` cover the figures and the name; dropping the tax push or the name's control-character check each failed a test). On a private display on 2026-10-21 the bar lit three arrows at Super fast and two at Fast, paused to a play button with the arrows dimmed, played at Normal from the first arrow, opened the budget from the funds, and opened the panel from the meter; a press on the panel with the road tool built nothing, and its x and Escape closed it, the next Escape quitting |
| UI-3 | Panels for the budget, power, and the city score | partial | The budget panel, the demand panel, and the overview map, with its power mode, are in (ECON-5, UI-2); there is no city score panel (UI-4) |
| UI-4 | A city score that rates the city the way the classic games do | missing | No scoring |
| UI-5 | A city saves to disk and reloads exactly, and a corrupt file is reported, not fatal | missing | No save format |
| UI-6 | A start screen and a new-city flow: name, seed, and map size | missing | No screen flow |

## Platform

| Id | Target | Status | Evidence |
|---|---|---|---|
| PLAT-1 | The solution builds with no new warnings | done | `dotnet build CityLimits.slnx` on 2026-10-20: 4 projects, 0 warnings, 0 errors (see BUG-1) |
| PLAT-2 | The engine is pinned to a known version | done | `CityLimits/CityLimits.csproj`: `Raylib-cs` `8.1.0` and `NLog` `6.2.2`; `CityLimits.Core.csproj` references no package |
| PLAT-3 | The native engine ships with the package, so no system install is needed | done | `~/.nuget/packages/raylib-cs/8.1.0/runtimes/`: `linux-x64/native/libraylib.so`, `win-x64/native/raylib.dll`, `win-x86/native/raylib.dll` |
| PLAT-4 | The game runs on Linux | done | `xvfb-run -a dotnet run --project CityLimits` on 2026-10-20: window created, shaders compiled, default font loaded, clean exit. On the same day the game was driven on a private X display: roads, zones, and buildings were drawn, the palette picked up the bulldozer, and the game exited on escape |
| PLAT-5 | The game builds and runs on Windows | missing | Not attempted; no Windows machine or build check yet |
| PLAT-6 | A release build can be handed to someone else | missing | No packaging, no version, no repository |
| PLAT-7 | The city can be run and checked without a display | done | `dotnet run --project CityLimits -- --headless` runs the simulation for a year with no window and writes each month to the console; `CityLimits/Game/HeadlessRun.cs` and `CityLimits/Game/GameLog.cs`, which sends NLog to the console and nowhere else |
| PLAT-8 | The command line refuses what it cannot use, and the game is the default | done | `CityLimits.CommandLine`: `CommandOptions` refuses an unknown option, a missing or malformed value, a value out of range, a repeat, and a stray argument, with exit code 2. Running with no command plays the game, and there is no `play` command. `--headless` is a global flag, and the dispatcher refuses it for a command that can't run without a window. On 2026-10-20, `--headless --ticks -1`, `--headless --bogus`, `--ticks 5` without `--headless`, `play`, `headless`, and `scenario town` without `--headless` were each refused with exit code 2; `--headless --ticks 2` and `--ticks 2 --headless --seed 9` ran; `scenario town --headless` reported that scenarios are not implemented and exited with 1 |
| PLAT-9 | The game can be modded with data, safely | done | `CityLimits.Core/Definitions/`: zone specs are data, the base game's in `BaseGame.Json` read by the same `DefinitionLoader` as a mod's, and `ModLoader` applies the mods in `Mods/` in id order, skipping a broken one whole with its reason. `DefinitionTests` covers the base game, patches, every refusal, load order, and a city paying a modded price. On 2026-10-20 a real mod made the headless town's commercial zone $40 ($19,670 instead of $19,610), a broken one was skipped and named on screen, the palette showed the modded price, and `scenario demo-town` was unchanged. `docs/modding/README.md` is the guide, and `docs/modding/tuning.md` lists what a tuning mod can change |

## Reliability

| Id | Target | Status | Evidence |
|---|---|---|---|
| REL-1 | Nothing ends the player's session: the frame loop catches, reports, and keeps drawing | done | `WindowedGame` catches every exception around the update and the draw, and `GameLoop.ReportError` puts the message on screen; the game keeps running |
| REL-2 | The world and the simulation never call the engine | done | `CityLimits.Core` references no package at all, and the game references the core library; no Raylib type appears in the core |
| REL-3 | Input, tile indices, map sizes, and files are validated where they enter | done | Map sizes, tile counts, and the settings file are checked, and the tile indexer throws outside the map. The mouse position is turned into a tile by `MapRenderer.TryGetTileAt`, which reports when the pointer is off the map, and `CityBuilder.Apply` refuses a tile that is off the map, ground that cannot be built on, and work the city cannot pay for |
| REL-4 | A test project covers the engine-free logic, and the tests pass | done | `CityLimits.Tests` references `CityLimits.Core` and `CityLimits.CommandLine` and holds 462 xUnit tests over the generator and the terrain edges, the power plants, lines, and grid, the budget, the stations and their coverage, parks, hospitals and churches, the map, the tile state, the rules, the calendar and the cycle, the clock, the simulation, the settings, the treasury, the road network and its connections, the map's change event, the builder and its strokes, the scenario loader and runner, every built-in scenario, the definitions and the mod loader, zone growth, the census and demand, trips, growth and the city maps, the option reader, and the dispatcher. All 481 passed on 2026-10-21. Letting an unknown option and an out-of-range number through on purpose failed 3 of them, and letting `--headless` reach a command that needs a window failed 2 |
| REL-5 | A determinism test proves the same seed gives the same city | done | `DeterministicRandomTests.TheSameSeedGivesTheSameSequence`, `CitySimulationTests.TheSameSeedAndTheSameTicksGiveTheSameCity`, and `CitySimulationTests.TheSameSeedAndTheSameWorkGiveTheSameCity`, which builds the same road and zones in two cities and compares the tiles and the money after 40 ticks |
| REL-6 | CI builds and tests on every push | done | `.github/workflows/dotnet.yml` runs on a push to `main` and on a pull request: restore, build, and test. The first run, on the commit 273fd9f on 2026-10-20, finished with the conclusion `success` (run 39443031762) |

## Documentation

| Id | Target | Status | Evidence |
|---|---|---|---|
| DOC-1 | An agent and contributor guide matches the code | done | `AGENTS.md`, reframed for CityLimits on 2026-10-20 and updated the same day for the four-project layout, the command line, the `Economy/` folder, the six tools, the tick, and the rule that the map holds the tile state |
| DOC-2 | A roadmap says what comes next, with estimates and a history | done | `docs/ROADMAP.md`, written 2026-10-20; the scenario design it schedules is in `docs/scenarios.md` |
| DOC-3 | A scorecard says what is true today, with evidence | done | This file, written 2026-10-20 |
| DOC-4 | An architecture overview in `docs/` | missing | Not written yet; write it once the layout settles in New Dullsville |
| DOC-5 | A README for a person who has not seen the project | missing | Not written yet |

## Known issues

| Id | Issue | Status | Note |
|---|---|---|---|
| BUG-1 | A build over an existing `obj/` folder warns twice with MSB4011 | open | Cause: `obj/` held both `CityLimits.csproj.nuget.g.props` and a lowercase `citylimits.csproj.nuget.g.props`, so the same file was imported twice. Fix: delete `obj/` and restore, which gave 0 warnings on 2026-10-20. The project name's casing is the trigger; it has not recurred since the clean restore, but watch for it in CI |
| BUG-2 | The game's namespace is `HelloWorld` | resolved | `CityLimits/Program.cs` now declares `namespace CityLimits;`, and the other files use `CityLimits.Game`, `CityLimits.Rendering`, and `CityLimits.UI` |
| BUG-3 | The tree is under git but has no commits | resolved | The repository has seven commits on `main`, pushed to `origin/main`, and the first CI run on 2026-10-20 finished green. The commits were made by the owner after the scorecard was first written |
| BUG-4 | The test project references no project, so it cannot test the core library | resolved | `CityLimits.Tests/CityLimits.Tests.csproj` now has a `ProjectReference` to `CityLimits.Core/CityLimits.Core.csproj`, and the game project references the core library too |
