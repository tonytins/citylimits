# Saving cities

CityLimits saves a city to a file and loads it back exactly: a loaded city
runs on, step for step, as if it had never stopped. This page describes
where saves go, how the game saves and loads them, and the two formats:
the binary city format, `.cl`, the game's own save, and the flat city
format, `.fcl`, which a person or an agent can read and compare. Both
hold exactly the same city. From 0.5 both are format 2.0; 0.5 still reads
every 1.x save, and can still write 1.10, the format 0.4 reads.

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
  saved, marking flat files. A click picks one and shows its stats
  beside a map of it; Open, Enter, or a second click opens it. Load Game
  is dimmed while there are none. A city that can't be read says why in
  the list.
- **Leaving a city** for the menu, for another city, or to quit asks
  first when the city has changes since it was last saved. A city has
  changes when its save would read differently from the last one.
- **Opening a city** runs it on the base game, as `common/` holds it,
  with the mods installed now. A city played with other mods opens
  anyway, and the news ticker says so: `It was played with Cheap
  commerce 2.0, and opens with no mods, so it may play differently.` A city whose buildings the installed
  definitions can't hold, such as a level a mod added, is refused, naming
  the mods to install.
- **Save for 0.4**, in the Settings window's Gameplay tab, saves cities,
  and scenarios' cities, as 1.10, the format 0.4 reads, so they can go
  back to it. Off by default. 0.5 offers it in the game, 0.6 moves it to
  the command line, and 0.7 drops it; every version keeps reading 1.x.
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
- the mods the city was played with, by name and version, in the order
  they were applied. A 2.0 save holds no definitions: it runs on the base
  game and the mods installed when it is opened, and says when those
  differ. A 1.x save held the definitions too, in the same format as the
  base game and mods ([modding](./modding/README.md)); 0.5 reads past
  them, and writes them into a 1.10 save for 0.4;
- the map, as described below;
- the traffic on the roads, from format 1.1, and the traffic each road
  shows and the year's traffic average, from format 1.3;
- the census and demand with their histories, the power grid's last trace
  and this cycle's count, police and fire coverage, hospitals and
  churches, land value, population density, the terrain's worth, and the
  commercial rate, each as the simulation last left it;
- the stadiums, seaports, and airports, this cycle's count of them, and
  the demand held back for want of one, from format 1.9;
- the year the city was founded, which the city panel counts its age
  from, from format 1.10.

The road network is not saved; it is worked out again from the map.

## The binary city format

A `.cl` file follows the pattern of Micropolis' city files, big-endian
numbers and the map as one 16-bit word a tile, column by column, but it
holds CityLimits' own state. It doesn't open in Micropolis, and isn't
meant to. At about 47 KB for a 120 by 100 city, it is around 60% the
size of the same city as `.fcl`.

- **The header:** the six bytes `CLCITY`, then the format's major version
  as a 16-bit number: `2`, or `1` for a 1.x file. The minor version is in
  the `VERS` chunk, so files from before minor versions, which have none,
  stay valid as 1.0.
- **The chunks**, one after another to the end of the file: a four-letter
  tag, the payload's length as a 32-bit number, the payload, and a
  CRC-32 of the tag and the payload.

| Tag | What it holds |
|---|---|
| `VERS` | The format's minor version, as a 16-bit number; written first, and read as 0 when it is missing |
| `FEAT` | The features list, each feature's name and its chunk's tag (see Opening a newer save), `traffic` twice, for `TRAF` and `RTRF`; from 2.0, written after `VERS` |
| `CITY` | The name, the seed, the generator, the steps, the calendar's count, and the money |
| `MODS` | The mods the city was played with; from 2.0 |
| `DEFS` | In format 1 only: the definitions, as definitions-format JSON text, and the mods applied |
| `MAP ` | The width and the height, then a word a tile |
| `BUDG`, `CENS`, `DEMD`, `POWR`, `SERV`, `COMM`, `CMAP`, `CYCL` | The budget, the census, demand, power, services, hospitals and churches, the city maps, and the cycle's counts |
| `TRAF` | The traffic on each 2 by 2 block; from format 1.1 |
| `PART` | In format 1 only: the parts list (see Opening a newer save); from 1.2, written after `VERS` |
| `RTRF` | The traffic each road shows, and the year's average; from format 1.3 |
| `RAIL` | The rail on the map, and the scan's count of it; from format 1.4 |
| `POLL`, `CRIM` | The pollution and the crime on each 2 by 2 block, and each one's average; from format 1.5 |
| `FIRE` | The fires on the map, and the holes they left in buildings; from format 1.6 |
| `FLOD` | The floods on the map, and how long the water spreads for; from format 1.7 |
| `CHAL` | The scenario being played, for a city that is a challenge; from format 1.8, and only in such a city |
| `FCLT` | The stadiums, seaports, and airports, the scan's counts of them, and the demand held back for want of one; from format 1.9 |
| `FNDG` | The year the city was founded; from format 1.10 |
| `ORDN` | The city's ordinances; a 2.0 feature, which a save for 0.4 leaves out |
| `CONN` | The city's deals with its neighbours; a 2.0 feature, which a save for 0.4 leaves out |
| `RADN` | The radioactive ground meltdowns left; a 2.0 feature, which a save for 0.4 leaves out |

