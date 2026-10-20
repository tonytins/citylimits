# CityLimits roadmap

> **Last reviewed:** 2026-10-20 · **Last updated:** 2026-10-20 · **Change:** major (Line City meets the Micropolis baseline: bridges, 3 by 3 zones, the $1 bulldozer) · **Stage:** pre-alpha

Forward-looking: where CityLimits stands, what blocks the next milestone,
and what comes next. Verified status and evidence are in the
[scorecard](./scorecard.md).

Keep it current: when work lands, update its milestone row and checklist,
re-estimate what remains, and add a line to the revision history.

Milestones are named after Micropolis scenarios. The names are labels, not
a release order: the owner may re-order or re-cut them as the design
settles. Estimates are rough hours for one developer and are re-measured
when a milestone lands.

## Alpha gate

Alpha means CityLimits is a playable little city: a player can start a
game, lay out a town, and watch it grow and misbehave in ways it can read.
A row blocks alpha only when the player cannot complete that workflow at
all. Missing depth or a clumsy path is `partial, not blocking`, which makes
it a later milestone.

| Core workflow | Works end to end? | Evidence | Hours to pass (est.) |
|---|---|---|---|
| Start a game and look at a map | yes | A resizable "CityLimits" window opens on a 128 by 128 seeded map, drawn from a tile sheet, with a camera that pans, zooms, and clamps, and a readout of the tile under the mouse (CORE-1, MAP-1, MAP-2, MAP-3, MAP-4, CORE-6) | 0 |
| Lay roads, zone land, and see the city grow | partial | The player can lay roads and bridges, zone 3 by 3 land, and bulldoze, as in Micropolis and at its prices, and a zone with a road along its edge grows buildings. But growth and time still follow other rules than the Micropolis baseline, and there is no population, no demand, and no decay yet (ZONE-1 to ZONE-5, SIM-1, SIM-4) | 21–35 |
| Read the city's state: money, power, demand, and problems | no | The HUD shows the date, the tick, the speed, the funds, and what the held tool costs, and the tile readout names what is on the tile. There is no power, no demand, and no budget panel (UI-2, ECON-1, ECON-3) | 25–40 |
| Keep the city running for a long session without a crash | partial | The frame loop catches and reports, the tick loop runs, and the settings survive a session (REL-1, CORE-8). The city itself cannot be saved yet (UI-5) | 10–20 |

**Result: pre-alpha.** One row passes, two are partial, and one is open.
Alpha is roughly 56–95 hours away, and the critical path runs through the
baseline refactor in Kowloon.

## Current focus

The engine has to play like Micropolis before anything else. Parts of
it were built to other rules: tools apply tile by tile with no preview,
roads are freehand, zones are 1 by 1, the bulldozer refunds money, every
zoned tile grows on a flat chance, a year passes in 3 seconds, and the
terrain has no rivers. The focus is the refactor that brings them to the
baseline, in order:

1. ~~**Tool strokes**: a preview with its total cost, applied all or
   nothing (Line City).~~ Done.
2. ~~**Roads, 3 by 3 zones, and the bulldozer** on strokes, with the
   baseline's rules and prices (Line City).~~ Done.
3. **Time and the 16-phase simulation cycle**, at the baseline's pace
   (Kowloon).
4. **Growth per zone**: population, demand, growth and decline (Kowloon).
5. **Terrain**: rivers, lakes, and smoothed edges, on Micropolis' 120 by
   100 map (Neat Map).

Then Joffburg: power, taxes, and the budget.

## Milestones

"Done" is the share of the milestone's checklist below that is complete.
"Remaining" estimates the open items only.

| Milestone | Done | Remaining (h, est.) | Notes |
|---|---:|---:|---|
| New Dullsville: Foundation | 100% | 0 | The solution, the core library, the tests, the fixed timestep, the camera, the tools, and the settings are all in |
| Neat Map: Terrain and the tile grid | 83% | 6–10 | Reopened for the baseline: the terrain needs rivers, lakes, and smoothed edges, and no rock |
| Line City: Roads and zoning | 100% | 0 | Meets the baseline: strokes with a preview, straight roads and bridges, 3 by 3 zones laid as a grid and served from their edges, and the $1 bulldozer, at Micropolis' prices |
| Kowloon: Simulation core | 25% | 27–45 | The tick advances the date, and the determinism test is in. The baseline's calendar and 16-phase cycle, per-zone growth, demand, and the scenario runner are still to come. The heart of the game |
| Joffburg: Services and economy | 0% | 15–25 | Nothing yet |
| Radial: Traffic and commuting | 0% | 12–20 | Nothing yet |
| Bad News: Land value, pollution, disasters | 0% | 12–20 | Nothing yet |
| Happy Isle: Player-facing game | 0% | 15–25 | Nothing yet |
| Yokohama: Polish and release | 0% | 10–20 | Nothing yet |
| | **Total** | **≈ 97–165** | |

