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
  Game/                         namespace CityLimits.Game: the game command, the window, the loop, the tools, the state, the headless run, the log
    Commands/                   namespace CityLimits.Game.Commands: the subcommands, one class each
  Rendering/                    namespace CityLimits.Rendering: the camera, the tile drawing, the tile sheet, the theme
  UI/                           namespace CityLimits.UI: the HUD, the tool palette
  Assets/                       images, fonts, and sounds, copied to the output folder
CityLimits.Core/              engine-free library: the world and the simulation, no Raylib
  CityLimits.Core.csproj        the project file; it must never reference Raylib-cs
  GlobalUsings.cs               global usings shared by the project
  Randomness/                   namespace CityLimits.Core.Randomness: the seeded generator the simulation owns
  World/                        namespace CityLimits.Core.World: the map, the tiles, the terrain, the tile rules, the build actions
  Simulation/                   namespace CityLimits.Core.Simulation: the clock, the date, the tick
  Economy/                      namespace CityLimits.Core.Economy: the money, the costs, the treasury
  Settings/                     namespace CityLimits.Core.Settings: the settings that survive a session
CityLimits.CommandLine/       engine-free library: the command line, no Raylib
  CityLimits.CommandLine.csproj the project file; it references no package and no other project
  GlobalUsings.cs               global usings shared by the project
  *.cs                          namespace CityLimits.CommandLine: the parser, the dispatcher, the option reader, the exit codes
CityLimits.Tests/              xUnit 2 tests for CityLimits.Core and CityLimits.CommandLine
  CityLimits.Tests.csproj       references the two libraries, never the game project
docs/
  ROADMAP.md                    milestones, current focus, estimates, revision history
  scorecard.md                  verified status per area, with evidence
  scenarios.md                  the scenario file and report formats (designed, not implemented yet)
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
  hands the city to `HeadlessRun`. Otherwise `WindowedGame.Play` opens a resizable window
  titled "CityLimits" at the saved size, runs the frame loop until the
  window is closed, saves the settings, and closes the window. Every frame is
  guarded: a failure is reported on the screen and the game keeps running.
- **The map.** `GameLoop` builds a 128 by 128 map at startup from the seed
  2000: value-noise terrain with water, grass, trees, and rock. The renderer
  draws only the tiles the camera can see, and it draws them from the tile
  sheet in `Assets/tiles.png`.
- **The camera.** The WASD keys and the arrow keys pan, the mouse wheel
  zooms toward the pointer, and the view is clamped to the map.
- **The clock.** A tick is a quarter of a second of real time, and it
  moves the city's calendar on by one month. Space pauses the game, and
  the comma and period keys change the speed between 1x and 3x. The speed
  changes how many ticks run in a frame, never the size of a tick.
- **The tick.** `CitySimulation.Tick` advances the date and then grows the
  city: a zoned tile that the road network serves develops a step, up to
  level 3, and the chance is taken from the simulation's own seeded
  generator, so the same seed and the same work give the same city.
- **The tools.** Six tools sit in the palette down the left, each with its
  icon, and the number keys pick one: inspect, road, residential,
  commercial, industrial, and bulldozer. Roads and zones are painted by
  dragging the mouse over the map, and the tile under the cursor is
  painted as it moves.
- **The money.** The city starts with a treasury. Each tool has a price,
  the HUD shows it, the treasury pays for the work, and a tool the city
  cannot pay for is refused with a message and changes nothing. The
  bulldozer gives half the price back.
- **The tile readout.** The tile under the mouse is outlined, and a panel
  names it and says what can be done with it.
- **The settings.** The window size and the speed are written to
  `settings.json` beside the executable when the game ends, and read back
  when it starts. A damaged file is reported and the defaults are used.
- **The headless run.** `citylimits --headless` runs the city with no
  window at all and writes each tick to the console, so the simulation can
  be checked on a machine with no screen. It takes `--seed` and `--ticks`,
  from 0 to 100 000, and builds a small demo town first so there is
  something to grow. `--ticks` without `--headless` is refused, because
  the window runs until it is closed.
- **The scenario command.** `scenario` reads its command line (an id or
  `--file`, `--ticks`, `--json`, and `scenario list`) but loads nothing yet.
  A run needs `--headless`, because a scenario will open a window once
  scenarios can be played; `list` doesn't. `list` reports that there are
  no scenarios, and a run reports that scenarios are not implemented and
  exits with code 1. The design is in `docs/scenarios.md`.
- **The log.** NLog writes to the console and nowhere else. Failures go to
  the log as well as to the screen.
- **The solution.** `CityLimits.slnx` holds four projects: the game, the
  core library, the command line library, and the tests. Every .NET project in the repository lives
  in its own folder and is listed in the solution.
- **The core library.** `CityLimits.Core` targets `net10.0` with nullable
  and implicit usings enabled, and references no package at all. It holds
  the map and its generator, the clock, the date, the simulation, the
  seeded generator, the tile rules, the build actions, the road network,
  the treasury, and the settings.
- **The command line library.** `CityLimits.CommandLine` targets `net10.0`
  with nullable and implicit usings enabled, and references no package
  and no project. It holds the parser, the dispatcher, the default
  command, the strict option reader, and the exit codes.
- **The tests.** `CityLimits.Tests` uses xUnit 2 with the VSTest runner
  and coverlet, and holds 140 tests over the generator, the map, the
  calendar, the clock, the simulation, the settings, the tile rules, the
  build actions, the road network, the treasury, the option reader, and
  the dispatcher.
