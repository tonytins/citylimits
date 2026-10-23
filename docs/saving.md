# Saving cities

CityLimits saves a city to a file and loads it back exactly: a loaded city
runs on, step for step, as if it had never stopped. This page describes
where saves go, how the game saves and loads them, and the two formats:
the binary city format, `.cl`, the game's own save, and the flat city
format, `.fcl`, which a person or an agent can read and compare. Both
hold exactly the same city.

## Where saves go

Saved cities are files in `Documents/CityLimits/Cities`, or in a
`Cities` folder beside the game when the game is portable (see
`GameFolders` in `CityLimits.Core/Settings/`). A city is saved under its
own name, such as `Joffburg.cl`; characters a file name can't hold
become underscores (`CityFiles.FileNameFor`). A city opened from a `.fcl`
file is saved back to one. There is no file dialog:
the game lists the folder's cities itself, newest first.

## Saving and loading in the game

- **Ctrl+S** saves the city under its name. Saving over the file it was
  opened from or last saved to is plain saving; replacing another city's
  file of the same name asks first.
- **Ctrl+O**, or **Load Game** on the start menu, lists the saved cities
  of both formats with their date, population, money, and when they were
  saved, marking flat files. Load Game
  is dimmed while there are none. A city that can't be read says why in
  the list.
- **Leaving a city** for the menu, for another city, or to quit asks
  first when the city has changes since it was last saved. A city has
  changes when its save would read differently from the last one.
- A save is written to a temporary file first and then takes the file's
  place, so a failure partway leaves the save before it as it was.

## Autosave

A city saves itself every 15 minutes of play, counting only time it runs:
paused, or with a panel holding it, the clock stops. An autosave sits
beside the city's own file, in the same format, such as
`Joffburg (autosave).cl`, or `Joffburg (autosave).fcl` for a city saved
as `.fcl`, so a newer save's parts it keeps go into its autosaves too.
It is skipped when the city is as it was last saved, by the player or by
an autosave. The news ticker says when one is written.

- **Off, one slot, or two.** The Gameplay tab of the settings sets
  `AutosaveSlots` in `settings.json`: `1` by default, `0` for no
  autosaves, or `2`: with two, `Joffburg (autosave 2).cl` joins the
  first, and each autosave replaces the older of the two, so the one
  before is still there if the last went wrong. A version from before
  0.3.109 reads `0` as out of range, warns, and keeps one.
- **Opening one.** Load Game marks autosaves. Saving a city opened from
  its autosave goes to its own file, `Joffburg.cl`, without asking.
- **It isn't the player saving.** Leaving a city still asks when it has
  changes since the player last saved it.
- `Autosave` in `CityLimits.Core/Saving/` decides when one is due and
  where it goes; `AutosaveTests` covers both.

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
- the traffic on the roads, from format 1.1, and the traffic each road
  shows and the year's traffic average, from format 1.3;
- the census and demand with their histories, the power grid's last trace
  and this cycle's count, police and fire coverage, hospitals and
  churches, land value, population density, the terrain's worth, and the
  commercial rate, each as the simulation last left it.

The road network is not saved; it is worked out again from the map.

## The binary city format

A `.cl` file follows the pattern of Micropolis' city files, big-endian
numbers and the map as one 16-bit word a tile, column by column, but it
holds CityLimits' own state. It doesn't open in Micropolis, and isn't
meant to. At about 47 KB for a 120 by 100 city, it is around 60% the
size of the same city as `.fcl`.

- **The header:** the six bytes `CLCITY`, then the format's major version
  as a 16-bit number: `1`. The minor version is in the `VERS` chunk, so
  files from before minor versions, which have none, stay valid as 1.0.
- **The chunks**, one after another to the end of the file: a four-letter
  tag, the payload's length as a 32-bit number, the payload, and a
  CRC-32 of the tag and the payload.

| Tag | What it holds |
|---|---|
| `VERS` | The format's minor version, as a 16-bit number; written first, and read as 0 when it is missing |
| `CITY` | The name, the seed, the generator, the steps, the calendar's count, and the money |
| `DEFS` | The definitions, as definitions-format JSON text, and the mods applied |
| `MAP ` | The width and the height, then a word a tile |
| `BUDG`, `CENS`, `DEMD`, `POWR`, `SERV`, `COMM`, `CMAP`, `CYCL` | The budget, the census, demand, power, services, hospitals and churches, the city maps, and the cycle's counts |
| `TRAF` | The traffic on each 2 by 2 block; from format 1.1 |
| `PART` | The parts list (see Opening a newer save); from format 1.2, written after `VERS` |
| `RTRF` | The traffic each road shows, and the year's average; from format 1.3 |
| `RAIL` | The rail on the map, and the scan's count of it; from format 1.4 |
| `POLL`, `CRIM` | The pollution and the crime on each 2 by 2 block, and each one's average; from format 1.5 |
| `FIRE` | The fires on the map, and the holes they left in buildings; from format 1.6 |
| `FLOD` | The floods on the map, and how long the water spreads for; from format 1.7 |

