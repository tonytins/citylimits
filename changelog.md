# CityLimits Changelog

## 0.5.104

Every window now has the same title bar, in the manner of macOS: a red
button at its left closes it, its title sits in the middle, and some
windows keep a toolbar at the right. The settings' tabs and the help's
pages live there now, and Apply, Cancel, and the like stay at the bottom.
Every window moves by its title bar, the ones on the start menu too.

To rename a city, click its name at the top of the city panel, type, and
press Enter; Escape leaves the name as it was.

Help has a second page, Editing scenarios, on making a scenario in the
game.

For developers: `WindowFrame` in `CityLimits.Interface/UI/` draws every
window's title bar and owns its placement, dragging, close button,
toolbar (`Toolbar`, `SelectedTool`), and in-place title editing
(`TitleEditable`, off by default, with `CheckTitle`). Its colours are in
`Theme`. A window that nothing else drags calls `FollowPointer`.

## 0.5.103

Power no longer needs a line beside every street. A zone or building on
a road has power whenever another one on the same road does. Roads use
none of a plant's capacity, so plants go further. When a city wants more
power than its plants make, the buildings nearest the plant along the
road keep theirs. Lines still reach anything no road does.

Stadiums, seaports, and airports now change the land around them, as
fire and police stations cover it. A stadium raises the land value
nearby, and the noise of a seaport or an airport lowers it, the airport's
furthest. Each needs power to do it.

For modders: definitions are format 2.1. `infrastructure.json` entries
for `road` and `rail` can set `carriesPower`; turn both off for
Micropolis' rule. Rail is off in the base game, and an entry may now
give `carriesPower` without a `price`. Facilities have `influences`, a
list of `effect` (`landValue`, `crime`, or `pollution`), `strength`, and
`reach`; `[]` takes the base game's away. 2.0 files still read. See
[Tuning mods](docs/modding/tuning.md).

For developers and agents: `PowerGrid.Trace` joins road and rail networks
(`InfrastructureSpec.RoadCarriesPower`, `RailCarriesPower`), walking each
nearest first and powering each building whole or not at all.
`FacilityInfluences` works out the facilities' influences from the map
when land value, pollution, and crime are, so there's no new save state.
The `docs/schemas/*.v2.1.schema.json` files are new.

The version now counts every commit, not only those that change the game
project: `GitCommitsRelativeTo` points GitInfo at the repository's root,
and `GitInfo.txt` stays where it is, so the count carries on from
0.5.102.

## 0.5.102

CityLimits now comes for Windows and macOS as well as Linux, each ready
to run with nothing else to install: a zip for Windows, an app for Apple
silicon and Intel Macs, and an AppImage or a plain folder for Linux. On
Windows, the game has its icon. On macOS, the app isn't signed by a
known developer yet, so the first time you open it, Control-click it and
choose Open.

For developers and agents: `packaging/` follows Photocraft's, with a
shared `env.sh` that reads the version once and a `package.sh` for each
platform, all runnable from Linux or macOS; see `packaging/README.md`.
Packages go to `dist/release/` and carry the .NET runtime unless given
`--framework-dependent`. `package-appimage.sh` is now
`linux/package.sh`, and appimagetool is pinned and checked. `citylimits
scenario --verbose` lists each tile an action changed, in the log and as
each action's `changes` in the JSON report, which stays format 1.

## 0.5.101

Mods can change more of the game: how strongly and how far fire and
police stations cover the city, the stadium, seaport, and airport (their
prices, the size of city that wants each, and how much they pollute),
what roads, rail, power lines, parks, and the bulldozer cost, and the
money and tax rate a new city starts with. A mod can now keep its files
in folders of its own. Power plants go in `power.json`; `plants.json`
still works. See [Tuning mods](docs/modding/tuning.md).

The game now writes its own numbers into a `common` folder beside its
settings, in the same files a mod uses, so you can see everything it
plays with. You can change them there too, but each new version writes
them again; a mod keeps a change for good.

Scenarios you save from the editor are now in a new format too. With
Save for 0.4 on, the editor saves them, and their cities, so 0.4 can
open them.

For developers and agents: scenarios are format 3.0, which is 2.2's
fields under the next major version, as every format rounds up with
0.5. Every 2.x and 1.x scenario still reads, and the built-ins stay as
they were. `ScenarioWriter.Write(scenario, folder, legacy: true)`
writes 2.2, and `ScenarioEdit.TrySave(city, out problem, legacy: true)`
writes the scenario as 2.2 and its city as 1.10.
`docs/schemas/scenario.v3.0.schema.json` is new.

