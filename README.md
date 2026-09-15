# Vampire Survivors UX

A BepInEx mod for Vampire Survivors with small UX improvements. It removes menu clicks between runs and in the menus.

On the end-of-run results page, the mod adds three buttons next to **Done**:

- **Retry**: start a new run on the same stage with the same setup.
- **Next stage**: start a new run on the next stage in the stage list.
- **Next new**: start a new run on the next stage that the main character has not completed yet.

"Setup" means the main character, the local co-op slots (character and CPU behaviour), the stage, the music, and the run modifiers (hyper, hurry, arcanas, limit break, inverse, endless).

The mod also adds double click to the menus:

- On the character page, a double click on a character selects and confirms it.
- In the party size popup and in the CPU type popup, a double click on an option confirms it.
- On an arcana page, a double click on a card selects and confirms it. No second click on **GET** is necessary.
- On the power-up page, a double click on a power-up buys it. No second click on **BUY** is necessary. A power-up
  at the maximum rank does not change.

On the collection page, the mod adds a **Seal all** button below **Unseal all**. It seals every item and weapon
that a click can seal, until the seal limit is full. The rules of the game do not change: an item must be found
and sealable, and the number of seals is still the limit.

When the game is paused, the mod shows the name of the current stage at the top of the screen.

The speed limit of the game goes from 2x to 5x. The speed button steps through 1x, 2x, 3x, 4x, 5x, and back to
1x. The rules of the game do not change: the speed-up still needs the Speed-Up relic, and it stays off on stages
that ban it and in online runs. The game has one icon for every speed from 2x up, so the mod adds the missing
arrows with the art of the game: 4x shows four arrows and 5x shows five.

The buttons do not appear in online runs and in adventures. The mod does not change game data or balance. The game still gives rewards and saves before a new run starts.

## Requirements

The mod has two builds. Pick the one for your platform.

| Platform | Game build | Loader |
| --- | --- | --- |
| Windows, Linux, Steam Deck | Windows build (`VampireSurvivors.exe` is a `PE32+` file), IL2CPP | [BepInEx Unity.IL2CPP, bleeding edge build 788](https://builds.bepinex.dev/projects/bepinex_be) or newer |
| macOS | macOS build (`Vampire_Survivors.app`), Mono | [BepInEx 5.4.23.5, macOS universal](https://github.com/BepInEx/BepInEx/releases) |

The [releases page](https://github.com/jaysonsantos/vampire-survivors-ux/releases) has one file for each build:

| File | Platform |
| --- | --- |
| `VampireSurvivorsUx.dll` | Windows, Linux, Steam Deck |
| `VampireSurvivorsUx-mono.dll` | macOS |

On Linux and on the Steam Deck, force Proton. The native Linux build uses Mono and does not work with either build of the mod.

Tested on 2026-09-10 with game build `25016043` (v1.16.107, Unity 6000.0.62f1) on Linux with Proton.
Tested on 2026-09-12 with the same game build on macOS 26 (Apple M4 Pro).

## Install on Windows

1. Close the game.
2. Download `BepInEx-Unity.IL2CPP-win-x64-6.0.0-be.788+5b766a3.zip` from the BepInEx build server.
3. Extract the zip into the game folder. The folder then contains `winhttp.dll`, `doorstop_config.ini`, `dotnet/`, and `BepInEx/`.
4. Download `VampireSurvivorsUx.dll` from the [releases page](https://github.com/jaysonsantos/vampire-survivors-ux/releases) and put it in `BepInEx/plugins/`.
5. Start the game. The first start takes about two minutes. BepInEx builds the interop assemblies in `BepInEx/interop/`.
6. Check `BepInEx/LogOutput.log`. It must contain `Loading [VampireSurvivorsUx`.

## Install on Linux and on the Steam Deck

1. Close the game.
2. In Steam, open *Vampire Survivors → Properties → Compatibility*. Select *Force the use of a specific Steam Play compatibility tool* and pick a current Proton.
3. Let Steam download the Windows build.
4. Set the launch option:

   ```text
   WINEDLLOVERRIDES="version=n,b" %command%
   ```

5. Do steps 2 to 4 of the Windows install.
6. Copy `winhttp.dll` to `version.dll` in the game folder. The launch option above loads `version.dll`.

   ```sh
   cd ~/.local/share/Steam/steamapps/common/"Vampire Survivors"
   cp winhttp.dll version.dll
   ```

7. Start the game and check the log as in the Windows install.

The script `tools/install-loader.sh bepinex` does steps 5 and 6 on a PC with the repository.

## Build from source

The build needs the interop assemblies that BepInEx makes on the first game start.

```sh
# Windows, Linux, Steam Deck (IL2CPP)
nix develop -c dotnet build src/VampireSurvivorsUx/VampireSurvivorsUx.csproj -c Release -p:Loader=BepInEx
# macOS (Mono)
nix develop -c dotnet build src/VampireSurvivorsUx/VampireSurvivorsUx.csproj -c Release -p:Loader=BepInExMono
```

The build writes `bin/Release/<Loader>/VampireSurvivorsUx.dll` and copies it to `BepInEx/plugins/` in the game
folder. Set `-p:GamePath=...` when the game is in another folder. Without `nix`, use the .NET 8 SDK.

`tools/gen-interop.sh` makes the interop assemblies without a game start. See `AGENTS.md` for the development notes.

`tools/build-release.sh` makes both release files in `dist/`. It needs the game assemblies for the two builds
on one computer. A build server cannot make the release, because the game assemblies belong to poncle and are
not in the repository.

## Install on macOS

The macOS build of the game uses Mono, so it needs BepInEx 5 and the `BepInExMono` build of the mod.
Rosetta 2 must be installed. The loader runs the game as `x86_64`, because the detour library in BepInEx 5
cannot patch `arm64` code.

1. Close the game.
2. Download `BepInEx_macos_universal_5.4.23.5.zip` and extract it into the game folder
   (`~/Library/Application Support/Steam/steamapps/common/Vampire Survivors`). The folder then contains
   `libdoorstop.dylib`, `run_bepinex.sh`, and `BepInEx/`, next to `Vampire_Survivors.app`.
3. Edit `run_bepinex.sh`:
   - Set `executable_name="Vampire_Survivors.app"`.
   - Change `export ARCHPREFERENCE="arm64,x86_64"` to `export ARCHPREFERENCE="x86_64,arm64"`.
   - Change `exec arch -e ...` to `exec arch -x86_64 -e ...`.
4. Run `chmod +x run_bepinex.sh`.
5. Download `VampireSurvivorsUx-mono.dll`, rename it to `VampireSurvivorsUx.dll`, and put it in `BepInEx/plugins/`.
6. In Steam, open *Vampire Survivors → Properties → General* and set the launch option:

   ```text
   "/Users/<you>/Library/Application Support/Steam/steamapps/common/Vampire Survivors/run_bepinex.sh" %command%
   ```

7. Start the game and check `BepInEx/LogOutput.log`. It must contain `Loading [VampireSurvivorsUx`.

The script `tools/install-loader.sh bepinex-macos` does steps 2 to 4 on a Mac with the repository.

## License

MIT. See `LICENSE`.

This project is not affiliated with poncle. The repository contains no game files, no game code, and no loader binaries. BepInEx, HarmonyX, and Il2CppInterop are separate projects with their own licenses. The mod links to them at run time and does not redistribute them.
