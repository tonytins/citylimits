# Design

Mock-ups of how CityLimits should look, kept beside the code so the
intent behind the interface is on record.

The files here were made by Tony Bark, the project's owner, not by Claude.
Claude, the AI assistant that helps with the code, only committed and
pushed them.

## The top bar

`citylimits-toolbar.figcraft` is the source, made in FigCraft, a
Figma-like design tool. `citylimits-toolbar.svg` and
`citylimits-toolbar.png` are exports of it.

The aim is a more modern, streamlined top bar, inspired mostly by Cities:
Skylines: a thin strip that shows the city's state at a glance and holds
the controls players reach for most, rather than a line of text. Left to
right:

- a pause and play button
- the month and year
- three speed buttons that double as a meter: one lit is Normal, two
  Fast, three Super fast
- the city's name
- a small residential, commercial, and industrial demand meter, in the
  zones' own colours, which opens a bigger demand panel when clicked
- the funds
- the population
- on the far right, the step count and the frame rate

The mock-up shows only the top bar. The tool palette and the overview
map are outlined where they sit, and the rest of the screen is left as
it was.

### Where the game differs from the mock-up

The mock-up is a guide, not a specification:

- **The date** is month and year (`MM/YYYY`). The simulation's calendar
  counts quarter-months, not days, so there is no day to show.
- **The font** is Raylib's built-in one. The mock-up's font is only
  FigCraft's default.
- **Sizes** follow the game's layout rather than the mock-up's pixels.
- **The demand meter**'s bars rise above or fall below a middle line, as
  the demand panel's always have, rather than standing at full height.
- **The step count** is a stopgap. It is meant to move into a future
  cheat system.