- **The game project.** `CityLimits.csproj` targets `net10.0` with nullable
  and implicit usings enabled, and references `Raylib-cs` `8.1.0` and
  `NLog` `6.2.2`. The native `raylib` library ships inside the NuGet
  package for `linux-x64`, `win-x64`, and `win-x86`, so no system install
  is needed on Linux or Windows.
- **The assets.** `CityLimits/Assets/` holds the tile sheet and the tool
  icons. The project file copies them to the output folder, and the game
  loads them once, after the window is open.
- **Version.** `VersionInfo` reports the version from GitInfo: `GitInfo.txt`
  holds the `major.minor.patch` the game reports, and it is bumped by hand
  when a change is worth a version, with a changelog entry to match.
  `BuildNumber` is derived from the commit hash. The changelog only records
  versions with user-facing and developer changes, so its latest entry is
  expected to lag behind the version the game reports. Developer changes
  include any public APIs, tools, debugging, and modding support.

Everything else in this file describes the rules the code must follow as it
grows. Do not describe planned behavior as if it exists.

## 3. Golden rules

### Never crash the game (outranks feature work )

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
  than the last frame, but it never changes the tick size. Pause, 1x, 2x,
  and 3x change how many ticks run per frame, not how big a tick is.
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

### Raylib rules

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
- **Batch what you can.** One draw call per tile is fine at first, but the
  map will grow; keep the tile drawing in one place, in
  `Rendering/`, so it can be batched later without touching the game rules.
- **Colors and layouts live in one place.** Tile colors, the UI palette, and
  the sprite sizes belong in a theme file, not scattered through the draw
  code.
- New dependencies need the owner's approval, and they must work with the
  pinned Raylib-cs and on both Linux and Windows.

### The log and the headless run

- **NLog writes to the console and nowhere else.** `Game/GameLog.cs` sets
  the rule up once, at startup, and `LogManager.Shutdown()` flushes it at
  the end. There is no file target, no database target, and no mail target.
- **Log what the player cannot see.** A failure goes to the log and to the
  screen, so the log is the record of what went wrong after the window is
  gone.
- **The headless run is the way to check the simulation on a machine with no
  screen.** `dotnet run --project CityLimits -- --headless --ticks 100`
  opens no window, runs the city for a hundred ticks, and writes each tick
  to the console. It must not touch Raylib, or it is not headless.
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

## 4. Legal

Micropolis, the open source version of SimCity, and the reimplementations
of it are under the GNU GPL, and their art, sounds, and city files belong to
their authors. The [SimHacker repository](https://github.com/SimHacker/micropolis)
is the canonical and official Micropolis repository, maintained by
[Don Hopkins](https://en.wikipedia.org/wiki/Don_Hopkins). CityLimits is
under the GPL version 3, and the art it ships is Micropolis' art, which is
under the same licence: `Assets/tiles.png` and the tool icons come from the
Micropolis core's images, and `Assets/README.md` records where they came
from.

- **New art has to be the owner's own work, or come with a licence that is
  compatible with the GPL version 3.** Credit it in `Assets/README.md`.
- **Don't copy source, assets, or data files from another project.**
  Mechanics are not copyrightable, but a close reimplementation of someone
  else's code or assets is. If you are unsure whether something is an idea
  or a copy, say so and ask.
- **CityLimits is not a SimCity product.** The SimCity and Micropolis name
  and trademarks belong to their owners, and neither the code nor the art may
  claim them.
- **Micropolis is the reference implementation.** It is the yardstick for
  what the game does, not a source to copy from.

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

  **What it does not license.** Both projects are under the GPL version 3,
  so copying Micropolis source into CityLimits would be legal. The rule
  above forbids it anyway: the licences line up, the originality rule does
  not bend. Write the behaviour down in CityLimits' own terms first,
  implement it in C# in this codebase's idiom, and prove it with a test.

  **Where the line is.** Don't port, translate, transliterate, or
  transcribe Micropolis' source in any language, the C, C++, and Java
  alike, and don't take its data tables or its city files. Don't rename
  identifiers to disguise a transcription. Reading Micropolis to learn
  what a rule does is fine; lifting it into the tree is not. When a
  mechanic is unclear, ask the owner.

  **What is borrowed, and it is only this.** The tile sheet and the tool
  icons, from the Micropolis core's images, credited in
  `Assets/README.md`. Anything else taken from Micropolis needs the
  owner's approval, and it has to be recorded there too.

## 5. Before you finish a task

The build, the tests, and the game itself are the gate. A change to the
simulation is proven by a test and a `--headless` run; a change to the
rendering is proven by running the game and looking at it. Whether the
change works in play is the owner's call, not yours.

```sh
dotnet build CityLimits.slnx                                  # must build with no new warnings
dotnet test  CityLimits.Tests/CityLimits.Tests.csproj         # all tests must pass
dotnet run   --project CityLimits -- --headless --ticks 100   # run the city with no window and read the log
dotnet run   --project CityLimits                             # open the window and look at it
```

- **The command line is the way in.** With no command, CityLimits opens
  the window, and `--headless` runs the same city with no window.
  `scenario` is scaffolding that runs nothing yet. `--help` lists the
  options and commands, and `<command> --help` describes one. Use these to
  run the game; don't wrap it in another launcher.
- **Check the simulation with `--headless`.** It needs no display, so it
  works on any machine. Pass `--seed` and `--ticks` to reproduce a city,
  and read the per-tick log for the change you made. A run that exits
  with anything other than 0 is a failure.
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