In a 2.0 file, the chunks from `TRAF` on are its features: each may be
left out, and is then filled in as the version that added it fills it.
In a 1.x file, each is required from the version that added it.

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
plants, stations, hospitals, churches, seaports, airports, and stadiums;
a stadium with a game on comes in as a stadium.

**Rail comes in as rail,** its tunnels and its crossings with roads and
power lines included.

**What CityLimits doesn't have yet becomes bare ground:** burning,
flooded, or radioactive tiles, as does what's left of a building with no
centre. The import lists what it
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
| `$schema` | The schema the file follows, such as `docs/schemas/city-save.v2.0.schema.json`, for editors; the game ignores it |
| `format` | The format's version, `"2.0"`, or a 1.x version such as `"1.10"`; a save written before versions had minor numbers says `1`, which reads as 1.0 |
| `features` | From 2.0, the names of the features the save holds beyond its core, such as `["traffic", "rail"]`, for people and tools to see; it may be left out. `traffic` covers both traffic sections, `traffic` and `roadTraffic`; a save from 0.5.100 names `roadTraffic` too, which reads the same |
| `name`, `seed`, `random` | The city's name, its seed, and the generator state |
| `tick`, `cityTime` | The steps run, and the calendar's count |
| `funds` | The money, which may be below zero |
| `mods` | The mods the city was played with, in order |
| `definitions` | In format 1 only: the definitions the city ran on, in the definitions format |
| `map` | The map: see below |
| `budget`, `census`, `demand`, `power`, `services`, `community`, `cityMaps` | Each part of the simulation's state |
| `cycle` | The road tiles counted this cycle, and what the last count set aside for the budget |
| `traffic` and `roadTraffic`, `rail`, `pollution`, `crime`, `fire`, `flood`, `challenge`, `facilities`, `founding`, `ordinances`, `connections`, `radiation` | The features' sections, each from the version the table under Versions gives, `ordinances`, `connections`, and `radiation` from 2.0 alone; a 2.0 save may leave any out |
| `parts` | In format 1 only: the parts list, from 1.2 |

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

Each format has a version, major and minor, such as 2.0. From 2.0, a save
names its features, as HTML does, rather than counting minor versions:
new state comes as a new feature, a section in `.fcl` and a chunk in
`.cl`, named in the save's features list, and the version moves only for
a change that breaks. A major version breaks with the one before.

- **Reading.** A save of any version up to the game's own major version
  loads; a newer major is refused with a message saying which this
  version reads, such as `this is format 3.0; this version of CityLimits
  reads up to 2.0.` A save with features this version doesn't know opens
  as its features list allows (see Opening a newer save).
- **Upgrading.** A 1.x save climbs to 1.10 one minor version at a time,
  each step filling in what that version added (`SaveUpgrades`), then
  becomes 2.0: its definitions and parts list are left behind. A 2.0
  save that leaves out a feature has it filled in by the same step.
  Bridgeport's fixtures stay at 1.0, so the steps stay tested.
