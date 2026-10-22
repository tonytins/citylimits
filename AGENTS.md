# AGENTS.md: guide for AI agents and contributors

CityLimits is a hobby city-building simulation, a SimCity-style game written
in **C#** on [Raylib-cs](https://github.com/ChrisDill/Raylib-cs) (`8.1.0`,
.NET 10). The player lays roads and zones land on a tile map, and a
tick-driven simulation grows a city: population, demand, power, traffic,
taxes, land value, pollution, and disasters. It is a desktop game, not a
web or mobile one. The project is `CityLimits`, the assembly is `CityLimits`,
and the namespaces start with `CityLimits`.

This is early-stage, solo work held to production standards. Read this file
first, then [docs/ROADMAP.md](./docs/ROADMAP.md) and
[docs/scorecard.md](./docs/scorecard.md). The roadmap says what comes next;
the scorecard says what is true today, with evidence.

## 1. Workspace map

```text
CityLimits.slnx             solution: the game, the core library, the command line library, and the tests
CityLimits/                  the game project (Raylib-cs, NLog, net10.0)
  CityLimits.csproj            package references, the library references, the target framework, and the asset copy
  Program.cs                    entry point: the log, the command dispatcher, shutdown
  GlobalUsings.cs               global usings shared by the project
  Game/                         namespace CityLimits.Game: the game command, the window, the loop, the tools, the city's messages, the mods folder, the headless run, the log
    Commands/                   namespace CityLimits.Game.Commands: the subcommands, one class each
  Rendering/                    namespace CityLimits.Rendering: the camera, the tile drawing, the chunk cache, the tile sheet, the theme
  UI/                           namespace CityLimits.UI: the loading screen, the start menu and its new-city panel, the HUD and its bar along the bottom, the news ticker, the pointer's hints, the tool palette, the budget, demand, city, and help panels and how windows are moved, the in-game menu, the overview map
  Assets/                       the tile sheet, the tool icons, the logo, and the built-in scenarios (Scenarios/*.json), copied to the output folder
CityLimits.Core/              engine-free library: the world and the simulation, no Raylib
  CityLimits.Core.csproj        the project file; it must never reference Raylib-cs
  GlobalUsings.cs               global usings shared by the project
  Randomness/                   namespace CityLimits.Core.Randomness: the seeded generator the simulation owns
  World/                        namespace CityLimits.Core.World: the map and its default size, the tiles and their footprints, the terrain and its edges, the tile rules, the build actions, roads, power lines
  Simulation/                   namespace CityLimits.Core.Simulation: the clock, the calendar, the tick and its cycle, the census, demand, growth, trips, traffic and its animation, the city maps, the power grid, service coverage, the city's messages
  Economy/                      namespace CityLimits.Core.Economy: the treasury, the costs, the budget
  News/                         namespace CityLimits.Core.News: the news ticker's queue, its headlines and the desk that picks them, and the loading screen's phrases
  Settings/                     namespace CityLimits.Core.Settings: the settings that survive a session, and the folders the game keeps its files in
  Formats/                      namespace CityLimits.Core.Formats: format versions, and the JSON Schemas generated from the readers
  Saving/                       namespace CityLimits.Core.Saving: the save model, the binary (.cl) and flat (.fcl) city formats, importing Micropolis cities (.cty), autosave, the map's layers and words, and the saved cities' files
  Scenarios/                    namespace CityLimits.Core.Scenarios: the scenario loader, the runner, the report, the demo town
  Definitions/                  namespace CityLimits.Core.Definitions: the definitions as data, the base game's, and the mod loader
CityLimits.CommandLine/       engine-free library: the command line, no Raylib
  CityLimits.CommandLine.csproj the project file; it references no package and no other project
  GlobalUsings.cs               global usings shared by the project
  *.cs                          namespace CityLimits.CommandLine: the parser, the dispatcher, the option reader, the exit codes
CityLimits.Tests/              xUnit 2 tests for CityLimits.Core and CityLimits.CommandLine, and every built-in scenario
  CityLimits.Tests.csproj       references the two libraries, never the game project
.agents/skills/                skills for agents and contributors
  dotnet-guidelines/            C# and .NET conventions
  raylib-cs/                    how CityLimits uses Raylib-cs 8.1.0, and how to check a visual change
docs/
  ROADMAP.md                    milestones, current focus, estimates, revision history
  scorecard.md                  verified status per area, with evidence
  scenarios.md                  the scenario file and report formats
  saving.md                     where saves go, saving and loading in the game, the save formats, their versions, and the schemas
  schemas/                      JSON Schemas for saves, scenarios, and mod files, one a version, generated and checked by a test
  design/                       the owner's mock-ups of the interface, with a README on their intent
  modding/
    README.md                   what a mod is, installing one, writing one, how mods are applied, and the code behind them
    tuning.md                   what a tuning mod can change in zones, plants, and stations, and the limits on each
```

The split is the point of the layout: `CityLimits.Core/` holds everything
that can be decided without a window, `CityLimits.CommandLine/` holds the
command line, which knows nothing of the city, and `CityLimits/` holds
everything that draws it and the commands that run it. The game project may
reference both libraries; neither library may reference the game, and
neither references the other.

The folders above are the target shape, and the code follows it today:
the world, the simulation, the generator, and the settings live in
`CityLimits.Core/`, and the window, the loop, the camera, and the drawing
live in `CityLimits/`. Keep new code in the folder that matches the map,
and don't invent a parallel structure.

Build output (`bin/`, `obj/`, `*.user`) is ignored; never commit it.

## 2. How it works today

