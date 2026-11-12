# Project Guidelines

## Project structure

CityLimits is a SimCity-style city builder by Tony Bark, written in Swift with
[raylib-swift](https://github.com/tonytins/raylib-swift) and
[Hudini](https://github.com/tonytins/Hudini). It takes its simulation from
[Micropolis](https://github.com/SimHacker/micropolis/), the open source
SimCity Classic, under the GPL. CityLimits is not bound by Micropolis' design:
where the two disagree, CityLimits' own decisions win. See
[the roadmap](docs/ROADMAP.md) for where it's going.

- `Sources/CityLimitsCore` holds the world and the simulation: plain Swift with
  no raylib, no window, and no clock.
- `Sources/CityLimits` holds the game: the window, the sessions, and the
  drawing.
- `Tests/CityLimitsCoreTests` covers the core.
- `docs/` holds the documentation, and `packaging/` the files for distributing
  the game.

### The port from C#

As of 0.6, CityLimits is being ported from C# to Swift. The C# build lives on
the `legacy/csharp` branch, where it stays as it was at 0.5 (Senri). Its
projects (`CityLimits/`, `CityLimits.Core/`, `CityLimits.Interface/`,
`CityLimits.CommandLine/`, and `CityLimits.Tests/`) stay in `main` as a
reference until the Swift game can be played, then they're removed. Don't
change them.

- Port what the game does, not how the C# did it. The C# threaded a scenario
  editor flag through the game loop and its menus; the Swift build doesn't.
- The Swift build should play like the C# one, but it doesn't have to match it
  tick for tick. Where a match comes cheap, as with the random numbers, keep it.
- It reads and writes the formats 0.5 introduced: city saves 2.0, scenarios
  3.0, and definitions 2.0. Older formats are left to the C# build.
- Linux is the only platform for now. raylib-swift hasn't been built on
  Windows yet.

## Build and test

```sh
swift build                 # must build with no new warnings
swift test                  # all tests must pass
swift run citylimits        # open the window and look at it
swift run citylimits --version
```

Run the game when a change affects what it does. Passing tests don't show that
a screen works. To look at the game without a desktop, follow the visual check
steps in the `raylib-swift` skill, or your local guidance.

Headless mode and scenarios return in the port. Until then, a test in
`CityLimitsCoreTests` is how to check a rule.

## Swift and raylib

- Follow the `swift-guidelines` and `raylib-swift` skills in `.agents/skills`.
  Where they disagree with this file, this file wins.
- Match the package's Swift 6 language mode and its existing naming and
  formatting.
- Use the resolved raylib-swift and Hudini sources in `.build/checkouts` as
  the reference for their APIs. Both track their `main` branches.
- Read input and update state before `BeginDrawing`, and keep drawing free of
  game changes.
- Load textures and fonts once, after `InitWindow`, and unload them before
  `CloseWindow`. A missing asset is reported, and the game carries on.
- Keep dependencies few. Add one only with Tony Bark's approval, and only if it
  builds on Linux with the pinned raylib-swift.

## Interface

- Write as much of a screen as possible in UIScript (`.uix`). Code owns what the
  format can't describe, such as text that comes from the city.
- Each window is its own type with its own handler, run by Hudini's
  `WindowManager`. A window shared by more than one session, such as a
  confirmation, is told what to do with its answer rather than checking which
  session it's in.
- Keep a control specific to this game in this repository. When Hudini is
  missing something reusable, work around it here and raise it with Hudini
  rather than patching a copy.

## Sessions, not modes

Play, the scenario editor, and later network play are separate sessions over
the same core. Each one chooses its own windows, tools, and rules (the editor
builds for free and never autosaves, for instance). Don't add a flag that a
shared loop checks to tell them apart. If two sessions need the same
behaviour, share the piece, not the loop.

## Game rules

- **Keep the core free of the engine.** No raylib, window state, frame timing,
  or input in `CityLimitsCore`. Rendering reads the simulation and never writes
  it. If a rule can't be tested without a window, it's in the wrong place.
- **The simulation is deterministic.** The same seed and the same actions make
  the same city. Pass the simulation's `SeededGenerator` to anything random,
  and never use the system generator or the clock in the simulation. The date
  may name a save file; it may not steer a city.
- **Ticks are fixed size.** Game speed changes how many ticks run per frame,
  not how long a tick is.
- **The map is the state.** A tile's kind, zone, and development live in the
  map, and the renderer reads them from there. Don't keep a second list of
  buildings beside it.
- **New state goes in the save.** A field that lasts from one tick to the
  next is saved and restored, as a new feature in the save, with a line in
  `docs/saving.md`. The format's version only moves for a change older readers
  can't safely read.
- **Keep data in plain text.** Definitions, scenarios, and tuning live in JSON
  where a mod can change them, not hard-coded.

### Never crash

A crash loses the player's city. Stability outranks features.

- Handle errors where they happen. A bad save, a click off the map, or a tool
  on a tile it can't use is reported to the player, and the game goes on.
  Don't swallow a failure, substitute a silent default, or show success for a
  step that was skipped.
- Validate everything that crosses a boundary: files, input, tile indices, and
  map sizes. Check sizes before converting or slicing.
- Bound every loop that depends on the outside world.
- Don't block the frame loop, and don't allocate per tile in it.

## Keeping docs current

Describe only what works today.

- [changelog.md](changelog.md): only changes a player would notice, under the
  version GitVersion reports for that commit. `GitVersion.txt` holds the base
  version, and every commit counts up the patch number. Each milestone in a
  release takes a feature band (`0.6.1xx`, `0.6.2xx`, ...): set
  `GitVersion.txt` to the band in the commit that starts the milestone. A new
  release, such as `0.7.100`, needs Tony Bark's word.
- `docs/ROADMAP.md` and `docs/scorecard.md`: tick what landed, with evidence,
  and add a revision-history line. Leave scope decisions to Tony Bark.
- `docs/saving.md` and `docs/scenarios.md` when a format changes.
- [README.md](README.md) when setup or build steps change, and this file when
  the layout or rules change.

State breaking changes, new dependencies, and anything you couldn't verify in
the summary or commit message. If you notice a risk outside your task, report
it rather than fixing it quietly.

See [REVIEW.md](REVIEW.md) for the project's review checklist.

## Why people play

There are plenty of valid reasons to play.

- **To play as mayor**. As early as SimCity's SNES port, the player became both the architect and mayor of their own city.
- **To fulfill an urban planner's dream**. Designing efficient transportation networks, balance residential and commercial zones, and solve real-world city planning problems in a low-stakes environment.
- **To build something from nothing**. Transform an empty plot of land into a thriving metropolis with someone's own vision and aesthetic.
- **To experiment with city layouts and infrastructure**. What if you made all streets diagonal? What happens if you zone only residential areas? A city building game offers a approx answer to these questions.
- **For creative expression**. Some like to make themed districts, design unique skylines, and craft cities that reflect your personal style and imagination.
- **To manage complex systems**. Some find amusement in balancing budgets, manage traffic flow, handle citizen happiness, and solve interconnected problems that emerge as your city grows.
- **For the satisfaction of optimization**. Some enjoy fine-tuning a zone placement, perfect traffic patterns, and achieve efficient city operations.
- **To experience progression and growth**. Sometimes just watching a city evolve from a small town into a major metropolis over time is enough.
- **For relaxation and low-pressure gameplay**. Many city building games are sandboxes that lack any fail state. CityLimit's scenarios, for example, just resume normal play regardless of the outcome.
- **To tell a story through your city**. Some people want to create narratives about their city's history, character, and inhabitants through deliberate design choices.
- **To share and compete**. Upload your cities, compare designs with other players, and gain recognition for creative or optimized designs.

There are probably plenty of other reasons not covered here. You just don't know, and that's okay. So as noble as it would be to target everyone, CityLimits has no intention on targeting everyone. (We all know how well that worked out for SimCity [in the end](https://en.wikipedia.org/wiki/SimCity_(2013_video_game)).) The genre is niche enough as it is.

If this project somehow covers the multiple of points listed above and then some, that's great! But it also doesn't mean the project is aiming to reach a wider audience. All that means is that there is audience. CityLimits can easily attract it's own group of people, regardless of the path it choices.

This section simply exists to address nuances.

## Personal Configuration

Keep personal preferences and machine-specific guidance in git-ignored
`*.local` files or directories, such as `AGENTS.local.md`, rather than in
shared project instructions. Check for relevant local guidance before
environment-specific work.
