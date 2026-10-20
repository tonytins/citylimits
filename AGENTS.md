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
CityLimits.slnx             solution: the game, the core library, and the tests
CityLimits/                  the game project (Raylib-cs, NLog, net10.0)
  CityLimits.csproj            package references, the core library reference, the target framework, and the asset copy
  Program.cs                    entry point: the options, the window, the main loop, shutdown
  GlobalUsings.cs               global usings shared by the project
  Game/                         namespace CityLimits.Game: the loop, the tools, the state, the options, the headless run, the log
  Rendering/                    namespace CityLimits.Rendering: the camera, the tile drawing, the tile sheet, the theme
  UI/                           namespace CityLimits.UI: the HUD, the tool palette
  Assets/                       images, fonts, and sounds, copied to the output folder
CityLimits.Core/              engine-free library: the world and the simulation, no Raylib
  CityLimits.Core.csproj        the project file; it must never reference Raylib-cs
  GlobalUsings.cs               global usings shared by the project
  Randomness/                   namespace CityLimits.Core.Randomness: the seeded generator the simulation owns
  World/                        namespace CityLimits.Core.World: the map, the tiles, the terrain, the tile rules
  Simulation/                   namespace CityLimits.Core.Simulation: the clock, the date, the tick
  Settings/                     namespace CityLimits.Core.Settings: the settings that survive a session
CityLimits.Tests/              xUnit 2 tests for CityLimits.Core
  CityLimits.Tests.csproj       references CityLimits.Core, never the game project
docs/
  ROADMAP.md                    milestones, current focus, estimates, revision history
  scorecard.md                  verified status per area, with evidence
