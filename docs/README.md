# CityLimits documentation

CityLimits is a SimCity-style city builder by Tony Bark, written in C# with
Raylib-cs, that plays like Micropolis, the open-source SimCity Classic, and
goes its own way from there. These pages are for anyone who wants to play
it, mod it, or work on it. The project's front page is the
[README](../README.md) at the top of the repository.

## Playing and modding

- [Modding CityLimits](./modding/README.md): what a mod is, where it goes,
  and how to write your first. Start here if you want to change how the
  game plays without touching its code.
- [Tuning mods](./modding/tuning.md): every number and name a mod can
  change, with the limits on each.
- [Scenarios](./scenarios.md): challenges to play, the editor built into
  the game, and the scenario file itself, which agents also run from the
  command line to check how the game behaves.

## How the game keeps things

- [Saving](./saving.md): where cities are saved, the two save formats
  (`.fcl`, flat JSON, and `.cl`, binary), autosave, importing Micropolis'
  cities, and how a city saved by a newer version opens in an older one.
- [Schemas](./schemas/): a JSON schema for every version of every file
  format, for editors such as VS Code to check a file as it is written.

## Where it's going

- [Roadmap](./ROADMAP.md): the milestones, the release lines, what comes
  next, and how long it might take.
- [Scorecard](./scorecard.md): what is done, checked against the code, a
  build, or a run, with the evidence for each.
- [Changelog](../changelog.md): what changed in each version.

## Design

- [Design](./design/README.md): Tony Bark's mock-ups of how the game
  should look.

## Working on the code

[AGENTS.md](../AGENTS.md) holds the rules the code keeps, for people and
coding agents alike: the simulation is deterministic, it never touches the
engine, the game never crashes, and every format change is versioned. It
also lists the commands to build, test, and run the game.
