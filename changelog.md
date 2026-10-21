# CityLimits Changelog

## 0.2.112

The budget and demand windows can be moved: drag either by its title.
Each opens where you last left it and stays on the screen.

The demand window now leads with a chart: three plain lines for
residential, commercial, and industrial demand, rising and falling over
up to ten years, newest on the right. The figures from before sit under
it.

For developers: `CityDemand` keeps ten years of history
(`ResidentialHistory`, `CommercialHistory`, `IndustrialHistory`, and
`HistoryCount`), one entry per update.

## 0.2.111

A new top bar, after a mock-up by the owner. From the left: a pause and
play button, the month and year, three speed buttons, the city's name, a
small demand meter, the funds, and the population, with the step count
and the frame rate on the far right. The speed buttons fill up like a
meter (one lit for Normal, two for Fast, three for Super fast) and stay
dimmed at your last speed while the game is paused; click one to play at
that speed. The bar no longer shows the held tool or the mouse position;
the palette's header still names the tool and its price.

Click the demand meter for the demand panel, which replaces the one under
the palette. It shows how far residential, commercial, and industrial
demand stand toward their limits, how much the economy and your taxes
moved each one last time, how many jobs there are for each resident, and
what the population is made of. The game keeps running while it's open;
close it with its x, the meter, or Escape.

Clicking the top bar never builds on the map underneath it any more.

For developers: a city has a name (`CitySimulation.Name` and `Rename`),
"CityLimits" until saving can set it. `CityDemand` reports what moved each
demand at its last update, and `CityCensus` reports residents',
commerce's, and industry's shares of the population.

## 0.2.110

An overview map, as in Micropolis. The whole city sits small in the
top-right corner with the part you're looking at outlined; click or drag
on it to move there. The buttons under it switch it to show power (lit
buildings red, dark ones blue, and the lines grey), fire and police
coverage, land value, or population density, each in four colours over
the city dimmed.

## 0.2.109

Hospitals and churches, as in Micropolis. You don't build them: once the
city has 256 residents it needs a hospital and a church, and another of
each for every 256 more, and an empty residential zone that would grow
becomes one now and then. If the city shrinks and has too many, one
turns back into an empty zone. Unlike Micropolis, a zone never becomes a
hospital and then a church at once, losing the hospital.

The modding guide has moved to `docs/modding/`: a guide for players
writing their first mod, and `tuning.md`, which lists everything a
tuning mod can change.

Scenarios can count hospitals and churches.

The Linux AppImage is named for the game's version, as in
`CityLimits-0.2.109-x86_64.AppImage`, rather than a commit hash, uses the
game's logo as its icon, and builds from any folder without FUSE.

## 0.2.108

Parks, as in Micropolis: $10 a tile, laid as a rectangle from the
palette's first row, on clear ground or rubble but not on trees. Most
park tiles are woods, which make the land around them worth more as
trees do; about one in five is a fountain. What you see in the preview is
what gets built.

Scenarios can lay and count parks.

## 0.2.107

Police and fire stations, as in Micropolis: $500 each to build and $100 a
year to keep up, placed from the palette, either side of the inspect
tool. Each covers the blocks around it, more strongly when it has power,
a road along its edge, and full funding; the fire and police coverage
will matter once fires and crime arrive. The city asks for a fire
station and a police station once it passes 60 people, and complains
when either is funded below 70%. An unpowered station blinks the
lightning bolt, as a zone does, and a power plant has power the moment
it is built.

Mods can change a station's name, price, and upkeep in a
`services.json`, and scenarios can build and count stations.

## 0.2.106

The budget panel. Press B, or click the money in the top bar, to see
the year's taxes and upkeep as they stand and last year's, and to set
the tax rate and how much of the roads', fire, and police upkeep the city
pays. The budget runs itself, but when a year's upkeep comes to more than
its taxes, or the money runs out and funding is cut, the panel opens on
its own and says why. The game pauses while the panel is open. The money
shows red below zero.

## 0.2.105

Taxes and upkeep, as in Micropolis. Once a year the city collects its
taxes, from its population and what its land is worth, at a 7% tax, and
pays to keep its roads up: 70 cents a road tile, a bridge counting as
five. When the money runs short, roads are paid first. Underfunded roads
crumble now and then, and the city says so; it also complains when
taxes go above 12%. The tax rate now pushes demand: lower taxes draw
people and businesses in, higher ones drive them away. The money can
go below zero at the year's end.

The headless log shows each year's budget. The budget panel, where the
tax rate and funding are set, comes next.

## 0.2.104

The tool palette is laid out as Micropolis' is: the tool's name and price
at the top, then rows of icon buttons, with the held tool's icon lit up.
Point at a button to see its name and price; the number keys still pick
tools. If an icon is missing, its button shows a short label instead of
disappearing.

## 0.2.103

Power works. Plants send power through lines into zones, and zones pass
it on to the zones and plants they touch, as in Micropolis. A coal plant
powers 700 tiles and a nuclear plant 2,000, and every plant on the map
adds to the total, connected or not. A zone with no power doesn't grow,
and now and then declines; a lightning bolt blinks over it, and the
readout says "no power". When the plants can't keep up, the city warns
of brownouts, and of blackouts when fewer than 7 in 10 zones have power;
a city of more than 10 zones with no plant is told it needs one.

