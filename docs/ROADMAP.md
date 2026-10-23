# CityLimits roadmap

> **Last reviewed:** 2026-10-21 · **Last updated:** 2026-10-22 · **Change:** minor (the interface library; Kyoto, the scenario editor, added) · **Stage:** beta

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
| Start a game and look at a map | yes | A resizable "CityLimits" window opens on a 120 by 100 seeded map of rivers, lakes, and woods, drawn from a tile sheet, with a camera that pans, zooms, and clamps, and a readout of the tile under the mouse (CORE-1, MAP-1, MAP-2, MAP-3, MAP-4, CORE-6) | 0 |
| Lay roads, zone land, and see the city grow | yes | The player lays roads and bridges, zones 3 by 3 land, and bulldozes, as in Micropolis and at its prices, and the city grows on its own: zones take trips, follow residential, commercial, and industrial demand, grow houses and then buildings where they cluster, and decline when demand turns (ZONE-1 to ZONE-5, SIM-1 to SIM-7) | 0 |
| Read the city's state: money, power, demand, and problems | yes | The HUD shows the date, the tick, the speed, the funds, and what the held tool costs, and the tile readout names what is on the tile. The status bar shows the population and a panel shows demand; unpowered zones blink a lightning bolt, the readout says when a zone has no power, and brownouts and blackouts show as messages. The budget panel shows the year's taxes and upkeep and opens by itself on a deficit. The overview map shows the whole city, and power, coverage, land value, and population density over it (UI-2, ECON-1, ECON-3, ECON-5) | 0 |
| Keep the city running for a long session without a crash | yes | The frame loop catches and reports, the tick loop runs, and the settings survive a session, a packaged game's too (REL-1, CORE-8). A city saves with Ctrl+S and loads from the start menu or Ctrl+O, running on exactly as it would have, and a damaged save is refused with the reason (UI-5) | 0 |

**Result: alpha, and now beta.** Every row passes: with saving in, each
core workflow works end to end. The owner marked CityLimits an alpha with
0.3.100, and Tony Bark marked it a beta with 0.4.100, once Deadwood was
complete; it stays a beta for a while, whatever its version, as the
focus turns to polish.

## Current focus

The engine plays like Micropolis: the baseline refactor (tool strokes,
roads and zones, time, growth, and terrain) is done, and Joffburg (power,
taxes and the budget, services, and the overview map) was built to the
baseline from the start and is complete. With saving finished, every row
of the alpha gate passes, and 0.3.100 is the alpha. The owner's liberties
so far are the always-automatic budget, parks' look chosen by their place,
Micropolis' hospital-then-church quirk fixed, the demand panel, and
unsupported things in imported cities becoming bare ground.

Next, in order:

1. **Scenarios to play**, the polish Tony Bark chose first: P1, a city's
   calendar starting in a later year, and P2, scenario format 2.0, are in.
   Next, in the order Tony Bark settled: the interface split out into a
   library of its own, which is in; P3, playing a scenario from the start
   page, with its goals, its result, and saving partway, with the
   demo-town fallback dropped; then Kyoto, the scenario editor; then P4,
   the first built-in challenges, made with the editor.
2. **Happy Isle's last items**: the city score, and map size on the
   start screen. Deadwood (pollution, crime, fire, floods, and
   earthquakes) and Radial (traffic and rail) are complete.

## Milestones

"Done" is the share of the milestone's checklist below that is complete.
"Remaining" estimates the open items only.

| Milestone | Done | Remaining (h, est.) | Notes |
|---|---:|---:|---|
| New Dullsville: Foundation | 100% | 0 | The solution, the core library, the tests, the fixed timestep, the camera, the tools, and the settings are all in |
| Neat Map: Terrain and the tile grid | 100% | 0 | Meets the baseline: Micropolis' terrain on its 120 by 100 map, with rivers, lakes, an island one map in ten, and woods, and shores and wood edges drawn from the sheet |
| Line City: Roads and zoning | 100% | 0 | Meets the baseline: strokes with a preview, straight roads and bridges, 3 by 3 zones laid as a grid and served from their edges, and the $1 bulldozer, at Micropolis' prices |
| Kowloon: Simulation core | 100% | 0 | Time runs as in Micropolis: the 16-phase cycle, the 48ths-of-a-year calendar, and its pace. Scenarios run from files and from the command line. Zones grow and decline one by one, with population, demand, and trips. The heart of the game |
| Joffburg: Services and economy | 100% | 0 | Meets the baseline: power, taxes and the budget, services (stations, coverage, parks, hospitals, and churches), and the overview map with its power, coverage, land value, and density modes, as in Micropolis |
| Radial: Traffic and commuting | 100% | 0 | Meets the baseline: versioned file formats with schemas, read without reflection, the traffic map trips leave and what it shows and costs, and rail, with tunnels, crossings, trips along it, its upkeep and message, and the commuter train |
| Deadwood: Land value, pollution, disasters | 100% | 0 | Meets the baseline: pollution and crime, fire with the ticker's fire-risk hints, floods, and earthquakes, as in Micropolis, with free repair and the settings' disaster switches |
| Happy Isle: Player-facing game | 50% | 13–22 | The palette, laid out as Micropolis', and the HUD, with its bar along the bottom over a news ticker after Tony Bark's mock-up, are in; the budget, demand, and city panels, the overview's power mode, and the settings window are too, and cities save, load, autosave, and import from Micropolis. Scenarios to play (P1 and P2 of four in), the city score, and map size on the start screen are left |
| Kyoto: The scenario editor | 0% | 10–16 | The interface library it shares with play is in; its checklist is Tony Bark's to settle |
| Yokohama: Polish and release | 0% | 10–20 | Nothing yet |
| | **Total** | **≈ 33–58** | |