- **The command line.** `Program.Main` sets up the log and hands the
  arguments to the `SubcommandDispatcher`. With no command named, the game
  itself runs; `scenario` is the only subcommand, and there is no `play`
  command, because playing is the default. Two global flags work anywhere
  on the line: `--help` prints the usage (or, after a command, that
  command's usage), and `--headless` runs a command with no window. A
  command that can't run headless refuses the flag, and `--help` wins over
  it. Every option is read strictly: an unknown option, a missing or
  malformed value, a value out of range, an option given twice, or a stray
  argument is reported on stderr with exit code 2, and nothing runs.
- **The game.** `GameCommand.Run` reads `--seed`, and with `--headless`
  hands the city to `HeadlessRun`. Otherwise `WindowedGame.Play` opens a
  resizable window titled "CityLimits" at the saved size, and runs the
  owner's flow from `docs/design/`: the loading screen
  (`UI/LoadingScreen.cs`), then the start menu (`UI/StartMenu.cs`), then
  the cities started from it, until the player quits or closes the
  window; then it saves the settings and closes the window. Every frame,
  the menu's and the city's, is guarded: a failure is logged, told on a
  city's news ticker, and the game keeps running.
- **The loading screen and the start menu.** The loading screen shows
  the logo on the blue background, with phrases scrolling along the
  bottom as the news ticker does (`LoadingPhrases`, starting with
  "Reticulating splines", a nod to SimCity 2000), for 2.5 seconds, or
  until a key or a click; it is for show, since the game loads in well
  under a second.
  The start menu glides the logo up from the middle to a sixth of the way
  down, with New Game, Load Game (dimmed while no city is saved), and
  Quit under it and the version in the bottom right. New Game opens a
  panel (`UI/NewCityPanel.cs`) with the city's name, "CityLimits" to
  start, and a seed, blank for any, read by `NewCity.TryRead`; `--seed`
  fills the seed in. Start, or Enter, begins the city; a blank seed is
  picked by `Random.Shared`, outside the simulation, and logged.
- **The map.** `GameLoop` builds a 120 by 100 map, Micropolis' size, when
  a city starts, from its seed (2000 by default with `--headless`). `TileMapGenerator` lays it out as Micropolis
  does: one map in ten is an island, and the rest have two wide rivers, a
  narrow one, and a few lakes; then woods are scattered and thinned. The
  ground is grass, water, trees, or rubble. The renderer
  draws only the tiles the camera can see, and it draws them from the tile
  sheet in `Assets/tiles.png`. Each tile is one lookup and one draw:
  `Rendering/TileAtlas.cs` turns a tile's state into a sheet index, and a
  road's index comes from the sides that join another road, which
  `RoadNetwork.ConnectionsAt` reads from the map. A road over water is
  drawn as a bridge. Water and trees are drawn from the sides with the
  same ground beside them, which `TerrainEdges.SidesAt` reads from the
  map, so shores and wood edges join up. A zone's nine tiles are drawn from the sheet as one
  3 by 3 drawing, chosen by the development its centre holds.
- **The chunk cache.** The map is drawn ahead of time into render
  textures of 32 by 32 tiles (`Rendering/MapChunkCache.cs`), and a frame
  draws only the chunks the camera can see. `TileMap.TileChanged` reports
  each tile whose state changes, and the cache redraws that tile's chunk,
  and its neighbours', because a road's, a shore's, and a wood's drawing depends on them, before the
  next frame begins. If the textures can't be made, the log says so and
  the map is drawn tile by tile, which gives the same picture. Traffic
  moves, so it is drawn over the cached map each frame: each road
  showing traffic is drawn again from its frame of the sheet's animation
  (`TileAtlas.TrafficIndexFor`).
- **The camera.** The WASD keys and the arrow keys pan, the mouse wheel
  zooms toward the pointer, and the view is clamped to the map.
- **The clock.** Time runs as in Micropolis. A tick is one step of the
  simulation, and each step is one phase of a 16-phase cycle
  (`CitySimulation.Tick`): phase 0 moves the calendar on by a 48th of a
  year, and phases 1 to 8 scan the map in 8 vertical bands; phase 9
  records the census and sets a 48th of the year's taxes and upkeep
  aside, settling the year when it comes round, 10 fades the traffic and checks the city's messages, 11 traces power, 12
  works out land value, 13 spreads police coverage, 14 population
  density, and 15 fire coverage; 13 and 15 are also where crime, and fire
  and disasters, will run.
  `CityCalendar` turns the count into a month: 4 counts a month, 48 a
  year, from January 1900. At Normal speed a step runs every quarter of a
  second, so a month takes 16 seconds and a year about 3 minutes; Fast is
  5 times that and Super fast 50 times (`GameSpeed`). Space or the top
  bar's pause button pauses the game and brings back the last speed, and
  the comma and period keys or the bar's speed buttons change the speed. The speed changes
  how many steps run in a frame, never the size of a step.
- **The zones.** A zone is 3 by 3 tiles, as in Micropolis. Each tile
  knows its part of the zone (`TileState.ZonePart`, 0 to 8, with 4 the
  centre). As in Micropolis, a zone's building is kept on its centre: its
  development level (0 for no building, then up to the levels its
  `ZoneSpec` has) and its land-value class. A residential zone with no
  building grows single houses first, each kept on the outer tile it
  stands on (`TileState.House`); at its spec's number of houses they make
  way for a level-1 building. A zone's population comes from its spec:
  one per house, or the building's figure for its level
  (`ZoneSpec.PopulationOf`). `TileMap.ZoneCenterOf` and `ZoneStateAt`
  find a zone's centre from any of its tiles. A zone is served when a road lies on one of the 12 tiles along
  its edges, just outside it, not at a corner (`RoadNetwork.IsZoneServed`).
- **Power plants.** A coal or a nuclear power plant is 4 by 4, as in
  Micropolis, with its centre one tile in from its top-left corner, like a
  zone's. `Footprint` says how big each use is (3 by 3 for a zone, 4 by 4
  for a plant, one tile for anything else) and numbers its parts across
  and then down, so a plant's tiles are parts 0 to 15 with 5 the centre.
  `TileState.HasFootprint` and `IsBuildingCenter` cover zones and plants
  alike; `IsZoned` and `IsZoneCenter` stay the three zones only. A plant's
  name, price, and capacity are a `PlantSpec` in the definitions, which a
  mod can change in its `plants.json`. A plant has power from the moment
  it is placed, as in Micropolis.
- **Police and fire stations.** A station is 3 by 3, placed like a plant,
  one at a time and centred on the pointer, and knocked down through its
  centre. Its name, price ($500), and yearly upkeep ($100) are a
  `ServiceSpec`, which a mod can change in its `services.json`, and the
  budget pays the upkeep for the stations the scan counted. As in
  Micropolis (`ServiceCoverage`), the scan adds each station's strength
  to the 8 by 8 tile block it stands in: its funding's effect (up to
  1 000), halved without power and halved again without a road along its
  edge. Phase 13 spreads the police map and phase 15 the fire map, each
  smoothed three times: a block keeps half of itself plus an eighth of
  each neighbour. Coverage acts on crime and fires, which come with
  Deadwood; until then nothing answers to it. The city says when more
  than 60 people have no fire station or no police station, and when
  either is funded below 70%. An unpowered station blinks the bolt, as a
  zone does.
- **Parks.** A park is $10 a tile, laid as a rectangle on clear ground or
  rubble (which it clears for $1 more), never on trees, water, or
  anything built, as in Micropolis; the bulldozer clears it for $1. Each
  tile is one of four wooded drawings or, one time in five, a fountain,
  chosen from the tile's place (`Parks.LookAt`), a liberty the owner
  chose so the preview shows what is built and building never spends the
  simulation's randomness. For land value, a wooded park counts as trees
  (+15 to the blocks around it, and not something built); a fountain
  counts as built, as in Micropolis.
- **The power grid.** As in Micropolis (`PowerGrid`), the scan counts the
  plants it passes and gives each zone the power the last trace left it,
  kept on its centre (`TileState.Powered`); a plant always has power.
  Phase 11 traces power from the plants through every tile that carries
  it toward the next, one tile at a time, until the plants' pooled
  capacity runs out: 700 tiles a coal plant and 2 000 a nuclear one,
  counted over every plant on the map, connected or not. A zone with no
  power scores -500, so it never grows and now and then declines. The
  city raises `CitySimulation.MessageSent` for brownouts when the trace
  runs out, and, at Micropolis' counts, for a city of more than 10 zones
  with no plant and for blackouts below 7 in 10 powered. The game blinks
  the sheet's lightning bolt over each unpowered zone, the readout says
  "no power", and the game and the headless log pass a repeated message
  on at most once a year (`CityMessageFilter`).
- **Power lines.** A line (`TileUse.PowerLine`) is strung like a road, in
  one straight line, and under water when something beside it carries
  power. Over a straight road it makes one tile that is both
  (`RoadAcrossPowerLine`, `RoadDownPowerLine`), which joins roads only
  along its road and lines only along its line, as in Micropolis.
  `PowerLines.RunsToward` says which sides of a tile carry power: every
  side of a line, a crossing's line, and every tile of a zone or a plant.
  `PowerLines.ConnectionsAt` reads a line's joins from the map, as
  `RoadNetwork.ConnectionsAt` does a road's, and the drawing comes from
  them. `TileState.IsRoad` includes crossings, so they carry traffic and
  serve zones.
