# Modding

> **Status:** the first pieces of modding support. A mod can change a
> zone's name, price, population per building level, house limit, and
> where its trips go today. More of the game becomes data as it is built, and each piece
> that does is listed here.

CityLimits is built to be modded. The game's rules live in definitions:
data, not code. The base game's definitions are Micropolis' numbers, and a
mod changes them. A mod is plain JSON, so it is safe to share, it can't
crash the game, and a city built with the same mods, seed, and work is the
same city every time.

## A mod's folder

Mods live in the `Mods/` folder beside the game's executable. Each mod is a
folder named for its id:

```text
Mods/
  cheap-commerce/
    mod.json       who the mod is (required)
    zones.json     what it changes in the zones (optional)
```

An id is lowercase letters, digits, and single hyphens, such as
`cheap-commerce`, and the folder's name must be the id.

### `mod.json`

```json
{
  "format": 1,
  "id": "cheap-commerce",
  "name": "Cheap commerce",
  "version": "1.0",
  "description": "Commercial zones cost $40 and are called markets."
}
```

| Field | Required | Meaning |
|---|---|---|
| `format` | yes | The definitions format. This version reads `1` |
| `id` | yes | The mod's id, the same as its folder's name |
| `name` | yes | The mod's name, 1 to 80 characters |
| `version` | no | The mod's own version, as you number it, up to 32 characters |
| `description` | no | What the mod does, up to 500 characters |

### `zones.json`

```json
{
  "format": 1,
  "zones": [
    { "id": "commercial", "name": "Market", "price": 40 }
  ]
}
```

Each entry names a zone by its id, `residential`, `commercial`, or
`industrial`, and changes only the fields it sets. A field left out keeps
the value it had.

| Field | Meaning |
|---|---|
| `id` | The zone to change: `residential`, `commercial`, or `industrial` |
| `name` | The zone's name, as the tile readout shows it, 1 to 80 characters |
| `price` | What one 3 by 3 zone costs to lay, from 0 to 1 000 000 |
| `populations` | The population of the zone's building at each level, from level 1 up, each from 0 to 100 000. It must have as many levels as the base game's zone: 4 residential, 5 commercial, 4 industrial |
| `houses` | How many single houses a residential zone grows before its first building, from 0 to 8, each counting one person; only a residential zone can have houses |
| `trips` | The kinds of zone a trip from this zone looks for, from `residential`, `commercial`, and `industrial`, each at most once. A residential zone counts as a place to go only once it has houses or a building. The base game sends residential trips to commercial and industrial zones, commercial trips to all three, and industrial trips to residential zones |

The number of building levels and the land-value classes (`valueClasses`)
can't be changed by a mod, because each level and class needs its own
drawings in the tile sheet.

## How mods are applied

- **In id order.** Mods apply one after another, sorted by id, each on top
  of the last. When two mods change the same field, the later id wins.
- **Checked first.** Every file is checked before it is used, as scenarios
  are: an unknown field is refused, so a misspelt one can't silently do
  nothing, and every number is range-checked. Files over 1 MiB are
  refused, and at most 64 mods are read.
- **Whole or not at all.** A mod with any problem is skipped entirely, never
  half-applied. The game carries on with the base game and the other mods.
  Each skipped mod is written to the log with the reason and the file it is
  in, and the game shows it on screen when it starts.
- **The game and `--headless` use mods.** The log lists the mods applied,
  in order.
- **Scenarios don't.** A scenario always runs on the base game's
  definitions, so a scenario gives the same result on every machine,
  whatever is in `Mods/`.

## For developers

The definitions are `CityLimits.Core/Definitions/`. The base game's are
`BaseGame.Json`, written in the same format a mod uses and read by the
same `DefinitionLoader`, so the base game goes through the mod path every
time it starts. `ModLoader.Load` reads a mods folder and applies it to
`CityDefinitions.Base`, and a city keeps the `CityDefinitions` it was made
with (`CitySimulation.Definitions`).

To make something moddable: put it in a spec, give it a field in the
definitions format, check the field in `DefinitionLoader`, let a mod's
patch change it, and list it in this document. Add a test that a mod can
change it, and one that a bad value is refused.