## Upcoming work, ranked

| Rank | Work | Milestone | Estimate (h) |
|---|---|---|---|
| 1 | Scenarios, P3: playing one from the start page | Happy Isle | 5–8 |
| 2 | Kyoto: the scenario editor | Kyoto | 10–16 |
| 3 | Scenarios, P4: the first built-in challenges, made with the editor | Happy Isle | 2–4 |
| 4 | Happy Isle: the city score and map size on the start screen | Happy Isle | 6–10 |

Every row of the alpha gate passes, and CityLimits is an alpha since 0.3.100.

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
- [x] Speed control: pause, Normal, Fast, and Super fast, changing how many ticks run
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
- [x] Terrain like the baseline's: meandering rivers, lakes, sometimes an
  island, and patches of trees, with shores and forest edges smoothed by
  the sheet's edge tiles, and no rock, on a map of 120 by 100 tiles, the
  size Micropolis uses. `CityLimits.Core/World/TileMapGenerator.cs` lays
  it out, `TerrainEdges` reads the edges from the map, and `TileAtlas`
  picks the sheet's shore and wood-edge drawings from them.

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
- [x] The baseline's calendar and cycle: the year counted in 48ths from
  January 1900, each count run as 16 phases with the map scanned in 8
  bands, at about 3 minutes a year at Normal speed, with Fast and Super
  fast at 5 and 50 times that. `CityCalendar`, `GameSpeed`, and
  `CitySimulation.Tick`.
- [x] Population: people, jobs, and migration between zones, through the
  census, demand, and each zone's growth and decline.
- [x] Demand: residential, commercial, and industrial, shown as three
  values the player can read: `CityCensus`, `CityDemand`, and the HUD's
  demand panel.
- [x] Development per zone, as in the baseline: a zone needs a traffic
  trip, and its score (demand plus its own worth) decides whether it
  grows, declines, or stays (`ZoneGrowth`), with land value and density
  from `CityMaps`. A zone without power never grows, since Joffburg's grid.
- [x] Housing capacity by development level, so population follows
  density: single houses, then building levels, with each level's
  population in the zone's spec.
- [x] A determinism test, in `CityLimits.Tests/`: the same seed and the
  same actions give the same city after a fixed number of ticks. It covers
  the tiles and the money today, and it grows with the population model.
- [x] Scenarios, phase 1, as [docs/scenarios.md](./scenarios.md)
  specifies. A scenario file holds a seed, timed tool strokes, and
  checks, and `CityLimits.Core/Scenarios/` loads and runs it.
  `citylimits scenario` runs it with no window, logs it, and writes a
  versioned JSON report with `--json`. The built-in scenarios live in
  `CityLimits/Assets/Scenarios/`, and each one runs as a test.
  `demo-town` is defined in code as the fallback, and falling back is
  always reported.

### Joffburg: Services and economy

- [x] Power plants and a power grid, with each tile either powered or
  unserved. `Footprint`, `PowerLines`, and `PowerGrid`; plants are a
  `PlantSpec`.
- [x] Power demand that follows the grid, and a visible brownout when
  demand outruns supply. As in Micropolis, every
  zone and line tile a plant powers draws on it, whether anyone lives
  there or not, so a coal plant carries about 50 zones; the owner chose
  this over demand by population. A brownout is a message, and the zones
  left dark blink the bolt.
- [x] Taxes, a budget, and a deficit the player has to answer.
  `CityBudget` and `UI/BudgetPanel.cs`; the budget is always automatic,
  and its panel opens by itself on a deficit or a cut.
- [x] Services: police, fire, and parks, with a coverage effect. Stations
  and their coverage maps (`ServiceCoverage`) and parks (`Parks`) are in;
  as in Micropolis, coverage acts on crime and fires, which come with
  Deadwood, and parks raise land value.
- [x] A budget panel and a power overlay that show what the simulation
  actually thinks. `UI/BudgetPanel.cs`, and `UI/OverviewMap.cs`, the
  overview map as in Micropolis, with modes for power, fire and police
  coverage, land value, and population density.

### Radial: Traffic and commuting

Built to the Micropolis baseline, in the phases Tony Bark settled. The
earlier plan's congestion feeding back into demand and land value isn't
Micropolis, and Tony Bark dropped it; traffic reaches land value through
pollution, in Deadwood.

- [x] R0: files that say their version and survive trimming. Saves,
  scenarios, and mod files carry major.minor format versions, every JSON
  format reads through generated code with reflection-based JSON off, the
  binary codec's model is kept for the trimmer, and `docs/schemas/` holds
  schemas generated from the readers (`docs/saving.md`).
- [x] R1: the traffic map. A trip that arrives leaves traffic on every
  other road tile it passed, on 2 by 2 blocks, fading each cycle, capped
  at 240 (where Micropolis caps it only one time in six, Tony Bark chose
  always). Saved from format 1.1, with 1.0 saves upgraded.
- [x] R2: what traffic shows and costs. Animated light and heavy traffic
  on roads, heavy traffic counting twice toward road upkeep, the yearly
  traffic average and its message, and a traffic mode on the overview
  map. Saved from format 1.3.