- **Growth and decline.** As in Micropolis, when the scan reaches a
  zone's centre (`ZoneGrowth.Visit`), a zone with people sometimes sends a
  trip out, and a zone with no road along its edge declines. Otherwise,
  now and then, the city's demand for the zone's kind plus the zone's own
  worth (residential: land value; commercial: the commercial rate;
  industrial: nothing) make a score, and a roll from the simulation's
  seeded generator decides whether it grows a step, declines a step, or
  stays. A residential zone grows houses, on the free outer tile with the
  most roads beside it, until its spec's number, then a building once the
  population density around it passes 64, so a lone zone stays at houses
  and buildings come where zones cluster. Buildings take their land-value
  class from land value, except industry's, which is a coin toss.
  Commerce grows only as far as land value divided by 32. A zone with no
  power never grows. The same seed and the same work give the same city.
  Until their systems exist, pollution and crime are none.
- **Hospitals and churches.** As in Micropolis (`CommunityBuildings`),
  nobody builds them: each month the city needs one of each for every 256
  residents, and one time in four, when an empty residential zone would
  grow, it becomes a hospital if one is needed, otherwise a church if one
  is needed. Micropolis can make both on one roll, losing the hospital;
  the owner chose to fix that, so a roll makes one. When the city has too
  many, each has a 1 in 21 chance a scan of becoming an empty residential
  zone again. They are 3 by 3, drawn from the sheet, need power like a
  zone, and the bulldozer knocks them down through their centre.
- **The city maps.** `CityMaps` holds land value and population density
  on 2 by 2 tile blocks, as Micropolis does. Land value (phase 12) is, for
  a block with something built on it, 34 less the distance to the city's
  centre of mass, times four, plus 15 for each tree, wooded park, or water tile in the
  4 by 4 area around it, smoothed, from 1 to 250. Population density
  (phase 14) spreads each zone's population, smoothed three times, and
  sets the centre of mass and the commercial rate.
- **Trips.** As in Micropolis, a zone's trip (`TripPlanner`) sets out
  from the first road along its edge and drives up to 30 road tiles, each
  step in a direction picked by the simulation's seeded generator, never
  straight back, looking for a zone of a kind its spec lists
  (`ZoneSpec.TripsTo`); a residential zone counts only once it has
  people. The outcome is no road, nowhere to go, or arrived, and the
  route is remembered every other step.
- **Traffic.** As in Micropolis (`TrafficMap`), a trip that arrives lays
  50 on each road tile it remembered (roads, bridges, and crossings), on
  2 by 2 blocks, as land value is kept, up to 240 a block. Micropolis caps
  at 240 only one time in six; CityLimits always does, as Tony Bark
  chose. Phase 10 fades every block: by 34 above 200, by 24 above 24, and
  to nothing below. The headless run logs the busiest block each month.
  When the scan passes a road, bridge, or crossing, it shows the level
  its block calls for (`TrafficMap.LevelAt`): none below 64, light to 191,
  heavy from 192, kept apart from the tile state as Micropolis keeps it
  in the road's drawing; a tile that changes starts again quiet. A road
  that showed heavy traffic when the scan reached it counts once more
  toward the roads' upkeep. The game draws light and heavy traffic from
  the sheet's four-frame animations, a frame every eighth of a second at
  Normal and every 25 ms faster (`TrafficAnimation`), still while paused,
  as Micropolis does. Each year, after the taxes and only for a city with
  people, the city works out its traffic average (`TrafficMap.Average`):
  the traffic on every tile with land value, averaged, times 2.4. Above
  60, the city reports frequent traffic jams.
- **The census and demand.** As in Micropolis, the scan adds each zone's
  population to the census (`CityCensus`), which starts again every
  cycle and is recorded once a month, keeping ten years of history; the
  city's population is residents plus eight per commercial and industrial
  unit, times twenty. Every second count, each of the three demands
  (`CityDemand`) moves by how far its population is from what jobs,
  births, and the market call for. `CityDemand` keeps what moved each
  demand at its last update (the economy's pull and the tax's push) and
  the jobs per resident and workers per job behind them, and `CityCensus`
  keeps residents', commerce's, and industry's shares of the population,
  so the demand panel can show them. The headless run logs the
  population and demand each month. The tax rate pushes all three demands by Micropolis' own
  steps, from +200 at 0% to -600 at 20%, none at 7%. Until their systems
  exist, the difficulty is Easy and the demand caps are off.
- **Taxes and the budget.** As in Micropolis (`Economy/CityBudget.cs`),
  every count phase 9 estimates the year from the city as it stands and
  sets a 48th aside; when the count comes round to a year, the city
  collects and pays (`CitySimulation.YearSettled`), and the money can go
  below zero (`Treasury.Settle`). Taxes are population (residents in
  eighths, plus commerce and industry) times average land value over
  120, as whole numbers, times the tax rate (0 to 20%, 7% to start) and
  Easy's 1.4. Upkeep is the road tiles the scan counted (a bridge counts
  five, and a road showing heavy traffic one more) times Easy's 0.7, and $100 a police or fire station, each paid at
  its funding level; when the money runs out, roads are paid first, then
  fire, then police, and the rest is cut. Roads funded below 30 of 32
  crumble now and then, to rubble or back to water, never at a crossing.
  The city says when taxes are above 12% and when roads need funding.
  Micropolis' numbers here, the tax table among them, were taken with
  the owner's approval. The budget is always automatic, a liberty the
  owner chose.
- **The budget panel.** `UI/BudgetPanel.cs` shows the year as it stands
  (`CitySimulation.EstimateBudget`, at the rates set now, from the roads
  the last count saw), last year, and the tax rate and the road, fire,
  and police funding, each with a lower and a raise button: a percent a
  press for the tax, a tenth for funding. It opens by itself when a
  settled year needs attention (`BudgetYear.NeedsAttention`: upkeep came
  to more than the taxes, or the money ran out and funding was cut),
  saying why; B or a click on the funds in the bar opens it too. The
  game pauses while it is open, stops ticking at once even partway through
  a frame, takes no other input, and gets its speed back when the panel
  closes, with Continue, Escape, Enter, or B. The funds show red below
  zero.
- **The bar.** `UI/Hud.cs` draws a black strip along the bottom of the
  screen, over the news ticker, laid out after Tony Bark's mock-up in
  `docs/design/`: a pause and play
  button, the month and year (`MM/YYYY`), three speed buttons that light
  up to the speed (one Normal, two Fast, three Super fast, dimmed while
  paused at the speed play brings back), the city's name, a small R, C,
  I demand meter, the funds, and the population, with the step count,
  the frame rate, and a green question mark on the right. A speed button sets that speed and plays a
  paused game. The meter opens the demand panel (`UI/DemandPanel.cs`): a
  chart of plain lines for R, C, and I over the history `CityDemand`
  keeps (each update, two a month, ten years at most), oldest on the
  left, each against its own limit; under it the demand now, what moved
  it at the last update, the tax's push, the jobs and workers behind it,
  and the population's parts. The game keeps running while it is open,
  and its x, the meter, or Escape closes it. It draws over the rest of
  the interface and takes presses before it. Micropolis has only the meter; the
  panel is a liberty the owner chose. A press anywhere on the bar or the
  ticker never reaches the map. The question mark, or F1, opens the help
  window (`UI/HelpPanel.cs`), which lists what the keys and the mouse do;
  the game keeps running while it is open, and its x, the question mark,
  F1, or Escape closes it. The palette and the overview sit in the top
  corners.
