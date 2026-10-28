# Modding CityLimits

You can change how CityLimits plays without touching its code. A mod is a
small folder of text files that you drop into your Documents folder. This page
explains what a mod is, how to install one, and how to write your first.

## What a mod can do today

CityLimits keeps its rules as data: what a zone costs, how many people a
building holds, how many tiles a power plant can power, how far a fire
station reaches, what a road costs, how much money a new city starts
with, what the city's ordinances cost and do, and so on. The
base game's numbers come from Micropolis, the open-source version of the
original SimCity. A mod changes some of those numbers, or the names the
game shows.

Today every mod is a **tuning mod**: it adjusts things the game already
has, such as cheaper zones, stronger power plants, cheaper roads, or a
police station called a sheriff's office. It can't add new kinds of buildings or change
how the city works. [Tuning mods](./tuning.md) lists everything you can
change and the limits on each value.

Mods are plain JSON, so they're safe to share: a mod can't run code, and a
broken mod can't crash the game.

## Installing a mod

1. Find the `Mods` folder. The game makes it the first time it opens:

   - **Linux:** `~/Documents/CityLimits/Mods` (your Documents folder may
     have another name if your desktop uses another language)
   - **Windows:** `Documents\CityLimits\Mods`
   - **A portable copy** (one that keeps a `settings.json` beside the
     `CityLimits` program): the `Mods` folder beside the program

2. Put the mod's folder inside `Mods`. Each mod is one folder, named for
   the mod, with its `mod.json` at the top and its other files anywhere
   within it:

   ```text
   Mods/
     cheap-commerce/
       mod.json
       zones.json
   ```

3. Start the game. It reads every mod in `Mods` when it starts, and says
   on screen if any were skipped.

Older versions of CityLimits read mods from a `Mods` folder beside the
program. Move any mods you had there into the new folder.

To remove a mod, delete its folder.

## Writing your first mod

This mod makes commercial zones cost $40 and calls them markets.

1. In `Mods`, make a folder called `cheap-commerce`.
2. In it, make a file called `mod.json` that says who the mod is:

   ```json
   {
     "$schema": "https://raw.githubusercontent.com/tonytins/citylimits/main/docs/schemas/mod.v2.2.schema.json",
     "format": "2.2",
     "id": "cheap-commerce",
     "name": "Cheap commerce",
     "version": "1.0",
     "description": "Commercial zones cost $40 and are called markets."
   }
   ```

3. Beside it, make `zones.json` with the change:

   ```json
   {
     "$schema": "https://raw.githubusercontent.com/tonytins/citylimits/main/docs/schemas/zones.v2.2.schema.json",
     "format": "2.2",
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
| `$schema` | no | Where the file's description is, so an editor such as VS Code can check your file and suggest what can go in it. The game ignores it |
| `format` | yes | The version of the mod format, `"2.2"`. Mods written with `"2.0"` or `"2.1"`, and for CityLimits 0.4 with `"1.0"` or `1`, still work |
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
- **Found in folders.** A mod's spec files may sit in folders of their
  own within it, up to 8 deep, and are read in the order of their paths;
  at most 64 are read from one mod.
- **A city keeps its mods.** A city runs on the mods it started with. The
  same mods, the same map seed, and the same building give the same city.
- **Scenarios don't use mods.** A scenario always runs on the base game, so
  it gives the same result on every machine, whatever is in `Mods`. See
  [Scenarios](../scenarios.md).

## The base game's files

The base game keeps its own numbers in spec files too, in a folder called
`common` beside the settings:

- **Linux:** `~/.config/CityLimits/common`
- **Windows:** `%APPDATA%\CityLimits\common`
- **A portable copy:** the `common` folder beside the program

They are the same files a mod uses, with every value written out, so they
show exactly what the game plays with. You can change them too: the game
reads them when it starts, before any mod. But the game writes them again
each time a new version or build of it starts, so a change you make there
lasts only until you update. To keep a change, make it a mod.

If a file in `common` has a problem, the whole folder is skipped, the
game plays with its own numbers, and the log says why. Deleting the
folder is safe; the game writes it again the next time it starts with a
new version, and plays with its own numbers until then.

Scenarios don't read `common` either: they always run on the game's own
numbers.

## For developers

The definitions live in `CityLimits.Core/Definitions/`:

- `BaseGame` is the base game's definitions, in code, so they can't go
  missing: `BaseGame.Json`, the zones, plants, and services in the
  definitions format 1.0 and read by the same `DefinitionLoader` a mod
  is, and the facilities, infrastructure, and economy as specs.
- `DefinitionLoader` reads and checks the base game's definitions and
  every spec file (`SpecFile`), reporting the first problem with where it
  is. Each kind of file becomes a patch (`ZonePatch`, `ServicePatch`, and
  so on) that sets only what it lists, and a later file's patch goes on
  top of an earlier one's (`Then`).
- `SpecFolder` finds a folder's spec files, however deep, in path order.
- `BaseSpecFiles` writes the base game's spec files into `common/`
  (`Refresh`, once for each build, by the stamp in `version.txt`) and
  applies them to `CityDefinitions.Base` (`Load`), without counting them
  as a mod.
- `ModLoader.Load` reads a mods folder, applies each valid mod in id order
  on top of the definitions it is given, and returns the problems it
  found.
- `CityDefinitions` holds the specs a city runs on (`ZoneSpec`,
  `PlantSpec`, `ServiceSpec`, `FacilitySpec`, `InfrastructureSpec`,
  `EconomySpec`); a city keeps the definitions it was made with
  (`CitySimulation.Definitions`).
- The game reads the folders `GameFolders` works out
  (`Core/Settings/GameFolders.cs`, `Game/GameMods.cs`): `common/` in the
  config folder, then `Documents/CityLimits/Mods`, or both beside the
  executable in a portable copy. Both the window and `--headless` use
  them; `ScenarioRunner` and the challenges never do.

To make something moddable:

1. Put the value in a spec instead of a constant.
2. Give it a field in the spec file it belongs to, its base value in
   `BaseGame`, and a line in `BaseSpecFiles.Write`, so `common/` shows it.
3. Check the field in `DefinitionLoader`, with a range and a message that
   says where the problem is.
4. Let the mod's patch change it, so a mod sets only what it lists.
5. Add tests: that a mod can change it, and that a bad value is refused
   with the right message.
6. Document it in [Tuning mods](./tuning.md), in words a player can
   follow.