Definitions are format 2.0: `zones`, `power`, and `services`, with
`strength` and `reach`, and the new `facilities`, `infrastructure`, and
`economy` (`FacilitySpec`, `InfrastructureSpec`, `EconomySpec`), each
with a v2.0 schema. 1.0 files still read, but strength and reach in one
are refused. The base game's values stay in code (`BaseGame`), and
`BaseSpecFiles` writes them to `common/` (`GameFolders.CommonFolder`) once
for each build and applies them before the mods. `SpecFolder` finds spec
files at any depth, in path order. `CitySimulation` takes its starting
funds and tax rate from the definitions when none are given. A save for
0.4 still carries definitions 1.0. In a save's features list, `traffic`
now stands for `roadTraffic` too; the sections keep their names.

## 0.5.100

The first of 0.5, Senri, a line kept for the long term. Cities now save
in a new format, 2.0, ready for what comes after. Every city you have
still opens, and 0.5 keeps opening them for good.

A city now opens with the mods you have installed. If you played it with
other mods, the news ticker says so, since it may play differently. A
city keeps the names of its mods, not the mods themselves.

0.4 can't open cities saved by 0.5. To take a city back to 0.4, turn on
Save for 0.4 in the Settings window's Gameplay tab before you save it;
anything 0.4 can't hold is left out.

For developers and agents: city saves are format 2.0, `.fcl` and `.cl`
alike. A save names its features (`"features": ["traffic", "rail", …]`,
the `FEAT` chunk) rather than counting minor versions: new state comes as a new feature, and the
version moves only for a change that breaks; a feature a save leaves out
is filled in as the version that added it fills it. A save lists its
mods (`"mods"`, the `MODS` chunk) and holds no definitions; reading takes
the definitions to run on (`CityFiles.TryRead`, `FlatCityFormat.TryRead`,
and `BinaryCityFormat.TryRead` take a `CityDefinitions`, the base game's
by default). 1.x saves still read, upgraded to 1.10 and then to 2.0.
`Write(city, legacy: true)` writes 1.10, which 0.4.111 opens, from the
same save model; 0.6 moves that choice to the command line, and 0.7
drops it. A part of a save this version doesn't know is left out, here
and when saving, apart from `connections`, which is reserved for Senri's
neighbours and carried through as it is.
`docs/schemas/city-save.v2.0.schema.json` is new.

## 0.4.111

Three new challenges join First Town in the Scenarios list, each a town  
that has grown into trouble. In Smokestack, it's 1965 and the mills in  
the middle of town are choking the homes around them: clear the air  
without losing the people. Hard Times, in 1972, is a town that grew fast  
and never hired a police force. And Aftershock, in 1922, has two  
earthquakes coming: rebuild after them without going broke. As with  
every challenge, the city plays on whether you win or lose.

For developers and agents: `smokestack`, `hard-times`, and `aftershock`  
are in `Assets/Scenarios`, each in format 2.2 with its city beside it as  
`.fcl`, made through `ScenarioEdit` and run as tests with the other  
built-ins.

**Warning**: don't let agents play Aftershock in headless mode unless  
you want them to be at it forever.

## 0.4.110

Scenarios you edit can be saved. Press Ctrl+S, or Save in the Escape  
menu, and the scenario goes to your `Documents/CityLimits/Scenarios`  
with its city beside it, so it starts from the city you built. Editing  
one of the game's own scenarios saves your own copy and leaves the  
original alone. The Scenarios list has a New button for a scenario of  
your own, and a scenario without goals yet is listed as a draft you can  
edit but not play.

New Game opens a new window: your city's name and its seed sit beside a  
map of what that seed grows, and Generate picks another seed until you  
like the land. It opens with a seed already picked, so there's always a  
map to look at.

For developers and agents: `ScenarioEdit` saves (`TrySave`), starts a  
new scenario (`New`), and tells whether anything changed since the last  
save (`HasChanges`); a scenario that starts from a saved city can move  
its start and money while that city is at its founding, checked against  
a copy of it. `Scenario.IsDraft` marks one with neither goals nor  
checks, which the shelf now lists. `NewCityPanel` shows the seed's map  
through `MapPreview.Show(TileMap)`.

## 0.4.109

A scenario's details can be edited in the game. While editing one, the  
city panel has an Edit scenario button under its goals, which opens a  
window of three columns: the scenario's name, start year, end, starting  
money, and description; its goals; and its disasters. Add a goal or a  
disaster with its column's Add, take one out with its x, and click a  
goal's measure or a disaster's kind to change it. A disaster can now  
repeat: every so many weeks, so many times. Nothing changes until you  
press Apply, and the window says what's wrong if the details don't add  
up; Cancel drops your edits. Moving the start year of a scenario made  
from a seed just moves its city's date: the map and everything built on  
it stay.