- **The news ticker.** Under the bar, `UI/NewsTicker.cs` scrolls lines
  from right to left without stopping, after Tony Bark's mock-up and
  SimCity 3000's ticker. Each line is coloured by its kind (`NewsKind`):
  white for headlines from the city's world, yellow for advisories (the
  game's notices, such as a save, an autosave, a failure, or what opening
  a city came to, and the city's messages), and red for disasters, which
  don't exist yet. Lines wait in a `NewsQueue`, disasters and advisories
  ahead of headlines, a line already waiting isn't queued twice, and it
  holds 16. When nothing waits, `NewsDesk` picks a headline
  (`Headlines`): a population milestone the city has just passed or a
  new year, otherwise, half the time, a line about how the city is doing
  (taxes, demand, money, traffic, power, the season), resting for 12
  headlines once shown, and the rest of the time flavour, each line once
  before any comes again. The desk only reads the city and picks with its
  own generator, so the news never changes what the city does and isn't
  saved; the milestones and the year a city is opened at aren't news.
  The ticker stands still while the city is paused, a panel holding it
  included, and a city's first line starts a third of the way across.
- **The pointer's hints.** What a stroke would cost while it is dragged,
  or why it can't be built, and what the work came to once it is let go,
  are shown beside the pointer in black on yellow (`Hud.DrawPointerHint`),
  one line or two, the last for 3 seconds. The city's name is `CitySimulation.Name`, set from the
  new-city panel: 1 to 32 characters once trimmed, with no control
  characters. The play and pause button is blue, as a lit speed is.
- **The city panel.** A click on the city's name in the bar opens
  `UI/CityPanel.cs`, which pauses the city. At the top, the name, ready
  to edit: Enter renames the city and says so, and closing with its x or
  Escape drops an edit that wasn't entered. Under it, two columns: the
  city (the date and its age since 1900, the population and its parts,
  demand, the funds and tax rate, this year's cash flow as it stands and
  last year's) and the mods it was made with, in the order applied,
  with their versions (`CityDefinitions.AppliedMods`). At the bottom,
  the seed, which a click copies to the clipboard, saying so.
- **Moving windows.** The budget, demand, and city panels are windows
  the player moves by dragging their title strip. Each implements
  `UI/IMovableWindow.cs` and keeps its place with a
  `UI/WindowPlacement.cs`, and `GameLoop` drags any of them the same
  way: the window a press starts dragging follows the pointer until the
  button is let go. Each opens where its panel puts it at first (the
  budget and the city panel in the middle of the screen, the demand
  panel under the palette), keeps where it was left for the next time it
  opens, and is held on the screen above the bar, even when the window
  is resized. The help window opens in the middle, like the budget.
- **The overview map.** As in Micropolis, `UI/OverviewMap.cs` shows the
  whole city small in the top-right corner, two pixels a tile, scaled
  down for a bigger map, with the main view outlined; a click or a drag
  on it moves the view there. A row of buttons under it switches it
  between the city, power (a building lit or dark by what its centre
  last had, and the lines grey), fire and police coverage, land value,
  population density, and traffic, each map in four bands of its own colours over
  the city dimmed. It reads the simulation and never writes it, and it is
  drawn into its own texture, redrawn when a tile changes, the mode
  changes, or a count begins; without the texture it is drawn straight to
  the screen. Clicks on it never reach the map.
- **The tools.** Twelve tools sit in the palette down the left, laid out as
  Micropolis' is (`UI/ToolPaletteView.cs`): the name and price of the tool
  under the pointer, or of the held one, then centred rows of icon
  buttons in Micropolis' order, leaving out the tools CityLimits doesn't
  have yet. The held tool shows its highlighted icon, and a missing icon
  falls back to a box with a short label. The number keys pick tools too:
  inspect, road, residential, commercial, industrial, bulldozer, coal
  plant, nuclear plant, and power line, 1 to 9; the park, beside the
  bulldozer and the power line, and the fire and police stations, either
  side of inspect, are picked from the palette. A tool is used in a stroke, as
  in Micropolis: pressing on the map starts a `BuildStroke`, dragging
  stretches it (a road or a power line runs in one straight line, a plant or a station is one building
  that follows the pointer, and the other tools cover a rectangle), and the map shows a preview of the tiles it would change
  and its total cost while the button is held. Releasing applies the
  stroke all at once: the tiles that can't be built on are skipped, and
  the whole stroke is refused when it runs off the map or the city can't
  pay for all of it. Escape cancels a stroke, closes the help window or
  the demand panel, or else opens the in-game menu (`UI/GameMenu.cs`): Resume, Main menu,
  which leaves the city for the start menu, and Quit. The menu, the
  budget, and the city panel each pause the city and take every key
  and click while open.
- **The money.** The city starts with a treasury, and the prices are
  Micropolis' (`Economy/TileCosts.cs`, and the zones' in their
  `ZoneSpec`, `PlantSpec`, and `ServiceSpec`): $10 a road tile, $50 a bridge tile, $5 a power line tile,
  $25 under water, $10 a park tile, $100 a zone, $500 a fire or police station, $3,000 a
  coal power plant, $5,000 a nuclear power plant, $1 more for each tile of trees or rubble a tool
  clears first, and $1 for each tile the bulldozer clears or each zone or
  plant it knocks down. Nothing is refunded. The palette and the HUD show the
  prices, the treasury pays for a stroke as a whole, and a stroke the city
  cannot pay for is refused with a message and changes nothing.
- **The building rules.** A road runs in a straight line and is laid on
  land, clearing trees and rubble; over water it becomes a bridge, which
  needs a road beside it to start from, so a stroke can start from either
  bank. A power line follows the same rules, under water from anything
  that carries power, and crosses a straight road, or is crossed by one,
  to make a road-and-line tile. Zones are laid as a grid, the first centred where the stroke
  started; a zone goes down whole or not at all, on ground with nothing on
  it. A plant or a station is placed the same way, one at a time, centred on the
  pointer. Parks are laid as a rectangle. The bulldozer clears roads, bridges, power lines, parks, trees, and rubble tile by tile,
  but a zone, a plant, or a station only through its centre, which turns all its tiles
  to rubble. Clicking the palette picks a tool, and never builds on the map
  under it.
- **The tile readout.** The tile under the mouse is outlined, and with
  the Inspect tool held, the pointer's hint names it on two lines: the
  tile, and what it is and what can be done with it, unless the pointer
  is over the palette, the overview, the bar, or a window. A zone is
  described as a whole, from its centre: its houses or its level, its
  population, and "no power" when the scan last found it without
  (`TileInfo.Describe`).
- **Saving cities.** As `docs/saving.md` describes, a city saves to
  `Documents/CityLimits/Cities` under its own name (`CityFiles`), in the
  binary city format, `.cl` (`Saving/BinaryCityFormat.cs`): a header and
  checksummed chunks, with the map as a 16-bit word a tile. A city opened
  from the flat city format, `.fcl` (`Saving/FlatCityFormat.cs`), one
  readable JSON file, saves back to it. Both hold everything that affects
  the steps to come, the cycle's half-done
  figures included, so a city saves at any step, even paused, and loads
  back running on exactly as it would have. Each part of the simulation
  captures and restores its own section, checking it; the map is written
  as rows of characters per layer (`MapCodec`), and the definitions in the
  mods' format, read back by `DefinitionLoader`. Reading refuses a file
  over 16 MB, an unknown or missing field, or anything out of range, and
  says where. In the game, Ctrl+S saves, asking before replacing another
  city's file; Ctrl+O and Load Game list the saved cities (`UI/LoadPanel.cs`);
  and leaving a city with changes since its last save asks first
  (`UI/ConfirmPanel.cs`). A save goes through a temporary file, so a failure
  leaves the last one whole. A loaded city resumes at the last speed.
  Every 15 minutes of play (only while the city runs), `GameLoop` writes
  an autosave beside the city's own file, in its format, such as
  `Joffburg (autosave).cl` or `.fcl`, unless
  the city is as it was last saved; `Saving/Autosave.cs` decides when and
  where, and `AutosaveSlots` in the settings keeps one or two, the newer
  replacing the older. An autosave isn't the player saving, so leaving
  still asks. Micropolis' and SimCity Classic's `.cty` files in the Cities
  folder are listed too, and opening one imports it
  (`Saving/MicropolisCityImport.cs`), never writing it back: tiles by
  Micropolis' numbers, buildings whole from their centres, and what
  CityLimits doesn't have yet (rail, seaports, airports, stadiums, fire,
  flood, radiation) as bare ground, listed in the log and on the news
  ticker.
