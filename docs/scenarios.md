# Scenarios

> **Status:** phase 1 is built: `CityLimits.Core/Scenarios/` and
> `citylimits scenario`, with `demo-town` and the built-in scenarios
> `bridges`, `zones-and-bulldozer`, and `small-town`, each run as a test. This document
> is the contract it was built to. It follows the Micropolis baseline: tools are strokes, zones are 3 by 3,
> the bulldozer refunds nothing, and a tick is one phase of the
> simulation's 16-phase cycle.

A scenario is a city set up from a file: a seed, a script of tool strokes
made at set ticks, and what should be true at the end. It serves two
audiences with one format:

- **Players** get a challenge: a starting city, maybe a problem along the
  way, and a goal to reach by a date (phase 2 and later).
- **Developers and agents** get a reproducible run. Because the simulation
  is deterministic, the same file gives the same city, tick for tick, on
  any machine. A bug that a scenario shows can be handed to someone else
  as a file, checked from the command line with no window, and kept as a
  test.

A scenario run is read-only. It never writes `settings.json`, a save, or
any other file, and it reports through the log and, on request, a JSON
report on stdout. It doesn't need a debugging protocol or a live
connection to the game.

## Phases

| Phase | Adds | Depends on |
|---|---|---|
| 1 | Seed, map size, starting funds, timed tool strokes, checks on tiles, zones, funds, and counts, the report | Today's simulation |
| 2 | Goals that win or lose: population, demand, the date | Kowloon's population and demand |
| 3 | Picking a scenario in the game, with its briefing and goal on screen | Happy Isle |

Only phase 1 is specified here. Phases 2 and 3 get their own sections
when their dependencies land.

## Running a scenario

```sh
citylimits scenario list                                # the built-in scenarios, by id
citylimits scenario demo-town --headless                # run a built-in scenario by id
citylimits scenario --file my-bug.json --headless       # run a scenario from a file
citylimits scenario demo-town --headless --ticks 768    # override the file's tick count
citylimits scenario demo-town --headless --json         # the report as JSON on stdout
```

Phase 1 runs scenarios only with the global `--headless` flag, and refuses
a run without it. In phase 3, `scenario <id>` without the flag will open
the scenario in a window. Requiring the flag now means a script written
today keeps the same meaning then and never starts opening windows.
`scenario list` opens no window in any phase, so it doesn't need the flag.