- **Writing for 0.4.** With Save for 0.4 on, a city is written as 1.10:
  the same save model, with its definitions and its parts list put back
  (`FlatCityFormat.Write` and `BinaryCityFormat.Write` with `legacy`).
  Anything 1.10 can't hold, such as a newer save's features kept here,
  is left out. A test runs a city written as 1.10 and read back in step
  with the city itself, and 0.4.111 itself was checked to open such
  files. 0.7 drops the 1.10 writer, and keeps reading 1.x.

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
| 1.8 | A challenge: `"challenge"`, and the `CHAL` chunk, only in a city being played as one | A city from before isn't one |
| 1.9 | Stadiums, seaports, and airports: `"facilities"`, and the `FCLT` chunk | A city from before has none, and no demand held back until the city next checks |
| 1.10 | The year the city was founded: `"founding"`, and the `FNDG` chunk | A city from before is counted from 1900, as it was; one that started later can't be told apart |
| 2.0 | Features named rather than counted: `"features"`, and the `FEAT` chunk; the mods by name, in `"mods"` and the `MODS` chunk, and no definitions (0.5) | A 1.x save becomes 2.0 at 1.10, leaving its definitions and parts list behind; it runs on the definitions it is opened with |

- **Growing a format.** New state goes in a new section or chunk, never a
  new field in an existing one, so a feature can be kept or left out
  whole.
- **The flat format** writes its version as a string, `"2.0"`, so 1.10
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

## Challenges

From format 1.8, a city being played as a scenario's challenge saves it
as a part of its own, `"challenge"` in `.fcl` and the `CHAL` chunk in
`.cl`, and a city that isn't one leaves the part out, so its save is just
as before. The part holds the scenario's name and description, its goals,
the step they are judged at, and the strokes and disasters still to
come, each at its step, every step counted from January 1900 as the
city's own are; and, once the goals are judged, whether it was won. A
version that doesn't know challenges opens the city as a free one, and
may keep the part as it was: when this version opens the city again,
anything that fell due meanwhile is made at once, and goals whose step
has passed are judged on the next step. Reading refuses a goal, a tool,
or a disaster the game doesn't know, or a step out of range, and says
where. The binary format holds a goal's missing least or most, and a
challenge not yet judged, as a byte saying the value is there or not,
then the value.

## Stadiums, seaports, and airports

From format 1.9, the city's stadiums, seaports, and airports are saved as
a part of their own, `"facilities"` in `.fcl` and the `FCLT` chunk in
`.cl` (`Saving/FacilityCodec.cs`), as rail is: the map holds the bare
ground beneath each, and the part lists each one by its centre, with what
it is (`Stadium`, `Seaport`, or `Airport`), whether it had power, and its
holes, as part numbers counted across and then down. It also holds the
counts of each the scan has made this cycle, and whether residential,
commercial, and industrial demand are held back for want of one.

A hole in one of these, from a fire, a flood, or an earthquake, is
written in the map as what it is, rubble or bare ground, and in the fire
and flood parts as a plain fire or flood, not as a hole, since the map
holds no building there. So a version that doesn't know them leaves the
part out, as the parts list allows, and opens the city as if they had
never been built, with what was lost from them lying where it fell. It
can't keep the part, since it may build where they stood. Reading refuses
a use that isn't one of the three, a building that runs off the map or
onto another, a tile under one that isn't bare ground (or rubble, at a
hole), or a hole that isn't an outer part, and says where.


## The founding year

From format 1.10, the year the city was founded is saved as a part of its
own, `"founding"` in `.fcl` and the `FNDG` chunk in `.cl`: `{ "year":
1906 }`. A city started in a later year, as a scenario may start one,
counts its age from it. It only says how old the city is, so it is
ignorable and keepable: a version that doesn't know it counts the age
from 1900, as it always did, and writes the part back as it was. A city
saved before 1.10 is counted from 1900. Reading refuses a year before
1900 or after the city's own date, and says so.

## Ordinances

The city's ordinances are a feature of format 2.0 with no version of its
own, the first added since it began: `"ordinances"` in `.fcl` and the
`ORDN` chunk in `.cl`:

```json
"ordinances": { "enacted": [ "salesTax" ], "incomeFund": 2880, "costFund": 0, "lastYear": [ 60, 0 ], "estimated": [ 60, 0 ] }
```