For developers and agents: scenarios are 2.2, adding `repeat` to an  
event, `{ "weeks": 10, "times": 4 }`; each strike is reported on its  
own. `ScenarioWriter` writes a scenario as a 2.2 file the loader reads  
back the same, and every built-in scenario is tested that way.  
`ScenarioEdit.TryChange` checks a `ScenarioDetails` through it, and  
`CitySimulation.MoveFounding` moves a city still at its founding to  
another year, leaving it as one founded then.  
`ButtonRow`, `TextArea`, and a smaller `TextField` are new in the  
interface library, for any window to use.

## 0.4.108

The Scenarios and Load windows show more of what you pick. Under the  
list, a scenario's briefing now sits beside a map of the city it starts  
with, and a saved city's date and age, population, funds, and when it  
was saved sit beside a map of it. Loading a city now takes two steps:  
click it to see it, then Open, press Enter, or click it again. Both  
windows are taller to make room. Editing a scenario, going back to  
before play or starting over keeps the tool you had in hand.

For developers: `MapPreview` draws a city's map into a texture once,  
when it is picked, in the colours `OverviewMap.CityColorAt` gives the  
overview's City mode. `LoadPanel` reads a city when it is picked and  
opens what it read. `StartMenu` and `LoadPanel` are disposable, and let  
go of their maps before the window closes.

## 0.4.107

Scenarios can be edited in the game. Pick one in the Scenarios list and  
press Edit: its city opens in the usual game view, paused, and the bar  
says you're editing it. Everything is free to build. Press play to test  
the scenario as a player would meet it, with its disasters on their  
dates and its goals judged at the end. When you've seen enough, the  
Escape menu's Back to before play puts the city back as it was when you  
pressed play, and Start over opens the scenario afresh. Scenarios can't  
be saved from the game yet; that comes next.

For developers and agents: `ScenarioEdit`, in `CityLimits.Core`, holds  
the scenario being edited and the test's checkpoint, kept as a flat  
save. `CityBuilder.BuildsForFree` makes building cost nothing, and a  
`BuildPlan` says when it is free. `GameLoop` takes the scenario being  
edited, and the game opens the checkpoint or the scenario afresh as it  
opens any other city, looking where it looked. `ConfirmPanel` wraps a  
long message.

## 0.4.106

The city panel no longer lists mods. When you play a challenge, a  
second column shows the challenge instead: its name, when it ends,  
whether it's under way, won, or lost, and the disasters still to come,  
with their dates. A free city's panel shows the city alone, and is  
narrower. A city that starts in a later year, as a scenario's may, now  
counts its age from that year instead of from 1900.

For developers and agents: `CityView`, in `CityLimits.Interface/UI/`,  
now holds what the map needs to be seen and worked on: the camera, the  
map's drawing and the overview, traffic, the train, fires, explosions,  
and an earthquake's shake, and the stroke being dragged with its  
preview. `GameLoop` draws the map through it, so the scenario editor can  
share it. `Challenge.UpcomingEvents` lists the disasters still to come  
with their dates. Saves are 1.10, adding the `founding` part (`FNDG` in  
`.cl`), the year the city was founded, which `CitySimulation.FoundedYear`  
holds; older versions leave it out, and older saves count from 1900.

## 0.4.105

Finally removed the tick counter from the HUD. Now that they're  
tied into the date system, I can finally get rid of that old relic.

## 0.4.104

The stadium, the seaport, and the airport are in, at the bottom of the  
palette, as in Micropolis: a stadium for $5,000 and a seaport for  
$3,000, each 4 by 4, and an airport for $10,000, 6 by 6. Each needs  
power. A big city needs them: past about 10,000 residents with no  
stadium, residential demand can't rise above zero, and the city says  
"Residents demand a stadium"; industry wants a seaport, and commerce an  
airport, in the same way. The demand panel says what is holding demand  
back. A seaport or an airport pollutes as a coal plant does. Now and then  
a powered stadium fills up and a game plays on its field. Micropolis  
cities you import now bring their seaports, airports, and stadiums with  
them.

For developers and agents: saves are 1.9, adding the `facilities` part  
(`FCLT` in `.cl`). The map holds the bare ground beneath each building,  
and the part lists each one by its centre with its power and its holes,  
so 0.4.103 and older open the city as if they had never been built.  
Scenarios are 2.1, adding the `Stadium`, `Seaport`, and `Airport` tools  
and counts; the report's counts include them. `CityDemand.Update` takes  
the `DemandCaps` that `CitySimulation.Facilities` holds, and  
`Footprint.Largest` is now 6.

## 0.4.103

Scenarios can be played. The start menu's Scenarios lists the  
challenges, the game's own and any you put in  
`Documents/CityLimits/Scenarios`, and a file that can't be read is  
listed with why. Pick one to read its briefing and goals, and Play  
starts its city. What the challenge builds and the disasters it brings  
come on their dates; your settings still decide whether disasters  
strike on their own. The city panel lists each goal and how it stands,  
and at the end a pop-up says whether you won, and which goals you  
missed. The city plays on either way. Saving partway keeps the  
challenge, so it carries on where it left off. First Town is an  
example: reach 2,000 people by 1910 and stay out of debt.