Every built-in scenario and the headless demo town now build a coal plant
first. The headless log shows the tiles powered each month, and the
city's messages.

Mods can change a power plant's name, price, and capacity in a
`plants.json`. Scenario tile checks can ask whether a zone is `powered`.

## 0.2.102

Power lines. Pick the line from the palette (key 9) and drag it like a
road: $5 a tile on land, $25 under water. A line can only go under water
from something beside it that already carries power, as a bridge needs a
road, so it can start from either bank. Strung across a straight road, or
crossed by one, it makes a tile that is both. Lines join each other,
zones, and plants, and the bulldozer clears them for $1 a tile. Nothing
is powered yet: the power grid comes next.

Scenarios can string lines with the `PowerLine` tool and count them.

## 0.2.101

Power plants, the start of Joffburg. A coal plant costs $3,000 and a
nuclear plant $5,000, as in Micropolis, and each is 4 by 4, drawn from the
tile sheet. Pick one from the palette (keys 7 and 8), and it follows the
pointer while the button is held, with its price beside it; letting go
builds it, whole or not at all. The bulldozer knocks a plant down through
its centre, as it does a zone. Plants don't make power yet: power lines
and the power grid come next.

Scenarios can build plants with the `CoalPowerPlant` and
`NuclearPowerPlant` tools and count them, and the JSON report's counts
include them.

## 0.2.100

CityLimits now plays like Micropolis at its core. Its features aren't yet at
parity with SimCity, or with any city builder made since, but it has the
basics down: a map to build on, roads and zones laid as Micropolis lays
them, and a city that grows and declines on its own, at Micropolis' pace.
There is no power, no budget, no traffic, no pollution or crime, no
disasters, and no saving yet; those come next.

### New in 0.2.100

The land is laid out as in Micropolis, on its 120 by 100 map. One map in
ten is an island. The rest are crossed by two wide rivers and a narrow
one, with up to ten lakes, and woods are scattered over the land and
thinned so they stand together. Shores and the edges of woods are drawn
from the tile sheet's edge drawings, so water and woods have soft, joined
edges instead of square ones. Rock is gone; Micropolis has none.

The same seed now gives a different map than it did before 0.2.100, so a
scenario file written for the old 128 by 128 map needs new coordinates.
The built-in scenarios have moved onto the new map.

### Since 0.1.100

- **Building.** Tools work in strokes: press, drag, and see what the
  stroke would build and what it would cost before letting go. A stroke
  the city can't pay for, or that runs off the map, builds nothing, and
  Escape cancels it.
- **Roads and bridges.** Roads run straight from where the drag started
  and join up as they are drawn. They cross water as bridges, from either
  bank.
- **Zones.** Residential, commercial, and industrial zones are 3 by 3,
  laid as a grid, and served by a road along one of their edges.
- **The bulldozer** costs $1 a tile, clears trees and rubble, and knocks a
  zone down through its centre, leaving rubble.
- **Prices** are Micropolis': $10 a road, $50 a bridge, $100 a zone, $1 to
  bulldoze or to clear a tile.
- **Time** runs as in Micropolis, from January 1900, at Normal, Fast, and
  Super fast, through its 16-phase simulation cycle.
- **Growth.** The city takes a census and feels demand for each kind of
  zone. Zones send trips along the roads, and demand and what the land is
  worth decide whether each one grows, declines, or stays. Residential
  zones grow houses, then buildings where zones cluster; shops grow as far
  as land value carries them; factories follow demand; and a zone with no
  road declines.
- **Land value** is highest near the city's centre and beside trees and
  water, and buildings come in styles that follow it.
- **The status bar** shows the date, the speed, the population, and the
  funds, and a panel shows the three demands. The tile readout describes
  the whole zone under the pointer: its houses, its level, and its
  population.

For developers, modders, and scripts:

- **The command line** checks every option and refuses a bad one with exit
  code 2. `--headless` runs the city with no window and logs it to the
  console.
- **Scenarios** set a city up from a file, with timed tool strokes and
  checks, and run with `citylimits scenario <id> --headless`. `--json`
  writes a versioned report, and the exit code says whether every check
  passed. Four are built in: the demo town, `bridges`,
  `zones-and-bulldozer`, and `small-town`.
- **Mods** are folders of JSON in `Mods/` that change the zones: their
  names, prices, populations, houses, and where their trips go. The
  game's own rules are written the same way. `docs/modding.md` explains
  how.

## 0.1.116

The city grows on its own, as in Micropolis. Each zone sends trips along
the roads, and the city's demand for its kind, with what the land around
it is worth, decides whether it grows, declines, or stays. Residential
zones fill with houses, then build up where zones cluster together; a
zone on its own stays at houses. Shops grow only as far as land value
lets them, factories follow demand, and a zone with no road along its
edge declines. Land is worth most near the city's centre and beside trees
and water, and buildings come in styles that follow its value.

