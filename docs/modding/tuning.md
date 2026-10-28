# Tuning mods

A tuning mod changes the numbers and names of things the game already
has: what they cost, how much they hold, what they're called. It can't add
new kinds of things or change how the city works. If you haven't made a
mod before, start with [Modding CityLimits](./README.md), which covers
installing a mod and writing `mod.json`.

A tuning mod can change seven kinds of things, each in a file of its own
named for what it holds. These files are called spec files:

```text
Mods/
  my-mod/
    mod.json             who the mod is (required, beside the mod's folder name)
    zones.json           residential, commercial, and industrial zones
    power.json           coal and nuclear power plants
    services.json        police and fire stations
    facilities.json      stadiums, seaports, and airports
    infrastructure.json  roads, rail, power lines, parks, and the bulldozer
    economy.json         what a new city starts with
    ordinances.json      the city's ordinances
```

Every spec file is optional, and can sit anywhere in the mod's folder,
in folders of your own, such as `my-mod/transport/infrastructure.json`.
The game reads them in the order of their paths, letter by letter, so
when two files in a mod change the same value, the later path wins.
Other files, such as a readme, are left alone.

Every file starts with `"format": "2.2"` and lists only what it changes.
A value you leave out keeps the base game's, which are Micropolis' unless
said otherwise. Files written for CityLimits 0.4, with `"format": "1.0"`
or `1`, still work, and so does `plants.json`, the name `power.json` had
then. Strength, reach, and `facilities.json`, `infrastructure.json`, and
`economy.json` are new in 2.0; `carriesPower` and `influences` in 2.1;
and `ordinances.json` in 2.2. Older files still work.

Each file can also name its schema in a `"$schema"` line, as the examples
below do. A schema describes what can go in the file, so an editor such
as VS Code can check it and suggest field names as you type. The game
ignores the line. The schemas are in `docs/schemas/`: `mod`, `zones`,
`power`, `services`, `facilities`, `infrastructure`, `economy`, and
`ordinances`.