For developers and agents: saves are 1.8, adding the `challenge` part  
(`CHAL` in `.cl`), which only a city being played as a challenge has;  
an older version may leave it out or keep it. The binary codec now  
holds nullable values. A scenario that can't be read no longer falls  
back to the demo town: `scenario` says why and exits with 1, and  
`ScenarioReport.Fallback` is gone. `GameFolders.ScenariosFolder` is the  
player's scenario folder.

## 0.4.102

For developers: the camera, the map's drawing, the theme, the tools, and  
every screen and panel moved out of the game into a library of their  
own, `CityLimits.Interface`, so a scenario editor can share them with  
play. Their namespaces are unchanged. The game keeps its window, its  
loop, its commands, and its version, which it gives the start menu.

## 0.4.101

For developers and agents: scenarios are format 2.0, the format's first  
major version. Times are the game's dates, a month written `YYYY-MM` and  
an optional `week` of it, rather than ticks: `ends` in place of `ticks`,  
and `at` in place of `tick`. A scenario can `start` in a later year, or  
start from a saved `city` beside it, a `.fcl` or `.cl` file. `events`  
strike a fire, a flood, or an earthquake on a date, whatever the  
`disasters` switch says, and `goals` set what a player has to meet by  
the end: a population, funds, or the pollution, crime, or traffic  
average. A scenario with goals is a challenge; the report says whether  
it was `won`, apart from `passed`, so a lost challenge still exits with  
0. The report gives dates beside its ticks, and each event and goal;  
`scenario list` marks challenges. Format 1 files are still read, and the  
built-in scenarios are rewritten in 2.0. `docs/scenarios.md` describes  
the new format, and `docs/schemas/scenario.v2.0.schema.json` is its  
schema.

## 0.4.100

CityLimits is now a beta. Your city lives through everything Micropolis  
put it through: traffic and rail, pollution and crime, fires, floods, and  
earthquakes, on top of the power, taxes, services, and growth the alpha  
brought. It will stay a beta for a while, whatever its version says, as  
the work turns to polish, starting with scenarios you can play from the  
start page. It still has no city score, no choice of map size, and no  
sound.

### Since 0.3.100

- **Traffic.** Your city's trips fill its roads, and busy roads show  
  light or heavy traffic that moves. Heavy traffic costs more upkeep,  
  and the city tells you when jams get frequent. The overview map has a  
  traffic mode.
- **Rail.** Lay rail like a road, under water as a tunnel, across roads  
  and power lines. Trips use it as they use roads, without the jams, and  
  a commuter train wanders the track once your city has people.
- **Pollution and crime.** Industry, coal plants, and busy roads pollute  
  the land around them, which lowers what it's worth and keeps homes from  
  growing where the air is worst. Crime is highest where land is cheap  
  and crowded, and police stations bring it down. The city tells you when  
  either is very high, and the overview map shows both.
- **Disasters.** Now and then a fire breaks out, a river or lake floods  
  the land beside it, or an earthquake shakes the city. Fires spread, and  
  fire stations put them out sooner; floods drain away in time. Damaged  
  buildings recover on their own, at no cost: zones as they grow, and  
  plants, stations, hospitals, and churches once their ground is clear.  
  The news ticker reports each disaster in red as it happens, and warns  
  you when your city is at risk of fire.
- **The interface.** The bar is along the bottom of the screen now, over  
  a news ticker that never stops, with headlines from your city's world,  
  some about how it's doing. A green question mark opens a help window  
  with the keys and the mouse. What your work will cost, or why it can't  
  be built, shows beside the pointer.
- **The menu and the settings.** Escape opens a column of buttons: Resume,  
  Save, Load, Settings, Main menu, and Quit. The settings window, also on  
  the start page, can make the game fill the screen, turn autosave off or  
  keep two, and turn disasters off, all of them or one at a time.
- **Older versions open newer cities.** A city saved by this version opens  
  in any version since 0.3.103, which leaves out what it doesn't know, and  
  0.3.100 to 0.3.102 have backports that do the same.
- **Micropolis cities** come in with their rail.

For developers, modders, and scripts:

- **File formats** say their version, as major.minor, and  
  `docs/schemas/` holds a JSON Schema for each, generated from the  
  game's own readers. Saves are at 1.7, scenarios at 1.3, and mods at 1.0.
- **Saves** list the parts each version added, with whether an older  
  version may leave each out or write it back, as PNG does with its  
  chunks; `docs/saving.md` describes every part.
- **Scenarios** can lay rail, count it, let disasters happen with  
  `"disasters": true`, and expect `Refused:OnFire` or `Refused:Flooded`.
