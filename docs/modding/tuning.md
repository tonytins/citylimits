# Tuning mods

A tuning mod changes the numbers and names of things the game already
has: what they cost, how much they hold, what they're called. It can't add
new kinds of things or change how the city works. If you haven't made a
mod before, start with [Modding CityLimits](./README.md), which covers
installing a mod and writing `mod.json`.

A tuning mod can change three kinds of things, each in its own file
beside `mod.json`:

```text
Mods/
  my-mod/
    mod.json       who the mod is (required)
    zones.json     residential, commercial, and industrial zones (optional)
    plants.json    coal and nuclear power plants (optional)
    services.json  police and fire stations (optional)
```

Every file starts with `"format": 1` and lists only what it changes. A
value you leave out keeps the base game's, which are Micropolis'.

## `zones.json`

```json
{
  "format": 1,
  "zones": [
    { "id": "commercial", "name": "Market", "price": 40 },
    { "id": "residential", "houses": 4 }
  ]
}
```

Each entry names a zone by its id, `residential`, `commercial`, or
`industrial`.

| Field | What it changes | Limits |
|---|---|---|
| `id` | Which zone to change | `residential`, `commercial`, or `industrial` |
| `name` | The zone's name, as the tile readout shows it | 1 to 80 characters |
| `price` | What it costs to lay one 3 by 3 zone. The base game's is $100 | 0 to 1 000 000 |
| `populations` | How many people the zone's building holds at each level, from level 1 up | Each 0 to 100 000, and one for every level the zone has: 4 for residential, 5 for commercial, 4 for industrial |
| `houses` | How many single houses a residential zone grows before it builds its first building; each house holds one person. The base game's is 8 | 0 to 8, and only for `residential` |
| `trips` | The kinds of zone people from this zone travel to. A commercial zone whose trips find nowhere to go doesn't grow | Any of `residential`, `commercial`, and `industrial`, each once. The base game sends residents to commerce and industry, commerce to all three, and industry to residents |

A mod can't change how many building levels a zone has, or how many
styles its buildings come in (`valueClasses`), because each needs its own
drawings in the tile sheet.

## `plants.json`

```json
{
  "format": 1,
  "plants": [
    { "id": "coal", "name": "Coal works", "price": 2000, "capacity": 900 }
  ]
}
```

Each entry names a plant by its id, `coal` or `nuclear`.

| Field | What it changes | Limits |
|---|---|---|
| `id` | Which plant to change | `coal` or `nuclear` |
| `name` | The plant's name, as the tile readout shows it | 1 to 80 characters |
| `price` | What one plant costs to build. The base game's are $3,000 for coal and $5,000 for nuclear | 0 to 1 000 000 |
| `capacity` | How many tiles of the map one plant can power: lines, zones, and other buildings each count a tile at a time. The base game's are 700 for coal and 2 000 for nuclear. As in Micropolis, every plant on the map adds its capacity, whether it is connected or not | 1 to 1 000 000 |

## `services.json`

```json
{
  "format": 1,
  "services": [
    { "id": "police", "name": "Sheriff", "price": 300, "upkeep": 40 }
  ]
}
```

Each entry names a station by its id, `fire` or `police`.

| Field | What it changes | Limits |
|---|---|---|
| `id` | Which station to change | `fire` or `police` |
| `name` | The station's name, as the tile readout shows it | 1 to 80 characters |
| `price` | What one station costs to build. The base game's is $500 | 0 to 1 000 000 |
| `upkeep` | What one station costs the city each year at full funding, paid from taxes. The base game's is $100 | 0 to 1 000 000 |

## Mistakes the game catches

Each file must hold only its own kind of entry: a zone in `plants.json`,
or a plant in `zones.json`, is refused. So is a field the game doesn't
know, a value outside its limits, or an id listed twice. When that
happens the whole mod is skipped, not just the bad line, and the log says
which file and which value, so you can fix it and try again.