- [x] R3: rail, as in Micropolis: a rail tool, rail crossing roads and
  power lines, tunnels, and trips along rail as well as roads, with
  rail's upkeep, its message, and the commuter train, drawn by the game
  alone as Tony Bark chose. Saved from format 1.4 as a part an older
  version can leave out. The palette's buttons became switches and its
  number keys were dropped, as Tony Bark chose.

### Deadwood: Land value, pollution, disasters

Built to the Micropolis baseline, in the phases Tony Bark settled. Of
Micropolis' disasters, fire, floods, and earthquakes are in Deadwood; its
tornado, monster, and meltdown are left for later, and the roll that
would bring them does nothing. Every disaster is reported in red on the
news ticker the moment it starts. Fire also gets hints ahead of time,
which Tony Bark asked for after SimCity 3000: flavour headlines when the
city's fire risk runs high, which read the city and never decide a fire.
Damage is repaired as in Micropolis, at no cost: a zone redraws its
damaged tiles as it next grows or declines, plants, stations, hospitals,
and churches redraw those that are bare ground now and then, and the
player clears rubble.
Starting a disaster on demand, as Micropolis' Disasters menu does, waits
for a new set of palette art from Tony Bark, since the interface follows
later games, where such tools live in the palette.

- [x] D1: land value, from a set of desirability factors, with an
  overlay. Closeness to the centre, terrain, pollution, and crime, as in
  Micropolis; the overview's Value mode shows it, and Crime has a mode of
  its own. Crime is 128 less land value plus density, less police
  coverage, and takes 20 off land value above 190.
- [x] D1: pollution from industry and traffic, and its effect on land
  value. As Micropolis' core counts it, every tile of a building, as Tony
  Bark chose over the Java port's centre alone; it lowers land value and
  homes' worth, stops homes growing above 128, and has an overview mode
  and a message. Its numbers are constants until the definitions format
  can grow without stopping older versions opening new saves. Saved from
  format 1.5, as two parts older versions may leave out or keep.
- [x] D2: fire. Fires start, spread to buildings, and are answered by
  the fire service, with hints on the ticker when the risk runs high,
  as in Micropolis (`World/Fires.cs`). A building keeps standing with the
  holes fire leaves, and is redrawn as it grows; a burning centre takes
  the building's tiles with something on them, as Tony Bark chose;
  explosions set their fires at once. Fires start on their own only when
  the player's Disasters and Fires switches allow, and the dice are
  rolled either way. Saved from format 1.6 with every building written
  whole, so older versions open the city repaired.
- [x] D3: earthquakes and floods, from Micropolis' disaster roll,
  reported as they start (`World/Floods.cs`). A flood rises from a shore,
  spreads for 30 cycles, and drains to bare ground; one reaching a centre
  floods the whole building, as Tony Bark chose. An earthquake shakes
  300 to 1 000 tiles and wrecks the outer tiles of buildings among them,
  to rubble or to a fire of its own, and the view shakes. Each has its own switch on the
  settings' Disasters tab. Saved from format 1.7, floods as a part of
  their own. With them came a small pop-up window (`UI/NoticePanel.cs`),
  which Tony Bark asked for, first used to say fullscreen waits for a
  restart.
- [x] D2 and D3: damage and recovery, as in Micropolis: zones redraw
  their holes as they grow, plants, stations, hospitals, and churches
  redraw holes of bare ground every 8 or 16 counts, and the player clears
  rubble (`World/Hazards.cs`).

### Happy Isle: Player-facing game

- [x] A tool palette that holds every tool. Laid out as Micropolis'
  (`UI/ToolPaletteView.cs`); its buttons are switches, and since Radial's
  R3 there are no number keys, as Tony Bark chose.
- [x] A HUD: population, funds, date, speed, and the demand bars
  (`UI/Hud.cs`). The bar follows Tony Bark's mock-up in
  `docs/design/`, along the bottom, with pause and speed buttons, the
  city's name, a demand meter that opens the demand panel
  (`UI/DemandPanel.cs`), and a help button that opens the help window
  (`UI/HelpPanel.cs`). Under it, a news ticker (`UI/NewsTicker.cs`)
  carries the game's notices, the city's messages, and headlines from
  the city's world; build hints show beside the pointer.
- [ ] Panels for the budget, power, and the city score. The budget panel
  (`UI/BudgetPanel.cs`) and power, as the overview map's power mode
  (`UI/OverviewMap.cs`), are in; the score is not.
- [x] Save and load: a city serializes to disk and reloads exactly, and a
  corrupt file is reported, not fatal, since S2c (`docs/saving.md`). The
  phases after it add formats and autosave. The owner settled the plan: one
  save model in `CityLimits.Core`, holding everything, the cycle's
  half-done figures included, so a city saves at any moment and loads
  back exactly; three formats over it; saves in
  `Documents/CityLimits/Cities`, listed in the game, with no file dialog.
  - [x] S1: per-user folders. Settings in the config folder, mods and
    saves under `Documents/CityLimits`, so a packaged game such as the
    AppImage can write them (`GameFolders`).
  - [x] S2a: the loading screen and the start menu, after the owner's
    mock-up, with New Game naming the city and the in-game menu leading
    back. Load Game is dimmed until S2c.
  - [x] S2b: the city panel, added by the owner: a click on the name
    shows the city at a glance, its mods, and its seed, which a click
    copies, and renames the city on Enter. The windows share
    `IMovableWindow`.
  - [x] S2c: the save model and `.fcl`, its flat JSON form, checked
    strictly; Ctrl+S to save and Ctrl+O to open, asking before
    overwriting; Load Game's list; and a test that a loaded city runs on
    tick for tick like one never saved.
  - [x] S3: `.cl`, the binary form, after Micropolis' city files, with
    tagged, checksummed chunks for CityLimits' own state; Ctrl+S writes
    it unless a city came from `.fcl`.
  - [x] S4: autosave every 15 minutes of play, skipped when nothing
    changed, written whole through a temporary file; one autosave, with
    an option for two.
  - [x] S5: importing Micropolis' `.cty` cities, reporting anything
    CityLimits doesn't have yet, which becomes bare ground. Import only.
