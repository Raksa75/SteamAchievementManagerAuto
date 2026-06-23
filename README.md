# Steam Achievement Manager — Auto Edition

Steam Achievement Manager (SAM) is a lightweight, portable application used to
manage achievements and statistics on Steam. It requires the
[Steam client](https://store.steampowered.com/about/), a Steam account and
network access — Steam must be running and you must be logged in.

This fork builds on the open-source release of SAM and adds **automatic
achievement unlocking** and a **refreshed dark interface**.

## Features

### Automatic unlocking
- **Per game (SAM.Game):** an **Auto-Unlock** button on the Achievements toolbar
  unlocks and commits every achievement that can legitimately be set. It
  automatically skips protected/online achievements (the ones shown in red) and
  any that are already unlocked. A game that is already complete is left
  untouched.
- **Whole library (SAM.Picker):** an **Auto-Unlock All** button processes every
  game shown in the list, unlocking the non-protected achievements of each one.
- **Headless mode:** `SAM.Game.exe <appId> auto` unlocks a single game without
  any user interaction and then closes itself (used internally by
  *Auto-Unlock All*).

> Protected/online achievements can never be changed by Steam Achievement
> Manager, so they are always skipped.

### Faster, smarter *Auto-Unlock All*
To avoid opening games that are already finished, *Auto-Unlock All* can read your
achievement progress directly from your Steam profile:

- Click **Steam API Key…** in SAM.Picker and paste a key from
  [steamcommunity.com/dev/apikey](https://steamcommunity.com/dev/apikey). Your
  Steam profile and game details must be set to public. The key is saved between
  runs.
- With a key set, SAM scans your profile in parallel and **skips games that are
  already 100% or have no achievements**, only opening the ones that actually
  need unlocking — much faster on large libraries.
- Without a key it still skips games that have no achievements locally. If the
  key/profile turns out to be unusable, it automatically falls back to this
  local mode.

### Appearance
A sober dark theme with a single blue accent, applied across both the picker and
the per-game manager.

## Building

The solution targets **.NET Framework 4.8 (WinForms)** and builds on Windows
with Visual Studio (or the Build Tools).

1. Open `SAM.sln` in Visual Studio.
2. Select the **Release** / **x86** configuration.
3. Build the solution (**Ctrl+Shift+B**).

The executables are produced in the `upload\` folder. Run `SAM.Picker.exe` from
there (outside the Steam directory) with Steam running.

## Attribution

This fork is based on [gibbed/SteamAchievementManager](https://github.com/gibbed/SteamAchievementManager).
Most (if not all) icons are from the [Fugue Icons](https://p.yusukekamiyamane.com/)
set.