- **The headless run** logs pollution, crime, fires, and floods each month.
- **A city can start in a later year**, through `CitySimulation`'s  
  `startYear`, ready for scenarios that start in one.

## 0.3.111

Floods and earthquakes have come to your city, as in Micropolis. Now and  
then a river or lake rises over the land beside it, spreading for a while  
before it drains away and leaves bare ground behind. An earthquake shakes  
the whole city, leaving rubble and fires in its buildings, and the screen  
shakes with it. Both are reported in red on the news ticker the moment  
they happen.

Damaged buildings recover, as they do in Micropolis, and it costs you  
nothing. Zones rebuild their damaged parts as they grow. Power plants,  
fire and police stations, hospitals, and churches rebuild any part that  
is clear ground, so they recover from a flood on their own; after a fire  
or an earthquake, bulldoze the rubble and they rebuild the rest.

The Disasters tab in the settings now has a switch for floods and one for  
earthquakes, beside fires. An earthquake's fires come with the  
earthquake, even with fires switched off.

Switching fullscreen on or off now opens a small window saying the change  
takes effect the next time you start the game.

For developers and agents: saves are format 1.7, with floods in a part of  
their own (`"flood"`, and the `FLOD` chunk). Scenarios are format 1.3,  
adding `Refused:Flooded`, and `"disasters": true` now lets floods and  
earthquakes happen too. `settings.json` gains `Floods` and `Earthquakes`.  
The headless run's monthly line gives the floods.

## 0.3.110

Fire has come to your city, as in Micropolis. Now and then a fire breaks  
out in a building, and the news ticker says so in red. It spreads to  
whatever can burn beside it, trees included, and burns down to rubble.  
Fire stations nearby put fires out much sooner. A building that loses a  
tile keeps standing, and rebuilds the burned part as it grows; when fire  
reaches a building's heart, the building is lost. Burning factories,  
power plants, and stations explode. You can't bulldoze a fire, but you  
can clear what's around it so it has nowhere to go.

The ticker also warns you when your city is at risk: when much of it is  
beyond the reach of a fire engine, when fire funding has been cut, or  
when homes stand right against the woods.

If you'd rather not have disasters, turn them off on the Gameplay tab of  
the settings, or turn off fires alone on the new Disasters tab. It's your  
setting, not your city's, so every city you play follows it.

Fullscreen now takes effect the next time the game starts, since  
switching back to a window while playing could leave the window filling  
the screen.

For developers and agents: saves are format 1.6, with fires in a part of  
their own (`"fire"`, and the `FIRE` chunk) and every building written  
whole in the map, so older versions open the city as if it had been  
repaired. Scenarios are format 1.2, with `"disasters": true` to let fires  
start on their own and `Refused:OnFire` for a burning tile.  
`settings.json` gains `Disasters` and `Fires`. The headless run's  
monthly line gives the fires burning. The new disaster roll shifts the  
city's later rolls, so growth timings move and `demo-town` ends at  
$16,572.

## 0.3.109

There's a settings window now. Open it from Settings on the start menu,  
or press Escape in your city and choose Settings. Its Graphics tab can  
make the game fill your screen, and its Gameplay tab can turn autosave  
off, or keep one or two autosaves. A change takes effect at once, and is  
kept for next time as soon as you close the window.

The menu Escape opens in your city is now a column of buttons: Resume,  
Save, Load, Settings, Main menu, and Quit, so you can save and open  
cities from it without the keyboard shortcuts.

For developers and agents: `settings.json` gains `Fullscreen`, and  
`AutosaveSlots` may be `0` for no autosaves; a version from before reads  
`0` as out of range, warns, and keeps one autosave.

## 0.3.108

Your city now has pollution and crime, as in Micropolis. Industry, coal  
plants, and busy roads pollute the land around them, which lowers what  
it's worth, and homes won't grow where the air is worst, so keep your  
coal plants and factories away from where people live and run a power  
line out to them. Crime is highest where land is cheap and people are  
crowded, and police stations bring it down; where it runs very high, it  
takes a little off the land's value too. The city tells you when  
pollution or crime is very high, and the overview map has two new modes,  
Pollution and Crime, to show you where.

For developers and agents: saves are format 1.5, adding pollution and  
crime as two parts (`"pollution"` and `"crime"`, and the `POLL` and  
`CRIM` chunks) that older versions may leave out or keep, so 0.3.103 and  
later, and the backports, still open new cities. The headless run's  
monthly line gives the pollution and crime averages, and its demo town,  
the built-in `demo-town` and `zones-and-bulldozer` scenarios, and  
`small-town` power their zones from a coal plant up a line, out of the  
pollution; `small-town`'s industry now holds back the homes and shops  
beside it, and its checks say so.

## 0.3.107