```

The split is the point of the layout: `CityLimits.Core/` holds everything
that can be decided without a window, and `CityLimits/` holds everything
that draws it. The game project may reference the core library; the core
library may never reference the game.

The folders above are the target shape, and the code follows it today:
the world, the simulation, the generator, and the settings live in
`CityLimits.Core/`, and the window, the loop, the camera, and the drawing
live in `CityLimits/`. Keep new code in the folder that matches the map,
and don't invent a parallel structure.

Build output (`bin/`, `obj/`, `*.user`) is ignored; never commit it.

## 2. How it works today

- **The game.** `Program.Main` reads the options, opens a resizable window
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
- **The tools.** Five tools sit in the palette down the left, each with its
  icon, and the number keys pick one. The tools do not do anything yet.
- **The tile readout.** The tile under the mouse is outlined, and a panel
  names it and says what can be done with it.
- **The settings.** The window size and the speed are written to
  `settings.json` beside the executable when the game ends, and read back
  when it starts. A damaged file is reported and the defaults are used.
- **The headless run.** `--headless` runs the city with no window at all and
  writes each tick to the console, so the simulation can be checked on a
  machine with no screen.
- **The log.** NLog writes to the console and nowhere else. Failures go to
  the log as well as to the screen.
- **The solution.** `CityLimits.slnx` holds three projects: the game, the
  core library, and the tests. Every .NET project in the repository lives
  in its own folder and is listed in the solution.
- **The core library.** `CityLimits.Core` targets `net10.0` with nullable
  and implicit usings enabled, and references no package at all. It holds
  the map and its generator, the clock, the date, the simulation, the
  seeded generator, the tile rules, and the settings.
- **The tests.** `CityLimits.Tests` uses xUnit 2 with the VSTest runner
  and coverlet, and holds 46 tests over the generator, the map, the
  calendar, the clock, the simulation, the settings, and the tile rules.
- **The game project.** `CityLimits.csproj` targets `net10.0` with nullable
  and implicit usings enabled, and references `Raylib-cs` `8.1.0` and
  `NLog` `6.2.2`. The native `raylib` library ships inside the NuGet
  package for `linux-x64`, `win-x64`, and `win-x86`, so no system install
  is needed on Linux or Windows.
- **The assets.** `CityLimits/Assets/` holds the tile sheet and the tool
  icons. The project file copies them to the output folder, and the game
  loads them once, after the window is open.
- **Version control.** The tree is under git on `main`, but nothing has been
  committed yet. There is no `README`, no changelog, and no CI.

Everything else in this file describes the rules the code must follow as it
grows. Do not describe planned behavior as if it exists.

## 3. Golden rules

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

### Never crash the game

There is no host process underneath and no supervisor to restart the
process, so an unhandled exception ends the player's session with no
explanation.

- **Handle errors where they happen.** A bad save file, a mouse position
  off the map, or a tool applied to an invalid tile is reported to the
  player and the game continues. Don't swallow a failure, don't substitute
  a silent default, and don't show success for a step that was skipped.
- **The frame loop is the last resort, not the plan.** `Program.Main`
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
- **`CityLimits.Core` never references Raylib-cs.** It has no package
  references at all, and it must keep it that way. The game project may
  reference the core library; the core library may never reference the
  game, and the test project may never reference the game either.
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
- **The options are parsed, not guessed.** `Game/CommandLine.cs` reads
  `--headless`, `--ticks`, `--seed`, and `--help`, and reports an option it
  does not understand instead of ignoring it.

### Originality and licensing

SimCity and the open-source reimplementations of it are under the GNU GPL,
and their art, sounds, and city files belong to their authors. CityLimits
is under the GPL version 3, and the art it ships is the Micropolis art,
which is under the same licence: `Assets/tiles.png` and the tool icons come
from [Micropolis](https://github.com/SimCitySimulator/micropolis), and
`Assets/README.md` records where they came from.

- **New art has to be the owner's own work, or come with a licence that is
  compatible with the GPL version 3.** Credit it in `Assets/README.md`.
- **Don't copy source, assets, or data files from another project.**
  Mechanics are not copyrightable, but a close reimplementation of someone
  else's code or assets is. If you are unsure whether something is an idea
  or a copy, say so and ask.
- **CityLimits is not a SimCity product.** The SimCity name and trademarks
  belong to their owners, and neither the code nor the art may claim them.

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
- Keep `Program.cs` thin: it opens the window, owns the loop, and shuts
  down. Everything else belongs in `CityLimits.Core/Randomness/`,
  `CityLimits.Core/World/`, `CityLimits.Core/Simulation/`,
  `CityLimits.Core/Settings/`, `CityLimits/Game/`, `CityLimits/Rendering/`,
  or `CityLimits/UI/`.
- Match the surrounding code when it disagrees with this file, and mention
  the disagreement.

## 4. Before you finish a task

The build, the tests, and the game itself are the gate. A change to the
simulation is proven by a test; a change to the rendering is proven by
running the game and looking at it.

```sh
dotnet build CityLimits.slnx                     # must build with no new warnings
dotnet test  CityLimits.Tests/CityLimits.Tests.csproj  # all tests must pass
dotnet run   --project CityLimits                 # run it and look at the window
```

- If the machine has no display, run the game under a virtual display and
  read the log: `xvfb-run -a dotnet run --project CityLimits`. If you
  could not run it, say that you couldn't. A run with no display at all is
  `dotnet run --project CityLimits -- --headless --ticks 100`.
- A black window means a frame was never presented. Check that the draw is
  bracketed by `BeginDrawing` and `EndDrawing` before looking anywhere else.
- If you changed the world or the simulation, add or update its tests in
  the same change, and check that a new test fails without your fix.
- If you changed what the player sees, run the game and confirm the
  change on screen. A build is not a visual check.
- Bring [docs/scorecard.md](./docs/scorecard.md) up to date: flip the rows
  you moved, with their new evidence, and update the summary counts.
- Bring [docs/ROADMAP.md](./docs/ROADMAP.md) up to date: tick checklist
  items, update the milestone's done percentage and estimate, bump the
  status line, and add a revision-history row. Pick work from its current
  focus and ranked list, and leave scope decisions to the owner.
- If the layout or the rules change, update the workspace map and the
  golden rules in this file.

Describe only what works today, and keep the tree building at every step.

## 5. Calling things out

The owner often reviews changes alone, so make review easy:

- State breaking changes, dependency bumps, and changes to the simulation's
  rules clearly in the summary or commit message. A change to growth,
  demand, power, or the economy changes every saved city.
- List judgment calls and anything you couldn't verify, such as "builds,
  but the game was not run".
- If you notice a risk outside your task, report it. Don't fix it quietly
  in an unrelated change.
