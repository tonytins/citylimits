# CityLimits Changelog

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
