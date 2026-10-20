---
name: raylib-cs
description: How CityLimits uses Raylib-cs 8.1.0 (raylib 6.0) for its window, its frame loop, the camera, textures, the tile sheet, render textures, text, and input, and how to check a visual change without the owner's desktop. Use this whenever you write, review, or debug code in CityLimits/ that calls Raylib, touches Rendering/, UI/, GameLoop, WindowedGame, or GameAssets, adds or draws an asset, or verifies something on screen, even if the request never says "Raylib".
---

# Raylib-cs in CityLimits

CityLimits draws with [Raylib-cs](https://github.com/raylib-cs/raylib-cs)
`8.1.0`, which wraps raylib `6.0`. This skill is how that library is used
here. `AGENTS.md` holds the project's rules and wins wherever the two
disagree. This file explains how to follow those rules with Raylib.

## 1. Check the API before you use it

The Raylib-cs API changes between releases, and the upstream docs and the
`main` branch track the latest, not the pinned version. Names have changed
under earlier agents' feet: `IsTextureReady` is now `IsTextureValid`, and
`IsRenderTextureValid` and `IsFontValid` follow the same pattern.

- **The installed package is the authority.** Its XML docs list every
  member of 8.1.0:
  `~/.nuget/packages/raylib-cs/8.1.0/lib/net10.0/Raylib-cs.xml`. Search it
  for `M:Raylib_cs.Raylib.<Name>(` before calling something you haven't
  seen in this codebase.
- **The examples at the matching tag are the reference.** Clone the
  repository into your scratch directory, check out `v8.1.0`, and read the
  example closest to what you are writing (section 9 has the index). Treat
  the clone as reference material: read it, don't build or run it from
  inside the clone, and don't copy it into the tree. Its examples are zlib-licensed
  ports of raylib's C examples. Learn the shape, then write CityLimits'
  own code.

  ```sh
  git clone --depth 50 --no-single-branch https://github.com/raylib-cs/raylib-cs.git <scratch>/raylib-cs
  git -C <scratch>/raylib-cs fetch --depth 1 origin tag v8.1.0
  git -C <scratch>/raylib-cs checkout v8.1.0
  ```

- **C# shapes in 8.1.0.** `Rectangle`, `Vector2` (from `System.Numerics`),
  and `Color` are structs. `Rectangle` and `Color` have positional
  constructors (`new Rectangle(x, y, w, h)`, `new Color(r, g, b, a)`), and
  the named colours are PascalCase statics (`Color.White`,
  `Color.RayWhite`). CityLimits writes them with object initializers
  (`new Color { R = .., G = .., B = .., A = 255 }`). Match that in its
  files. Enums are PascalCase too: `KeyboardKey.Escape`,
  `MouseButton.Left`, `TextureFilter.Point`, `ConfigFlags.ResizableWindow`.
  Raylib's functions are statics on `Raylib`. This codebase calls them
  qualified (`Raylib.DrawText`), while the examples use `using static`.
  Keep the qualified form here.

## 2. Where Raylib is allowed

| Place | Raylib? |
|---|---|
| `CityLimits/Rendering/`, `CityLimits/UI/`, `GameLoop`, `WindowedGame`, `SplashScreen` | yes |
| `CityLimits.Core/`, `CityLimits.CommandLine/`, `CityLimits.Tests/` | never: no package reference, no type, no call |
| The `--headless` path (`GameCommand` with `Headless` set, `HeadlessRun`, `scenario`) | never: not even `SetTraceLogLevel`. A Raylib call there opens a window or loads native code a server may not have |

If you can't decide something without Raylib, it belongs in the game
project. If something can be decided without it, it belongs in Core,
behind a test. Which road tile a crossing needs is an example: Core reports
the connections (`RoadNetwork.ConnectionsAt`), and only the choice of
drawing is in `Rendering/`.

## 3. The lifecycle

The order matters, because textures need the OpenGL context that
`InitWindow` creates. In CityLimits it lives in `Game/WindowedGame.cs`:

1. `SetConfigFlags(ConfigFlags.ResizableWindow)`, before the window.
2. `InitWindow(width, height, "CityLimits")`, at the saved size.
3. `SetTargetFPS(60)`.
4. `GameAssets.Load(...)`: every texture is loaded **once**, here, after
   `InitWindow`. Never load inside the frame loop.
5. The splash screen, then the frame loop, while `!WindowShouldClose()`
   and the game hasn't asked to close.
6. `game.SaveSettings()`, then, in `finally`, `assets.Dispose()`
   (`UnloadTexture` for each), then `CloseWindow()` last.

Anything that owns GPU memory (a `Texture2D`, a `RenderTexture2D`, a
`Font`) is created after step 2, kept for the life of the game, and
released before step 6's `CloseWindow`. If you add one, give it an owner
that is disposed in that `finally`.

## 4. The shape of a frame

`GameLoop.Draw` is the model. Every frame is bracketed, or the window stays
black because the buffer is never presented:

```text
BeginDrawing
  ClearBackground
  BeginMode2D(camera)      world space: the map, the hover outline
  EndMode2D
  the UI, in screen space: the HUD, the palette, the notices
EndDrawing
```

- **World first, then the UI.** Anything drawn between `BeginMode2D` and
  `EndMode2D` is in world units (a tile is `MapRenderer.TileSize` = 16) and
  moves with the camera. The HUD and the palette are drawn after
  `EndMode2D`, in screen pixels.
- **Updates before the draw.** `GameLoop.Update` reads input and runs the
  ticks that are due; `Draw` only reads state. Rendering never writes the
  simulation's state.
- **Don't allocate in the frame loop.** `Rectangle`, `Vector2`, and
  `Color` are structs, so they're free. A new `List`, an array, a LINQ
  query, or a closure per tile is not. Reuse buffers held in fields.
- **No blocking.** No `Thread.Sleep`, no waiting on a file or a task in the
  loop. The window must keep drawing.

## 5. The map and the tile sheet

The map is drawn from `Assets/tiles.png`, a sheet of 16-pixel tiles 16
across. The rule in `AGENTS.md` is that the map is drawn from the sheet,
not with shapes.

- **One table.** Every sheet index lives in `Rendering/TileAtlas.cs`, and
  `Assets/README.md` lists them. `TileAtlas.IndexFor(state, roads, x, y)`
  picks the drawing, and `TileAtlas.SourceFor(index)` gives the source
  rectangle. The art is Micropolis' until the owner replaces it, so keep
  sheet knowledge in that table and nowhere else.
- **One draw per tile.** `DrawTexturePro(sheet, source, destination,
  Vector2.Zero, 0f, Color.White)`, with the destination at whole-tile world
  coordinates. `Textures/SrcRecDstRec.cs` shows source and destination
  rectangles.
- **Only what the camera sees.** `MapRenderer.VisibleRange` clips the
  loops to the visible tiles.
- **Pixel art stays sharp.** `GameAssets` sets `TextureFilter.Point` on
  every texture. Keep it for anything drawn from the sheet. Bilinear
  filtering blurs 16-pixel art and bleeds neighbouring tiles at the edges.
- **A missing sheet is not a crash.** `GameAssets.TileSheet` is null when
  the file is missing, and the renderer falls back to the flat colours in
  `Theme`. Keep that path working when you change the renderer.

### Render textures (the chunk cache)

The map is drawn once into `RenderTexture2D` chunks of 32 by 32 tiles
and reused each frame, in `Rendering/MapChunkCache.cs`.
`Core/RenderTexture.cs` and `Core/SmoothPixelperfect.cs` are the
references. What it does, and what anything like it must do:

- `LoadRenderTexture(w, h)` after `InitWindow`; check
  `IsRenderTextureValid`; `UnloadRenderTexture` before `CloseWindow`.
- Draw into it between `BeginTextureMode(target)` and `EndTextureMode()`,
  outside `BeginDrawing` or before the `BeginMode2D` of the frame. Not
  inside `BeginMode2D`, or the camera transform applies to it too.
- **It is upside down.** OpenGL's render targets are flipped, so draw one
  with a negative source height:
  `new Rectangle(0, 0, target.Texture.Width, -target.Texture.Height)`.
- Set `TextureFilter.Point` on `target.Texture` as well, or the cached map
  blurs when the camera zooms.
- Redraw a chunk only when one of its tiles changes, and in step with the
  map: a chunk that is redrawn late shows the old city. The cache listens
  to `TileMap.TileChanged`, and `GameLoop.Draw` calls
  `MapRenderer.Refresh()` before `BeginDrawing`.
- If a tile's drawing depends on its neighbours, as a road's does, a
  change must stale the neighbours' chunks too.
- Keep a fallback. If `IsRenderTextureValid` fails, log it and draw tile
  by tile through the same `MapRenderer.DrawTile`.
- Prove a change to the cache by comparing it with the fallback: take the
  same screenshot both ways (force `TryCreate` to return null for the
  baseline, then restore it) and diff the map area. Identical pixels is
  the bar.

## 6. The camera

`Rendering/Camera.cs` wraps `Camera2D`: `ToRaylib()` for `BeginMode2D`,
`ScreenToWorld` for the mouse, `ZoomAt` to zoom toward the pointer, and
`ClampTo` to keep the view on the map. `Core/Camera2dMouseZoom.cs` is the
reference for zooming toward the pointer: set the offset to the mouse,
set the target to the world point under it, then change the zoom.

To find the tile under the mouse, use
`MapRenderer.TryGetTileAt(map, camera.ScreenToWorld(mouse), ...)`. It
reports when the pointer is off the map, and an off-map tile must never
reach the builder.

## 7. Text and the UI

- The UI uses raylib's default font through `DrawText` and `MeasureText`.
  Lay text out from `MeasureText`, not from guessed widths. The tool
  palette once overlapped its prices with its labels ("Residenti$5")
  because it used a fixed-width column; it now sizes itself from the
  measured widths (`ToolPaletteView.Bounds`). Do the same for any panel.
- A panel that takes clicks must keep them off the map under it.
  `GameLoop.HandleStroke` asks `ToolPaletteView.Contains` before it starts
  a stroke.
- Colours and sizes for the UI live in `Rendering/Theme.cs`, not in the
  draw code.
- `Text/` in the examples covers fonts (`FontLoading.cs`) and measuring
  (`RectangleBounds.cs`). A custom font is an asset: load it once after
  `InitWindow`, check `IsFontValid`, and credit it in `Assets/README.md`.

## 8. Input

- Raylib polls input **once per frame**. `IsMouseButtonPressed` is true
  only on the frame the button went down, `IsMouseButtonDown` while it is
  held, and `IsMouseButtonReleased` on the frame it came up. Building
  tools use all three, through a stroke: `Pressed` starts a
  `BuildStroke`, `Down` drags it, and the first frame the button is no
  longer down applies it (`GameLoop.HandleStroke`). Don't apply work on
  `Down` frame by frame; that was the old painting, and it had no preview.
- `IsKeyPressed` for toggles (pause, speed, tools), `IsKeyDown` for held
  movement (panning).
- Escape: raylib's default exit key would make `WindowShouldClose()` true,
  so `WindowedGame` calls `SetExitKey(KeyboardKey.Null)` and `GameLoop`
  handles Escape itself: it cancels a stroke in progress, and quits when
  there is none.
- Input crosses a boundary: check a mouse position against the map before
  using it as a tile.

## 9. The examples, by subject

At tag `v8.1.0`, under `Examples/`:

| You are writing | Read |
|---|---|
| The window, the loop, shutdown | `Core/BasicWindow.cs`, `Core/WindowFlags.cs`, `Core/WindowShouldClose.cs` |
| The 2D camera, pan and zoom | `Core/Camera2dDemo.cs`, `Core/Camera2dMouseZoom.cs`, `Core/WorldScreen.cs` |
| Drawing the map into a cached texture | `Core/RenderTexture.cs`, `Core/SmoothPixelperfect.cs`, `Textures/FramebufferRendering.cs` |
| Drawing tiles and sprites from a sheet | `Textures/SrcRecDstRec.cs`, `Textures/SpriteAnim.cs`, `Textures/DrawTiled.cs` |
| Animated tiles (traffic, later) | `Textures/SpriteAnim.cs`, `Textures/SpriteExplosion.cs` |
| Mouse and keyboard | `Core/InputMouse.cs`, `Core/InputMouseWheel.cs`, `Core/InputKeys.cs` |
| Clipping a panel | `Core/ScissorTest.cs` |
| Text, fonts, measuring | `Text/FontLoading.cs`, `Text/RectangleBounds.cs`, `Text/RaylibFonts.cs` |
| Overlays, blending | `Textures/BlendModes.cs`, `Textures/FogOfWar.cs` |
| Sound (later) | `Audio/SoundLoading.cs`, `Audio/MusicStreamDemo.cs` |

## 10. Checking a visual change

`AGENTS.md` sets the bar: a change to what the player sees is checked by
running the game and looking at it. Whether it *plays* well is the owner's
call, not yours.

- **Never drive the owner's desktop.** Don't send input to their real
  display (`:0`, Wayland). Use a private virtual display.
- **The recipe.** Write a script that starts the game, drives it with
  `xdotool`, screenshots with ImageMagick's `import`, and stops the game.
  Then run the script under `xvfb-run`:

  ```sh
  xvfb-run -a -s "-screen 0 1280x720x24" ./drive.sh <scratch>
  ```

  Inside the script: `dotnet run --no-build --project CityLimits &`,
  `sleep 6`, find the window with `xdotool search --name CityLimits`,
  pick a tool with `xdotool key 2`, draw, then
  `import -window root <scratch>/shot.png` and `kill` the game. Killing it
  skips `SaveSettings`, so the run leaves `settings.json` alone.
- **Hold every press across frames.** Raylib compares input between
  frames, so a press and a release within one frame are never seen: an
  instant `xdotool click` builds nothing, and an instant `xdotool key`
  doesn't pick a tool or pause. Hold them:
  `xdotool keydown 2; sleep 0.08; xdotool keyup 2`, and for the mouse
  `mousedown 1`, move in small steps with `sleep 0.03` between them, then
  `mouseup 1`. A step that seems to do nothing is usually this.
- **Look closely.** Crop and enlarge the part that changed
  (`magick shot.png -crop WxH+X+Y -filter point -resize 300% zoom.png`),
  then read the image. A full screenshot at 1280 by 720 hides 16-pixel
  mistakes.
- **Report what you saw, and what you didn't.** "A crossroads, two
  T-junctions, and corners drew with the right tiles; not play-tested" is
  a good report. "Builds" is not a visual check.

## 11. Pitfalls

- **A black window:** a frame never reached `EndDrawing`. Check the
  bracketing before anything else.
- **A blank or white texture:** it was loaded before `InitWindow`, or
  after `CloseWindow`, or the file is missing. Check `IsTextureValid`.
- **A cached map upside down:** a render texture drawn without the
  negative source height.
- **A blurry or seamed map:** a texture without `TextureFilter.Point`, or
  tiles drawn at fractional positions.
- **The UI moving with the map:** it was drawn inside `BeginMode2D`.
- **A stutter as the city grows:** allocation or texture loading in the
  frame loop, or drawing tiles the camera can't see.
- **A crash in `--headless`:** a Raylib call reached the headless path.
- **Raylib's own log lines:** the native library prints `INFO:` lines to
  stdout when the window opens. They aren't NLog output, and they never
  appear in `--headless`, which doesn't load raylib.
