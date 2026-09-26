# Changelog

## 1.0.14

- Keel's files changed: Daywheel now runs on Keel 2. Keel's working parts moved into one shared core, `Keel\Keel.Core.2.0.0.dll`. Installed by hand, every Keel 2 mod in a game uses the newest core there, so a fix to the core reaches them all at once. `Keel\Keel.dll` is now a small starter that won't change again, and it starts BepInEx itself when BepInEx is in the game folder, older BepInEx 5 versions included.
- Installed by hand, Daywheel now comes with what it needs to start on Linux and on a Mac too, with one launch option in Steam: `sh ./Keel/run.sh %command%`, or on a Mac the same with `run.sh`'s full path. The readme has the steps. So far only Valheim's Linux dedicated server has run it; the Linux game, the Steam Deck, the Mac and Proton haven't been tested yet.
- Keel moved to Doorstop 4.5.0, which the zip now carries for Windows, Linux and Mac. Its `winhttp.dll` is Doorstop 4.5.0's own, so it's no longer the same file BepInExPack carries.
- Keel writes `Keel\Keel.cfg` the first time it runs. Besides the BepInEx switch it has `SkipCores`, for passing over a core that doesn't work.
- Keel is now released under the Mozilla Public License 2.0, so `Keel\Keel-LICENSE.txt` holds that text. Daywheel itself stays under the MIT licence.
- No change to the wheel itself, and your settings carry over.

## 1.0.13

- Daywheel no longer needs BepInEx. Installed by hand into your Valheim folder, it runs on its own, through Keel, a small starter that comes in the same zip.
- It still works with BepInEx and mod managers, and your settings there carry over. If BepInEx is in the same game folder, it keeps starting first and your other mods keep running.
- Installed by hand, it lets the game know that it's modded, the way BepInEx does, so the main menu says so and achievements are off.

## 1.0.12

- Pick the day number's colour while moving the wheel: hold the middle mouse button and move the mouse. A quick tap puts back the colour it came with. There's a new `DayColor` setting for it too.
- A clearer move menu: a dark box beside the wheel with one key to a line, and the colour field under it.
- Fixed: dragging the wheel to the left side of the screen pushed the move menu off the screen. The menu now stays on screen, next to the wheel, wherever the wheel is.
- The wheel itself can't be moved off the screen any more.
- Fixed: moving the wheel with a hammer, hoe or cultivator out could place a building piece or open the build menu. Put the tool away first now.
- The store page has a picture of the wheel and its move menu in game.

## 1.0.9

- Added a link to the source code.
- Added this changelog.
- No change to the mod itself.

## 1.0.8

- First public release.