- **The settings and the game's folders.** `GameFolders` works out where
  the game keeps its files, because a packaged game, such as the
  AppImage, runs from a read-only folder. The settings go in the hidden
  per-user config folder (`~/.config/CityLimits/settings.json`, or
  `%APPDATA%\CityLimits` on Windows), and mods and, later, saved cities
  under `Documents/CityLimits` (`Mods/`, `Cities/`), which the window
  makes the mods folder of. A copy with a `settings.json` beside the
  executable, in a folder it can write to, is portable and keeps
  everything there, as development builds do. Settings an older version
  left beside a read-only executable are read once and saved to the new
  place. Without a config or Documents folder, the game falls back to
  the executable's folder and logs why. The window size and the speed
  are written when the game ends and read back when it starts; a damaged
  file is reported and the defaults are used. Each setting is read on
  its own: one that is the wrong type or out of range falls back to its
  default with a warning, and the rest are kept, so a value a newer
  version wrote can't reset them all. `AutosaveSlots`, 1 or 2,
  says how many autosaves a city keeps; a settings file without it keeps
  one.
- **The headless run.** `citylimits --headless` runs the city with no
  window at all and writes a line to the console each month, so the
  simulation can be checked on a machine with no screen. It takes `--seed`
  and `--ticks`, the number of steps, from 0 to 100 000 (768 by default, a
  year), and builds a small demo town first so there is something to
  grow: a coal plant, a road, and three zones. Each month's line includes
  the tiles powered and the capacity, and the busiest road's traffic,
  each settled year's budget gets a
  line with the traffic average, and the city's messages are logged as warnings. `--ticks` without `--headless` is refused, because
  the window runs until it is closed.
- **Scenarios.** A scenario is a city set up from a file: a seed, tool
  strokes made at set ticks, and checks on tiles, zones, funds, and
  counts (`docs/scenarios.md`, phase 1). `ScenarioLoader` reads and checks
  a file, reporting the first problem with where it is; `ScenarioRunner`
  runs it deterministically and returns a `ScenarioReport`, raising an
  event per action and check instead of logging. `citylimits scenario
  <id> --headless` runs a built-in one (`demo-town`, defined in code, and
  the files in `Assets/Scenarios/`), `--file <path>` runs any file, and
  `scenario list` lists them. `--json` writes the report to stdout and the
  log to stderr. A scenario that can't be loaded falls back to the demo
  town, logged and recorded in the report. Exit codes: 0 passed, 1 could
  not run, 2 a bad command line, 3 a check or expectation failed. A run
  needs `--headless`, because a scenario will open a window once
  scenarios can be played; `list` doesn't. Every built-in scenario runs as
  a test, so a rule that changes is caught by the scenarios that depend on
  it.
- **Scenarios are how agents test the game.** A scenario file, the
  command line, and the JSON report are the whole interface: an agent
  writes the strokes and checks it wants, runs them, and reads the report
  and the exit code, with no MCP server, no window, and no way to change a
  running game. It is read-only and deterministic, so a failing scenario
  is a reproducible bug report. Players get the same files later, as
  challenges.
- **The log.** NLog writes to the console and nowhere else. Failures go to
  the log as well as to the screen.
- **The solution.** `CityLimits.slnx` holds four projects: the game, the
  core library, the command line library, and the tests. Every .NET project in the repository lives
  in its own folder and is listed in the solution.
- **The core library.** `CityLimits.Core` targets `net10.0` with nullable
  and implicit usings enabled, and references no package at all. It holds
  the map and its generator, the clock, the calendar, the simulation and
  everything it runs (growth, trips, the census, demand, the city maps,
  the power grid, the budget), the seeded generator, the tile rules, the
  build actions, the road network and power lines, the treasury, the
  settings, saving, the scenarios, and the definitions and mods.
- **The command line library.** `CityLimits.CommandLine` targets `net10.0`
  with nullable and implicit usings enabled, and references no package
  and no project. It holds the parser, the dispatcher, the default
  command, the strict option reader, and the exit codes.
- **The tests.** `CityLimits.Tests` uses xUnit 2 with the VSTest runner
  and coverlet, and holds 673 tests over the news ticker's queue, its headlines, and the loading phrases, opening newer saves, traffic and its animation, saving, loading, autosave, importing Micropolis cities, format versions and schemas, with a city
  the owner built in both formats in `CityLimits.Tests/Assets/`, the generator, the terrain edges, the power plants, lines, and grid, the budget, the stations and their coverage, parks, hospitals and churches, the map, the
  calendar, the clock, the simulation, the settings, the tile rules, the
  build actions, the road network, the treasury, the scenario loader and
  runner, every built-in scenario, the definitions and the mod loader, zone growth, the census and demand, trips, growth and the city maps,
  the option reader, and the dispatcher.
- **The game project.** `CityLimits.csproj` targets `net10.0` with nullable
  and implicit usings enabled, and references `Raylib-cs` `8.1.0` and
  `NLog` `6.2.2`. The native `raylib` library ships inside the NuGet
  package for `linux-x64`, `win-x64`, and `win-x86`, so no system install
  is needed on Linux or Windows.
- **The assets.** `CityLimits/Assets/` holds the tile sheet, the tool
  icons and their highlighted versions, the logo, and the
  built-in scenarios. The project file copies them to the output folder,
  and the game loads the images once, after the window is open.
- **Version.** `VersionInfo` reports the version from GitInfo, which works
  it out from git: `GitInfo.txt` holds the base `major.minor.patch`, and
  GitInfo adds its own count of the commits since that file last changed
  to the patch. Don't edit `GitInfo.txt`; changing it restarts the count
  and can send the version backwards. Only the owner changes the base.
  Patch numbers have three digits and always start at 100, as the .NET
  SDK's do: a new minor version's base is `major.minor.100`, such as
  `0.2.100`, never `0.2.0`. A changelog entry is titled with the version
  GitInfo gives the commit that carries it: commit, rebuild, read `GitSemVerPatch` from
  `CityLimits/obj/Debug/net10.0/GitInfo.cache`, and correct the title if
  it differs. GitInfo counts only commits that touch the game project's
  folder, `CityLimits/`: a commit that changes only the libraries, the
  tests, or the docs keeps the version before it, so its notes join that
  version's entry instead of starting a new one. `BuildNumber` is derived from the commit hash. The changelog only records
  versions with user-facing and developer changes, so its latest entry is
  expected to lag behind the version the game reports. Developer changes
  include any public APIs, tools, debugging, and modding support.

Everything else in this file describes the rules the code must follow as it
grows. Do not describe planned behavior as if it exists.

## 3. Golden rules

### Never crash the game (outranks feature work)

Players trust CityLimits with their city, and a crash loses it. An unhandled
exception closes the window and ends the session with no explanation; nothing
restarts it. Fix a crash before building on top of it.

