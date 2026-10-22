# Design

Mock-ups of how CityLimits should look, kept beside the code so the
intent behind the interface is on record.

The files here were made by Tony Bark, the project's owner, not by Claude.
Claude, the AI assistant that helps with the code, only committed and
pushed them.

## The loading screen, the start menu, and the game

`citylimits.figcraft` shows the game's three stages in order, as frames
linked like a prototype: **Loading**, then the **Start Menu**, then the
**Game**. It shows where things go; sizes come from the game and its
art.

- **Loading:** the logo in the middle of the blue screen, and a line of
  text in the bottom right. Clicking moves on, and the logo glides up to
  its place on the start menu.
- **Start Menu:** the logo near the top, New Game and Load Game under it,
  and the version in the bottom right.
- **Game:** the city under the top bar, for completeness; it adds
  the play and pause button the toolbar mock-up lacked, and corrects the
  date to `MM/YYYY`.

### Where the game differs

- **The loading screen is for show:** the game loads in well under a
  second, so it stays 2.5 seconds, or until a key or a click.
- **Quit** joins the start menu, and **Load Game** is dimmed until cities
  can be saved.
- **New Game** opens a panel for the city's name and a seed, which the
  mock-up doesn't show.
- **The play and pause button** is blue, as the mock-up has it, in both
  states, so it doubles as a sign the city is running.

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
