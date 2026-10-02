# Steam Achievement Manager — Auto Edition

[![Build](https://github.com/Raksa75/SteamAchievementManagerAuto/actions/workflows/build.yml/badge.svg)](https://github.com/Raksa75/SteamAchievementManagerAuto/actions/workflows/build.yml)

Steam Achievement Manager (SAM) is a lightweight, portable application used to
manage achievements and statistics on Steam. This fork builds on the
open-source release of SAM and adds **automatic achievement unlocking** across
your whole library, with a clean dark interface.

> Requires the [Steam client](https://store.steampowered.com/about/), running
> and logged in, and network access.

## Features

- **Auto-Unlock All** (game list): unlocks every achievement SAM can change in
  all listed games, one game at a time, with a progress bar, a **Stop** button
  and a summary at the end (achievements unlocked, games skipped, failures).
- **Smart skipping** with an optional Steam Web API key: your profile is
  checked first, so games that are already 100% or have no achievements are
  never opened. Much faster on large libraries.
- **Auto-Unlock** (single game): tells you exactly how many achievements will be
  unlocked before saving them.
- **Protected achievements are never touched.** Achievements shown in red can
  only be earned in the game itself (online/server-side); SAM always skips them.
- A **sober dark theme** with a blue accent, dark title bar and scrollbars on
  Windows 10/11, plus progress and keyboard shortcuts in the game window.

## Getting started

1. Get the executables: download the `SteamAchievementManager` artifact from the
   latest [Build run](https://github.com/Raksa75/SteamAchievementManagerAuto/actions/workflows/build.yml),
   or [build it yourself](#building).
2. Keep `SAM.Picker.exe`, `SAM.Game.exe` and `SAM.API.dll` together, in a folder
   **outside** the Steam directory.
3. Start Steam, then run `SAM.Picker.exe`.
4. Click **Auto-Unlock All**, or double-click a game to manage it individually.

### Optional: Steam Web API key (recommended)

1. Get a free key at [steamcommunity.com/dev/apikey](https://steamcommunity.com/dev/apikey)
   (any domain name works, e.g. `localhost`).
2. In your [Steam privacy settings](https://steamcommunity.com/my/edit/settings),
   set **My profile** and **Game details** to **Public**.
3. In SAM, click **Set API key**, paste the key, click **Test key**, then **Save**.

The key is stored encrypted for your Windows account
(`%AppData%\SteamAchievementManager\settings.ini`). Without a key, or if it can't
be used, SAM still skips games without achievements and falls back to opening
the others briefly.

### Keyboard shortcuts (game window)

| Shortcut | Action                          |
|----------|---------------------------------|
| Ctrl+S   | Save changes to Steam           |
| F5       | Reload achievements and stats   |
| Ctrl+F   | Search achievements             |

## Command line

```
SAM.Game.exe <appId>        Open the manager for one game
SAM.Game.exe <appId> auto   Unlock everything SAM can change, without a window
```

In `auto` mode the exit code is the number of newly unlocked achievements, or a
negative error code: `-1` Steam unavailable, `-2` stats unavailable (game not
owned?), `-3` no achievements, `-4` Steam rejected the changes, `-5` unexpected
error.

## Building

The solution targets **.NET Framework 4.8 (WinForms)** and builds on Windows
with Visual Studio 2019 or later (or the Build Tools).

1. Open `SAM.sln`.
2. Select the **Release** / **x86** configuration.
3. Build the solution (**Ctrl+Shift+B**).

The executables are written to the `upload\` folder. Every push to `master` is
also built by GitHub Actions.

## Attribution

Based on [gibbed/SteamAchievementManager](https://github.com/gibbed/SteamAchievementManager).
Most (if not all) icons are from the [Fugue Icons](https://p.yusukekamiyamane.com/) set.