Older versions can now open your newer cities too. 0.3.100, 0.3.101, and  
0.3.102 each have a backport, 0.3.100.1, 0.3.101.1, and 0.3.102.1, that  
opens cities saved by 0.3.103 and later, as 0.3.103 does. They leave out  
what they don't have, such as rail, and keep what they can, such as  
traffic, so it comes back when you open the city here again. CityLimits  
already opened older cities well, and it had been bothering me that the  
older versions couldn't open newer ones. The backports also read each  
setting on its own, as 0.3.103 does.

For developers and agents: a save that kept a newer save's parts but had  
to fall back to this version, because one part couldn't be kept, now  
opens again with them: before, a `.fcl` save was refused, and a `.cl`  
save dropped them. The backports live on `release/0.3.100` to  
`release/0.3.102`, and a save from each is a test fixture. The version  
now comes from `GitInfo.txt` alone, even with release tags in the  
repository.

## 0.3.106

Rail is here, as in Micropolis. Lay it like a road with the new rail tool  
beside the road in the palette, at $20 a tile; under water it becomes a  
tunnel at $100, starting from rail on the bank. Rail crosses straight  
roads and power lines, and they cross it.

Your city's trips use rail and roads alike, so rail along a zone's edge  
serves it, and rail never gets traffic jams. Each tile of rail costs as  
much upkeep as two tiles of road, and like roads, rail crumbles when  
it's underfunded. A city of more than 50 zones with little rail will hear  
that its rail system is inadequate.

Once your city has more than 20 people, a commuter train now and then  
sets out and wanders the track, disappearing into tunnels.

Micropolis and SimCity Classic cities now import with their rail, instead  
of losing it.

The tool palette's buttons are now switches: click a tool to pick it up,  
and click it again to put it down. The game starts with no tool held, so  
the Inspect tool's hint only shows when you choose it. The number keys  
no longer pick tools.

For developers and agents: saves are format 1.4, with rail in a part of  
its own (`"rail"`, and the `RAIL` chunk) over the map's ground, roads, and  
lines, so a version that doesn't know rail opens the city without it.  
Scenarios are format 1.1, adding the `Rail` tool and the `Rail` count,  
and a built-in `rail` scenario.

## 0.3.105

The bar is now at the bottom of the screen, with a news ticker running  
underneath it that never stops, much like SimCity 3000's. It carries the  
game's notices and the city's warnings in yellow (saves, autosaves, "The  
city needs a power plant") among white headlines from your city's world.  
Some headlines react to how your city is doing: its population passing a  
milestone, a new year, high taxes, a housing shortage, money troubles,  
traffic, blackouts, the season. Others are just flavour. Red is kept for  
disasters, which come later.

The key list along the bottom is now a Help window. Open it with the  
green question mark at the end of the bar or with F1, move it by its  
title, and close it with its x, the question mark, F1, or Escape. The  
city keeps running while it's open.

What a stroke costs, why it can't be built, and what it came to now show  
beside the pointer, in black on yellow. Hold the Inspect tool and point  
at a tile to read what it is in the same way; the readout panel in the  
corner is gone.

The ticker stands still while your city is paused.

The tool palette and the overview map have moved up into the top  
corners, and the loading screen's phrases now scroll by like the ticker.

For developers: the ticker's queue, its headlines, and the desk that  
picks them live in `CityLimits.Core/News/`. The desk only reads the city  
and uses its own random generator, so headlines never change the  
simulation and aren't saved.

## 0.3.104

Traffic now shows on your roads. Cars appear on a road when its block  
gets busy and pack it when traffic is heavy, on bridges and crossings  
too, and they move while the city runs and stop while it's paused, as in  
Micropolis.

Busy roads cost more: a road that's showing heavy traffic when the  
simulation passes it counts twice toward the roads' upkeep, as in  
Micropolis. Each year the city works out its average traffic, and if  
it's high, you'll hear that traffic jams are frequent.

The overview map has a Traffic mode, and its buttons are now in two rows  
so their names fit.

For developers and agents: saves are format 1.3, adding the traffic each  
road shows and the year's average (`"roadTraffic"`, and the `RTRF`  
chunk); older saves start with quiet roads. The headless run's yearly  
line includes the traffic average. The demo town's funds after two years  
are now $16,603, down from $16,609.

## 0.3.103

From this version on, CityLimits can open a city saved by a newer  
version, as long as the newer version says it's safe to. Parts it  
doesn't understand yet are kept as they were when you save, where that's  
safe, or left out for the newer version to rebuild. The game tells you  
when a city came from a newer version, and the log says what happens to  
each part. Before, a newer save simply wouldn't open.

Autosaves now use the same format as your city: a city saved as `.fcl`  
autosaves as `.fcl`.

If one setting in `settings.json` is wrong, only that setting goes back  
to its default now; the rest are kept.

