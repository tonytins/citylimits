# Scenarios

> **Status:** format 2.1 is built: `CityLimits.Core/Scenarios/` and
> `citylimits scenario`, with `demo-town`, the built-in scenarios
> `bridges`, `rail`, `zones-and-bulldozer`, and `small-town`, and the
> example challenge `first-town`, each run as a test, all written in 2.0,
> which 2.1 reads, since none needs what 2.1 added.
> Files in format 1, which counted time in ticks, are still read. A
> challenge can be [played in the game](#playing-a-scenario). It follows the Micropolis
> baseline: tools are strokes, zones are 3 by 3, the bulldozer refunds
> nothing, and a tick is one phase of the simulation's 16-phase cycle.

A scenario is a city set up from a file: a seed and a script of tool
strokes, or a saved city, disasters that strike on set dates, what should
be true along the way, and goals to meet by an end date. It serves two
audiences with one format:

- **Players** get a challenge: a starting city, maybe a disaster along the
  way, and goals to reach by a date. A scenario with goals is a challenge.
- **Developers and agents** get a reproducible run. Because the simulation
  is deterministic, the same file gives the same city, tick for tick, on
  any machine. A bug that a scenario shows can be handed to someone else
  as a file, checked from the command line with no window, and kept as a
  test. Checks are for them: a scenario with only checks is a test.

A scenario run is read-only. It never writes `settings.json`, a save, or
any other file; it reads only the saved city a scenario starts from. It
reports through the log and, on request, a JSON report on stdout. It
doesn't need a debugging protocol or a live connection to the game.

## Playing a scenario

The start menu's Scenarios opens a list of the challenges a player can
pick (`UI/ScenarioPanel.cs`, `ChallengeShelf`): the scenarios with goals
in the game's own folder, then those in the player's own,
`Documents/CityLimits/Scenarios`, which the game makes. A scenario with
only checks is a test, for agents, and isn't listed. A file that can't be
read is listed with why, and can't be played. Picking one shows its
briefing, its description and its goals; Play, or Enter, starts it.

A played scenario is a city like any other, with its challenge
(`Challenge`, kept by `CitySimulation.Challenge`): a city made from a
seed runs on the base game's definitions, whatever mods are installed,
and takes the scenario's name. Its actions are made on their dates, as
a scripted town grows, and its events strike on theirs; its checks are
left out of play. Disasters on their own follow the player's settings,
as in any city; `disasters` decides them only in a run. The city panel
shows the challenge, its end date, and each goal with what the city
measures now. At the end date the goals are judged, and a pop-up says
whether the challenge was won or lost, and which goals weren't met; the
city plays on after, won or lost. Saving partway keeps the challenge,
with what is still to come (`docs/saving.md`).

## Editing a scenario

A city builder is a map editor turned into a game, so a scenario is
edited in the game itself, in the same view it is played in. The
Scenarios list's Edit opens the scenario's city (`ScenarioEdit`), paused,
with the bar saying it is being edited:

- Building is free, paused or playing; the city's money is left as the
  scenario has it.
- The scenario's goals, actions, and events are kept aside as a draft
  that nothing acts on; the city panel shows it, read-only for now.
- Playing tests the scenario. The city as it stands is kept as a
  checkpoint, and the scenario runs on it as it would for a player: its
  actions and events come on their dates, and its goals are judged at
  its end date.
- The Escape menu's Back to before play puts the city back as it was at
  the checkpoint, undoing the test and everything built during it. Start
  over opens the scenario's city afresh, dropping every change.

A test from the same checkpoint runs the same every time, as the
simulation is deterministic. A scenario can't be saved from the game
yet, and no other city can be opened while one is edited.

## Running a scenario

```sh
citylimits scenario list                                # the built-in scenarios, by id; challenges say so
citylimits scenario demo-town --headless                # run a built-in scenario by id
citylimits scenario --file my-bug.json --headless       # run a scenario from a file
citylimits scenario demo-town --headless --ticks 768    # run this many ticks instead of to the end date
citylimits scenario demo-town --headless --json         # the report as JSON on stdout
```

Scenarios run only with the global `--headless` flag, and a run without
it is refused, so a script written today keeps its meaning once a
scenario can open in a window. `scenario list` opens no window, so it
doesn't need the flag.

Built-in scenarios are the files in `CityLimits/Assets/Scenarios/`, copied
to the output folder with the other assets, plus `demo-town`, which is
defined in code, where it can't go missing, as the town this document's
examples describe. A file scenario's id is its file name without `.json`.
A scenario that can't be read says why and runs nothing, with exit code
1; nothing ever runs in its place. An id may contain only lowercase letters, digits,
and hyphens, so an id can never name a path outside that folder.

### Exit codes

| Code | Meaning |
|---|---|
| 0 | The scenario ran and every check passed |
| 1 | The scenario could not run: the file is missing, too large, or invalid, or its saved city can't be opened. This includes a run that fell back to `demo-town` |
| 2 | The command line was wrong (`ExitCodes.Usage`) |
| 3 | The scenario ran, and at least one check failed |

Code 3 is `ExitCodes.ChecksFailed`, added for scenarios. A caller
can then tell a broken scenario (1) from a city that didn't do what was
expected (3) without reading the output. Goals don't change the exit
code: a challenge that was lost is still a run that worked, and the
report's `won` says how it went.

### Output

Without `--json`, the run writes to the log as `citylimits --headless` does: the setup,
each action, disaster, and check with its date and tick, each goal at the
end, and a summary line.

With `--json`, stdout carries the report and nothing else, so it can be
piped into a parser. For that run, the log moves to stderr:
`GameLog.UseStandardError` points the console target at the error stream.
NLog still writes to the console and nowhere else.

## Time

A file names its times as the game's dates: a month, written `YYYY-MM`,
and the week of it, 1 to 4, the first when none is given. The calendar
moves on a week, a 48th of a year, every 16 ticks, so a month is 64 ticks
and a year 768, and January 1900 is where it starts unless a scenario
starts later. A date is the first step of its week: `"1902-03"` with
`"week": 2` is 16 ticks into March 1902. A zone gets its chance to grow
when the scan reaches its band, once a week.

Underneath, the runner counts ticks from the scenario's start, and the
report gives each time as both a date and a tick. A tick is one step of
the simulation, one phase of Micropolis' cycle, the same unit as
`citylimits --headless --ticks`; `CityCalendar` holds these numbers.

## The file, version 2.1

2.1 adds the `Stadium`, `Seaport`, and `Airport` tools and counts to 2.0;
a 2.0 file reads as it did.

```json
{
  "$schema": "https://raw.githubusercontent.com/tonytins/citylimits/main/docs/schemas/scenario.v2.0.schema.json",
  "format": "2.0",
  "name": "Demo town",
  "description": "A road with a residential, a commercial, and an industrial zone, powered by a coal plant up a line to the north; the industry is knocked down after a year.",
  "seed": 2000,
  "funds": 20000,
  "ends": "1902-01",
  "actions": [
    { "at": "1900-01", "tool": "Road", "from": { "x": 34, "y": 53 }, "to": { "x": 42, "y": 53 }, "expect": "Built" },
    { "at": "1900-01", "tool": "ZoneResidential", "from": { "x": 35, "y": 51 }, "expect": "Built" },
    { "at": "1900-01", "tool": "ZoneCommercial", "from": { "x": 38, "y": 51 }, "expect": "Built" },
    { "at": "1900-01", "tool": "ZoneIndustrial", "from": { "x": 41, "y": 51 }, "expect": "Built" },
    { "at": "1900-01", "tool": "CoalPowerPlant", "from": { "x": 35, "y": 40 }, "expect": "Built" },
    { "at": "1900-01", "tool": "PowerLine", "from": { "x": 35, "y": 43 }, "to": { "x": 35, "y": 49 }, "expect": "Built" },
    { "at": "1901-01", "tool": "Bulldoze", "from": { "x": 41, "y": 51 }, "expect": "Built" }
  ],
  "checks": [
    { "at": "1900-01", "count": "Road", "equals": 9 },
    { "at": "1902-01", "tile": { "x": 35, "y": 51 }, "use": "Residential", "development": 0 },
    { "at": "1902-01", "tile": { "x": 38, "y": 51 }, "use": "Commercial", "development": 0 },
    { "at": "1902-01", "count": "Developed", "equals": 1 },
    { "at": "1902-01", "tile": { "x": 38, "y": 50 }, "served": true, "powered": true },
    { "at": "1902-01", "tile": { "x": 42, "y": 52 }, "kind": "Rubble", "use": "None" },
    { "at": "1902-01", "fundsEquals": 16572 },
    { "at": "1902-01", "count": "Industrial", "equals": 0 }
  ]
}

```

A challenge has goals, and may start later and have a disaster:

```json
{
  "$schema": "https://raw.githubusercontent.com/tonytins/citylimits/main/docs/schemas/scenario.v2.0.schema.json",
  "format": "2.0",
  "name": "Quake country",
  "description": "A town on a fault line. Grow it to 2,000 people by 1916, earthquake or not.",
  "seed": 77,
  "start": 1906,
  "ends": "1916-01",
  "events": [ { "at": "1908-04", "disaster": "Earthquake" } ],
  "goals": [
    { "measure": "Population", "atLeast": 2000 },
    { "measure": "Funds", "atLeast": 0, "text": "Stay out of debt" }
  ]
}
```

### Fields

| Field | Required | Meaning |
|---|---|---|
| `$schema` | no | The scenario's schema, `docs/schemas/scenario.v2.0.schema.json`, for editors to check the file against; the game ignores it |
| `format` | yes | The file format version, `"2.1"` or `"2.0"`. A file of format 1, such as `"1.3"`, is read as [format 1](#format-1). A newer minor version, or another major one, is refused with a message saying which this version reads |
| `name` | yes | A display name, 1 to 80 characters |
| `description` | no | One or two sentences, up to 500 characters |
| `seed` | yes, without `city` | The seed the map and the simulation are built from: any 32-bit integer |
| `map` | no | `width` and `height`, each from 16 to 512. Default: the game's map, `MapSize.DefaultWidth` by `MapSize.DefaultHeight` |
| `funds` | no | The starting treasury, from 0 to 1 000 000. Default: `Treasury.StartingFunds` |
| `start` | no | The year the city's calendar starts in, January of it, from 1900 to 9999. Default: 1900 |
| `city` | no | A saved city to start from instead of a seed: see [Starting from a saved city](#starting-from-a-saved-city) |
| `ends` | yes | The month the scenario ends at, its first step, when the goals are judged; no earlier than the start, and at most 100 000 ticks, about 130 years, after it. It may be the start itself, for a scenario that only looks at its first moment |
| `disasters` | no | `true` lets disasters happen on their own, as they do in the game with the player's settings on: fires, floods, and earthquakes. Default: `false`, so a scenario's result never depends on the machine's settings. A fire or flood already under way goes on either way, and `events` strike either way |
| `actions` | no | Tool strokes made on the map; see below. Up to 10 000 entries |
| `events` | no | Disasters that strike on set dates; see below. Up to 10 000 entries |
| `checks` | no | What must be true along the way; see below. Up to 10 000 entries |
| `goals` | no | What a player has to meet by the end; see below. Up to 10 000 entries. A scenario with any is a challenge |

### Starting from a saved city

`"city": "riverton.fcl"` starts the scenario from a city saved as a
`.fcl` or a `.cl` file beside the scenario file, or in a folder beside
it; a path that leaves the scenario's folder is refused. The city brings
its own seed, map, money, definitions, and date, so `seed`, `map`,
`funds`, and `start` are refused beside it, and every date counts from
the city's own. The city is opened when the scenario is read, so a city
that can't be opened is refused then, with the reason, and again afresh
each time the scenario runs. Only a scenario read from a file can start
from a city; one given as text alone, as `demo-town` is, can't.

### Actions

An action is one tool stroke, made exactly as a player makes it: the
tool pressed on `from` and dragged to `to`.

| Field | Required | Meaning |
|---|---|---|
| `at`, `week` | yes, `week` no | The date the stroke is made at, before the city's step then |
| `tool` | yes | A `BuildAction` name: `Road`, `ZoneResidential`, `ZoneCommercial`, `ZoneIndustrial`, `Bulldoze`, `CoalPowerPlant`, `NuclearPowerPlant`, `PowerLine`, `FireStation`, `PoliceStation`, `Park`, `Rail`, and from 2.1 `Stadium`, `Seaport`, `Airport` |
| `from` | yes | `{ "x", "y" }`: the tile the stroke starts on |
| `to` | no | `{ "x", "y" }`: the tile the stroke is dragged to. Default: `from`, a stroke that doesn't move |
| `expect` | no | A `BuildStatus` (`Built`, `Unchanged`, `Refused`), or a status and a `BuildRefusal`, as in `Refused:NotEnoughFunds`, `Refused:PartOfAZone`, `Unchanged:AlreadyDone`, `Refused:OnFire`, for a tile that is burning, or `Refused:Flooded`, for one under water |
A stroke covers what `BuildStroke.Bounds` says it covers, as in the
game:

- **A road** runs in one straight line from `from`, across or down,
  whichever way `to` is further; across wins a tie. Over water it is a
  bridge, which needs a road beside it to start from.
- **Zones** are laid as a grid of 3 by 3 zones, the first centred on
  `from`, adding a zone for each whole 3 tiles `to` lies away. A single
  zone is a stroke with no `to`, centred on `from`.
- **A power line** runs like a road. Under water it needs something
  beside it that carries power to start from, and over a straight road
  it makes a road-and-line tile.
- **Parks** cover the rectangle from `from` to `to`, a tile each, on clear
  ground or rubble.
- **A fire or police station** is one 3 by 3 building, placed as a power
  plant is, below.
- **A power plant** is one 4 by 4 building, centred on `to` (or on `from`
  when there is no `to`), one tile in from its top-left corner. As in the
  game, dragging moves it rather than adding more.
- **The bulldozer** covers the rectangle from `from` to `to`. It knocks a
  zone or a plant down only when its centre is inside that rectangle.

Every action goes through `CityBuilder.Apply`, so it is priced, refused,
and skipped exactly as the tools are: $10 a road tile, $50 a bridge
tile, $20 a rail tile, $100 a tunnel, $5 a power line tile, $25 under water, $10 a park tile, $100 a zone, $500 a fire or police station, $3,000 a coal power plant, $5,000 a nuclear power
plant, $1 more for each tile of trees or rubble cleared, $1 for each tile
bulldozed or zone or plant knocked down, and nothing refunded. A
stroke the city can't pay for, or that runs off the map, changes
nothing; within a stroke, a tile or zone that can't be built on is
skipped and the rest is built. Actions on the same date run in file
order.

The money also changes once a year, as in Micropolis: the city collects
its taxes and pays its upkeep at phase 9 of each count that comes round
to a whole year, the first time nine ticks into the scenario and then
each January, at the default 7% tax and full funding. A `fundsEquals` check after a
year's turn includes it.

A refusal is not an error, because a scenario may set out to show one. It
is recorded in the report, and it fails the run only if `expect` says
otherwise. `expect` describes the stroke's result as a whole, the same
`BuildResult` the game turns into its message.

### Events

An event is a disaster that strikes on a date, at a random place, as
Micropolis' scenarios strike theirs, whatever `disasters` says. Each is
reported as one on its own is, and the report says whether it found
anywhere to strike.

| Field | Required | Meaning |
|---|---|---|
| `at`, `week` | yes, `week` no | The date it strikes, after the actions then and before the checks |
| `disaster` | yes | `Fire`, a fire on something that can burn, as Micropolis' Disasters menu starts one; `Flood`, the water rising from a shore onto the land beside it, which a map with no shore can't have; or `Earthquake`, which shakes the whole city (`CitySimulation.Strike`) |

### Goals

A goal is what a player has to meet by the end. Every goal is judged on
the scenario's last step, at `ends`; the challenge is won when all of
them are met, and lost otherwise. Goals are for players, and don't fail
a run: the report's `won` says how it went.

| Field | Required | Meaning |
|---|---|---|
| `measure` | yes | What it measures: `Population`, as the bar shows it; `Funds`; `Pollution`, the city's average over the blocks that have any, 0 to 255; `Crime`, its average, 0 to 250; or `Traffic`, the year's traffic average, 0 to 576 |
| `atLeast`, `atMost` | one or both | The least and the most the measure may be. Funds may be below zero |
| `text` | no | How the goal reads to a player, up to 120 characters. Default: worded from the measure, such as "Have a population of at least 2,000" or "Keep average crime at most 60" |

### Checks

A check is made at its `at` and `week`, after the actions and the events
then. Each check names one subject and one or more conditions. A check
outside the scenario's dates is refused when the file is loaded.

| Subject | Conditions |
|---|---|
| `tile`: `{ "x", "y" }` | `kind` (a `TileKind`), `use` (a `TileUse`), `development`, `developmentAtLeast`, `developmentAtMost`, `served` (true or false), `powered` (true or false) |
| `funds`, no value | `fundsAtLeast`, `fundsAtMost`, `fundsEquals` |
| `count`: a subject below | `equals`, `atLeast`, `atMost` |

A tile check reads the tile as the game's readout does. On any tile of a
zone, `development` is the zone's building level, which its centre holds:
0 while it has no building (a residential zone may have houses), then 1
up to its spec's levels, 4 for residential, 5 for commercial, and 4 for
industrial (`TileMap.ZoneStateAt`); a file may give it from 0 to 5.
`served` is whether a road or rail lies along the zone's edge (`RoadNetwork.IsZoneServed`). `served` on a tile that isn't
part of a zone fails, with the reason that the tile has no zone.
`powered` is whether the tile's zone or plant had power when the scan
last reached its centre (`TileState.Powered`); a zone is first given
power on the scan after the first trace, in phase 11, so a check on it
belongs in the month's third week or later, 32 ticks in. On a tile that isn't part of a zone or a
plant it fails, with the reason that the tile has no zone or plant.

| Count | What it counts |
|---|---|
| `Road` | Road tiles, bridges included |
| `Bridge` | Road tiles over water |
| `Residential`, `Commercial`, `Industrial` | Zones of that kind, by their centres, not tiles |
| `Developed` | Zones with any population, single houses included, as the `--headless` summary does |
| `CoalPowerPlant`, `NuclearPowerPlant` | Power plants of that kind, by their centres, not tiles |
| `PowerLine` | Tiles with a power line, under water or under a road included |
| `FireStation`, `PoliceStation` | Stations of that kind, by their centres, not tiles |
| `Park` | Park tiles |
| `Hospital`, `Church` | Hospitals and churches, by their centres; a residential zone becomes one when the city needs it |
| `Rail` | Rail tiles, tunnels and crossings included |
| `Stadium`, `Seaport`, `Airport` | Stadiums, seaports, and airports, by their centres, not tiles; from 2.1 |

### Validation

The file crosses a boundary, so it is checked before anything runs, and the
first problem is reported with its location, as in
`actions[3].from.x: needs a number from -512 to 1023, not 5000.`.

- A file over 1 MiB is refused before it is parsed.
- An unknown field is refused, so a misspelt check can't silently pass.
  (`System.Text.Json`'s `UnmappedMemberHandling.Disallow`.)
- Enum names are matched exactly, with the casing of the C# names.
- Every number is range-checked as the tables above say. A stroke's
  coordinates run from -512 to 1023, so a stroke can be made to run off
  the map on purpose; the builder refuses it with `OffTheMap`, which is
  part of what a scenario can test.
- A tile check outside the map is refused at load time, as is a date
  outside the scenario's, a week outside 1 to 4, and a goal that can
  never be met.

`System.Text.Json` ships with .NET, so this needs no new package, and
`CityLimits.Core` keeps its rule of no package references.

## Format 1

Files of format 1, `"1.0"` to `"1.3"`, are still read, so scripts and
agents' files written before 2.0 keep working. They count time in ticks
from January 1900, where 2.0 names dates: `"ticks"` in place of `ends`,
and `"tick"` in place of `at` and `week` on actions and checks. They have
no `start`, `city`, `events`, or `goals`, and a field of one format is
refused in the other. 1.1 added the `Rail` tool and count, 1.2
`disasters` and `Refused:OnFire`, and 1.3 `Refused:Flooded`, with floods
and earthquakes among the disasters. Their schemas are
`docs/schemas/scenario.v1.0.schema.json` to `scenario.v1.3.schema.json`.

## The schema

`docs/schemas/scenario.v2.1.schema.json` describes the file (`scenario.v2.0.schema.json` 2.0, which differs only in its name), and the files for 1.0 to 1.3 format 1: its fields
and their types, with no others. Name it in `"$schema"`, as the built-in
scenarios do, and an editor such as VS Code checks the file and suggests
its fields as you write. The schema is generated from the loader's own
code and checked by a test, so it stays current; the loader's checks go
further, to ranges and the rules between fields, and decide what runs.

## The report, version 1

The report is a stable interface: agents, scripts, and later a server may
read it. Adding a field is allowed within a version. Renaming, removing,
or changing the meaning of a field means bumping `format`. Unlike the
files people write, the report's `format` stays a whole number, so
scripts already reading it keep working.

```json
{
  "format": 1,
  "scenario": { "name": "Demo town", "source": "demo-town", "seed": 2000, "ticks": 1536, "start": "1900-01", "ends": "1902-01" },
  "passed": true,
  "final": {
    "tick": 1536,
    "cityTime": 96,
    "date": "1902-01",
    "funds": 16572,
    "counts": { "road": 9, "bridge": 0, "residential": 1, "commercial": 1, "industrial": 0, "developed": 1, "coalPowerPlant": 1, "nuclearPowerPlant": 0, "powerLine": 7, "fireStation": 0, "policeStation": 0, "park": 0, "hospital": 0, "church": 0, "rail": 0 }
  },
  "actions": [
    { "index": 0, "tick": 0, "tool": "Road", "status": "Built", "tiles": 9, "skipped": {}, "cost": 90, "met": true, "date": "1900-01" },
    { "index": 1, "tick": 0, "tool": "ZoneResidential", "status": "Built", "tiles": 9, "skipped": {}, "cost": 100, "met": true, "date": "1900-01" },
    { "index": 4, "tick": 0, "tool": "CoalPowerPlant", "status": "Built", "tiles": 16, "skipped": {}, "cost": 3000, "met": true, "date": "1900-01" },
    { "index": 5, "tick": 0, "tool": "PowerLine", "status": "Built", "tiles": 7, "skipped": {}, "cost": 35, "met": true, "date": "1900-01" },
    { "index": 6, "tick": 768, "tool": "Bulldoze", "status": "Built", "tiles": 9, "skipped": {}, "cost": 1, "met": true, "date": "1901-01" }
  ],
  "events": [],
  "checks": [
    { "index": 0, "tick": 0, "passed": true, "date": "1900-01" },
    { "index": 3, "tick": 1536, "passed": true, "date": "1902-01" }
  ],
  "goals": []
}
```

A failed check carries what was expected and what was found, and sets the
top-level `passed` to false:

```json
{ "index": 3, "tick": 1536, "passed": false, "expected": "funds equal 19609", "actual": "19599", "date": "1902-01" }
```

A challenge's report also says whether it was won, how each goal stood,
and how each event went:

```json
"won": false,
"events": [ { "index": 0, "tick": 1728, "date": "1908-04", "disaster": "Earthquake", "happened": true } ],
"goals": [
  { "index": 0, "text": "Have a population of at least 2,000", "met": false, "found": 1460 },
  { "index": 1, "text": "Stay out of debt", "met": true, "found": 3120 }
]
```

`source` is the built-in id, or the file path as given on the command
line. For an action, `status` and, when
it wasn't built, `reason` are its `BuildResult`; `tiles` is how many
tiles it changed; `skipped` maps each `BuildRefusal` to how many tiles it
skipped for that reason, or how many zones for a zone stroke, as
`BuildPlan.SkippedBecause` counts them; and `met` is present only when
the action had an `expect`. `start` and `ends`, and each action's,
event's, and check's `date`, are the same times as their ticks, as the
file names dates. `won` is present only for a challenge; `found` is what
the goal's measure came to at the end. The example shows the shape of the report. It
is not the output of a real run, and growth in particular depends on the
seed's rolls.

## Where the code goes

| Part | Place |
|---|---|
| The file model, the loader, and the validation | `CityLimits.Core/Scenarios/` |
| The runner and the report | `CityLimits.Core/Scenarios/` |
| The command | `CityLimits/Game/Commands/ScenarioSubcommand.cs`, which loads, runs, logs, and writes the report |
| The built-in scenarios | `CityLimits/Assets/Scenarios/*.json`, and `demo-town` in `CityLimits.Core/Scenarios/` |
| The tests | `CityLimits.Tests/`: the loader, the runner, and one test that runs every built-in scenario and expects it to pass |

The runner lives in the core library, not in the game, so the tests can
run a scenario exactly as the command does. A bug found with a scenario
becomes a regression test by adding the file. The test project gets the
built-in files by linking them in its project file. It still never
references the game project.

The runner takes no logger. It returns the report and raises an event per
action, disaster, and check, and the command turns those into log lines. That
keeps the core library free of NLog and of any package.

The default map size is `MapSize.DefaultWidth` and `MapSize.DefaultHeight`
in `CityLimits.Core/World/`, which the game, the headless run, and the
scenario defaults all read, so they can't drift apart.

## What it is not

- **Not a save format.** A scenario describes how to build a city, or
  names a saved city to start from, not a snapshot of one. Saves are
  described in `docs/saving.md`.
- **Not a scripting language.** There are no loops, variables, or
  conditions. If a scenario needs one, write a test instead.
- **Not live control.** A scenario runs to the end and reports. It doesn't
  accept commands while it runs.
- **Not a Micropolis scenario.** The roadmap borrows the scenarios' names,
  and nothing else: their city files belong to their authors, and every
  CityLimits scenario is original work.

## Open questions

- Whether the report should list every tile an action touched, or only
  the totals. Totals keep the report small; a `--verbose` flag could add
  the tiles later without a format bump.