Each tile's word holds, from its lowest bit: the ground (2 bits), the use
(4 bits), the footprint part (4 bits), power (1 bit), and 5 bits more: a
building centre's level (3 bits) and land-value class (2 bits), or a house
tile's house. The ground, uses, and parts are numbered as below.

The other chunks hold their section of the save model field by field, in
the order the model declares them, which is the order the flat format
writes them in: whole numbers as variable-length zigzag integers (seven
bits a byte), yes-or-no as a byte, text as UTF-8 after its length, and a
list after its count. A section, whether a field of its own or an item in
a list, starts with a byte that says whether it is there (1) or missing
(0), and then holds its fields. Since both formats write the same model,
a field added to it is in both.

A chunk this version doesn't know is skipped, and the game notes it in
the log, so a later version can add one. A known chunk that is damaged,
missing, or there twice refuses the whole file, since a city needs all of
them to run on exactly.

## Importing Micropolis cities

CityLimits imports the city files of Micropolis and SimCity Classic,
`.cty`, and never writes them. Drop a `.cty` file into the Cities folder
and Load Game lists it as a Micropolis city; opening it imports it, and
Ctrl+S then saves it as a CityLimits city, `<name>.cl`, asking first if
one is already there. Leaving an imported city before saving it asks
first.

A `.cty` file is 27,120 bytes, or 27,248 with the 128-byte header some
editions of SimCity add, of big-endian 16-bit numbers:

1. six histories of 240 numbers: residential, commercial, industrial,
   crime, pollution, and money;
2. a block of 120 numbers for the city's settings: its populations, its
   demand, the date, the land value average, the money, the tax rate, and
   the funding levels;
3. the 120 by 100 map, a number a tile, column by column: a Micropolis
   tile number in the low 10 bits and flags above, power the highest.

The import (`MicropolisCityImport` in `CityLimits.Core/Saving/`) reads the
tiles with Micropolis' tile numbers, which Tony Bark approved for this
and which the tile sheet uses too. Ground, water, trees, rubble, parks and
fountains, roads at any level of traffic, bridges and drawbridges, power
lines, and road-and-line crossings come in a tile at a time. Buildings come
in from their centres, so each is whole: zones at the level and land-value
class their drawing says, with an empty residential zone's houses, and
plants, stations, hospitals, and churches.

**Rail comes in as rail,** its tunnels and its crossings with roads and
power lines included.

**What CityLimits doesn't have yet becomes bare ground:** seaports,
airports, stadiums, and burning, flooded, or radioactive tiles, as does
what's left of a building with no centre. The import lists what it
replaced; the game logs it, and the news ticker says so.

Also carried over: the date, the money, the tax rate (back to 7% when it
is outside 0 to 20, as Micropolis does), the funding levels, the
populations, demand, the land value average, and the residential,
commercial, and industrial history. A `.cty` file has no seed, so the
import makes one from the file's contents: the same file always imports
as the same city. An imported city runs on the base game's definitions,
and its power is traced at once, as Micropolis does when it loads a city.

## The flat city format

A `.fcl` file is one JSON object. Its fields are in `camelCase`, and
arrays of numbers sit on one line each.

| Field | What it is |
|---|---|
| `$schema` | The schema the file follows, `docs/schemas/city-save.v1.0.schema.json`, for editors; the game ignores it |
| `format` | The format's version, `"1.0"`; a save written before versions had minor numbers says `1`, which reads as 1.0 |
| `name`, `seed`, `random` | The city's name, its seed, and the generator state |
| `tick`, `cityTime` | The steps run, and the calendar's count |
| `funds` | The money, which may be below zero |
| `definitions`, `mods` | The definitions, in the definitions format, and the mods applied, in order |
| `map` | The map: see below |
| `budget`, `census`, `demand`, `power`, `services`, `community`, `cityMaps` | Each part of the simulation's state |
| `cycle` | The road tiles counted this cycle, and what the last count set aside for the budget |
| `parts`, `traffic`, `roadTraffic`, `rail`, `pollution`, `crime`, `fire`, `flood` | The parts list and the parts later versions added, each from the version the table under Versions gives |

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

## Versions

Each format has a version, major and minor, such as 1.0. A minor version
only adds: 1.1 brings something new, such as traffic, and a game that
reads 1.1 reads 1.0 too, starting what 1.0 didn't have empty. A major
version breaks with the one before. Each new edition of the Micropolis
baseline that adds to the city takes the next minor version.