- [x] A settings window, added by Tony Bark: from the start menu or the
  Escape menu, which became one column of buttons (Resume, Save, Load,
  Settings, Main menu, Quit), with tabs for Graphics (fullscreen) and
  Gameplay (autosave off, one, or two) (`UI/SettingsPanel.cs`).
- [ ] Scenarios to play, in the phases Tony Bark settled. Format 2.0
  counts time in the game's dates, a month with an optional week, not
  ticks, and still reads 1.x files; a scenario starts from a seed and
  strokes or a saved city, can start in a later year, has goals judged
  at an end date and disasters on dates, and is listed on the start page
  when it has goals, so agents' tests stay on the command line. Saving
  partway keeps the scenario; at the end a pop-up says won or lost, and
  the city plays on. The first challenges are written fresh.
  - [x] P1: a city's calendar can start in a later year
    (`CitySimulation`'s `startYear`), counted from 1900 in steps so
    saves don't change.
  - [x] P2: scenario format 2.0: dates in place of ticks, a start year
    or a saved city to start from, disasters on dates, and goals judged at
    the end, reported as `won` apart from `passed`; format 1 still read,
    and the built-ins rewritten.
  - [ ] P3: playing a scenario from the start page.
  - [ ] P4: the first built-in challenges.
- [ ] A city score, rating the city the way the classic games do.
- [ ] A start screen and a new-city flow: name, seed, and map size. The
  loading screen, the start menu, and New Game's name and seed are in
  (S2a), and clicking the name in the top bar renames the city; map size
  waits for a second size to choose.

### Kyoto: The scenario editor

A city builder is a level editor turned into a game, as Tony Bark put it,
so CityLimits gets a built-in editor for scenarios, sharing what play
draws and how the player works it. It saves what format 2.0 reads: a
scenario file and the city it starts from beside it.

- [x] The interface split out of the game into `CityLimits.Interface`, a
  library of its own the editor can share with play.
- [ ] The rest of the checklist, which Tony Bark settles before the work.

### Yokohama: Polish and release

- [ ] A visual pass: one tile atlas, one palette, and no stray colors in
  the draw code.
- [ ] Sound effects and music.
- [ ] Performance: the frame rate holds with a large city.
- [ ] Documented builds for Linux and Windows.
- [ ] A release build the owner can hand to a friend.

## Critical path

New Dullsville, Neat Map, Line City, and Kowloon are done: the loop, the
map, the tools, and a simulation that grows a city as Micropolis does,
proven on ordinary .NET through `CityLimits.Core` and its tests. Joffburg
is nearly done; its power overlay is the last piece.

Alpha now waits on Happy Isle's saving, and saving waits on the owner's
choice of format. Radial and Deadwood hang off the simulation's data
model, which is settled: trips already record their routes for traffic,
and the coverage maps and land value are ready for crime, pollution, and
fire. Happy Isle's remaining panels can be built alongside them.

The long poles are now Radial and Deadwood. If they run long, cut scope
inside Deadwood's disasters first, not inside traffic or crime, which the
baseline's growth depends on.

## Risks most likely to push estimates up

- The tick is the design center of the game. A demand model, a growth
  model, and an economy that all read one set of numbers is a shape
  decision, and shape decisions cost more than code.
- A city builder grows faster than it can be drawn. The chunk cache draws
  a 120 by 100 map at 60 FPS today; overlays and animated tiles are drawn
  over it, and each has to stay off the per-frame allocation path.
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
| 2026-10-23 | minor | The interface library: `Rendering/`, `UI/`, and the tools moved out of the game into `CityLimits.Interface`, as Tony Bark chose, so a scenario editor can share them; a pure move, with the start menu given the game's version. Tony Bark added Kyoto, the scenario editor, as a milestone, and settled the order: P3, then Kyoto, then P4's challenges made with it; and dropped the demo-town fallback, a relic of when only agents ran scenarios. 33–58 h left in total, 793 tests. |
| 2026-10-23 | major | Scenarios, P2: format 2.0, the scenario format's first major version, as Tony Bark asked, so players needn't know what a tick is. Times are dates, a month and an optional week; a scenario starts in a year of its own or from a saved `.fcl` or `.cl` city beside it; `events` strike disasters on dates whatever the switches say (`CitySimulation.Strike`); `goals` (population, funds, pollution, crime, traffic) are judged at `ends`, and the report says `won` apart from `passed`, so a lost challenge isn't a failed run. Format 1 files are still read; every built-in is rewritten in 2.0, with `zones-and-bulldozer`'s checks a step before its bulldozing moved to the year's last week. The report gains dates, events, and goals. 23–42 h left in total, 793 tests. |
| 2026-10-22 | major | Tony Bark marked CityLimits a beta: the version's base is 0.4.100, and the changelog sums up everything since 0.3.100. Polish begins with scenarios to play, in four phases Tony Bark settled (format 2.0 with dates, goals, and events; the start page; built-in challenges); P1 is in: a city can start in a later year, counted from 1900 in steps, so saves are unchanged. 27–48 h left in total, 764 tests. |
| 2026-10-22 | minor | Deadwood, D3, and with it Deadwood, is complete, as in Micropolis: floods (two disaster kinds in nine) rise from a shore, spread for 30 cycles over any land but a fire, and drain to bare ground, a flooded centre taking its whole building, as Tony Bark chose; earthquakes (one in nine) shake 300 to 1 000 tiles, wreck the outer tiles of buildings among them to rubble or their own fires, and shake the view. Micropolis' repair: plants and stations every 8 counts, hospitals and churches every 16, redraw holes of bare ground. Each disaster has a switch on the Disasters tab; an off disaster rolls as if it harmed nothing. A small movable pop-up with an Okay button, which Tony Bark asked for, says fullscreen waits for a restart. Hole logic moved to `World/Hazards.cs`. Saves move to 1.7 with floods a part of their own; scenarios to 1.3 for `Refused:Flooded`. Deadwood is at 100%; 16–30 h left in total, 758 tests. |
| 2026-10-22 | minor | Deadwood, D2, as in Micropolis: fire. A disaster roll each cycle (1 in 481 on Easy, a fire 2 times in 9) starts a fire on a building's outer tile; it spreads one visit in four to each side one time in eight, burns out sooner under fire coverage, leaves holes in buildings that are redrawn as they grow, and takes a building's furnished tiles when it reaches the centre, as Tony Bark chose; industry, plants, and stations explode, at once. The ticker reports fires and explosions in red, and, as Tony Bark asked after SimCity 3000, hints at zones out of an engine's reach, cut fire funding, and homes against the woods. The settings gain a Disasters switch on Gameplay and a Disasters tab with Fires, the player's and never a city's; the dice are rolled either way. Fullscreen now applies only at launch, after Tony Bark found going back to a window at runtime left a screen-sized window. Saves move to 1.6 with fire a part of its own and buildings written whole; scenarios to 1.2 with `disasters`, off by default. The new roll shifts every later one, so the demo town ends $4 richer. Deadwood is at 60%; 19–35 h left in total, 746 tests. |
| 2026-10-22 | minor | A detour before Deadwood's D2, at Tony Bark's request: a movable settings window with tabs, from the start menu or the Escape menu, holding fullscreen (a borderless window the size of the monitor) and autosave (now with Off), applied at once and written when it closes; the Escape menu becomes a single column of buttons with Save, Load, and Settings added. Fullscreen is now really saved, which the foundation's checklist had claimed. The disasters switch Tony Bark wants, a player's setting rather than a city's, comes with D2, when it has something to switch. 724 tests. |
| 2026-10-22 | minor | Deadwood, D1, as in Micropolis: pollution from industry, coal plants, and busy roads, every tile of a building counting as in Micropolis' core (Tony Bark's choice over the Java port), lowering land value and homes' worth and stopping homes above 128; crime from land value, density, and police coverage, taking 20 off land value above 190; messages above averages of 60 and 100; Pollution and Crime overview modes. Saves move to format 1.5 with two parts older versions may leave out or keep, and 0.3.106 opened one. Tony Bark settled Deadwood's phases: fire (D2, with SimCity 3000-style hints on the ticker), then floods and earthquakes (D3); no tornado, monster, or meltdown yet; no warnings before a disaster beyond fire's hints; Micropolis' free repairs. The built-in scenarios' and the headless town's coal plants moved up power lines out of their homes' pollution; small town's industry now holds back its neighbours. Deadwood is at 40%; 23–41 h left in total, 722 tests. |
| 2026-10-22 | patch | A save that kept a newer save's parts but fell back to this version opens again with them; before, the flat format refused it and the binary one dropped them. 0.3.100 to 0.3.102 got backports that open saves from 0.3.103 on, 0.3.100.1 to 0.3.102.1, on release branches, and a save from each is a fixture. 705 tests. |
| 2026-10-22 | minor | Radial, R3, and with it Radial, is complete, as in Micropolis: rail at $20 a tile, tunnels at $100, crossings with roads and lines, trips on road and rail alike with traffic only on roads, rail serving zones, each rail tile two road tiles of upkeep, crumbling underfunded rail, and the inadequate-rail message past 50 zones; the commuter train wanders the track, drawn by the game alone with its own generator, as Tony Bark chose. Saves move to format 1.4, with rail a part an older version can leave out, and scenarios to 1.1 for the Rail tool and count; a built-in `rail` scenario. Imports bring rail in as rail. The palette's buttons became switches and the number keys were dropped. Radial is at 100%; 28–50 h left in total, 697 tests. |
| 2026-10-22 | minor | An interface update of its own before Radial's R3, after Tony Bark's revised mock-up and SimCity 3000's ticker: the bar moves to the bottom, over a news ticker that never stops (white headlines, some reacting to the city and some flavour; yellow notices and the city's messages; red for disasters to come), replacing the notice line; the key hints become a movable help window behind a green question mark or F1; build results and refusals, and the Inspect tool's tile readout in place of its old panel, show beside the pointer in black on yellow; the ticker stands still while the city is paused; the palette and the overview move to the top corners; and the loading screen's phrases scroll like the ticker. 673 tests. |
| 2026-10-22 | minor | Radial, R2, as in Micropolis: the scan shows each road light traffic from 64 and heavy from 192, drawn over the map from the sheet's four-frame animations at Micropolis' pace; a road that showed heavy traffic counts twice toward upkeep, which leaves the demo town $6 poorer after two years; the year's traffic average reports jams above 60; and the overview gains a Traffic mode, its buttons in two rows. Saves move to format 1.3. Radial is at 75%; 34–60 h left in total, 657 tests. |
| 2026-10-22 | minor | An update of its own before Radial's R2, settled with Tony Bark: from 0.3.103 a newer save opens in an older version when every part it doesn't know is ignorable, by a parts list each save carries with two flags a part, as PNG does, and the keepable parts are written back in their own format; saves move to format 1.2 for the list. Autosaves follow their city's format. Each setting falls back on its own, so one bad value keeps the rest. 640 tests. |
| 2026-10-22 | minor | Radial, R1: the traffic map, as in Micropolis. A trip that arrives lays 50 on each road tile it remembered, on 2 by 2 blocks, capped at 240 always (Micropolis caps only one time in six; Tony Bark chose always), and phase 10 fades it. Saves move to format 1.1 with a `traffic` section and a `TRAF` chunk; 1.0 saves upgrade to quiet roads. The headless run logs the busiest road. Traffic has no effect until R2. Radial is at 50%; 38–67 h left in total, 626 tests. |
| 2026-10-22 | minor | Radial begins, phase R0, settled with Tony Bark: saves, scenarios, and mod files carry major.minor format versions (a minor version only adds; each new Micropolis edition takes the next), every JSON format reads through source-generated code with reflection-based JSON off in the game and tests, Core is trim-analysed, and `docs/schemas/` holds JSON Schemas generated from the readers and checked by a test. Radial's checklist becomes R0 to R3, rail included, and the congestion item is dropped. Radial is at 25%; 41–72 h left in total, 618 tests. |
| 2026-10-21 | major | Tony Bark marked CityLimits an alpha: the version's base is 0.3.100, and the changelog sums up everything since 0.2.100. Every row of the alpha gate passes. Traffic, Deadwood, the city score, and polish are left; 40–70 h in total. |
| 2026-10-21 | minor | Saving, S5, and with it saving, is complete: Micropolis' and SimCity Classic's `.cty` files in the Cities folder are listed and imported, never written. Tiles come in by Micropolis' tile numbers, with the owner's approval, and buildings whole from their centres; what CityLimits doesn't have yet becomes bare ground, as the owner chose, and is listed. The date, money, tax, funding, populations, demand, and history come across, the seed is made from the file, and power is traced at once. Bridgeport is a fixture in `.cty` too, made by a writer that lives only in the tests. 40–70 h left in total, 599 tests. |
| 2026-10-21 | minor | Saving, S4: autosave every 15 minutes of play, counted only while the city runs, to `Joffburg (autosave).cl` beside the city's own file, skipped when the city is as it was last saved. `AutosaveSlots` in the settings keeps one or two, the newer replacing the older. Load Game marks autosaves, and saving a city opened from one goes to its own file without asking. 43–76 h left in total, 571 tests. |
| 2026-10-21 | minor | Saving, S3: the binary city format, `.cl`, after Micropolis' city files: a header, then tagged chunks with CRC-32 checksums, the map as a 16-bit word a tile, column by column, and the other sections written field by field from the same model as `.fcl`. An unknown chunk is skipped and noted; a damaged one refuses the file. Ctrl+S writes `.cl` unless a city came from `.fcl`, and Load Game lists both. Bridgeport, a city the owner built, is a test fixture in both formats. 45–79 h left in total, 566 tests. |
| 2026-10-21 | minor | Saving, S2c: cities save and load. A save holds everything that affects the steps to come, the cycle's half-done figures included, so a city saves at any step, even paused, and runs on exactly as it would have; a test runs a saved and a loaded town in step for two years. The flat city format, `.fcl`, is one JSON file with the map as rows of characters, read strictly. Ctrl+S saves under the city's name, asking before replacing another city's file; Load Game and Ctrl+O list the saved cities; leaving a city with unsaved changes asks first. The save-and-load checklist item is done, Happy Isle is at 50%, and every row of the alpha gate passes; calling it alpha is the owner's call. 47–83 h left in total, 551 tests. |
| 2026-10-21 | minor | Saving, S2b, a phase the owner added: a click on the city's name opens the city panel, paused and movable, with the name to rename on Enter, the city at a glance (date and age, population, demand, funds and tax, this year's and last year's cash flow), the mods in the order applied, and the seed, which a click copies. The owner swapped the new phase in ahead of the save model, which becomes S2c; the rows below use the new letters. The budget, demand, and city panels share `IMovableWindow`. 51–90 h left in total, 513 tests. |
| 2026-10-21 | minor | Saving, S2a, with the start menu folded in at the owner's request: after the owner's mock-up of loading, start, and game, the window opens on a loading screen and a start menu (New Game, Load Game dimmed until S2c, Quit). New Game names the city and takes a seed, Escape in a city opens a menu that leads back, and clicking the name in the bar renames the city. The play button turns blue. The legal section of AGENTS.md was reframed with the owner beforehand. 52–92 h left in total, 513 tests. |
| 2026-10-21 | minor | The owner settled how cities are saved: one save model and three formats (`.cl`, CityLimits' binary form after Micropolis' layout with its own chunks; `.fcl`, the same as flat JSON; and `.cty` import only), saves under Documents with no file dialog, the cycle's half-done figures saved so a city saves at any moment, and autosave every 15 minutes, skipped when nothing changed. S1 is done: `GameFolders` puts the settings in the per-user config folder and mods and saves under `Documents/CityLimits`, keeping a writable copy with settings beside it portable. Before, an AppImage could never save its settings. Mods move, which the modding guide now says. Saving grows to 14–26 h; 54–96 h left in total, 500 tests. |
| 2026-10-21 | patch | At the owner's request, the budget and demand panels are windows the player drags by their titles, which remember where they were left and stay on the screen, and the demand panel leads with a chart of plain lines for R, C, and I over the ten years of demand history the city now keeps. No milestone moves; 491 tests. |
| 2026-10-21 | minor | Happy Isle, ahead of saving, as the owner chose, since it is cosmetic: the top bar follows the owner's mock-up in `docs/design/`. It has a pause and play button, the month, speed buttons that fill up like a meter, the city's name (`CitySimulation.Name`, "CityLimits" until saves can name it), an R, C, I meter, the funds, and the population, with the steps and the frame rate on the right. The meter opens a demand panel, a liberty the owner chose since Micropolis has only the meter: each demand against its limit, what moved it, and the census behind it. The held tool's name and the mouse position leave the bar; the palette's header still names the tool. The panels item notes power as the overview's mode. 489 tests. |
| 2026-10-21 | minor | Joffburg, phase J4, and with it Joffburg, is complete: the overview map, as in Micropolis, shows the whole city in the top-right corner with the main view outlined, moves the view where it is clicked or dragged, and switches between the city, power, fire and police coverage, land value, and population density. The owner chose the overview and its modes. The alpha gate's read-the-city row passes; 46–82 h left in total, 481 tests. |
| 2026-10-21 | patch | The owner settled the power-demand item: demand follows the grid, every zone and line tile drawing power whether anyone lives there or not, as in Micropolis (a coal plant carries about 50 zones), not population. The item is reworded and checked off; Joffburg is at 80%, with the power overlay left. 48–86 h left in total. |
| 2026-10-21 | patch | Housekeeping, at the owner's request: the current focus is put in order (the power overlay, saving, traffic, then crime, pollution, and disasters), Happy Isle's palette and HUD are checked off (33%), Joffburg's and Kowloon's notes and the critical path are brought up to date, and the ranked list gains traffic. 49–88 h left in total. |
| 2026-10-21 | minor | Joffburg, phase J3c, and with it J3, services, is complete: hospitals and churches, as in Micropolis, one of each needed for every 256 residents; an empty residential zone becomes one a time in four when it would grow, and one too many turns back now and then. The owner chose to fix Micropolis' quirk that loses a hospital when both are needed. The modding doc is now `docs/modding/`, a player's guide and `tuning.md`, and AGENTS.md says how docs are written. 481 tests. |
| 2026-10-21 | minor | Joffburg, phase J3b: parks, as in Micropolis: $10 a tile, laid as a rectangle on clear ground or rubble, never trees; woods or, one time in five, a fountain, chosen from the tile's place as the owner chose. A wooded park adds to land value as trees do; a fountain counts as built. The services checklist item is done; Joffburg is at 60%. 470 tests. |
| 2026-10-21 | minor | Joffburg, phase J3a: police and fire stations, as in Micropolis: 3 by 3, $500 and $100 a year, a `ServiceSpec` a mod can change in `services.json`. Each adds its funding effect to its 8 by 8 block, halved without power or a road, and the maps are smoothed at phases 13 and 15; coverage acts on crime and fires in Deadwood. The budget pays their upkeep, and the city asks for stations past 60 people and for their funding below 70%. A plant now has power from the moment it is placed, and an unpowered station blinks the bolt. The owner chose parks' look by position and fixing Micropolis' hospital-then-church quirk. 462 tests. |
| 2026-10-21 | minor | Joffburg, phase J2b, and with it J2, taxes and the budget, is complete: the budget panel shows the year as it stands, last year, and the tax rate and funding with buttons to change them. It opens by itself when a year's upkeep outran its taxes or funding was cut, saying why, and with B or a click on the funds; the game pauses while it is open. The funds show red below zero. Joffburg is at 40%; 444 tests. |
| 2026-10-21 | minor | Joffburg, phase J2a: taxes and upkeep, as in Micropolis, with its numbers taken by the owner's leave. Each count estimates the year and sets a 48th aside; the year is settled into the treasury, which can go below zero. Taxes come from population and average land value at the tax rate; roads cost 0.7 a tile, a bridge five tiles, and stations $100, at their funding levels, roads paid first. The tax rate pushes demand, underfunded roads crumble, and the city warns of high taxes and underfunded roads. The owner chose a panel that pauses the game, opens itself on a deficit or a cut, and opens with B or the funds. 443 tests. |
| 2026-10-20 | minor | Joffburg, phase J1d, and with it J1, power, is complete: the tool palette is laid out as Micropolis', a header with the name and price of the tool under the pointer or the held one, over centred rows of icon buttons in Micropolis' order, with the held tool's highlighted icon. The number keys still pick tools. A missing icon now falls back to a labelled box, as it was meant to: `GameAssets.IconFor` never returned null. 417 tests. |
| 2026-10-20 | minor | Joffburg, phase J1c: the power grid, as in Micropolis. The scan counts plants and gives each zone its power, kept on its centre; phase 11 traces power from the plants through lines, zones, and plants until their pooled capacity (700 tiles coal, 2 000 nuclear, every plant counted) runs out. An unpowered zone scores -500 and never grows. The city sends brownout, blackout, and need-power messages; the game blinks a lightning bolt over unpowered zones and the readout says "no power". Plant prices and capacities move into a `PlantSpec` a mod can change in `plants.json`, and scenario tile checks gain `powered`. Every built-in scenario and the headless demo town gain a coal plant. Joffburg is at 20%; 61–105 h left, 417 tests. |
| 2026-10-20 | minor | Joffburg, phase J1b: power lines, as in Micropolis. $5 a tile and $25 under water, strung in a straight line; under water only from something beside it that carries power, as a bridge needs a road. A line over a straight road, or a road over a straight line, makes a road-and-line tile that joins roads only along its road and lines only along its line. Lines join zones and plants, and the bulldozer clears them for $1. 397 tests. |
| 2026-10-20 | minor | Joffburg begins, phase J1a: coal and nuclear power plants, 4 by 4 at $3,000 and $5,000, as in Micropolis, placed one at a time centred on the pointer and knocked down through their centre. `Footprint` generalises the zone's 3 by 3 layout to any size. The plan, from the owner: services follow Micropolis, the budget is always automatic and opens only when spending outpaces income, and the palette takes Micropolis' layout. No milestone moves yet; 375 tests. |
| 2026-10-20 | major | Neat Map is complete, and with it step 5 and the whole baseline refactor: the terrain is laid out as in Micropolis, on its 120 by 100 map. One map in ten is an island; the rest have two wide rivers and a narrow one, and up to ten lakes. Woods are scattered and thinned, and shores and wood edges are drawn from the sheet's edge tiles, chosen from the ground beside each tile. Rock is gone. The built-in scenarios move onto the new map. The version becomes 0.2.100. The focus moves to Joffburg; 64–110 h left, 360 tests. |
| 2026-10-20 | patch | The land value, pollution, and disasters milestone is renamed from Bad News to Deadwood, another of the cities Micropolis ships with, like the other milestones' names. No change to its scope. |
| 2026-10-20 | major | Kowloon is complete, and with it step 4 of the baseline refactor: zones grow and decline as in Micropolis. Each zone takes trips, and demand plus its worth against a roll decide each step; residential zones grow houses and then buildings where they cluster, commerce is held to what land value carries, and zones without a road decline. Land value comes from distance to the city's centre and nearby trees and water, and population density from the zones around. A new built-in scenario, `small-town`, grows twelve zones for ten years. The alpha gate's growth row passes; 70–120 h left, 343 tests. |
| 2026-10-20 | patch | Step 4d: zones' trips, as in Micropolis. A trip sets out from the first road along a zone's edge and drives up to 30 road tiles, picking its way with the seeded generator, looking for a zone of a kind the zone's spec lists, which a mod can change. Growth uses trips from 4e. 76–130 h left, 331 tests. |
| 2026-10-20 | minor | Step 4c: the census and demand, as in Micropolis. The scan counts each zone's population, the census is recorded monthly with ten years of history, and residential, commercial, and industrial demand move every second count from employment, births, and the market. The HUD shows the population and an R, C, I demand panel; the headless run logs both monthly. Kowloon is at 75%, 79–135 h left, 319 tests. |
| 2026-10-20 | minor | Step 4b: zones keep their state as Micropolis does. A residential zone grows up to 8 single houses on its outer tiles, then a building; buildings have 4 levels (5 for commercial) in land-value classes, drawn from the sheet, with each level's population in the zone's spec, which a mod can change. Growth still comes on a flat chance until 4e. Kowloon is at 63%, 82–140 h left, 312 tests. |
| 2026-10-20 | minor | Step 4 begins with zones as data: a `ZoneSpec` holds each zone's name and price, the base game's definitions are JSON read by the same loader as a mod's, and data mods in `Mods/` change them, checked first and skipped whole when broken. Scenarios run on the base game. `docs/modding.md` is the guide. 295 tests; no milestone moves until growth lands. |
| 2026-10-20 | minor | Scenarios, phase 1: `CityLimits.Core/Scenarios/` reads and checks scenario files, runs them deterministically, and reports in a versioned JSON format; `citylimits scenario` runs one by id or file with `--json`, falls back to the demo town when one can't be loaded, and exits 0 to 3. Three built-in scenarios (`demo-town`, `bridges`, `zones-and-bulldozer`) run as tests. Kowloon is at 50%, 85–145 h left, 278 tests. |
| 2026-10-20 | patch | The scenario spec is revised for the Micropolis baseline: actions are tool strokes (`tool`, `from`, `to`) shaped and priced as in the game, ticks are phases (64 a month, 768 a year), zone checks read the zone's centre, counts give zones for the three zone kinds, and the example is the demo town, checked against the engine. Scenarios, phase 1, can now be built. |
| 2026-10-20 | minor | Step 3 of the baseline refactor: time runs as in Micropolis. A tick is one phase of a 16-phase cycle; phase 0 moves the calendar on by a 48th of a year, from January 1900, and phases 1 to 8 scan the map in bands. A month takes 16 seconds and a year about 3 minutes at Normal, with Fast and Super fast at 5 and 50 times that. The headless run defaults to a year. Kowloon is at 38%, 91–155 h left, 219 tests. |
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