- **Handle errors where they happen.** A bad save file, a mouse position
  off the map, or a tool applied to an invalid tile is reported to the
  player and the game continues. Don't swallow a failure, don't substitute
  a silent default, and don't show success for a step that was skipped.
- **The frame loop is the last resort, not the plan.** `WindowedGame`
  catches everything as a backstop, reports it, and keeps running. Do not
  rely on it in place of handling an error at its source.
- **Validate everything that crosses a boundary:** saved files, mouse and
  keyboard input, tile indices, map sizes, and anything read from disk.
  Reject bad input with a message the player can act on.
- **Check sizes and offsets before casting or slicing.** Tile arrays,
  packed save buffers, and file formats all use counts that can exceed
  `int`. Check the range and the overflow before narrowing.
- **Bound every loop that depends on the outside world.** A generation pass
  must terminate, and a file read must not run away with memory.
- **Don't add `Thread.Sleep` or blocking waits** to the frame loop. The
  game must keep drawing while it waits.
- **Don't allocate in the frame loop.** Reuse buffers. A frame that
  allocates a new list per tile is a frame that will stutter when the city
  grows.

### The simulation is the product

A city builder lives or dies on its simulation, not its art. The rules:

- **The simulation is deterministic.** The same seed and the same player
  actions must produce the same city, tick for tick. Use a seeded PRNG
  owned by the simulation, never `Random.Shared` and never the system
  clock. `DateTime.Now` may label a save file; it may not steer a
  simulation.
- **Ticks are fixed size.** The world advances in discrete ticks of a fixed
  duration. Rendering runs once per frame and may draw more or fewer ticks
  than the last frame, but it never changes the tick size. Pause, Normal,
  Fast, and Super fast change how many ticks run per frame, not how big a
  tick is.
- **The simulation never touches the engine.** No Raylib call, no window
  state, no frame timing, and no input inside the simulation or world code.
  Rendering reads simulation state and never writes it. This is what makes
  the simulation testable on ordinary .NET, and it is the rule that decides
  where every new file goes.
- **Engine-free code goes behind a test.** The world and the simulation
  are plain C# in `CityLimits.Core/` that never calls Raylib, and
  `CityLimits.Tests/` proves them on ordinary .NET. Parsing, generation,
  growth, power, and budget all belong there. If you cannot test something
  without opening a window, it probably belongs in the rendering layer.
- **The map is the state.** A tile's kind, its zone, and its development
  live in the map's own tile state, and the renderer reads them from
  there. There is no second list of buildings or zones beside the map to
  keep in step, because two lists drift.
- **New state goes in the save.** A field that lasts from one step to the
  next, in any part of the simulation, is captured and restored by that
  part's `Capture` and `Restore`, with a field in `CitySave` (which both
  formats write) and a line in `docs/saving.md`, and the save format's
  minor version goes up with an upgrade step from the one before (see
  File formats below); otherwise a loaded city drifts from the one that
  was saved. `FlatCityFormatTests` runs a saved and a loaded town in step and
  compares them, which catches state left out as long as it changes what
  the city does.

### Raylib rules

- **Use the `raylib-cs` skill** in `.agents/skills/raylib-cs/` for any
  work that calls Raylib: it covers the lifecycle, the frame, the tile
  sheet, render textures, input, the examples to read, and how to check a
  visual change on a private display. These rules win where the two
  disagree.
- The pinned version is `Raylib-cs` `8.1.0`, which wraps raylib `6.0`. Check
  the API of that version before using it; the Raylib-cs API changes between
  releases, and the upstream docs track the latest.
- **The examples are the reference.** The Raylib-cs repository ships a set of
  runnable examples at
  <https://github.com/raylib-cs/raylib-cs/tree/main/Examples>, one per
  subject: `Examples/Core` for the window and the loop, `Examples/Textures`
  for images, `Examples/Shapes` for drawing, `Examples/Text` for text and
  fonts, `Examples/Audio` for sound, and `Examples/Models` for meshes. Read
  the one closest to what you are writing before you write it, and follow the
  shape it uses: a frame is `BeginDrawing`, the drawing, `EndDrawing`, and a
  texture is loaded after `InitWindow` and unloaded before `CloseWindow`.
- **Every frame is bracketed.** `GameLoop.Draw` opens with
  `Raylib.BeginDrawing()` and closes with `Raylib.EndDrawing()`. Without
  them the window stays black, because the buffer is never presented.
- **The libraries never reference Raylib-cs.** `CityLimits.Core` and
  `CityLimits.CommandLine` have no package references at all, and they must
  keep it that way. The game project may reference them; they may never
  reference the game, and the test project may never reference the game
  either.
- **Load assets once.** Create textures and fonts at startup, keep them for
  the life of the game, and unload them at shutdown. Never load a texture
  inside the frame loop. Textures need an OpenGL context, so they are loaded
  after `InitWindow`, not before it.
- **The assets live in `CityLimits/Assets/`.** The project file copies them
  to the output folder, and `Rendering/GameAssets.cs` loads them from
  `AppContext.BaseDirectory`. A missing asset is reported, and the game
  carries on: the map falls back to flat colours, and the tool keeps its
  label.
- **Draw the map from the chunk cache.** A frame draws a few cached
  textures, not every tile. Anything that is part of the map (terrain,
  roads, zones, and later buildings) is drawn by `MapRenderer.DrawTile`,
  which both the cache and the tile-by-tile fallback use, and it must
  change only through `TileMap.SetState`, so the cache hears about it.
  Things that move or follow the mouse every frame (the hover outline, a
  tool preview, sprites) are drawn over the cached map, not into it.
- **Colors and layouts live in one place.** Tile colors, the UI palette, and
  the sprite sizes belong in a theme file, not scattered through the draw
  code.
- **The map is drawn from the sheet, not with shapes.** Every sheet index
  lives in `Rendering/TileAtlas.cs` and is listed in `Assets/README.md`, so
  new art is a change to that table, not to the renderer. Which drawing a
  tile needs is decided from the map's state (for a road, by
  `RoadNetwork.ConnectionsAt`), and the renderer only looks it up. Don't
  draw a map feature with rectangles and lines when the sheet has its
  art. The flat colours in `Theme` are only for a missing sheet.
- New dependencies need the owner's approval, and they must work with the
  pinned Raylib-cs and on both Linux and Windows.

### File formats

Saves, scenarios, and mod definitions are files people, agents, and older
versions of the game write, so they are versioned, described, and read
without reflection.

- **Versions are major.minor.** A minor version only adds; a reader of 1.1
  reads 1.0 too, starting what 1.0 lacked empty, and refuses 1.2 or 2.0
  with a message (`FormatVersion.ProblemReadingWith`). Each new edition of
  the Micropolis baseline that adds state takes the next minor version, as
  Tony Bark set out. JSON writes the version as a string, `"1.1"`; a whole
  number from before, `1`, reads as `1.0`. A binary save keeps its major
  version in the header and its minor in the `VERS` chunk, and new state
  goes in new chunks, never new fields in old ones. The scenario report's
  `format` stays a whole number, since scripts read it. Saves are at 1.3:
  1.1 added traffic, 1.2 the parts list, and 1.3 the traffic each road
  shows and the year's average.
- **Older saves climb one minor version at a time,** each step a small
  function in `SaveUpgrades`, with a test from a fixture; Bridgeport's
  fixtures in `CityLimits.Tests/Assets/` stay at 1.0 for that.