For developers and agents: saves are format 1.2, adding a parts list  
(`"parts"`, and the `PART` chunk) that names what each version added and  
whether an older game may ignore it or keep it. `docs/saving.md`  
explains how a newer save opens.

## 0.3.102

The roads now keep track of traffic. Every time someone in your city  
makes a trip, the roads they drive along get a little busier, and the  
traffic fades away again on roads nobody uses. You can't see it yet:  
drawing it on the roads, and what heavy traffic costs, come next.

Saves now use format 1.1, which includes the traffic. Cities saved  
before load as they were, with quiet roads that fill up again as people  
drive.

For developers and agents: `TrafficMap` holds each 2 by 2 block's  
traffic, the headless run logs the busiest road each month, and  
`docs/saving.md` lists what each save format version added. The schema  
for 1.1 saves is `docs/schemas/city-save.v1.1.schema.json`.

## 0.3.101

For modders, scenario authors, and agents: saves, scenarios, and mod  
files now carry a version with a major and a minor number, written as  
`"format": "1.0"`. Files that say `"format": 1` still work. A file can  
also name its schema in a `"$schema"` line, and an editor such as VS Code  
will then check it and suggest its fields as you type; the schemas are in  
`docs/schemas/`, and the built-in scenarios name theirs. The game ignores  
the line, and writes it into every `.fcl` save.

Under the hood, every file the game reads or writes as JSON goes through  
code generated at build time instead of reflection, so saves, scenarios,  
mods, and settings keep working in a trimmed build.

## 0.3.100

CityLimits is now an alpha: a playable little city. You can start a city,  
lay it out, power it, tax it, watch it grow and run into trouble, and keep  
it, saving, loading, and autosaving it, or bring one in from Micropolis.  
It still isn't at parity with SimCity, or with any city builder made since:  
there is no traffic, pollution, crime, fire, or disaster yet, and no city  
score. Those come next.

### Since 0.2.100

- **Starting a game.** CityLimits opens on a loading screen and a start  
  menu. New Game asks for your city's name and, if you like, a seed, the  
  number its map grows from; Load Game opens a saved city.
- **Power.** Coal and nuclear plants, at $3,000 and $5,000, power the  
  zones around them and pass power on through power lines, which cross  
  water and roads. Each plant powers only so many tiles; past that, the  
  city browns out. Zones without power don't grow, and blink a lightning  
  bolt to say so.
- **Taxes and the budget.** The city collects taxes and pays for its  
  roads and stations each year. The budget panel shows the year as it  
  stands and opens by itself when spending outruns income. High taxes  
  slow growth; underfunded roads crumble.
- **Services.** Police and fire stations cover the blocks around them,  
  parks make land worth more, and the city grows hospitals and churches  
  as it needs them.
- **The overview map** shows the whole city in a corner, and power, fire  
  and police coverage, land value, and population density over it.
- **The interface.** A new top bar has pause, play, and speed buttons,  
  the city's name, a demand meter, the money, and the population. The  
  demand meter opens a chart of demand over time; your city's name opens  
  the city panel, with the city at a glance, its mods, and its seed. The  
  budget, demand, and city windows can be moved, and Escape opens a menu.  
  The tool palette is laid out as Micropolis' is.
- **Saving.** Ctrl+S saves your city, Ctrl+O and Load Game open one, and  
  the city saves itself every 15 minutes of play. A loaded city carries  
  on exactly where it left off. Leaving a city with unsaved changes asks  
  first.
- **Micropolis cities.** Drop a `.cty` file from Micropolis or SimCity  
  Classic into your Cities folder to bring it in.
- **Your files** live where your computer expects them: saves and mods in  
  `Documents/CityLimits`, settings in your hidden settings folder. That  
  also lets the Linux AppImage save its settings.

For developers, modders, and scripts:

- **Saves** come in two formats: `.cl`, the game's own, and `.fcl`, one  
  readable JSON file for people and agents to read and compare.  
  `docs/saving.md` describes both, and the Micropolis import.
- **Mods** can change plants and stations as well as zones, and now live  
  in `Documents/CityLimits/Mods`. `docs/modding/` is a guide for players  
  writing their first mod, and `tuning.md` lists everything a mod can  
  change.
- **Scenarios** can build, count, and check everything above, power  
  included.
- **The Linux AppImage** is named for the game's version and builds from  
  any folder without FUSE.

## 0.2.119

You can bring your old Micropolis and SimCity Classic cities into  
CityLimits. Drop a `.cty` file into `Documents/CityLimits/Cities` and it  
shows up in Load Game as a Micropolis city; open it and it comes in with  
its roads, power lines, zones, and buildings, its money, tax rate, and  
date. Press Ctrl+S to keep it as a CityLimits city. The `.cty` file  
itself is never changed.

Some things CityLimits doesn't have yet, like rail, seaports, airports,  
and stadiums, become bare ground when a city comes in; the game tells  
you how many, and the log lists them. Where rail crossed a road or a  
power line, the road or the line stays.