Built-in scenarios are the files in `CityLimits/Assets/Scenarios/`, copied
to the output folder with the other assets, plus `demo-town`, which is
defined in code because it is the [fallback](#the-fallback-scenario). A
file scenario's id is its file name without `.json`. An id may contain only lowercase letters, digits,
and hyphens, so an id can never name a path outside that folder.

### Exit codes

| Code | Meaning |
|---|---|
| 0 | The scenario ran and every check passed |
| 1 | The scenario could not run: the file is missing, too large, or invalid. This includes a run that fell back to `demo-town` |
| 2 | The command line was wrong (`ExitCodes.Usage`) |
| 3 | The scenario ran, and at least one check failed |

Code 3 is `ExitCodes.ChecksFailed`, added for scenarios. A caller
can then tell a broken scenario (1) from a city that didn't do what was
expected (3) without reading the output.

### Output

Without `--json`, the run writes to the log as `citylimits --headless` does: the setup,
each action and its result, the checks, and a summary line.

With `--json`, stdout carries the report and nothing else, so it can be
piped into a parser. For that run, the log moves to stderr:
`GameLog.UseStandardError` points the console target at the error stream.
NLog still writes to the console and nowhere else. This already works:
`scenario <id> --headless --json` leaves stdout empty today, because
there is no report yet.

## Time

A tick is one step of the simulation: one phase of Micropolis' 16-phase
cycle, the same unit as `citylimits --headless --ticks`. The calendar
moves on by a 48th of a year every 16 ticks, so there are 64 ticks in a
month and 768 in a year, and a city starts in January 1900. A zone gets
its chance to grow when the scan reaches its band, once every 16 ticks.
`CityCalendar` holds these numbers.

Phase 1 counts time in ticks only, because a tick is the unit the
simulation is deterministic in. Phase 2 adds goals by date, such as
"5 000 people by 1910".

## The file, version 1

```json
{
  "format": 1,
  "name": "Demo town",
  "description": "A road with a residential, a commercial, and an industrial zone, powered by a coal plant; the industry is knocked down after a year.",
  "seed": 2000,
  "funds": 20000,
  "ticks": 1536,
  "actions": [
    { "tick": 0, "tool": "Road", "from": { "x": 34, "y": 53 }, "to": { "x": 42, "y": 53 }, "expect": "Built" },
    { "tick": 0, "tool": "ZoneResidential", "from": { "x": 35, "y": 51 } },
    { "tick": 0, "tool": "ZoneCommercial", "from": { "x": 38, "y": 51 } },
    { "tick": 0, "tool": "ZoneIndustrial", "from": { "x": 41, "y": 51 } },
    { "tick": 0, "tool": "CoalPowerPlant", "from": { "x": 35, "y": 47 }, "expect": "Built" },
    { "tick": 768, "tool": "Bulldoze", "from": { "x": 41, "y": 51 }, "expect": "Built" }
  ],
  "checks": [
    { "tick": 1536, "tile": { "x": 35, "y": 51 }, "use": "Residential", "development": 0 },
    { "tick": 1536, "tile": { "x": 38, "y": 51 }, "use": "Commercial", "development": 0 },
    { "tick": 1536, "count": "Developed", "equals": 1 },
    { "tick": 1536, "tile": { "x": 38, "y": 50 }, "served": true, "powered": true },
    { "tick": 1536, "tile": { "x": 42, "y": 52 }, "kind": "Rubble", "use": "None" },
    { "tick": 1536, "fundsEquals": 16609 },
    { "tick": 1536, "count": "Road", "equals": 9 },
    { "tick": 1536, "count": "Industrial", "equals": 0 }
  ]
}
```

### Fields

| Field | Required | Meaning |
|---|---|---|
| `format` | yes | The file format version. Phase 1 reads `1` and refuses anything else |
| `name` | yes | A display name, 1 to 80 characters |
| `description` | no | One or two sentences, up to 500 characters |
| `seed` | yes | The seed the map and the simulation are built from: any 32-bit integer |
| `map` | no | `width` and `height`, each from 16 to 512. Default: the game's map, `MapSize.DefaultWidth` by `MapSize.DefaultHeight` |
| `funds` | no | The starting treasury, from 0 to 1 000 000. Default: `Treasury.StartingFunds` |
| `ticks` | yes | How many ticks the run takes, from 0 to 100 000, the same limit as `--ticks` |
| `actions` | no | Tool strokes made on the map; see below. Up to 10 000 entries |
| `checks` | no | What must be true; see below. Up to 10 000 entries |

### Actions

An action is one tool stroke, made exactly as a player makes it: the
tool pressed on `from` and dragged to `to`.

| Field | Required | Meaning |
|---|---|---|
| `tick` | yes | The stroke is made after this many ticks, before the next one. `0` is the start |
| `tool` | yes | A `BuildAction` name: `Road`, `ZoneResidential`, `ZoneCommercial`, `ZoneIndustrial`, `Bulldoze`, `CoalPowerPlant`, `NuclearPowerPlant`, `PowerLine`, `FireStation`, `PoliceStation`, `Park` |
| `from` | yes | `{ "x", "y" }`: the tile the stroke starts on |
| `to` | no | `{ "x", "y" }`: the tile the stroke is dragged to. Default: `from`, a stroke that doesn't move |
| `expect` | no | A `BuildStatus` (`Built`, `Unchanged`, `Refused`), or a status and a `BuildRefusal`, as in `Refused:NotEnoughFunds`, `Refused:PartOfAZone`, or `Unchanged:AlreadyDone` |

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
tile, $5 a power line tile, $25 under water, $10 a park tile, $100 a zone, $500 a fire or police station, $3,000 a coal power plant, $5,000 a nuclear power
plant, $1 more for each tile of trees or rubble cleared, $1 for each tile
bulldozed or zone or plant knocked down, and nothing refunded. A
stroke the city can't pay for, or that runs off the map, changes
nothing; within a stroke, a tile or zone that can't be built on is
skipped and the rest is built. Actions on the same tick run in file
order.

The money also changes once a year, as in Micropolis: the city collects
its taxes and pays its upkeep at phase 9 of each count that comes round
to a whole year, the first time at tick 9 and then every 768 ticks, at
the default 7% tax and full funding. A `fundsEquals` check after a
year's turn includes it.

A refusal is not an error, because a scenario may set out to show one. It
is recorded in the report, and it fails the run only if `expect` says
otherwise. `expect` describes the stroke's result as a whole, the same
`BuildResult` the game turns into its message.

### Checks

A check is evaluated after its `tick` has run, after that tick's actions.
Each check names one subject and one or more conditions. A check on a tick
past `ticks` is refused when the file is loaded.

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
`served` is whether a road lies along the zone's edge (`RoadNetwork.IsZoneServed`). `served` on a tile that isn't
part of a zone fails, with the reason that the tile has no zone.
`powered` is whether the tile's zone or plant had power when the scan
last reached its centre (`TileState.Powered`); a zone is first given
power on the scan after the first trace, in phase 11, so a check on it
belongs at tick 32 or later. On a tile that isn't part of a zone or a
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

Phase 2 adds `population` and the demand values as subjects.

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
- A tile check outside the map is refused at load time.

`System.Text.Json` ships with .NET, so this needs no new package, and
`CityLimits.Core` keeps its rule of no package references.

## The report, version 1

The report is a stable interface: agents, scripts, and later a server may
read it. Adding a field is allowed within a version. Renaming, removing,
or changing the meaning of a field means bumping `format`.

```json
{
  "format": 1,
  "scenario": { "name": "Demo town", "source": "demo-town", "seed": 2000, "ticks": 1536 },
  "passed": true,
  "final": {
    "tick": 1536,
    "cityTime": 96,
    "date": "1902-01",
    "funds": 16609,
    "counts": { "road": 9, "bridge": 0, "residential": 1, "commercial": 1, "industrial": 0, "developed": 1, "coalPowerPlant": 1, "nuclearPowerPlant": 0, "powerLine": 0, "fireStation": 0, "policeStation": 0, "park": 0, "hospital": 0, "church": 0 }
  },
  "actions": [
    { "index": 0, "tick": 0, "tool": "Road", "status": "Built", "tiles": 9, "skipped": {}, "cost": 90, "met": true },
    { "index": 1, "tick": 0, "tool": "ZoneResidential", "status": "Built", "tiles": 9, "skipped": {}, "cost": 100 },
    { "index": 4, "tick": 0, "tool": "CoalPowerPlant", "status": "Built", "tiles": 16, "skipped": {}, "cost": 3000, "met": true },
    { "index": 5, "tick": 768, "tool": "Bulldoze", "status": "Built", "tiles": 9, "skipped": {}, "cost": 1, "met": true }
  ],
  "checks": [
    { "index": 0, "tick": 1536, "passed": true },
    { "index": 3, "tick": 1536, "passed": true }
  ]
}
```

A failed check carries what was expected and what was found, and sets the
top-level `passed` to false:

```json
{ "index": 3, "tick": 1536, "passed": false, "expected": "funds equal 19609", "actual": "19599" }
```

`source` is the built-in id, or the file path as given on the command
line. `fallback` is present only when the requested scenario could not
be loaded and `demo-town` ran instead. For an action, `status` and, when
it wasn't built, `reason` are its `BuildResult`; `tiles` is how many
tiles it changed; `skipped` maps each `BuildRefusal` to how many tiles it
skipped for that reason, or how many zones for a zone stroke, as
`BuildPlan.SkippedBecause` counts them; and `met` is present only when
the action had an `expect`. The example shows the shape of the report. It
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
action and per check, and the command turns those into log lines. That
keeps the core library free of NLog and of any package.

The default map size is `MapSize.DefaultWidth` and `MapSize.DefaultHeight`
in `CityLimits.Core/World/`, which the game, the headless run, and the
scenario defaults all read, so they can't drift apart.

## What it is not

- **Not a save format.** A scenario describes how to build a city, not a
  snapshot of one. Saves are UI-5 and are designed separately. A later
  phase may let a scenario start from a save.
- **Not a scripting language.** There are no loops, variables, or
  conditions. If a scenario needs one, write a test instead.
- **Not live control.** A scenario runs to the end and reports. It doesn't
  accept commands while it runs.
- **Not a Micropolis scenario.** The roadmap borrows the scenarios' names,
  and nothing else: their city files belong to their authors, and every
  CityLimits scenario is original work.

## The fallback scenario

`demo-town` is the fallback: when a scenario can't be loaded, CityLimits
runs `demo-town` instead of stopping. This follows the rule that nothing
ends the player's session. Exactly when the fallback applies (a broken
built-in, a broken player file, a scenario picked in the game) is left
open until phase 3 makes scenarios something a player picks. Phase 1
fixes only the parts the rest of the design depends on:

