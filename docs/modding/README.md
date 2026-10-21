# Modding CityLimits

You can change how CityLimits plays without touching its code. A mod is a
small folder of text files that you drop beside the game. This page
explains what a mod is, how to install one, and how to write your first.

## What a mod can do today

CityLimits keeps its rules as data: what a zone costs, how many people a
building holds, how many tiles a power plant can power, and so on. The
base game's numbers come from Micropolis, the open-source version of the
original SimCity. A mod changes some of those numbers, or the names the
game shows.

Today every mod is a **tuning mod**: it adjusts things the game already
has, such as cheaper zones, stronger power plants, or a police station
called a sheriff's office. It can't add new kinds of buildings or change
how the city works. [Tuning mods](./tuning.md) lists everything you can
change and the limits on each value.

Mods are plain JSON, so they're safe to share: a mod can't run code, and a
broken mod can't crash the game.

## Installing a mod

1. Find the folder the game runs from, the one holding the `CityLimits`
   program and its `Assets` folder.
2. Make a folder called `Mods` there, if there isn't one.
3. Put the mod's folder inside `Mods`. Each mod is one folder, named for
   the mod:

   ```text
   Mods/
     cheap-commerce/
       mod.json
       zones.json
   ```

4. Start the game. It reads every mod in `Mods` when it starts, and says
   on screen if any were skipped.

To remove a mod, delete its folder.

## Writing your first mod

This mod makes commercial zones cost $40 and calls them markets.

1. In `Mods`, make a folder called `cheap-commerce`.
2. In it, make a file called `mod.json` that says who the mod is:

   ```json
   {
     "format": 1,
     "id": "cheap-commerce",
     "name": "Cheap commerce",
     "version": "1.0",
     "description": "Commercial zones cost $40 and are called markets."
   }
   ```

3. Beside it, make `zones.json` with the change:

   ```json
   {
     "format": 1,
     "zones": [
       { "id": "commercial", "name": "Market", "price": 40 }
     ]
   }
   ```

4. Start the game. The palette shows commercial zones at $40, and the tile
   readout calls them markets.

A mod only lists what it changes. Everything it leaves out keeps the base
game's value, so a small mod stays small.

## `mod.json`

Every mod needs one. It names the mod; the changes go in the other files.

| Field | Required | Meaning |
|---|---|---|
| `format` | yes | The version of the mod format. This version of the game reads `1` |
| `id` | yes | The mod's id: lowercase letters, digits, and single hyphens, such as `cheap-commerce`. The mod's folder must have the same name |
| `name` | yes | The mod's name, 1 to 80 characters |
| `version` | no | Your own version number for the mod, up to 32 characters |
| `description` | no | What the mod does, up to 500 characters |

## How mods are applied

- **In order of id.** Mods apply one after another, sorted by id, each on
  top of the last. When two mods change the same value, the one whose id
  comes later wins.
- **Checked first.** Every file is checked before the game uses it. A
  misspelt or unknown field is refused, rather than quietly doing nothing,
  and every number must be within its limits. Files over 1 MiB are
  refused, and at most 64 mods are read.
- **Whole or not at all.** A mod with any problem is skipped entirely,
  never half-applied, and the game carries on with the rest. The game
  shows which mod was skipped when it starts, and the log says why and in
  which file, as in
  `zones.json: zones[0].price: needs a number from 0 to 1000000, not -5.`
- **A city keeps its mods.** A city runs on the mods it started with. The
  same mods, the same map seed, and the same building give the same city.
- **Scenarios don't use mods.** A scenario always runs on the base game, so
  it gives the same result on every machine, whatever is in `Mods`. See
  [Scenarios](../scenarios.md).

## For developers

The definitions live in `CityLimits.Core/Definitions/`:

- `BaseGame.Json` is the base game's definitions, written in the same
  format a mod uses and read by the same `DefinitionLoader`, so the mod
  path runs every time the game starts. It lives in code, so it can't go
  missing.
- `DefinitionLoader` reads and checks the base game's definitions and each
  mod's files, reporting the first problem with where it is.
- `ModLoader.Load` reads a mods folder, applies each valid mod in id order
  to `CityDefinitions.Base`, and returns the problems it found.
- `CityDefinitions` holds the specs a city runs on (`ZoneSpec`,
  `PlantSpec`, `ServiceSpec`); a city keeps the definitions it was made
  with (`CitySimulation.Definitions`).
- The game reads `Mods/` beside its executable (`Game/GameMods.cs`), and
  both the window and `--headless` use it; `ScenarioRunner` never does.

To make something moddable:

1. Put the value in a spec instead of a constant.
2. Give it a field in the definitions format, in `BaseGame.Json` and in
   the mod file it belongs to.
3. Check the field in `DefinitionLoader`, with a range and a message that
   says where the problem is.
4. Let the mod's patch change it, so a mod sets only what it lists.
5. Add tests: that a mod can change it, and that a bad value is refused
   with the right message.
6. Document it in [Tuning mods](./tuning.md), in words a player can
   follow.
