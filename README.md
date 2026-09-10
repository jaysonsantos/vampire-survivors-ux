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

The buttons do not appear in online runs and in adventures. The mod does not change game data or balance. The game still gives rewards and saves before a new run starts.

## Requirements

- Vampire Survivors, the **Windows** build (`VampireSurvivors.exe` is a `PE32+` file). The Linux and macOS builds use Mono and do not work with this mod.
- [BepInEx Unity.IL2CPP, bleeding edge build 788](https://builds.bepinex.dev/projects/bepinex_be) or newer.
- On Linux and on the Steam Deck: Proton 10 or newer.

Tested on 2026-09-10 with game build `25016043` (v1.16.107, Unity 6000.0.62f1) on Linux with Proton.

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
nix develop -c dotnet build src/VampireSurvivorsUx/VampireSurvivorsUx.csproj -c Release -p:Loader=BepInEx
```

The build copies `VampireSurvivorsUx.dll` to `BepInEx/plugins/` in the game folder. Set `-p:GamePath=...` when the game is in another folder. Without `nix`, use the .NET 8 SDK.

`tools/gen-interop.sh` makes the interop assemblies without a game start. See `AGENTS.md` for the development notes.

## macOS

Not supported at this time. The macOS build of the game uses Mono, and this mod uses the IL2CPP interop layer. A Mono variant of the mod needs a different build and a test on a Mac.

## License

MIT. See `LICENSE`.

This project is not affiliated with poncle. The repository contains no game files, no game code, and no loader binaries. BepInEx, HarmonyX, and Il2CppInterop are separate projects with their own licenses. The mod links to them at run time and does not redistribute them.
