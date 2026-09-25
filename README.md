# Daywheel

A small day and night wheel for your HUD, so you can see at a glance how much
daylight is left. It doesn't need BepInEx: copy it into your Valheim folder
and play, or install it with a mod manager as usual.

The mark at the top is "now". The ring turns as the day goes on, so the
distance from the mark to the dark part is the daylight you've got left. The
day number sits underneath.

It follows the game's own clock. Day runs from a quarter to three quarters of
the way through each 20 minute cycle, so the light and dark halves match the
sky you're under.

![Daywheel in game, with its move menu and colour field open](docs/screenshot.png)

## Themes

There are seven looks to pick from:

- **Ember and moonstone** (the default): amber by day, deep blue at night
- **Northern twilight**: pale gold and indigo, a bit softer
- **Rootbound forest**: a ring of roots and leaves, mossy at night
- **Dvergr lantern**: an eight-sided gold and violet frame
- **The wolf chase**: a light wolf and a dark wolf chasing each other's tails
- **Mountain relic**: frost-blue carved stone
- **Ashlands**: black rock with lava running through it

Set one in the config, or flip through them in game (see below).

## Moving and resizing

Press **F8** to move the wheel. A small menu opens beside it, listing the
keys, and it stays on screen wherever the wheel goes. Your character stands
still until you finish. Put away a hammer, hoe or cultivator first: while
one is out, the game uses the mouse for building. Then:

- hold the left mouse button and move the mouse to drag the wheel. You don't
  need to point at it, so it works anywhere on screen
- **Page Up** and **Page Down** (or **+** and **-**) make it bigger or smaller
- the arrow keys nudge it (hold Shift for bigger steps, Ctrl for smaller)
- right-click, or press **End**, for the next look, and **Home** for the one
  before
- hold the middle mouse button and move the mouse to pick the day number's
  colour: left and right change the colour, up and down make it lighter or
  darker, and with Shift held, stronger or softer. A small field in the menu
  shows where you are. A quick tap of the middle button puts back the colour
  it came with

Press **F8** again when you're done, and your controls come back. The
position, size, look and colour are saved. Opening your bag, the map, the
build menu or the game menu also ends it.

It's done this way so the minimap stays in view while you line the wheel up.
The bag's crafting panel and the big map both cover that corner.

## Config

| Setting | What it does |
|---|---|
| `Show` | Turns the wheel on or off |
| `ShowDayCount` | Shows the day number under the wheel |
| `DayColor` | The day number's colour, as a hex code. `FFFFFF` is white |
| `Size` | Width, in the game's interface units, so it follows the game's UI scale |
| `X`, `Y` | Position, measured from the top right corner |
| `Theme` | Which look to use, spelled exactly as listed above |
| `Glow` | Glow strength. 1 is normal, 0 turns it off |
| `MoveKey` | The key for moving the wheel, F8 by default |

When Keel starts Daywheel, it keeps these in `Keel\Daywheel\Daywheel.cfg`,
which appears the first time you play. When BepInEx starts it, as it does
with a mod manager, they're in BepInEx's config folder, where the manager's
config editor can change them. You won't usually need to touch `Size`, `X`
or `Y` yourself, since dragging the wheel sets them.

## Installing

You don't need BepInEx. Daywheel comes as one zip that works either way.

**By hand:** from the zip, copy `winhttp.dll`, `doorstop_config.ini` and the
`Keel` folder into your Valheim folder, the one with `valheim.exe` in it. In
Steam you can find it by right-clicking Valheim and choosing Manage, then
Browse local files. The two files are what start Daywheel with the game, and
the `Keel` folder holds Daywheel itself. Then play as usual. Installing by
hand works on Windows; on Linux and the Steam Deck, use a mod manager.

**With a mod manager:** install Daywheel from Thunderstore with r2modman or
the Thunderstore Mod Manager, like any other mod. Mod managers start their
mods through BepInEx, and Daywheel loads there as an ordinary BepInEx mod. To
install a zip you've downloaded, go to **Settings → Import local mod** in the
manager and pick it.