## Upcoming work, ranked

| Rank | Work | Milestone | Estimate (h) |
|---|---|---|---|
| 1 | Time and the 16-phase simulation cycle, at the baseline's pace | Kowloon | 6–10 |
| 2 | Scenarios, phase 1, with the spec revised for the new rules ([spec](./scenarios.md)) | Kowloon | 6–10 |
| 3 | Growth per zone: population, demand, growth and decline | Kowloon | 15–25 |
| 4 | Terrain: rivers, lakes, islands, and smoothed edges, on a 120 by 100 map | Neat Map | 6–10 |

Alpha needs ranks 1 and 3 in substance, about 21–35 hours. Rank 2
doesn't block alpha, but it comes before rank 3 because it is how growth
is watched and debugged as it is built.

## Milestone checklists

### New Dullsville: Foundation

- [x] A solution with the game project, the core library, and the test
  project, each in its own folder.
- [x] The test project references the core library, and the game project
  has the folders the workspace map names.
- [x] A window that opens and closes cleanly.
- [x] The window carries the game's name and can be resized.
- [x] A fixed-timestep loop: the simulation ticks at a fixed rate, the
  renderer draws once per frame, and the tick size never changes.
- [x] Speed control: pause, 1x, 2x, and 3x, changing how many ticks run
  per frame and nothing else.
- [x] Input: the mouse position in world space, keyboard shortcuts, and a
  selected tool.
- [x] A camera: pan, zoom, and clamping to the map bounds.
- [x] One seeded PRNG, owned by the simulation, so a run is reproducible.
- [x] Settings (window size, fullscreen, speed) saved to disk and read
  back on start, with a bad file reported rather than fatal.
- [x] A test project that builds and runs.
- [x] A first real test: the same seed gives the same sequence.

### Neat Map: Terrain and the tile grid

- [x] A tile grid of a fixed size, stored as arrays, not as one object per
  tile, in `CityLimits.Core/World/`.
- [x] Terrain generation: water, trees, and grass, from the seed, so the
  same seed gives the same map.
- [x] Terrain rendering from a tile atlas, with no per-frame allocation.
  `CityLimits/Assets/tiles.png` is drawn from `Rendering/TileAtlas.cs`, and
  the sheet is loaded once at startup.
- [x] A hover highlight and a readout of the tile under the mouse. The
  readout names the ground and says what can be done with it.
- [x] The map edge is defined: the world outside the map is not a tile
  that can be built on.
- [ ] Terrain like the baseline's: meandering rivers, lakes, sometimes an
  island, and patches of trees, with shores and forest edges smoothed by
  the sheet's edge tiles, and no rock, on a map of 120 by 100 tiles, the
  size Micropolis uses.

### Line City: Roads and zoning

- [x] Draw roads, with a cost, and a refusal when the player cannot pay.
- [x] Roads are drawn from the sheet, with the drawing chosen from the
  sides that join another road.
- [x] Buildings are drawn from the tile's own state, not from a second
  list of buildings.
- [x] Tools are strokes, as in the baseline: pressing starts one,
  dragging stretches it, a preview shows its tiles and its total cost,
  and releasing applies it, skipping the tiles that can't be built on and
  refusing it whole when it runs off the map or the city can't pay for
  all of it. Escape cancels it. `CityLimits.Core/World/BuildStroke.cs`,
  `BuildPlan.cs`, and `CityBuilder.Plan` and `Apply`.
- [x] Roads snap to a straight line, cross water as bridges at $50 a
  tile, started from a road beside them, and clear trees and rubble for
  $1 more.
- [x] Zones are 3 by 3 with a centre, laid as a grid by dragging, refused
  unless their tiles are clear, at $100 a zone, and drawn from the sheet.
- [x] A zone is served when a road runs along its edge, not at a corner.
- [x] The bulldozer costs $1 a tile and refunds nothing, and clears a
  zone only through its centre, leaving rubble.

### Kowloon: Simulation core

- [x] A tick that advances the date and updates the world, and does the
  same thing every time given the same inputs, in
  `CityLimits.Core/Simulation/`.
- [ ] The baseline's calendar and cycle: the year counted in 48ths, each
  count run as 16 phases with the map scanned in bands, at about 3
  minutes a year at normal speed.
- [ ] Population: people, jobs, and migration between zones.
- [ ] Demand: residential, commercial, and industrial, shown as three
  values the player can read.
- [ ] Development per zone, as in the baseline: a zone needs power and a
  traffic trip, and its score (demand plus its own value) decides whether
  it grows, declines, or stays. Zones develop today tile by tile, on a
  fixed chance, which the baseline replaces.
