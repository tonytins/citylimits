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
tools/iccoal.png     coal power plant
tools/icnuc.png      nuclear power plant
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
| Trees  | 37         | woods, trees on every side |

Water and trees are drawn from the sides of them with the same ground
beside them, which `TerrainEdges.SidesAt` reads from the map; the edge of
the map counts as the same ground. Each shore and each wood edge has two
drawings, used on alternate tiles: a shore's second is the next tile, and a
wood edge's second is eight tiles on. The tables were made by measuring
where the land and the trees sit in each drawing of this copy of the sheet:

| Water has land               | Tile index | What it looks like                 |
| ---------------------------- | ---------- | ---------------------------------- |
| to the north                 | 5, 6       | a shore along the top              |
| to the north and east        | 7, 8       | a shore across the top-right       |
| to the east                  | 9, 10      | a shore down the right             |
| to the east and south        | 11, 12     | a shore across the bottom-right    |
| to the south                 | 13, 14     | a shore along the bottom           |
| to the south and west        | 15, 16     | a shore across the bottom-left     |
| to the west                  | 17, 18     | a shore down the left              |
| to the north and west        | 19, 20     | a shore across the top-left        |
| on no side, or on opposite or three or four sides | 2 | open water; the sheet has no drawing for the rest |

| Trees have trees             | Tile index | What it looks like                 |
| ---------------------------- | ---------- | ---------------------------------- |
| to the east, south, and west | 21, 29     | trees in the bottom half           |
| to the south and west        | 22, 30     | trees in the bottom-left           |
| to the north, south, and west | 23, 31    | trees in the left half             |
| to the north and west        | 24, 32     | trees in the top-left              |
| to the north, east, and west | 25, 33     | trees in the top half              |
| to the north and east        | 26, 34     | trees in the top-right             |
| to the north, east, and south | 27, 35    | trees in the right half            |
| to the east and south        | 28, 36     | trees in the bottom-right          |
| on every side, or on fewer than two sides that meet | 37 | woods; a new map has very few of the second |

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
tile's place.

A zone is 3 by 3. Its nine tiles are nine tiles in a row in the sheet,
read across and then down, so a tile's drawing is the drawing's first
tile plus its part number (0 to 8, with 4 the centre). Each zone has an
empty-zone drawing, then a building for each development level within
each land-value class, the lowest class first, nine tiles apart:

| Zone        | Empty zone | First building | Levels | Land-value classes | Last building |
| ----------- | ---------- | -------------- | ------ | ------------------ | ------------- |
| Residential | 240        | 261            | 4      | 4                  | 396           |
| Commercial  | 423        | 432            | 5      | 4                  | 603           |
| Industrial  | 612        | 621            | 4      | 2                  | 684           |

A building's first tile is the zone's first building plus 9 times
(class times levels plus level minus 1). A residential zone with no
building shows its single houses on its outer tiles: tiles 249 to 260,
three for each land-value class. 405 and 414, after the residential
buildings, are a hospital and a church.

A power plant is 4 by 4, its sixteen tiles in a row in the sheet, read
across and then down, so a tile's drawing is the plant's first tile plus
its part number (0 to 15, with 5 the centre):

| Plant               | Tiles      |
| ------------------- | ---------- |
| Coal power plant    | 745 to 760 |
| Nuclear power plant | 811 to 826 |

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
