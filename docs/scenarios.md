# Scenarios

> **Status:** design, not implemented. The `scenario` command reads its
> command line today and runs nothing. This document is the contract
> for phase 1, which is on the [roadmap](./ROADMAP.md) under Kowloon.
>
> **To be revised before it is built.** It was written against the rules
> the Micropolis baseline refactor on the [roadmap](./ROADMAP.md) replaces:
> single-tile actions, a bulldozer refund, and a tick of a month. Its
> actions become tool strokes, its prices and checks follow the new rules,
> and its `ticks` follow the new calendar.

A scenario is a city set up from a file: a seed, a script of work done to
the map at set ticks, and what should be true at the end. It serves two
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
| 1 | Seed, map size, starting funds, timed build actions, checks on tiles, funds, and counts, the report | Today's simulation |
| 2 | Goals that win or lose: population, demand, the date | Kowloon's population and demand |
| 3 | Picking a scenario in the game, with its briefing and goal on screen | Happy Isle |

Only phase 1 is specified here. Phases 2 and 3 get their own sections
when their dependencies land.

## Running a scenario

```sh
citylimits scenario list                                # the built-in scenarios, by id
citylimits scenario demo-town --headless                # run a built-in scenario by id
citylimits scenario --file my-bug.json --headless       # run a scenario from a file
citylimits scenario demo-town --headless --ticks 240    # override the file's tick count
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

## The file, version 1

```json
{
  "format": 1,
  "name": "Demo town",
  "description": "A road and three zones near the middle of the map.",
  "seed": 2000,
  "map": { "width": 128, "height": 128 },
  "funds": 20000,
  "ticks": 12,
  "actions": [
    { "tick": 0, "do": "Road", "x": 64, "y": 64, "width": 3 },
    { "tick": 0, "do": "ZoneResidential", "x": 64, "y": 63, "width": 2 },
    { "tick": 0, "do": "ZoneCommercial", "x": 66, "y": 63, "expect": "Built" },
    { "tick": 6, "do": "Bulldoze", "x": 66, "y": 64 }
  ],
  "checks": [
    { "tick": 12, "tile": { "x": 64, "y": 63 }, "use": "Residential", "developmentAtLeast": 1 },
    { "tick": 12, "tile": { "x": 66, "y": 64 }, "use": "None" },
    { "tick": 12, "fundsAtLeast": 19900 },
    { "tick": 12, "count": "Road", "equals": 2 }
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
| `map` | no | `width` and `height`, each from 16 to 512. Default: 128 by 128, the game's map |
| `funds` | no | The starting treasury, from 0 to 1 000 000. Default: `Treasury.StartingFunds` |
| `ticks` | yes | How many ticks the run takes, from 0 to `HeadlessRun.MaxTicks` |
| `actions` | no | Work done to the map; see below. Up to 10 000 entries |
| `checks` | no | What must be true; see below. Up to 10 000 entries |

### Actions

An action is one `BuildAction` applied to a tile or a rectangle of tiles.

| Field | Required | Meaning |
|---|---|---|
| `tick` | yes | The action runs after this many ticks, before the next one. `0` is the start |
| `do` | yes | A `BuildAction` name: `Road`, `ZoneResidential`, `ZoneCommercial`, `ZoneIndustrial`, `Bulldoze` |
| `x`, `y` | yes | The top-left tile |
| `width`, `height` | no | The size of the rectangle, default 1. At most 4 096 tiles per action |
| `expect` | no | A `BuildStatus` (`Built`, `Unchanged`, `Refused`), or `Refused:<BuildRefusal>` such as `Refused:NotEnoughFunds` |

Actions on the same tick run in file order, and a rectangle runs row by
row, left to right. That's the same order the city would see from a
player dragging the tool. Every action goes through `CityBuilder.Apply`,
so it pays, refuses, and refunds exactly as the tools do.

A refusal is not an error, because a scenario may set out to show one. It is
recorded in the report. It fails the run only if `expect` says otherwise.
For a rectangle, `expect` must hold for every tile in it.

A rectangle may reach past the map edge. The tiles outside it are refused
with `OffTheMap` like any other tile, so `expect: "Built"` on such a
rectangle fails.

### Checks

A check is evaluated after its `tick` has run, after that tick's actions.
Each check names one subject and one or more conditions. A check on a tick
past `ticks` is refused when the file is loaded.

| Subject | Conditions |
|---|---|
| `tile`: `{ "x", "y" }` | `kind` (a `TileKind`), `use` (a `TileUse`), `development`, `developmentAtLeast`, `developmentAtMost`, `served` (true or false, from `RoadNetwork.IsServed`) |
| `funds`, no value | `fundsAtLeast`, `fundsAtMost`, `fundsEquals` |
| `count`: a `TileUse`, or `Developed` | `equals`, `atLeast`, `atMost` |

`Developed` counts the zoned tiles with a development level above zero, as
the `--headless` summary does. Phase 2 adds `population` and the demand
values as subjects.

### Validation

The file crosses a boundary, so it is checked before anything runs, and the
first problem is reported with its location, as in
`actions[3].width: needs a number from 1 to 64, not 0.`.

- A file over 1 MiB is refused before it is parsed.
- An unknown field is refused, so a misspelt check can't silently pass.
  (`System.Text.Json`'s `UnmappedMemberHandling.Disallow`.)
- Enum names are matched exactly, with the casing of the C# names.
- Every number is range-checked as the tables above say. A rectangle's
  tile count is computed in `long` before it is compared with the limit.
- A tile check outside the map is refused at load time. An action outside
  the map is not refused, because the builder's refusal is part of what a
  scenario can test.

`System.Text.Json` ships with .NET, so this needs no new package, and
`CityLimits.Core` keeps its rule of no package references.

## The report, version 1

The report is a stable interface: agents, scripts, and later a server may
read it. Adding a field is allowed within a version. Renaming, removing,
or changing the meaning of a field means bumping `format`.

```json
{
  "format": 1,
  "scenario": { "name": "Demo town", "source": "demo-town", "seed": 2000, "ticks": 12 },
  "passed": true,
  "final": {
    "tick": 12,
    "date": "2001-01",
    "funds": 19960,
    "counts": { "Road": 2, "Residential": 2, "Commercial": 1, "Industrial": 0, "Developed": 3 }
  },
  "actions": [
    { "index": 0, "tick": 0, "do": "Road", "tiles": 3, "built": 3, "unchanged": 0, "refused": {}, "cost": 30, "refund": 0 },
    { "index": 2, "tick": 0, "do": "ZoneCommercial", "tiles": 1, "built": 1, "unchanged": 0, "refused": {}, "cost": 5, "refund": 0, "met": true }
  ],
  "checks": [
    { "index": 0, "tick": 12, "passed": true },
    { "index": 3, "tick": 12, "passed": true }
  ]
}
```

A failed check carries what was expected and what was found, and sets the
top-level `passed` to false:

```json
{ "index": 3, "tick": 12, "passed": false, "expected": "Road count equals 2", "actual": "3" }
```

`source` is the built-in id, or the file path as given on the command line.
`fallback` is present only when the requested scenario could not be loaded
and `demo-town` ran instead.
`refused` maps each `BuildRefusal` to how many tiles it stopped. `met` is
present only when the action had an `expect`. The example shows the shape
of the report. It is not the output of a real run, and the `Developed`
count in particular depends on the seed's growth rolls.

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

`citylimits --headless` builds its own demo town today, in `HeadlessRun`.
It doesn't depend on scenarios, but it may build its town
from the `demo-town` definition once that exists, so the two can't drift.

## Open questions

- Which failures fall back to `demo-town` and which only report, once
  players can pick scenarios (phase 3).
- Whether the report should list every tile an action touched, or only
  the totals. Totals keep the report small; a `--verbose` flag could add
  the tiles later without a format bump.