- [ ] Housing capacity by development level, so population follows
  density.
- [x] A determinism test, in `CityLimits.Tests/`: the same seed and the
  same actions give the same city after a fixed number of ticks. It covers
  the tiles and the money today, and it grows with the population model.
- [ ] Scenarios, phase 1, as [docs/scenarios.md](./scenarios.md)
  specifies. A scenario file holds a seed, timed build actions, and
  checks, and `CityLimits.Core/Scenarios/` loads and runs it.
  `citylimits scenario` runs it with no window, logs it, and writes a
  versioned JSON report with `--json`. The built-in scenarios live in
  `CityLimits/Assets/Scenarios/`, and each one runs as a test.
  `demo-town` is defined in code as the fallback, and falling back is
  always reported.

### Joffburg: Services and economy

- [ ] Power plants and a power grid, with each tile either powered or
  unserved.
- [ ] Power demand that follows population, and a visible brownout when
  demand outruns supply.
- [ ] Taxes, a budget, and a deficit the player has to answer.
- [ ] Services: police, fire, and parks, with a coverage effect.
- [ ] A budget panel and a power overlay that show what the simulation
  actually thinks.

### Radial: Traffic and commuting

- [ ] A road graph built from the tile map, rebuilt only when the roads
  change.
- [ ] Traffic volumes per road tile, from commuting trips.
- [ ] Congestion above a road's capacity, feeding back into demand and
  land value.
- [ ] A traffic overlay, and a test on a small hand-made map.

### Bad News: Land value, pollution, disasters

- [ ] Land value, from a set of desirability factors, with an overlay.
- [ ] Pollution from industry and traffic, and its effect on land value.
- [ ] Fire: fires start, spread to buildings, and are answered by the
  fire service.
- [ ] Earthquakes and floods, on a timer, with a warning first.
- [ ] Damage, repair cost, and recovery.

### Happy Isle: Player-facing game

- [ ] A tool palette with a hotkey for every tool.
- [ ] A HUD: population, funds, date, speed, and the demand bars.
- [ ] Panels for the budget, power, and the city score.
- [ ] A city score, rating the city the way the classic games do.
- [ ] Save and load: a city serializes to disk and reloads exactly, and a
  corrupt file is reported, not fatal.
- [ ] A start screen and a new-city flow: name, seed, and map size.

### Yokohama: Polish and release

- [ ] A visual pass: one tile atlas, one palette, and no stray colors in
  the draw code.
- [ ] Sound effects and music.
- [ ] Performance: the frame rate holds with a large city.
- [ ] Documented builds for Linux and Windows.
- [ ] A release build the owner can hand to a friend.

## Critical path

New Dullsville is first and everything else waits on it: the loop, the
camera, and the input model decide how the map, the tools, and the panels
are written. Its last act is the seam between the game and
`CityLimits.Core`: once the core library holds the world and the
simulation, and the test project references it, the simulation can be
developed and proven on ordinary .NET while the game only draws it.

Neat Map gives the game a world to put a city in, and Line City gave it the
tools that change that world: roads, zones, a bulldozer, and a price for
each. Kowloon is the real work and the real risk now, because a tick that
grows a city needs people, demand, and a shape for all three. Joffburg and
Radial hang off Kowloon's data model, and Bad News hangs off Joffburg and
Radial. Happy Isle can be built in parallel with the simulation once the
shapes of the panels are agreed.

The long poles are Kowloon and Joffburg. If the simulation is slow to get
right, cut scope inside Radial and Bad News first, not inside Kowloon.

## Risks most likely to push estimates up

- The tick is the design center of the game. A demand model, a growth
  model, and an economy that all read one set of numbers is a shape
  decision, and shape decisions cost more than code.
- A city builder grows faster than it can be drawn. Rendering a large map
  at 60 FPS may force a camera-and-atlas design earlier than planned.
- The rule that the simulation never touches Raylib is easy to state and
  easy to break. A single `Raylib.GetFrameTime()` in the tick code makes
  every test flaky.
- Determinism is cheap to keep and expensive to recover: one
  `Random.Shared` call in the simulation makes saved cities irreproducible
  and tests unreliable.
- Art is a hidden cost. The tile sheet and the tool icons are the
  Micropolis art under the GPL, so CityLimits is under the GPL too; new art
  has to be the owner's own work or GPL-compatible, and the owner may want
  to redo what is here more than once.
- Everything here is one person's evenings. Estimates assume that.

## Revision history

