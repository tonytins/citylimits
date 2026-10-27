# AGENTS.md

This file provides project instructions and guidelines for coding agents working in this repository.

## Project Overview

This repository contains the CityLimits game engine and other tools.

Key entrypoints:

- ``CityLimits/`` - The game client
- ``CityLimits.CommandLine/`` - A custom command line library
- ``CityLimits.Core/`` - Engine-agnostic portions of the game
- ``CityLimits.Interface/`` - The UI library
- ``CityLimits.Tests/`` - Unit tests of the core library
- ``docs/`` - The game's documentation and development progress
- ``packaging/`` - Files for distributing the game


## About this Project

CityLimits is a SimCity-style city building engine designed by Tony Bark and written in C# (.NET 10) using [Raylib-cs](https://github.com/ChrisDill/Raylib-cs). The engine adopts core simulation mechanics from [Micropolis](https://github.com/SimHacker/micropolis/), the official open source SimCity Classic, licensed under the GPL, but as CityLimits _is_ CityLimits, this project is not beholden to Micropolis's design constraints.

### Micropolis

CityLimits uses [`micropolis-activity`](https://github.com/sugarlabs/micropolis-activity) and [`micropolis-java`](https://github.com/SimHacker/micropolis/tree/master/micropolis-java), the official OLPC and Java ports of SimCity, respectively, both of which are open source, licensed under the GPL, as the baseline reference. [`MicropolisCore`](https://github.com/SimHacker/MicropolisCore), a more up-to-date version of the code, is used as a simple cross-reference to ensure fidelity in traffic simulation, zone demand calculation, citizen behaviour patterns, or elsewhere.

If there is any disagreement between any version of Micropolis, the project's own design decisions wins.

## Common Commands

```sh
dotnet build CityLimits.slnx                                  # must build with no new warnings
dotnet test  CityLimits.Tests         # all tests must pass
dotnet run   --project CityLimits -- --headless --ticks 768   # run the city for a year with no window and read the log
dotnet run   --project CityLimits                             # open the window and look at it
```

#### Scenarios

Although designed with coding agents, CityLimits lacks a true MCP on purpose. As an alternative, agents can use the game's existing scenario mechanic in headless mode to validate behaviour during development.[^1]

**What is a scenario?**

Scenarios are a challenge for the player. CityLimits implementats theirs using a JSON-formatted file that defines:
- Initial game state (map, budget, infrastructure, population)
- Events (disasters, policy changes, zone demand shifts)
- Goals (population targets, budget thresholds, infrastructure milestones)

Whatever the engine currently supports, at the moment.

**Scenario workflow**

An agent runs a scenario like this:

```sh
citylimits scenario <id> --headless [options]
citylimits scenario --file <path> --headless [options]
citylimits scenario list
```

The agent then:
1. Loads the scenario (initial state is deterministic)
2. Steps the simulation forward for a fixed number of ticks
3. Queries the resulting game state (population, zones, budget, traffic, etc.)
4. Validates the results against expected values

Since there is no explicit failure mode (the game continues running regardless) the tick count is what halts execution. The agent's job is to read the final state and compare it to what was expected.

**When to use scenarios**

Scenarios are suited for:
- Regression testing (verify existing behavior still works)
- Feature validation (does the new traffic system behave as intended?)
- Deterministic playback (same scenario, same ticks = same result)

A headless scenario is _not_ a substitute for actual gameplay. They validate specific new mechanics in isolation using a preexisting feature; they don't catch emergent issues, can't balance problems during normal gameplay, and it can't account for how the user interface is supposed to look and behave in normal game play.

## Golden rules

### Design Process

- **Design through iteration.** Treat a first pass at a design, a draft shape, or a first naming attempt as a starting point to revise. Expect to refine or rebuild clumsy parts as understanding improves.
- **Ask when decisions are open.** For ambiguous choices, unclear behavior, or tradeoffs between fidelity and clean design, flag them rather than deciding unilaterally. Keep critical thinking collaborative.
- **Adapt to the user's context.** Understand their tools, constraints, and workflow. Work with what they have rather than imposing external requirements.
- **Explain the reasoning.** Show the why behind suggestions and changes so the user can evaluate, learn, and steer the work; teach rather than just deliver.
- **Trust the user's judgment.** Defer to their domain expertise and priorities. Flag concerns when they matter, but don't override their decisions.
- **Favor simplicity.** Entities must not be multiplied beyond necessity. The simplest correct approach that matches observed behavior is almost always preferable to over-engineered alternatives.
- **Account for constraints.** Differences between source and target environments are real costs, not failures of design. Use workarounds only when necessary.
- **Make implicit things explicit.** State assumptions, unverified behavior, confidence levels, and what remains uncertain. Don't let important details stay hidden.
- **Suggest validation early.** Offer test cases, assertions, or verification approaches as understanding solidifies. A test that catches the problem before implementation is valuable feedback.
- **Let evidence carry weight.** Report what was tried, what succeeded, what failed, and what remains open. Let the results speak rather than over-interpreting or jumping to conclusions.
- **Flag blindspots.** Call out what you can't verify, what might break, what needs play-testing or expert review, and where the approach is unproven.

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

### Game mechanics

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
- **The engine-free libraries never reference Raylib-cs.** `CityLimits.Core`  
  and `CityLimits.CommandLine` have no package references at all, and they  
  must keep it that way. Only the game and `CityLimits.Interface` reference  
  Raylib-cs. The game project may reference every library; no library may  
  reference the game, and the test project may never reference the game  
  or the interface library either.
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
- **For relaxation and low-pressure gameplay**. Many city building games are sandboxes that lack any fail state. CityLimit's scenarios (see below), for example, just resume normal play regardless of the outcome.
- **To tell a story through your city**. Some people want to create narratives about their city's history, character, and inhabitants through deliberate design choices.
- **To share and compete**. Upload your cities, compare designs with other players, and gain recognition for creative or optimized designs.

There are probably plenty of other reasons not covered here. You just don't know, and that's okay. So as noble as it would be to target everyone, CityLimits has no intention on targeting everyone. (We all know how well that worked out for SimCity [in the end](https://en.wikipedia.org/wiki/SimCity_(2013_video_game)).) The genre is niche enough as it is.

If this project somehow covers the multiple of points listed above and then some, that's great! But it also doesn't mean the project is aiming to reach a wider audience. All that means is that there is audience. CityLimits can easily attract it's own group of people, regardless of the path it choices.

This section simply exists to address nuances.

[^1]: Headless mode was added as early scaffolding for a future [network edition](https://en.wikipedia.org/wiki/SimCity_2000#SimCity_2000_Network_Edition)-style server.