- **Newer saves open when they can, as PNG does** (`SaveParts`). Each save
  lists the parts added after 1.0, each ignorable (an older game may open
  the file without it) and keepable (an older game that changed the city
  may write it back as it was), as Tony Bark settled. A part this version
  doesn't know is checked against that list: one that isn't ignorable
  refuses the file. The city carries the parts it kept (`KeptParts`) and
  writes back the keepable ones in their own format, at the newer version
  only when it kept them all. A new part is a whole new section or chunk,
  with an entry in `SaveParts.Known` saying its two flags.
- **JSON goes through generated code.** Each JSON format reads and writes
  through a System.Text.Json source-generated context, set as its
  options' `TypeInfoResolver` and used through `GetTypeInfo`, so it
  survives trimming. Reflection-based JSON is off in the game and the
  tests (`JsonSerializerIsReflectionEnabledByDefault`), so a path that
  still needs it fails at once, and `CityLimits.Core` is trim-analysed
  (`IsTrimmable`), so new reflection is a build warning. The binary
  codec's reflection is allowed because the save model's classes are
  marked `[DynamicallyAccessedMembers(SectionCodec.ModelMembers)]`; a new
  model class gets the mark too.
- **Schemas are generated, not written.** `FormatSchemas` makes a JSON
  Schema for each format from the readers' own code, and
  `docs/schemas/` holds them, one a version. When a format changes,
  `FormatSchemaTests` fails and writes the new schema beside the tests,
  to look at and copy in. Every loader accepts `"$schema"` and ignores it;
  the game writes it into `.fcl` saves. The game's checks, not the
  schemas, decide what loads.

### Definitions and mods

- **Behaviour is data where it can be.** A zone's name and price are a
  `ZoneSpec` in `CityLimits.Core/Definitions/`, not a constant or a switch
  statement, and a mod can change them. When a rule gains a number a
  player or a modder could want to change, it goes in a spec, with a field
  in the definitions format, a check in `DefinitionLoader`, a test, and a
  line in `docs/modding/tuning.md`, written for a player.
- **The base game is a mod like any other.** `BaseGame.Json` is in the
  same format and goes through the same loader, so the mod path runs every
  time the game starts. It lives in code, so it can't go missing.
- **A mod can't crash the game.** Mods are JSON only, checked before use,
  and skipped whole on any problem, which is logged and shown. A city keeps
  the definitions it was made with, so the same mods, seed, and work give
  the same city.
- **Scenarios run on the base game.** `ScenarioRunner` never reads
  `Mods/`, so a scenario's result doesn't depend on the machine.

### The log and the headless run

- **NLog writes to the console and nowhere else.** `Game/GameLog.cs` sets
  the rule up once, at startup, and `LogManager.Shutdown()` flushes it at
  the end. There is no file target, no database target, and no mail target.
  A command whose output stream carries a report for a script, such as
  `scenario --json`, calls `GameLog.UseStandardError` to move the log to
  stderr. It is still the console.
- **Log what the player cannot see.** A failure goes to the log and to the
  screen, so the log is the record of what went wrong after the window is
  gone.
- **The headless run is the way to check the simulation on a machine with no
  screen.** `dotnet run --project CityLimits -- --headless --ticks 768`
  opens no window, runs the city for a year of steps, and writes each
  month to the console. It must not touch Raylib, or it is not headless.
- **`--headless` is a global flag, not a command.** It means the same
  thing for every command: run with no window. A command declares
  `SupportsHeadless`, and the dispatcher refuses the flag for a command
  that doesn't. A command that does support it must never open a window
  when it is set. Don't add a `headless` command or another per-command
  way to skip the window.
- **The options are parsed, not guessed.** Every command reads its
  arguments with `CommandOptions.TryParse` against an `OptionSpec`, and its
  numbers with `TryGetInt` and a range. A bad command line is written to
  the command's `Error` writer with a pointer to `--help`, and returns
  `ExitCodes.Usage`; it is never ignored and never replaced by a default.
- **The game is the default, not a subcommand.** Running CityLimits with
  no command plays it. Don't add a `play` command or another name for the
  default.
- **Don't report success for work a command did not do.** A command that
  is not implemented yet says so and returns `ExitCodes.Failure`.

### Writing docs

- **Write for an outsider looking in.** The docs in `docs/` are for a
  contributor who has never seen CityLimits: say what a thing is before
  how it works, name the file or type that does it, and don't lean on
  this conversation's history or on private notes.
- **The modding docs are for players first.** `docs/modding/` is read by
  new and existing players as well as developers: explain the terms a
  player meets (what a tile, a zone, or a capacity is), give a working
  example, and say what the game does when something is wrong. Its
  developer sections follow the contributor rule above.
- **Describe only what works today.** A doc may say a thing doesn't exist
  yet; it doesn't promise what will.

### Code style

- Follow the `dotnet-guidelines` skill for naming, comments, control flow,
  interfaces, API shape, error handling, and tests. Where it and this file
  disagree, this file wins for CityLimits.
- File-scoped namespaces; `var` for locals; pattern matching and switch
  expressions; expression-bodied members on one line; `PascalCase` types,
  members, and constants; `camelCase` locals and private fields; `I` and
  `T` prefixes for interfaces and type parameters.
- Put a namespace's shared usings in `GlobalUsings.cs` rather than at the
  top of each file, and don't add duplicates there.
- Give public and internal APIs `///` doc comments. Comments explain why,
  not what.
- Keep `Program.cs` thin: it sets up the log, registers the commands with
  the dispatcher, and shuts the log down. The window and the frame loop
  belong in `Game/WindowedGame.cs`, and everything else belongs in `CityLimits.Core/Randomness/`,
  `CityLimits.Core/World/`, `CityLimits.Core/Simulation/`,
  `CityLimits.Core/Settings/`, `CityLimits.CommandLine/`, `CityLimits/Game/`,
  `CityLimits/Rendering/`, or `CityLimits/UI/`.
- Match the surrounding code when it disagrees with this file, and mention
  the disagreement.

## 4. Legal, and Micropolis

