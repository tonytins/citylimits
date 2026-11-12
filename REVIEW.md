# Review Guide

Use this checklist when reviewing a change to CityLimits. It is adapted from
[Hudini's review guide](https://github.com/tonytins/Hudini/blob/main/REVIEW.md),
by way of Distro Tycoon 2 and VentureOS, with checks for a city builder. The
rules it checks against are in [AGENTS.md](AGENTS.md).

## Before reviewing

- Read the change description and the relevant code, tests, and docs. If it
  names a roadmap milestone, check that it does what that entry says.
- Identify what the player should see, or the problem the change addresses.
- Check the available build, test, and CI results. Run `swift build` and
  `swift test` when the change or its verification warrants it, and run the
  game when a screen changed. Report checks that couldn't be run.

## Review checklist

- Does the behaviour match the stated intent, including empty, blank, and
  over-long input, the map's edges, and a city with no money?
- **Stability:** Can anything in the change crash the game? Is every new
  failure reported to the player, with the game carrying on?
- **Boundaries:** Are saves, scenarios, definitions, mouse positions, and tile
  indices validated before use? Are sizes checked before converting or
  slicing?
- **Core:** Does `CityLimitsCore` still avoid raylib, the window, and the
  clock? Does everything random draw from the simulation's `SeededGenerator`?
- **Saving:** Is new lasting state saved and restored as a feature, and
  described in `docs/saving.md`? Does a city loaded from a save made before the
  change still play?
- **Sessions:** Does the change keep play and the editor apart, without a flag
  checked in shared code?
- **Balance:** Does a rule change keep the city playable? Look for things that
  have become free or pointless, and numbers that can run away, go below zero,
  or overflow.
- **Interface:** Does input stop at the top window, so a click on a window
  doesn't also reach the map? Does the screen work at the smallest window size
  and after a resize? Is the layout in UIScript where the format allows?
- **Rendering:** Are textures loaded once, after `InitWindow`, and released
  before `CloseWindow`? Does the frame loop avoid allocating per tile?
- **Dependencies:** Are new packages or version bumps approved, called out, and
  in a change of their own?
- **Tests:** Do focused tests cover changed core behaviour, and would a new
  test fail without the fix?
- **Docs:** Are the changelog, roadmap, scorecard, README, and `AGENTS.md`
  accurate after the change? Do they describe only what has been shown to work?

## Writing review comments

- Report concrete correctness, stability, compatibility, balance, or
  missing-test concerns. Don't block changes on personal style preferences.
- Make each comment specific, constructive, and actionable. Point to the
  smallest relevant location and explain the impact and a useful next step.
- Prioritize findings by impact:
  - **Blocking**: stops the game from building or starting, crashes it, or
    loses a city in normal play.
  - **High**: breaks a common interaction or a save, or makes a city
    trivially exploitable.
  - **Medium**: causes a limited but meaningful behaviour, balance, or
    reliability issue, or leaves docs describing something that isn't true.
  - **Low**: a small edge case or non-blocking improvement.
- If no actionable issues remain, say so and note any important checks that
  were not run.