- **It can't be missing.** `demo-town` is defined in code, in
  `CityLimits.Core/Scenarios/`, not in a file in `Assets/Scenarios/`. A
  fallback that has to be loaded from disk could fail for the same reason
  as the scenario it replaces. `scenario list` still lists it, and it
  runs by id like the built-in files.
- **It is valid by construction.** A test runs `demo-town` through the
  same validation as a file and expects it to pass.
- **It is never silent.** The fallback is a substitute, and the golden
  rules forbid substituting a silent default. The failure that caused it
  goes to the log, and to the screen when there is one. The report
  carries a `fallback` field: `{ "requested": "<id or path>", "reason":
  "<the load error>" }`, and its `scenario.source` is `demo-town`.
- **The exit code still tells the truth.** On the command line, a run
  that fell back exits with 1, the code for a scenario that could not
  run, even though `demo-town` did. An agent or script then never
  mistakes the fallback for the scenario it asked for.

`demo-town` is the town `citylimits --headless` builds today, in
`HeadlessRun`: a road nine tiles long with a residential, a commercial,
and an industrial zone along its north side. The headless run doesn't
depend on scenarios, but it should build its town from the `demo-town`
definition once that exists, so the two can't drift.

## Open questions

- Which failures fall back to `demo-town` and which only report, once
  players can pick scenarios (phase 3).
- Whether the report should list every tile an action touched, or only
  the totals. Totals keep the report small; a `--verbose` flag could add
  the tiles later without a format bump.