`enacted` lists the ordinances enacted, by id. The rest is their share of
the budget, kept here rather than in `"budget"`, which keeps Micropolis'
shape: what they have earned and cost this year so far, set aside each
count as the taxes are, at 48 times a year's worth, and last year's and
the latest estimate's income and cost, when the budget has those. A save
without the section opens with none enacted. Reading refuses an id the
definitions don't have.

The feature is ignorable and keepable: a version that doesn't know it
plays the city with no ordinances and may write the section back as it
was, since enacting one changes nothing else that is saved; the year's
ordinance money is then short by the months it played. A save for 0.4
leaves it out, and its parts list doesn't name it.

## Neighbours

The city's dealings with the cities beyond its edges are a feature of
format 2.0, `"connections"` in `.fcl` and the `CONN` chunk in `.cl`, the
name 0.5.100 reserved:

```json
"connections": {
  "deals": [ { "side": "east", "kind": "cityBuys", "power": 300, "money": 180, "endStep": 16800 } ],
  "offer": null,
  "grudgeUntil": [ 0, 0, 0, 0 ],
  "incomeFund": 0, "costFund": 4320, "lastYear": [ 0, 180 ], "estimated": [ 0, 180 ]
}
```

`deals` are the deals in force, one at most with each neighbour, by
side: which way the power goes (`cityBuys` or `citySells`), how much
capacity, what changes hands a year, and the step the deal ends at.
`offer` is the deal a neighbour has offered and the player hasn't
answered, with its `years` and the step it lapses at, or null.
`grudgeUntil` holds, for north, east, south, and west, the step until
which that neighbour won't deal, after a deal with it was broken. The
rest is the deals' share of the budget, kept here as the ordinances'
is in theirs.

The neighbours' names and sizes come from the city's seed, and what
connects to them from the map, so neither is saved. A save without the
section opens with no deals. Reading refuses a side or a kind it doesn't
know, two deals with one neighbour, and a `grudgeUntil` without four
steps.

The feature is ignorable and keepable, as the ordinances are: a version
that doesn't know it plays the city with no deals, so their power and
money stop while it does, and writes the section back as it was. A save
for 0.4 leaves it out.

## Radiation

The radioactive ground a nuclear plant's meltdown leaves is a feature of
format 2.0, `"radiation"` in `.fcl` and the `RADN` chunk in `.cl`
(`Saving/RadiationCodec.cs`), saved as floods are: for each row of the
map, a character a tile, `.` for none and `r` for radioactive ground.
Beneath it the map holds bare ground, or, where the ground is a hole in
a building, the building whole, the fire part marking the hole. A save
without the section opens with none.

It is ignorable but not keepable, as floods are: a version that doesn't
know it opens the city as if the ground had decayed, and can't write it
back, since it may build there. A save for 0.4 leaves it out. Reading
refuses radiation on a fire, a flood, or anything but bare ground or a
marked hole, and says where.

## Opening a newer save

From 0.3.103, CityLimits can open a city a newer version saved, as PNG
readers handle chunks they don't know.[^backports] From 2.0 the rule is
simple, as Tony Bark chose: a section of a `.fcl` save, or a chunk of a
`.cl` one, that this version doesn't know is left out. The city opens
without it, the news ticker says so, and saving leaves it out too; the
newer version fills it in again, as it fills in any feature a save
doesn't hold. A feature an older version couldn't safely leave out, such
as one that puts new kinds of tiles on the map, comes with a new major
version instead, which older versions refuse with a message. In `.fcl`,
something unknown that isn't a section of its own, such as `"cheats":
true`, is refused like any unknown field.

The one exception is a feature this version reserves: it knows the name,
doesn't read inside, and writes it back exactly as it found it, in the
same format it came in (a binary chunk can't become JSON without being
understood). `connections`, to neighbouring cities, was reserved in
0.5.100 (`SaveParts.Reserved`, the `CONN` chunk), so a city that has them
passes through 0.5.100 to 0.5.106 intact; Senri's N5 gave them their
contents in 0.5.107, and nothing is reserved now.

A 1.x save lists its parts by the version that added them (`"parts"`, the
`PART` chunk), each with two flags, ignorable and keepable, for a game
that doesn't know the part; 0.4 and the backports open newer 1.x saves
that way, falling back to their own version when they leave a part out.

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
the base game's, one file a version, such as `city-save.v2.0.schema.json`.
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
