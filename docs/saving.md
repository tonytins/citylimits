# Saving cities

CityLimits saves a city to a file and loads it back exactly: a loaded city
runs on, step for step, as if it had never stopped. This page describes
where saves go, how the game saves and loads them, and the flat city
format, `.fcl`, which a person or an agent can read and compare.

## Where saves go

Saved cities are files in `Documents/CityLimits/Cities`, or in a
`Cities` folder beside the game when the game is portable (see
`GameFolders` in `CityLimits.Core/Settings/`). A city is saved under its
own name, such as `Joffburg.fcl`; characters a file name can't hold
become underscores (`CityFiles.FileNameFor`). There is no file dialog:
the game lists the folder's cities itself, newest first.

## Saving and loading in the game

- **Ctrl+S** saves the city under its name. Saving over the file it was
  opened from or last saved to is plain saving; replacing another city's
  file of the same name asks first.
- **Ctrl+O**, or **Load Game** on the start menu, lists the saved cities
  with their date, population, money, and when they were saved. Load Game
  is dimmed while there are none. A city that can't be read says why in
  the list.
- **Leaving a city** for the menu, for another city, or to quit asks
  first when the city has changes since it was last saved. A city has
  changes when its save would read differently from the last one.
- A save is written to a temporary file first and then takes the file's
  place, so a failure partway leaves the save before it as it was.

## What a save holds

Everything that affects the steps to come, so a save can be made at any
step, even partway through the simulation's 16-step cycle or while the
game is paused:

- the name, the seed, and the simulation's random generator, as 16
  hexadecimal digits;
- the step count and the calendar's count, which must agree: the count
  is the steps divided by 16;
- the money, and the budget: the tax rate, the funding levels and their
  effects, the year's taxes and upkeep set aside so far, last year, and
  the latest estimate;
- the definitions the city runs on, in the same format as the base game
  and mods ([modding](./modding/README.md)), with the mods that made them.
  A city keeps them, so it loads the same even if the mods folder has
  changed since;
- the map, as described below;
- the census and demand with their histories, the power grid's last trace
  and this cycle's count, police and fire coverage, hospitals and
  churches, land value, population density, the terrain's worth, and the
  commercial rate, each as the simulation last left it.

The road network is not saved; it is worked out again from the map.

## The flat city format

A `.fcl` file is one JSON object. Its fields are in `camelCase`, and
arrays of numbers sit on one line each.

| Field | What it is |
|---|---|
| `format` | The format's version: `1` |
| `name`, `seed`, `random` | The city's name, its seed, and the generator state |
| `tick`, `cityTime` | The steps run, and the calendar's count |
| `funds` | The money, which may be below zero |
| `definitions`, `mods` | The definitions, in the definitions format, and the mods applied, in order |
| `map` | The map: see below |
| `budget`, `census`, `demand`, `power`, `services`, `community`, `cityMaps` | Each part of the simulation's state |
| `cycle` | The road tiles counted this cycle, and what the last count set aside for the budget |

### The map

The map is written as rows of characters, one string per row, a layer for
each part of a tile, so it can be read and compared:

- `ground`: `.` grass, `~` water, `T` trees, `#` rubble.
- `use`: `.` nothing, `=` road, `R` `C` `I` residential, commercial,
  industrial, `c` coal plant, `n` nuclear plant, `+` power line, `x` a
  road across a power line, `y` a road along one, `F` fire station, `P`
  police station, `k` park, `H` hospital, `U` church.
- `part`: for a tile of a building, which part of its footprint it is, as
  a hexadecimal digit counted across and then down from the top-left
  (`4` is a 3 by 3 building's centre, `5` a 4 by 4 one's); `.` for any
  other tile.

What only some tiles hold is listed beside the layers: `buildings`, one
entry for each building's centre with its `development`, `valueClass`,
and `powered`; and `houses`, one for each house on a residential zone's
outer tile.

## Reading is strict

A save comes from outside the game, so loading checks all of it and
refuses the file on the first problem, saying what and where, such as
`map at 5, 3: '?' isn't a kind of ground.`:

- a file over 16 MB, a field the format doesn't have, or one it needs
  that is missing;
- another format version, a name a city can't have, or a generator state
  that isn't 16 hexadecimal digits;
- a map over 1024 tiles either way, rows of the wrong length, an unknown
  character, a building whose footprint isn't whole, or a building level
  or land-value class its zone's spec doesn't have;
- definitions the definitions loader refuses, and any number out of its
  range or array of the wrong length in the simulation's state.

A city that can't be loaded is never half loaded: the game keeps what it
was doing and shows the reason.

## For contributors

- `CityLimits.Core/Saving/` holds the format: `CitySave` is the save model
  and the shape of the file, `FlatCityFormat` writes and reads it,
  `MapCodec` handles the map's layers, and `CityFiles` names the files and
  lists a folder.
- Each part of the simulation captures and restores its own state with
  internal `Capture` and `Restore` methods, and checks what it restores.
- **New state goes in the save.** A field that lasts from one step to the
  next is captured and restored, or a loaded city drifts from the one
  that was saved. `FlatCityFormatTests` saves a busy town at several
  steps, loads it, and runs both in step for two years, comparing their
  saves as they go; it catches state left out of the save, as long as
  the state changes what the city does.
- The binary format, `.cl`, autosave, and importing Micropolis' `.cty`
  are later phases on the [roadmap](./ROADMAP.md).
