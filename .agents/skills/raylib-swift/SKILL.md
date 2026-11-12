---
name: raylib-swift
description: Use raylib-swift from Swift for window setup, drawing, input, cameras, textures, fonts, and audio. Load when writing, reviewing, or debugging Swift code that imports Raylib or uses raylib resources.
---

# Using raylib-swift

[raylib-swift](https://github.com/tonytins/raylib-swift) exposes raylib's C API to Swift and adds Swift conveniences. Follow the
repository's own instructions first, then use this guide for raylib-specific behavior.
Check the version actually resolved by the package before relying on an API: raylib's
documentation and default branch may describe a different release.

## Check the API that Swift sees

After resolving or building the package, inspect its checkout under
`.build/checkouts/raylib-swift/`. Find the raylib version there, then verify unfamiliar
functions against that version's header and examples. The C header is not always the
exact Swift interface:

- The Clang module's API notes can rename enum cases and flags. Use the Swift spelling,
  such as `.leftShift` or `.windowResizable`, rather than the C spelling.
- Generated overloads may accept Swift `Int` where raylib's C declaration accepts `int`.
- Colors and other Swift conveniences may be declared in generated source files.

Read the matching-version raylib C example for the closest task when useful. Treat it as
a behavior reference; write code that fits the Swift package rather than mechanically
translating the C example.

### Common Swift shapes

- Construct C structs with labeled fields, such as
  `Rectangle(x: 0, y: 0, width: 10, height: 10)` or `Rectangle()` for zero values.
- raylib functions are global, for example `DrawText` and `IsKeyDown`; there is no
  `Raylib.` prefix.
- Convert integer-only raylib results to `Int` when combining them with Swift integers.
  Functions such as `GetScreenWidth`, `GetCharPressed`, and `GetFPS` may return `Int32`.
  Avoid mixing `Int` and `Int32` in one overload call.
- Use imported enum cases and typed flag sets, for example
  `IsKeyPressed(.escape)` and `SetConfigFlags([.windowResizable, .vsyncHint])`.
  Imported C enums can gain cases, so include `@unknown default` in exhaustive switches.
- Vectors support arithmetic operators and common values such as `.zero`; raymath is
  available when the package includes it.
- Swift strings can be passed to C string parameters. Copy C string results immediately
  with `String(cString:)` if the library may reuse the returned buffer.
- For pointer parameters, pass an in-scope mutable value by address when the imported
  signature permits it, for example `ImageDrawPixel(&image, x, y, .red)`.

## Window and resource lifetime

Create the graphics context before loading GPU resources, and release those resources
before closing the window:

1. Set window flags before creating the window.
2. Call `InitWindow`.
3. Configure frame timing and other window state.
4. Load textures, fonts, render textures, shaders, and models once, after initialization.
5. Run the frame loop.
6. Unload every resource owned by the application, then call `CloseWindow`.

Make ownership explicit for GPU-backed values. Do not rely on `deinit` to unload them:
deinitialization order may occur after the graphics context is gone. Check the matching
release function and keep each resource alive only while its context is valid.

## Frame structure and input

Bracket drawing every frame so raylib presents the back buffer:

```text
BeginDrawing
  ClearBackground
  BeginMode2D(camera) or BeginMode3D(camera)  optional world drawing
  EndMode2D or EndMode3D
  screen-space interface drawing
EndDrawing
```

Draw world content inside the camera mode and screen-space controls after it. Update
application state and read input before drawing; keep rendering focused on displaying
state rather than changing simulation or application data.

raylib samples input once per frame:

- Use `IsMouseButtonPressed` and `IsKeyPressed` for one-time actions.
- Use `IsMouseButtonDown` and `IsKeyDown` for held actions such as dragging or panning.
- A drag usually starts on a press, follows while held, and ends when the button is no
  longer down.
- `GetCharPressed` is a queue. Read it until it returns `0` to consume all typed input.
- Escape is raylib's default exit key. Call `SetExitKey(.null)` when the application
  needs to handle Escape without requesting window closure.

Avoid blocking work and unnecessary allocation in the frame loop. Cache derived text or
geometry when its inputs change, and reuse buffers when creating per-frame collections
would be costly.

## Cameras and coordinates

Use `BeginMode2D` or `BeginMode3D` only around world-space drawing. Screen-space UI should
not inherit the world camera transform. Convert pointer coordinates explicitly when
using them to select or modify world objects, and validate converted positions against
the world bounds before indexing or changing state.

raylib also exports a camera type named `Camera`. If a module defines its own camera
abstraction, qualify the intended type where imports make the name ambiguous.

## Textures, models, and render textures

- Load graphics resources after `InitWindow`, validate them with the validity function
  available in the resolved raylib version, and unload them before `CloseWindow`.
- Treat missing or invalid assets as reportable errors. Use a visible fallback where
  that is appropriate instead of dereferencing an invalid resource.
- Use `DrawTexturePro` with explicit source and destination rectangles when drawing
  regions or scaling. Keep pixel-art destinations aligned to whole pixels and use point
  filtering when sharp pixels are intended.
- Clip large draw loops to visible content when practical.
- A failed model load may produce an empty model; check its mesh data before use. Assign
  textures to the material used by the mesh rather than assuming material zero.
- OpenGL render textures are vertically flipped when drawn. Use a negative source height
  when displaying their color texture. Validate render targets and release them before
  closing the window.
- Use `MeasureText` to lay out text with raylib's default font. Font measurement requires
  the initialized graphics context, so keep such tests separate from headless logic tests.
- Load custom fonts once after window initialization and verify that the font is valid.

## Examples and visual verification

Use examples shipped with the resolved raylib version as a reference for window lifecycle,
camera movement, input, rendering, text, textures, clipping, and logging. Prefer the
example closest to the feature being changed.

For visual changes, run the project's own demo or screenshot mode when available. If a
window must be driven for automated checking, use a private virtual display rather than
the user's active desktop. Inspect the resulting image and report what was actually
visible and interacted with; a successful build alone does not verify appearance.

## Common failures

- A black window usually means drawing did not reach `EndDrawing`.
- A blank texture often indicates loading before `InitWindow`, after `CloseWindow`, or
  from a missing file.
- A blurry render target or sprite may be using the wrong filter or fractional pixels.
- UI that moves with the world is being drawn inside a camera mode.
- A compile error about a missing enum case may mean C spellings were used instead of
  names from the Swift API notes.
- An overload error can come from mixing `Int` and `Int32`; convert one side explicitly.
- A camera member error can be caused by Swift resolving raylib's `Camera` instead of a
  project-defined camera type.
