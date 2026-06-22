# Steam Achievement Manager

Steam Achievement Manager (SAM) is a lightweight, portable application used to manage achievements and statistics in the popular PC gaming platform Steam. This application requires the [Steam client](https://store.steampowered.com/about/), a Steam account and network access. Steam must be running and the user must be logged in.

This is the code for SAM. The closed-source version originally released in 2008, last major release in 2011, and last updated in 2013 (a hotfix).

The code is being made available so that those interested can do as they like with it.

There are some changes to the code since the last closed-source release:
- General code maintenance to bring it into a more modern state.
- Icons have been replaced with ones from the Fugue Icons set.
- Version has been bumped to 7.0.x.x to indicate the open-source release.

[Download latest release](https://github.com/gibbed/SteamAchievementManager/releases/latest).

[![Build status](https://ci.appveyor.com/api/projects/status/00vic6jliar6j0ol/branch/master?svg=true)](https://ci.appveyor.com/project/gibbed/steamachievementmanager/branch/master)

## Auto-Unlock (this fork)

This fork adds automatic achievement unlocking on top of the original SAM:

- **Per game (SAM.Game):** an **Auto-Unlock** button on the Achievements toolbar
  unlocks and commits every achievement that can be set, automatically skipping
  protected/online achievements (the ones shown in red) and any that are already
  unlocked. A game that is already complete is left untouched.
- **Whole library (SAM.Picker):** an **Auto-Unlock All** button processes every
  game currently shown in the list, opening each one briefly and unlocking its
  non-protected achievements before moving on to the next.
- **Headless mode:** `SAM.Game.exe <appId> auto` does the same for a single game
  without user interaction and then closes itself (used internally by
  *Auto-Unlock All*).

Protected/online achievements can never be changed by Steam Achievement Manager,
so they are always skipped.

## Attribution

Most (if not all) icons are from the [Fugue Icons](https://p.yusukekamiyamane.com/) set.
