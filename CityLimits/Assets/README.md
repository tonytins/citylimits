# Assets

The images the game loads at startup. They are copied to the output folder
beside the executable, so the game finds them at run time.

```text
tiles.png            the sheet that holds every kind of ground, 256 by 960
tools/               the icon of each tool in the palette
tools/icqry.png      inspect
tools/icroad.png     road
tools/icres.png      residential
tools/iccom.png      commercial
tools/icind.png      industrial
tools/icbulldozer.png bulldozer
Scenarios/           the built-in scenarios, one JSON file each, named for its id
```

## The tile sheet

`tiles.png` holds 16 by 16 pixel tiles, sixteen across and sixty down. A tile
is found by its index: the tile at index *n* is at column *n* mod 16 and row
*n* divided by 16. `Rendering/TileAtlas.cs` holds the indices CityLimits uses:

| Ground | Tile index | What it looks like        |
| ------ | ---------- | ------------------------- |
| Grass  | 0          | clear ground              |
| Water  | 2          | open water                |
| Trees  | 21 to 28   | eight drawings of trees   |
| Rock   | 44         | bare rubble               |

A road is drawn from the sides of it that join another road, which
`RoadNetwork.ConnectionsAt` reads from the map. The table was made by
looking at the drawings in this copy of the sheet:

| Road joins                     | Tile index | What it looks like                  |
| ------------------------------ | ---------- | ----------------------------------- |
| nothing, east, west, or both   | 66         | a straight road, east to west       |
| north, south, or both          | 67         | a straight road, north to south     |
| north and east                 | 68         | a corner                            |
| east and south                 | 69         | a corner                            |
| south and west                 | 70         | a corner                            |
| north and west                 | 71         | a corner                            |
| east, west, and north          | 72         | a junction, with a crossing north   |
| north, south, and east         | 73         | a junction, with a crossing east    |
| east, west, and south          | 74         | a junction, with a crossing south   |
| north, south, and west         | 75         | a junction, with a crossing west    |
| all four sides                 | 76         | a crossroads with a signal          |

A road over water is a bridge: tile 64 runs across, and tile 65 runs down,
used when the bridge joins a road only above or below it.

Rubble, which a bulldozed zone leaves, is tiles 44 to 47, chosen by the
tile's place. Rock borrows tile 44 until the new terrain removes it.

A zone is 3 by 3. Its nine tiles are nine tiles in a row in the sheet,
read across and then down, so a tile's drawing is the zone's first tile
plus its part number (0 to 8, with 4 the centre):

| Zone        | Empty zone | Development 1 | Development 2 | Development 3 |
| ----------- | ---------- | ------------- | ------------- | ------------- |
| Residential | 240        | 261           | 270           | 279           |
| Commercial  | 423        | 432           | 441           | 450           |
| Industrial  | 612        | 621           | 630           | 639           |

The buildings are the first three of each zone's drawings, the lowest
land value, until population and land value choose among them. The
residential buildings start at 261, after the twelve single houses at 249
to 260; 405 and 414 are a hospital and a church.

## Licensing

The tile sheet and the tool icons come from Micropolis
(<https://github.com/SimHacker/micropolis>), the canonical and official
Micropolis repository, maintained by
[Don Hopkins](https://en.wikipedia.org/wiki/Don_Hopkins), which is under the
GNU General Public License version 3. CityLimits is under the same licence, so the
art can be used and redistributed here. The tile sheet is the core's
`MicropolisCore/src/images/micropolisEngine/tiles.png`, byte for byte. The
tool icons are the same drawings, converted to PNG in the same way as the
others; the bulldozer icon is the Micropolis dozer icon, `icdozr`. The art
is the property of its authors, and CityLimits is not a SimCity product and
does not use the SimCity name or trademarks.

Any art added later has to be the owner's own work, or come with a licence
that is compatible with the GPL version 3 and be credited in this file.
