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