| Date | Change | Summary |
|---|---|---|
| 2026-10-20 | major | Step 2 of the baseline refactor: Line City meets Micropolis. Roads bridge water at $50 a tile from a road beside them; zones are 3 by 3, laid as a grid, served by a road along their edge, and drawn from the sheet with their buildings; the bulldozer costs $1 a tile, refunds nothing, and knocks a zone down to rubble through its centre; a tool clears trees and rubble for $1. The palette is sized to its text and picks a tool when clicked, instead of building on the map under it. Line City is complete again (100%), 97–165 h left, 201 tests. |
| 2026-10-20 | minor | Step 1 of the baseline refactor: tools are strokes. Pressing starts a `BuildStroke`, dragging stretches it (roads in a straight line, other tools in a rectangle), the map previews its tiles and its total cost, and releasing applies it all at once or refuses it whole. Escape cancels. The per-tile rules and prices are unchanged until step 2. Line City is at 50%, 107–181 h left, 170 tests. |
| 2026-10-20 | major | The Micropolis baseline: the owner set the first goal as an engine that plays like Micropolis, with creative liberties later. A comparison with the Java port found that tools, roads, zones, the bulldozer, growth, time, and terrain were built to other rules. Neat Map (83%) and Line City (38%) reopen, Kowloon gains the calendar and cycle (25%), and the focus becomes the five-step refactor, starting with tool strokes. The map becomes 120 by 100, Micropolis' size, with the terrain step. 113–191 h of work left in total. |
| 2026-10-20 | patch | The map is drawn from a chunk cache: `TileMap.TileChanged` reports each changed tile, `MapChunkCache` redraws only the 32 by 32 tile chunks that changed, and a frame draws the visible chunks. It falls back to tile-by-tile drawing when render textures can't be made, and both give the same pixels. 152 tests. |
| 2026-10-20 | patch | Roads are drawn from the tile sheet: `RoadNetwork.ConnectionsAt` tells which sides of a road join another, and `TileAtlas` turns that into the sheet's straights, corners, junctions, and crossroads. The hand-drawn road shapes are gone. The owner decided that zones become 3 by 3, like Micropolis, at $100 a zone, centred on the click, one per click, and refused unless all nine tiles are clear; that work is next. |
| 2026-10-20 | patch | Groundwork for scenarios, phase 1: the default map size moves into `CityLimits.Core/World/MapSize.cs`, `ExitCodes.ChecksFailed` (3) is added, and `scenario --json` moves the log to stderr. Kowloon's checklist item stays open until the runner lands. |
| 2026-10-20 | minor | `--headless` is a global flag again, not a `headless` command: `citylimits --headless` runs the city with no window, any command can declare that it supports the flag, and the dispatcher refuses it for one that doesn't. Scenarios need the flag until they can open a window, so a script written now keeps its meaning later. 140 tests, no milestone moves. |
| 2026-10-20 | minor | Scenarios join Kowloon: [docs/scenarios.md](./scenarios.md) specifies phase 1, a read-only, deterministic run from a file, with timed build actions, checks, and a versioned JSON report, for players' challenges later and for debugging now. It is ranked first because it is how population and demand will be watched as they are built. Kowloon is at 29% with 21–35 h left, and 58–107 h are left in total. |
| 2026-10-20 | minor | The command line is strict: every option is checked, and a bad one is refused with exit code 2 instead of being ignored. The `play` command is gone, because the game is what runs with no command, and the old `--headless` flag points to the `headless` command. `scenario` refuses to report success for work it does not do. 133 tests, no milestone moves, and the focus stays on Kowloon. |
| 2026-10-20 | major | Line City is complete: the tools change the world. Roads can be drawn and bulldozed, land can be zoned by dragging, the city charges for the work and refuses what it cannot pay for, the bulldozer gives half the money back, and a zoned tile that touches a road grows a building drawn from the tile's own state. 52–97 h of work left in total, and the focus moves to Kowloon. |
| 2026-10-20 | major | Neat Map is complete: the terrain is drawn from a tile sheet loaded once at startup, the tile under the mouse is highlighted and read out, the tool palette shows the icon of each tool, and the game can be run with no display and logged to the console. 64–117 h of work left in total, and the focus moves to Line City. |
| 2026-10-20 | major | New Dullsville is complete: the solution, the core library, the tests, a resizable "CityLimits" window, a fixed timestep, pause and 1x–3x, a camera that pans, zooms, and clamps, a tool palette, and settings that survive a session. Neat Map is at 60%: the tile grid, the seeded terrain, and the terrain on screen are in. 68–125 h of work left in total. |
| 2026-10-20 | minor | The solution, `CityLimits.Core`, and `CityLimits.Tests` are in. New Dullsville moves to 20%: the projects exist, but the test project is not wired to the core library and the game project's folders are not created yet. 13–21 h left in New Dullsville. |
| 2026-10-20 | major | First CityLimits roadmap: an alpha gate around a playable little city, nine milestones (New Dullsville to Yokohama) from foundation to release, a ranked list of the next work, and estimates. The project is a Raylib-cs window that draws "Hello, world!", so every row is open. |