Growth swings as demand does: zones fill, empty, and fill again as the
balance of residents, shops, and factories shifts.

A new built-in scenario, `small-town`, grows twelve zones for ten years.

## 0.1.115

The city takes a census and feels demand, as in Micropolis. The status bar
shows the city's population, and a panel under the tool palette shows how
much the city wants more residential, commercial, and industrial zones: a
bar rising above the line when it wants more, falling below when it wants
less. Demand follows jobs, births, and the market, a month behind the
census, so a new town's first residents look unemployed until its shops
and factories are counted. Zones don't follow demand yet; that comes next.

The headless run writes the population and the three demands each month.

Zones can take trips, as in Micropolis: from a road along a zone's edge, a
drive of up to 30 road tiles looking for somewhere to go, residents for
shops and factories, shops for anyone, and factories for residents. Zones
don't depend on their trips yet; that comes with the next growth rules.
Mods can change where a zone's trips go, with a `trips` list in
`zones.json`.

## 0.1.114

Zones grow as they do in Micropolis. A residential zone first grows single
houses on its outer tiles, the first ones by the road, and once it holds
eight they make way for a building. Buildings grow a level at a time: four
levels for residential and industrial zones, five for commercial. Each
level holds more people, and the tile readout shows a zone's houses, its
level, and its population.

Mods can now change how many people each building level holds and how many
houses a residential zone grows before its first building.

## 0.1.113

CityLimits can be modded. A mod is a folder in `Mods/` beside the game,
holding a `mod.json` that names it and a `zones.json` that changes the
zones: today, a zone's name and its price. The game's own rules are
written in the same format and read the same way, so a mod changes them
on equal terms. Mods apply in id order, and each is checked before it is
used: a mod with any problem is skipped whole, never half-applied, and
the game says which one and why, on screen and in the log.
`docs/modding.md` explains how to write one.

The game and the headless run use mods. Scenarios always run on the game
as it ships, so they give the same result on every machine.

## 0.1.112

Scenarios. A scenario is a city set up from a file: a seed, tool strokes
made at set times, and checks on what the city should look like along the
way. `citylimits scenario <id> --headless` runs one with no window and
logs each stroke and check; `--file <path>` runs any scenario file, and
`citylimits scenario list` lists the built-in ones: the demo town,
`bridges`, and `zones-and-bulldozer`.

`--json` writes a report a script can read, in a versioned format, with the
log moved to stderr. The exit code says what happened: 0 when every check
passed, 3 when one failed, 2 for a mistake on the command line, and 1 when
the scenario couldn't be loaded; then the demo town runs in its place and
the report says why.

A scenario file is checked before anything runs, and the first problem is
reported with where it is, so a misspelt field or a number out of range is
caught instead of quietly ignored. Every built-in scenario runs as a test,
so a rule that changes is caught by the scenarios that depend on it.

## 0.1.111

Time runs as in Micropolis. A city starts in January 1900, and at Normal
speed a month takes 16 seconds and a year about 3 minutes. Fast is five
times that and Super fast fifty times, and the status bar names the speed.
Each step of the simulation is one phase of Micropolis' 16-phase cycle:
one phase moves the calendar on by a 48th of a year, and eight scan the
map a band at a time, which is when a zone gets its chance to grow.

The headless run takes a year of steps by default and writes a line each
month.

## 0.1.110

Roads, zones, and the bulldozer now play like Micropolis, at its prices.

Building is done in strokes. Pressing on the map starts one, dragging
stretches it, and while the button is held the map shows what the stroke
would build and the cost appears beside the pointer, or why it can't be
built. Letting go builds it all at once; a stroke the city can't pay for,
or that runs off the map, builds nothing. Roads run in a straight line
from where the drag started. Escape cancels a stroke, or quits when there
is none.

Zones are 3 by 3. Dragging lays them as a grid, the first one centred
where the stroke started. A zone costs $100 and goes down whole or not at
all, and it is served by a road along one of its edges, not at a corner.

Roads cross water as bridges, at $50 a tile, starting from a road beside
them, so a bridge can be built from either bank.

The bulldozer costs $1 a tile and gives nothing back. It knocks a zone
down only through its centre, which turns all nine tiles to rubble for $1;
clicking any other part of a zone says to bulldoze the centre instead.
The tools clear trees and rubble for $1 more a tile.

Zones, their buildings, bridges, and rubble are drawn from the tile sheet,
and a zone keeps its development on its centre tile only.

The tool palette fits its text, so the prices no longer run into the
names, and clicking it picks a tool instead of building on the map
underneath.

The headless demo town is a road nine tiles long with a residential, a
commercial, and an industrial zone along it.

## 0.1.101

Roads, zoning, and a bulldozer. The player can lay roads, zone land as
residential, commercial, or industrial, and bulldoze it again, and the city
charges for the work and refuses what it cannot pay for. A zoned tile that
touches a road grows a building, drawn from the tile's own state.

The core library now holds the build actions, the road network,
the tile state, and the treasury.

## 0.1.100

Restarted project using Raylib-cs and with the aid of AI.