The base game's own spec files are in the `common` folder beside the
settings, where you can read every value the game plays with, in the
same format. See [Modding CityLimits](./README.md#the-base-games-files).

## `zones.json`

```json
{
  "$schema": "https://raw.githubusercontent.com/tonytins/citylimits/main/docs/schemas/zones.v2.2.schema.json",
  "format": "2.2",
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

## `power.json`

```json
{
  "$schema": "https://raw.githubusercontent.com/tonytins/citylimits/main/docs/schemas/power.v2.2.schema.json",
  "format": "2.2",
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
| `capacity` | How many tiles of the map one plant can power: lines, zones, and other buildings each count a tile at a time, and roads none. The base game's are 700 for coal and 2 000 for nuclear. As in Micropolis, every plant on the map adds its capacity, whether it is connected or not | 1 to 1 000 000 |

## `services.json`

```json
{
  "$schema": "https://raw.githubusercontent.com/tonytins/citylimits/main/docs/schemas/services.v2.2.schema.json",
  "format": "2.2",
  "services": [
    { "id": "police", "name": "Sheriff", "price": 300, "upkeep": 40 },
    { "id": "fire", "strength": 150, "reach": 2 }
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
| `strength` | How strongly one station covers the land around it, as a percentage of the base game's: 200 covers twice as well, and 0 not at all. The base game's is 100 | 0 to 1 000 |
| `reach` | How far a station's cover spreads. Each step spreads it a little further over the 8 by 8 tile blocks around the station, and thinner. The base game's is 3, as in Micropolis; at 0 a station covers only its own block | 0 to 8 |

## `facilities.json`

```json
{
  "$schema": "https://raw.githubusercontent.com/tonytins/citylimits/main/docs/schemas/facilities.v2.2.schema.json",
  "format": "2.2",
  "facilities": [
    { "id": "stadium", "name": "Arena", "demandCap": 800 },
    { "id": "airport", "pollution": 40, "influences": [
      { "effect": "landValue", "strength": -40, "reach": 3 },
      { "effect": "crime", "strength": 10, "reach": 1 }
    ] }
  ]
}
```

A big city wants a stadium, a seaport, and an airport. Until it has one,
the demand it serves stops rising: residents want a stadium, industry a
seaport, and commerce an airport. Each entry names one by its id,
`stadium`, `seaport`, or `airport`.

| Field | What it changes | Limits |
|---|---|---|
| `id` | Which to change | `stadium`, `seaport`, or `airport` |
| `name` | Its name, as the tile readout shows it | 1 to 80 characters |
| `price` | What one costs to build. The base game's are $5,000 for a stadium, $3,000 for a seaport, and $10,000 for an airport | 0 to 1 000 000 |
| `demandCap` | How big the city grows before it wants one, counted as the city's zones are, in the same units the census uses. The base game's, Micropolis', are 500 residential for a stadium, 70 industrial for a seaport, and 100 commercial for an airport | 0 to 100 000 |
| `pollution` | How much one pollutes the land around it. The base game's are 0 for a stadium and 100, a coal plant's, for a seaport and an airport | 0 to 255 |
| `influences` | What a powered one does to the land around it, as a list; see below. A list replaces the base game's whole, and `[]` takes them all away | Each effect at most once |

An influence works as a fire or police station's cover does: its
strength goes to the 8 by 8 tile block the building stands in, then
spreads to the blocks around, thinner each step. One without power has
none.

| Field | What it changes | Limits |
|---|---|---|
| `effect` | Which of the city's maps it changes: `landValue`, `crime`, or `pollution` | One of those three |
| `strength` | How much it raises the map at the building's block, or lowers it when negative, in the map's own units: land value runs from 1 to 250, crime from 0 to 250, and pollution from 0 to 255 | -255 to 255 |
| `reach` | How many times it spreads to the blocks around | 0 to 8 |

The base game's are CityLimits' own, not Micropolis': a stadium raises
land value by 20, with reach 2; a seaport's noise lowers it by 20, with
reach 2; and an airport's, by 30, with reach 3. None changes crime or
pollution this way.

## `infrastructure.json`

```json
{
  "$schema": "https://raw.githubusercontent.com/tonytins/citylimits/main/docs/schemas/infrastructure.v2.2.schema.json",
  "format": "2.2",
  "infrastructure": [
    { "id": "road", "price": 15 },
    { "id": "rail", "price": 15, "carriesPower": true }
  ]
}
```

What each tile of the city's networks costs to build, and what carries
power. Each entry names one by its id, and gives its `price`, from 0 to
1 000 000, its `carriesPower`, or both.

| `id` | What it prices | The base game's |
|---|---|---|
| `road` | A tile of road | $10 |
| `bridge` | A tile of road over water | $50 |
| `rail` | A tile of rail | $20 |
| `tunnel` | A tile of rail under water | $100 |
| `powerLine` | A tile of power line | $5 |
| `underwaterPowerLine` | A tile of power line under water | $25 |
| `park` | A tile of park | $10 |
| `bulldozer` | Clearing a tile, or knocking down a zone or a building, with the bulldozer, for each tile | $1 |
| `clearing` | Clearing trees or rubble from a tile a tool builds on | $1 |

Power reaches more than lines in CityLimits. `carriesPower` says how, for
`road` and `rail` only:

| `id` | With `carriesPower` on | The base game's |
|---|---|---|
| `road` | A road joins every zone and building along it: once one of them has power, the rest do too, so a street needs no power line beside it. The road isn't powered itself and uses none of the plants' capacity. When the capacity runs short, the buildings nearest the plant along the road keep their power, each whole | on |
| `rail` | Rail joins the zones and buildings along it, as a road does | off |

Turn both off for Micropolis' rule, where only lines, zones, and
buildings carry power. Parks never carry power.

## `ordinances.json`

```json
{
  "$schema": "https://raw.githubusercontent.com/tonytins/citylimits/main/docs/schemas/ordinances.v2.2.schema.json",
  "format": "2.2",
  "ordinances": [
    { "id": "neighbourhoodWatch", "name": "Night patrols", "money": -3 },
    { "id": "salesTax", "effects": [ { "effect": "commercialDemand", "amount": -20 } ] }
  ]
}
```

Ordinances are city-wide rules the player turns on and off at any time,
after SimCity 3000's. While one is on, it earns the city money or costs
it, by its population, and changes how the city behaves. Each entry names
one of the game's by its id; a mod can change what it is called, what it
costs, and what it does, but can't add new ones.

| Field | What it changes | Limits |
|---|---|---|
| `id` | Which ordinance to change; see the table below | One of the ten |
| `name` | Its name, as the player sees it | 1 to 80 characters |
| `money` | What it earns each year for every 1,000 people, or costs, below zero. A city of 10,000 collects about $500 a year in taxes at 7% | -1 000 to 1 000 |
| `effects` | What it does while it is on, as a list of `effect` and `amount`; a list replaces the game's whole, and `[]` takes them all away | Each effect at most once |

| `effect` | What its `amount` changes | Limits |
|---|---|---|
| `residentialDemand`, `commercialDemand`, `industrialDemand` | That demand, added each time demand moves, as the tax rate pushes it; a percent of tax is about 20 to 30 | -2 000 to 2 000 |
| `crime` | Crime everywhere, as police take it away | -250 to 250 |
| `industrialPollution` | The pollution industry gives off, in percent: -30 leaves 70% | -100 to 900 |
| `fires` | The chance of a fire breaking out by itself, in percent: -50 halves it | -100 to 0 |
| `powerCapacity` | What the power plants can power, in percent: 15 makes it 115% | -100 to 900 |
| `traffic` | The traffic each trip leaves on the roads, in percent: -10 leaves 90% | -100 to 900 |

The game's ordinances, CityLimits' own:

| `id` | Name | Money a year per 1,000 people | Effects |
|---|---|---|---|
| `salesTax` | Sales tax | +$6 | Commercial demand -40 |
| `incomeTax` | Income tax | +$8 | Residential demand -60 |
| `legalizedGambling` | Legalized gambling | +$10 | Crime +15 |
| `parkingFines` | Parking fines | +$2 | Traffic -10% |
| `neighbourhoodWatch` | Neighbourhood watch | -$5 | Crime -15 |
| `smokeDetectors` | Smoke detectors | -$3 | Fires -50% |
| `pollutionControls` | Pollution controls | -$6 | Industrial pollution -30%, industrial demand -40 |
| `energyConservation` | Energy conservation | -$4 | Power capacity +15% |
| `tourismPromotion` | Tourism promotion | -$5 | Commercial demand +50 |
| `industrialPromotion` | Industrial promotion | -$5 | Industrial demand +50 |

When a city of 2,000 people or more could use one it hasn't turned on,
the news ticker asks for it, once a quarter, the most pressing first:
energy conservation for brownouts, a neighbourhood watch for crime,
pollution controls for smog, smoke detectors while fires burn, a way to
raise money after a year in the red, and promotion when business is
slack. What an ordinance asks for stays the same when a mod changes what
it does.

## `economy.json`

```json
{
  "$schema": "https://raw.githubusercontent.com/tonytins/citylimits/main/docs/schemas/economy.v2.2.schema.json",
  "format": "2.2",
  "startingFunds": 50000,
  "taxRate": 6
}
```

What a new city starts with. A city that already exists keeps its money
and its tax rate.

| Field | What it changes | Limits |
|---|---|---|
| `startingFunds` | The money a new city starts with. The base game's is $20,000 | 0 to 1 000 000 |
| `taxRate` | The tax rate a new city starts at, in percent. The base game's is 7 | 0 to 20 |

## Mistakes the game catches

Each file must hold only its own kind of entry: a zone in `power.json`,
or a plant in `zones.json`, is refused. So is a field the game doesn't
know, a value outside its limits, or an id listed twice. When that
happens the whole mod is skipped, not just the bad line, and the log says
which file and which value, so you can fix it and try again.