If you use a mod manager for other mods, install Daywheel through it too. The
manager puts its own copies of `winhttp.dll` and `doorstop_config.ini` back
into the game folder every time it starts the game, so a copy installed by
hand wouldn't start.

If you run BepInEx without a mod manager, the simplest way is to put
`Keel\Daywheel\Daywheel.dll` into `BepInEx\plugins`, in place of any older
`Daywheel.dll` there.

You can also install by hand as above, into a game folder that has BepInEx
in it. Keel then starts BepInEx first, so BepInEx and your other mods keep
running. Delete any older `Daywheel.dll` from `BepInEx\plugins` first,
because Keel leaves Daywheel to BepInEx while BepInEx has a copy. Keep the
`Keel` folder from then on, since Keel is what starts BepInEx now, and if you
update BepInEx by hand later, copy Daywheel's `doorstop_config.ini` back in
afterwards. If you'd switched BepInEx off and want it to stay off, add the
line `StartBepInEx = false` to `Keel\Keel.cfg` before you start the game,
making the file if it isn't there yet.

To remove Daywheel, delete the `Keel\Daywheel` folder, or `Daywheel.dll` from
`BepInEx\plugins` if that's where you put it, or uninstall it in your mod
manager.

## Compatibility

Daywheel doesn't patch the game and doesn't add anything to the network, so
it's purely client side. Friends without it can still join your server, and
it never changes your world or character files. It writes nothing but its
own settings file. Installed by hand, Keel also keeps a short log of what it
started, and a settings file of its own once it finds BepInEx in the game
folder. While you're moving the wheel it pauses your character's controls,
and it gives them back the moment you finish. If something goes wrong inside
it, it logs one line and keeps going.

Like any mod, it lets the game know that it's modded, so the main menu says
so and achievements are off while it's installed, the same as with BepInEx.

## Building from source

You'll need the .NET 8 SDK or newer, plus some DLLs from your own Valheim
install. They aren't included here, so copy them yourself into a folder
called `refs` at the top of the repo:

- From `Valheim/valheim_Data/Managed/`: `assembly_valheim.dll`,
  `assembly_utils.dll`, `UnityEngine.dll`, `UnityEngine.CoreModule.dll`,
  `UnityEngine.UI.dll`, `UnityEngine.UIModule.dll`,
  `UnityEngine.TextRenderingModule.dll`,
  `UnityEngine.InputLegacyModule.dll`, `UnityEngine.ImageConversionModule.dll`
  and `Unity.TextMeshPro.dll`
- From a BepInEx install's `core` folder: `BepInEx.dll`. Daywheel runs
  without BepInEx, but its BepInEx side is built against it.

Then build Daywheel and Keel, the small starter it comes with:

```
dotnet build src/Daywheel.csproj -c Release
dotnet build keel/loader/Keel.csproj -c Release
```

That leaves `Daywheel.dll` in `src/bin/Release`, with every picture built into
it, and `Keel.dll` in `keel/loader/bin/Release`. `keel/README.md` shows how
the two go into the zip with Doorstop. If your DLLs are somewhere else, add
`-p:RefDir=<that folder>` to both commands.

Built against Valheim `25253791`. On Thunderstore the package lists
`denikson-BepInExPack_Valheim-5.4.2350` as a dependency, only so that mod
managers, which start everything through BepInEx, install it along with
Daywheel.

## Licence and credits

Daywheel is released under the MIT licence. See `LICENSE`.

It starts through [Keel](https://github.com/VysesDarkheart/keel), the small
framework in the `keel` folder, which is MIT licensed too and travels in the
zip with its licence as `Keel\Keel-LICENSE.txt`. Keel in turn starts with the game through
[Unity Doorstop](https://github.com/NeighTools/UnityDoorstop) by NeighTools.
The zip carries Doorstop's `winhttp.dll` unchanged, under the GNU Lesser
General Public License 2.1, with that licence in `Keel\Doorstop-LICENSE.txt`
and Doorstop's source code beside it. With a mod manager, Daywheel runs on
[BepInEx](https://github.com/BepInEx/BepInEx), which is released under the
LGPL 2.1 as well.

The name on the store icon is set in Marcellus SC by Astigmatic, used under
the SIL Open Font License.