Micropolis, the open source version of SimCity, and the reimplementations
of it are under the GNU GPL, and their art, sounds, and city files belong to
their authors. The [SimHacker repository](https://github.com/SimHacker/micropolis)
is the canonical and official Micropolis repository, maintained by
[Don Hopkins](https://en.wikipedia.org/wiki/Don_Hopkins). CityLimits is
under the GPL version 3, and the art it ships is Micropolis' art, which is
under the same licence: `Assets/tiles.png` and the tool icons come from the
Micropolis core's images, and `Assets/README.md` records where they came
from.

This section has two parts: what the law and the licences require, and
how the owner chooses to use Micropolis. Only the first is a legal
matter. It is the project's guidance, not legal advice; when something is
unclear, say so and ask the owner.

### What the law and the licences require

- **Copyright protects expression, not ideas.** Code, art, sound, and
  written text are protected. Game mechanics, how a game is played, and
  names, titles, short phrases, and slogans are not, however well known.
  So in-jokes and nods, such as SimCity 2000's "Reticulating splines",
  and saying what inspired CityLimits are fine.
- **The GPL is copyleft.** CityLimits is under the GPL version 3, so it may
  use Micropolis' code, art, and numbers, as long as CityLimits stays
  under the GPL. Code taken from Micropolis keeps its copyright notice and
  says where it came from and that it was changed.
- **CityLimits is not a SimCity product.** Micropolis' GPL release comes
  with Electronic Arts' additional terms: no use of the SimCity trademark,
  no claim of any link to Electronic Arts, and modified versions marked as
  modified. "Micropolis" is itself claimed as a trademark by another
  owner. Neither name, nor any other game's logo or branding, may name
  CityLimits or anything in it, or suggest that its owners made or
  endorse it.
- **New art has to be the owner's own work, or come with a licence that is
  compatible with the GPL version 3.** Credit it in `Assets/README.md`.

### Micropolis is the reference, and CityLimits goes its own way

The owner likes SimCity and wants CityLimits to start from what made it
work, but to take it in their own direction. That, not the law, is why
Micropolis is the reference rather than the source: code written fresh,
in CityLimits' own structure and idiom, is the owner's to change, and a
liberty taken later changes CityLimits' own design instead of unpicking
an inherited one.

**The baseline comes first.** CityLimits will take creative liberties
later, but the first goal is an engine that plays like Micropolis. Until
it does, match the Java port's behaviour when a rule is in question,
rather than inventing a better one, and leave deviations to the owner.
The refactor that brought the early engine to the baseline is done; new
systems are built to the baseline from the start, and the liberties the
owner has chosen so far (the always-automatic budget) are recorded where
they apply.

**Write our own; copy when it serves.** Read Micropolis to learn what a
rule does, write the behaviour down in CityLimits' own terms, implement
it in C# in this codebase's idiom, and prove it with a test. That is the
default. Copying or porting a piece of Micropolis' code is allowed when
it serves CityLimits better than writing it fresh, such as an intricate
algorithm where a faithful copy is the point; keep its notice, say where
it came from, and call it out in the summary and the commit so the owner
can review it.

**Numbers are generally welcome; their fit is the owner's call.**
Micropolis' numbers (prices, thresholds, factors, and tables such as the
tax table) may be used, but whether each one fits the owner's direction
is the owner's decision, so ask before relying on one. Mark each with a
comment where it is used, saying where it came from.

**Which tree to read.** Read the ports that ship the whole game, because
they show the behaviour and not only the engine behind it:
[`micropolis-java`](https://github.com/SimHacker/micropolis/tree/master/micropolis-java)
is the main reference (MicropolisJ, the complete game in Java, with its
GUI and its gameplay; it is forked and maintained at
[dheid/micropolis](https://github.com/dheid/micropolis)), and
[`micropolis-activity`](https://github.com/SimHacker/micropolis/tree/master/micropolis-activity),
the same game packaged as an OLPC activity, with the classic Tk GUI, the
editor, and the original C simulation in `src/sim/`, is the second
reference for the same questions. The core,
[`MicropolisCore`](https://github.com/SimHacker/MicropolisCore), is the
engine with no GUI: read it only to cross-reference a detail the GUI
does not show, such as a constant, a formula, or a data table.

**What it settles.** When a rule is in question (how a zoned tile
develops, how demand and power will behave, what a road network has to
serve, how a city grows over time), Micropolis is the behaviour
CityLimits aims to match. Watch it in the Java port to see the rule
work, and read the core only to pin the numbers down. Its scenarios are
the names the roadmap's milestones borrow.

**What is borrowed so far.** The tile sheet and the tool icons, with their
highlighted versions, from the Micropolis core's images, credited in
`Assets/README.md`; and, with the owner's approval, Micropolis' numbers
for its rules, such as the tax table and the Easy difficulty factors in
`CityDemand` and `CityBudget`, each marked where it is used; and its tile
numbers, to read its city files (`MicropolisCityImport`), which the tile
sheet's drawings use too. Code copied from Micropolis, if any is, carries
its notice where it is.

## 5. Before you finish a task

The build, the tests, and the game itself are the gate. A change to the
simulation is proven by a test and a `--headless` run; a change to the
rendering is proven by running the game and looking at it. Whether the
change works in play is the owner's call, not yours.

```sh
dotnet build CityLimits.slnx                                  # must build with no new warnings
dotnet test  CityLimits.Tests/CityLimits.Tests.csproj         # all tests must pass
dotnet run   --project CityLimits -- --headless --ticks 768   # run the city for a year with no window and read the log
dotnet run   --project CityLimits                             # open the window and look at it
```

- **The command line is the way in.** With no command, CityLimits opens
  the window at its start menu, and `--headless` runs a city with no
  window.
  `scenario` runs a scenario (`docs/scenarios.md`). `--help` lists the
  options and commands, and `<command> --help` describes one. Use these to
  run the game; don't wrap it in another launcher, and don't reach for an
  MCP server or another way into a running game.
- **Check the simulation with `--headless`.** It needs no display, so it
  works on any machine. Pass `--seed` and `--ticks` to reproduce a city,
  and read the monthly and yearly lines in the log for the change you
  made. A run that exits with anything other than 0 is a failure.
- **Check a rule with a scenario.** To see a rule work, write a scenario
  that builds what it needs and checks what should follow, and run it:
  `dotnet run --project CityLimits -- scenario --file <path> --headless --json`.
  Exit code 0 means every check passed, and 3 that one failed; the JSON
  report says what was expected and what was found. Run the built-in ones
  too (`scenario list`); a rule change that moves their figures is a
  change to call out. When a scenario shows something worth keeping,
  make it a built-in one in `Assets/Scenarios/`, where it runs as a test.
- **A visual change needs a real window.** `--headless` never draws, so it
  cannot confirm what the player sees. If the machine has no display,
  say that the game was not looked at, rather than reporting it as
  checked.
- **Playing the game is the owner's call.** Your check ends at showing
  that the change runs in both modes: the window opens, draws, and closes
  cleanly, and the `--headless` run finishes with exit code 0 and the log
  you expect. Anything that needs the game actually played to judge (how
  a tool feels, whether the city grows sensibly, whether a balance number
  is right, whether a screen reads well) is the owner's judgment. Don't
  report it as verified, and don't change the game to suit your own sense
  of it. Say what you checked, and list what is left for the owner to
  play-test.
- A black window means a frame was never presented. Check that the draw is
  bracketed by `BeginDrawing` and `EndDrawing` before looking anywhere else.
- A new subcommand is a class in `CityLimits/Game/Commands/` that
  implements `ISubcommand`, registered with the dispatcher in
  `Program.cs`. It says whether it supports `--headless`, it reads its
  options with `CommandOptions`, and its tests
  go beside `CommandOptionsTests` and `SubcommandDispatcherTests` when it
  adds anything to the library.
- If you changed the world or the simulation, add or update its tests in
  the same change, and check that a new test fails without your fix.
- If you changed what the player sees, run the game and confirm the
  change appears on screen. A build is not a visual check, and seeing the
  change drawn is not the same as knowing it plays well.
- Bring [docs/scorecard.md](./docs/scorecard.md) up to date: flip the rows
  you moved, with their new evidence, and update the summary counts.
- Bring [docs/ROADMAP.md](./docs/ROADMAP.md) up to date: tick checklist
  items, update the milestone's done percentage and estimate, bump the
  status line, and add a revision-history row. Pick work from its current
  focus and ranked list, and leave scope decisions to the owner.
- If the layout or the rules change, update the workspace map and the
  golden rules in this file.

Describe only what works today, and keep the tree building at every step.

## 6. Calling things out

The owner often reviews changes alone, so make review easy:

- State breaking changes, dependency bumps, and changes to the simulation's
  rules clearly in the summary or commit message. A change to growth,
  demand, power, or the economy changes every saved city.
- List judgment calls and anything you couldn't verify, such as "builds,
  but the game was not run".
- List what needs play-testing, such as "the window opens and the new
  tool draws, but it hasn't been played". That's the owner's call to make.
- If you notice a risk outside your task, report it. Don't fix it quietly
  in an unrelated change.

## Commit attribution

If Kilo authored the changes, append `Co-authored-by: Kilo AI <kilo@local>` to
the commit message, in the same area as other `Co-authored-by` trailers. Do
not invent or impose attribution when Kilo did not author the changes.