- **Reading.** A save of the game's own major version and of its minor
  version or an older one loads; a newer one is refused with a message
  saying which this version reads, such as `this is format 1.2; this
  version of CityLimits reads up to 1.1.`
- **Upgrading.** An older save climbs to the current version one minor
  version at a time, each step filling in what that version added
  (`SaveUpgrades`). Bridgeport's fixtures stay at 1.0, so the steps stay
  tested.

| Version | What it added | Upgrading from the one before |
|---|---|---|
| 1.0 | The first versioned saves | |
| 1.1 | Traffic: `"traffic"`, and the `TRAF` chunk | The roads start quiet, and the city's trips fill them again |
| 1.2 | The parts list: `"parts"`, and the `PART` chunk, so a newer save can open in an older game | Nothing: a save without a list has no newer parts |
| 1.3 | The traffic each road shows and the year's traffic average: `"roadTraffic"`, and the `RTRF` chunk | The roads show no traffic until the scan passes them, within a cycle, and the average is 0 until the year ends |
| 1.4 | Rail: `"rail"`, and the `RAIL` chunk | A city from before has no rail |
| 1.5 | Pollution and crime: `"pollution"` and `"crime"`, and the `POLL` and `CRIM` chunks | A city from before has none until phases 12 and 13 work them out, within a cycle |
| 1.6 | Fire: `"fire"`, and the `FIRE` chunk | A city from before has nothing burning, and its buildings whole |
| 1.7 | Floods: `"flood"`, and the `FLOD` chunk | A city from before has no flood |
- **Growing the binary format.** New state goes in new chunks, not new
  fields in old ones, so a minor version stays an addition.
- **The flat format** writes its version as a string, `"1.0"`, so 1.10
  can't read as 1.1; a whole number, as saves before minor versions
  wrote, reads as that major version's .0. Scenarios and mod definitions
  are versioned the same way.

## Rail

From format 1.4, rail is saved as a part of its own, `"rail"` in `.fcl`
and the `RAIL` chunk in `.cl` (`Saving/RailCodec.cs`), apart from the
map. The map holds what lies beneath the rail: clear ground or water
under track or a tunnel, and the road or the power line at a crossing.
The rail part holds, for each row of the map, a character a tile: `.`
for none, `r` for track or a tunnel, and `a` or `d` for rail running
across or down over the road or the line there; and the rail tiles the
scan has counted and the budget last used. A version that doesn't know
rail leaves the part out, as the parts list allows, and opens the city
as if it had no rail, with its roads and lines whole. It can't keep the
part, since it may build where the rail was, so the city it saves has no
rail. Reading refuses rail where it can't be, such as track on a road
or a crossing on clear ground, and says where.

## Pollution and crime

From format 1.5, the city's pollution and crime are saved as two parts,
`"pollution"` and `"crime"` in `.fcl` and the `POLL` and `CRIM` chunks
in `.cl`. Each holds a number for every 2 by 2 block of the map, row by
row (pollution from 0 to 255, crime from 0 to 250), and the average the
city last worked out. Both are worked out again every cycle from the
map, so they are ignorable and keepable: a version that doesn't know
them opens the city without them, and one that kept them stale writes
them back, which costs the newer game a cycle of land value worked out
from old figures.

## Fire

From format 1.6, the city's fires are saved as a part of their own,
`"fire"` in `.fcl` and the `FIRE` chunk in `.cl` (`Saving/FireCodec.cs`).
When a tile burns, what stood there is lost, so the city holds rubble
beneath the fire; an outer tile of a building that burns leaves the
building standing with a hole. The part holds, for each row of the map,
a character a tile: `.` for nothing, `f` for a fire, `x` for a hole fire
left in a standing building, and `b` for a hole still burning.

The map in the save holds every standing building whole: a hole is
written as the building's own tile, with nothing on it, and the part
says it is a hole. A version that doesn't know fire leaves the part out,
as the parts list allows, and opens the city as if its fires had burned
out and its buildings had been repaired, as Tony Bark chose; before 1.6,
every version refuses a building that isn't whole, so a map with holes
in it would have stopped older versions opening the city at all. It
can't keep the part, since it may build where the fires were. Reading
refuses a fire on anything but rubble, a hole that isn't an outer tile of
a building, or any other character, and says where.

## Floods

From format 1.7, the city's floods are saved as a part of their own,
`"flood"` in `.fcl` and the `FLOD` chunk in `.cl`
(`Saving/FloodCodec.cs`): for each row of the map, a character a tile,
`.` for none and `w` for a tile under water, and how many more cycles the
water spreads for, from 0 to 30. Beneath the water the map holds bare
ground, what a flood leaves when it drains. A flooded hole in a building
is written whole in the map, as any hole is, and marked `x` in the fire
part, so a version that knows fire but not floods opens it as a hole, and
one that knows neither opens the building whole. A version that doesn't
know floods can't keep them, since it may build where the water was.
Reading refuses a flood on anything but bare ground or a hole, or on a
fire, and says where.