For developers: `MicropolisCityImport` reads `.cty` files, and  
`docs/saving.md` describes the format and how each tile comes across.

## 0.2.118

Your city now saves itself every 15 minutes of play, so a crash or a  
power cut can't cost you more than that. The clock only runs while the  
city does: paused, nothing is counted. Autosaves sit beside your saves  
as `Joffburg (autosave).cl`, are marked in Load Game, and are skipped  
when nothing has changed since your last save. Open one and press Ctrl+S  
to save it as the city's own.

To keep the last two autosaves instead of one, set `"AutosaveSlots": 2`  
in `settings.json`; each new autosave then replaces the older.

## 0.2.117

Cities now save in CityLimits' own binary format, `.cl`: about 40% smaller  
than before, and quick to read and write. Each part of the file carries a  
checksum, so a damaged save is caught and named rather than loading  
wrong. Cities you saved as `.fcl` still load, and save back as `.fcl`;  
Load Game lists both, marking the flat ones.

For developers and agents: `BinaryCityFormat` reads and writes `.cl`, and  
`CityFiles.TryRead` and `TryWrite` handle either format by its  
extension. `docs/saving.md` describes the binary layout. The flat format  
stays the one to read and compare.

## 0.2.116

You can save your city. Press **Ctrl+S** and it's saved under its name in  
`Documents/CityLimits/Cities`; **Load Game** on the start menu, or  
**Ctrl+O** in a city, lists your saved cities with their date,  
population, and money, newest first. A loaded city picks up exactly  
where it left off, down to the step, even if you saved partway through a  
month or while paused.

- Saving over the same city's file just saves. If another city has the  
  same name, the game asks before replacing its file.
- Leaving a city for the menu, for another city, or to quit asks first  
  when it has changes you haven't saved.
- A save is written in full before it replaces the old one, so a failure  
  can't spoil your last save.
- A city keeps the mods it was made with, so it loads the same even if  
  you change your mods afterwards.

For developers and agents: saves use the flat city format, `.fcl`, one  
JSON file with the map as rows of characters, described in  
`docs/saving.md`. Reading one is strict and says exactly what is wrong  
and where. `FlatCityFormat` and `CityFiles` (`CityLimits.Core/Saving`)  
read and write them.

## 0.2.115

Click your city's name in the top bar for the city panel: the city at a  
glance, with its date and age, population, demand, funds and tax rate,  
and this year's and last year's cash flow; the mods it was made with;  
and, at the bottom, its seed. Click the seed to copy it, so you can share  
it or grow the same map again with `--seed`. Type a new name at the top  
and press Enter to rename the city. The game pauses while the panel is  
open, and you can move it by its title, as with the budget and demand  
windows.

For developers: `CityDefinitions.AppliedMods` lists the mods a city was  
made with, with their names and versions, and the movable windows share  
`IMovableWindow`.

## 0.2.114

CityLimits now opens with a loading screen and a start menu, after a  
mock-up by the owner. The loading screen shows the logo and a line in the  
corner while it reticulates its splines; press a key or click to move on,  
and the logo glides up into the start menu.

- **New Game** asks for your city's name and, if you like, a seed, the  
  number its map grows from. Leave the seed blank for a new map each  
  time. `--seed` on the command line fills it in.
- **Load Game** is dimmed for now; it lights up once cities can be saved.
- **Quit** leaves the game.

In a city, Escape (when there's no stroke or panel to close) opens a  
menu: Resume, Main menu, and Quit. Click the city's name in the top bar  
to rename it. The play and pause button is blue, so you can tell at a  
glance the city is running.

For developers: `NewCity.TryRead` checks the new-city form, and a city  
started in the window no longer always uses seed 2000; the log names the  
seed it used.

## 0.2.113

CityLimits now keeps its files where your computer expects them, so  
packaged versions such as the Linux AppImage, which run from a read-only  
folder, can save their settings at last.

- **Settings** go in your hidden settings folder: `~/.config/CityLimits`  
  on Linux, `%APPDATA%\CityLimits` on Windows. Settings an older version  
  left beside the game are picked up once and moved there.
- **Mods** now go in `Documents/CityLimits/Mods`, which the game makes  
  the first time it opens. Move any mods from the old `Mods` folder  
  beside the game. Saved cities will go beside them, in  
  `Documents/CityLimits/Cities`, once saving arrives.
- **A portable copy** (one with a `settings.json` beside the game, in a  
  folder it can write to) keeps everything beside the game, as before.

For developers: `GameFolders` (`CityLimits.Core/Settings`) works out  
these folders, and `GameMods.Load` takes the mods folder. The AppImage  
script restores packages before asking GitInfo for the version, so it  
works from a fresh clone.

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