## Opening a newer save

From 0.3.103, CityLimits can open a city a newer version saved, as PNG
readers handle chunks they don't know.[^backports] Each save lists the parts every
version after 1.0 added (`"parts"` in `.fcl`, the `PART` chunk in `.cl`),
each with two flags that say what a game that doesn't know the part may
do with it:

- **Ignorable:** the game may open the file without it. If any part the
  game doesn't know isn't ignorable, such as one that puts new kinds of
  tiles on the map, the file is refused with a message naming it.
- **Keepable:** a game that has changed the city may write the part back
  exactly as it found it, because nothing in it goes wrong when the rest
  of the city moves on. Traffic is: any amount on any road is valid, and
  it fades. A part that isn't keepable is left out, and the newer game
  rebuilds it, as an upgrade does.

Saving such a city writes back the keepable parts it kept, in the same
format they came in (a binary chunk can't become JSON without being
understood). When it kept them all, the save keeps the newer version;
when it left one out, it is this version's, so the newer game knows to
rebuild, and the keepable parts still go in, named in its parts list, so
the save opens here again with them and the newer game gets them back.
A city opened this way says so on the news ticker, and the log
lists each part and what will happen to it. A part a newer version adds
is always a whole new section or chunk, never a new field in an existing
one, so it can be kept or left out whole.

[^backports]: 0.3.100, 0.3.101, and 0.3.102 have backports of this, 0.3.100.1,
    0.3.101.1, and 0.3.102.1, on the branches `release/0.3.100` to
    `release/0.3.102`. Each opens saves from 0.3.103 on (format 1.2 and
    later, which list their parts) as described here, and writes back the
    parts it kept; a save newer than its own without a parts list, such as
    0.3.102's 1.1 in 0.3.100.1 or 0.3.101.1, is still refused. A city a
    backport makes saves as its version always did, with no parts list, and
    0.3.100.1 writes its saves byte for byte as 0.3.100 did, so the original
    versions open them. A backport's `GitInfo.txt` names its version with a
    fourth number, such as `0.3.100.1`, so it can't run into the next
    release's.

## Schemas

`docs/schemas/` holds a JSON Schema for each format people and agents
write or read: `city-save` for `.fcl` saves, `scenario`, and the mod
files `mod`, `zones`, `plants`, and `services`, with `definitions` for
the base game's, one file a version, such as `city-save.v1.0.schema.json`.
A schema describes a file's shape: its fields and their types, with no
others. A file names its schema in `"$schema"`, which every loader
accepts and ignores, and an editor such as VS Code then checks the file
and suggests its fields. The game writes it into every `.fcl` save.

The schemas are generated from the same code that reads the files
(`FormatSchemas` in `CityLimits.Core/Formats/`), and a test fails when a
committed one no longer matches, writing the current one beside the
tests to look at and copy in. The game's own checks go further than a
schema can, to ranges and the rules between fields, and decide what
loads; the schemas are for people, editors, and agents.

## Reading needs no reflection

Every JSON format reads and writes through code generated at build time
(System.Text.Json's source generator), not reflection, so saves,
scenarios, mods, and settings survive a trimmed build. The binary
format's codec reads the save model's properties by reflection, so the
model's classes are marked for the trimmer to keep them. Reflection-based
JSON is switched off in the game and the tests, so a path that still
needs it fails at once, and the core library is analysed for trimming,
so new reflection shows up as a build warning.

## For contributors

- `CityLimits.Core/Saving/` holds the formats: `CitySave` is the save
  model and the shape of the flat file, `FlatCityFormat` and
  `BinaryCityFormat` write and read it, `SectionCodec` writes a section as
  bytes, `MapCodec` handles the map's layers and words, and `CityFiles`
  names the files, lists a folder, and reads and writes either format by
  its extension.
- `CityLimits.Tests/Assets/` holds Bridgeport, a city Tony Bark built in
  the game, in both formats. The tests read each and check they are the
  same city, so a change that stops an older save loading is caught.
- Each part of the simulation captures and restores its own state with
  internal `Capture` and `Restore` methods, and checks what it restores.
- **New state goes in the save.** A field that lasts from one step to the
  next is captured and restored, or a loaded city drifts from the one
  that was saved. `FlatCityFormatTests` saves a busy town at several
  steps, loads it, and runs both in step for two years, comparing their
  saves as they go; it catches state left out of the save, as long as
  the state changes what the city does.
- `CityLimits.Tests/Assets/Bridgeport.cty` is Bridgeport in Micropolis'
  format, made by a writer that lives only in the tests
  (`MicropolisCityImportTests`); importing it gives back Bridgeport's map
  tile for tile.
