# Vampire Survivors UX mod

This repo holds a BepInEx mod for Vampire Survivors with small UX improvements.
The first feature adds three buttons to the end-of-run recap page:

- **Retry**: start a new run on the same stage with the same setup.
- **Next stage**: start a new run on the next stage with the same setup.
- **Next new**: start a new run on the next stage that the main character has not completed yet (`PlayerOptionsData.StageCompletionLog`). Falls back to the next stage when all are complete.

On the character selection page, a double click on a character selects and confirms it (`SelectCharacter(false)` then `ConfirmCharacter()` one frame later). This works in solo and in party mode.
In the party size and CPU type popups (`LargeMultiOptionPopup`), a second click on the same option within 0.45 s confirms it (postfix on `SelectOption(GameObject)` calls `Confirm()`).
The pause page shows the name of the current stage at the top (postfix on `PausePage.OnShowStart`).
A double click on an arcana card selects and confirms it, in place of a click on the card and then a click on
the GET button (postfix on `ArcanaCardUI.OnClick` calls `Select()` one frame later).
The speed limit of the game goes from 2x to 5x. The toggle order is 1x, 2x, 3x, 4x, 5x, then 1x.

"Setup" means the main character, the local co-op slots (character and CPU behaviour), the stage, the BGM, and the run modifiers.
Example setup: 4 characters, all CPU, all `Aggressive`.

## Scope

- Target the Windows build of the game (IL2CPP) with BepInEx. On Linux and on the Steam Deck, run it under Proton.
- Target the macOS build of the game (Mono) with BepInEx 5. See "macOS port".
- Use the same setup on the PC and on the Steam Deck.
- Support offline runs only. Hide the buttons in online runs.
- Do not change game data or balance.

## Facts checked on 2026-09-10

- Steam app ID: `1794680`. Build ID: `25016043`. Unity: `6000.0.62f1`.
- The PC runs the native Linux build at this time. That build uses Mono. Steam has no forced Proton for this game.
- The Windows build uses IL2CPP (`GameAssembly.dll`). IL2CPP dumps show only type and method names, not method bodies.
- The Steam Deck also runs the native Linux build by default. You must force Proton to get the Windows build.
- The game contains official Workshop code in `VampireSurvivors.Framework.Workshop`. It loads custom characters and power-up packs. It does not support UI or logic changes.
- Community posts report MelonLoader problems on Unity 6 builds of this game. Test the loader before you write mod code.
- The Vampire Survivors mod community moved to BepInEx after the Unity 6 update (May 2026). MelonLoader 0.7.3 (2026-05-14) is older than that update. Current Nexus mods (for example VST_Core 4.0.0) need `BepInEx-Unity.IL2CPP-win-x64-6.0.0-be.785` or newer.
- Tested on 2026-09-10 under Proton: MelonLoader 0.7.3 generates the assemblies, loads the mod, and then the game exits without an error, also with no mod installed. BepInEx BE 788 loads and the game runs. Use BepInEx.
- Doorstop (`winhttp.dll` from BepInEx) also exports the `version.dll` functions. A copy named `version.dll` works with the launch option `WINEDLLOVERRIDES="version=n,b"`.
- The interop assemblies can be made without a game start. See `tools/gen-interop.sh`.
- Interop assembly file names get the `Il2Cpp` prefix too: `Il2CppVampireSurvivors.Runtime.dll`, `Il2Cppmscorlib.dll`. Unity and `Assembly-CSharp` keep their names.

## Repo layout

| Path | Content |
| --- | --- |
| `flake.nix`, `flake.lock` | Dev shell with `dotnet-sdk_8` (8.0.424) and `ilspycmd` (9.1). |
| `.envrc` | `use flake`. The global gitignore of the user ignores this file. |
| `reference/managed-mono-build25016043/` | Mono DLLs from `VampireSurvivors_Data/Managed/` of the Linux build. |
| `reference/decompiled/VampireSurvivors.Runtime/` | Decompiled C# (3017 files). Most game logic is here. |
| `reference/decompiled/Assembly-CSharp/` | Decompiled C# (8045 files). |
| `reference/interop/` | Output of `tools/gen-interop.sh`. Interop assemblies made without the game. |
| `src/VampireSurvivorsUx/` | The mod. One csproj, one core, three loader entry points. |
| `reference/loaders/` | Loader zip files that `tools/install-loader.sh` downloads. |
| `tools/gen-interop.sh` | Makes interop assemblies from `GameAssembly.dll` with Cpp2IL and Il2CppInterop. |

Public repository: `github.com/jaysonsantos/vampire-survivors-ux` (MIT). `README.md` has the install steps for players.

## Rules

- Run all tools inside the dev shell: `nix develop`, or `direnv allow` once.
- Add new tools to `flake.nix`. Do not install system packages with `pacman`.
- Never commit `reference/`, game DLLs, or decompiled game code. They belong to poncle.
- Read logic in the decompiled Mono code. The IL2CPP build has the same types and methods.
- MelonLoader adds an `Il2Cpp` prefix to namespaces and file names: `Il2CppVampireSurvivors.UI.RecapPage` in `Il2CppVampireSurvivors.Runtime.dll`. BepInEx and the Mono build add no prefix: `VampireSurvivors.UI.RecapPage` in `VampireSurvivors.Runtime.dll`. `Il2CppSystem` and `Il2Cppmscorlib.dll` keep the prefix in MelonLoader and BepInEx. The source uses `#if MELONLOADER` blocks for the using directives.
- Two more compile symbols select the runtime: `IL2CPP` for MelonLoader and BepInEx, `MONO` for BepInEx 5 on the macOS build. Put all runtime differences in `Interop.cs`.
- Write documentation, commit bodies, and PR descriptions in STE. Follow `~/.agents/skills/ste-writing/SKILL.md`.
- Before you start work, check that the current branch is up to date with `origin/main`, if a remote exists.

## Step 1: Set up the Windows build and the loader

The user does steps 1-4 in Steam. An agent can do steps 5-7.

1. Close the game.
2. In Steam, open *Vampire Survivors → Properties → Compatibility*.
3. Select *Force the use of a specific Steam Play compatibility tool*. Select a current Proton version.
4. Let Steam download the Windows build.
5. Check the build type:

   ```sh
   file "$HOME/.local/share/Steam/steamapps/common/Vampire Survivors/VampireSurvivors.exe"
   ```

   The result must show `PE32+ executable`. If it shows `ELF`, Steam still uses the Linux build.
6. Install BepInEx: run `tools/install-loader.sh bepinex`. It puts `winhttp.dll`, `doorstop_config.ini`, `dotnet/`, and `BepInEx/` from `BepInEx-Unity.IL2CPP-win-x64-6.0.0-be.788+5b766a3.zip` in the game folder, plus a copy of the proxy as `version.dll`. BepInEx ships its own CoreCLR, so no .NET install in the Wine prefix is necessary. MelonLoader 0.7.3 does not work on this build (see the facts above).
7. Set the Steam launch option:

   ```text
   WINEDLLOVERRIDES="version=n,b" %command%
   ```

8. Start the game once. Wait for the main menu. Close the game.
9. Check that `BepInEx/interop/` exists and contains `VampireSurvivors.Runtime.dll`.
10. Read the log (`MelonLoader/Latest.log` or `BepInEx/LogOutput.log`). If the MelonLoader log shows .NET runtime errors, run `protontricks 1794680 dotnetdesktop6`.

Stop here if no loader loads. Report the log to the user.

## Step 2: Build the mod project

The project is `src/VampireSurvivorsUx/VampireSurvivorsUx.csproj`. It supports three loaders with the MSBuild
property `Loader`. Each loader writes to `bin/$(Configuration)/$(Loader)/`, so a stale DLL cannot hide in a
shared folder. `Directory.Build.props` also gives each loader its own `obj/$(Loader)/`, because the restore
assets in `obj/` are per target framework. With a shared `obj/`, a `net6.0` build followed by a
`netstandard2.1` build resolves no reference at all.

| Loader | Target | Loader libraries | Game assemblies | Copy target |
| --- | --- | --- | --- | --- |
| `BepInEx` (Windows build) | `net6.0` | `$(GamePath)/BepInEx/core/` | `$(GamePath)/BepInEx/interop/` | `$(GamePath)/BepInEx/plugins/` |
| `BepInExMono` (macOS build) | `netstandard2.1` | `$(GamePath)/BepInEx/core/` | `$(GamePath)/Vampire_Survivors.app/Contents/Resources/Data/Managed/` | `$(GamePath)/BepInEx/plugins/` |
| `MelonLoader` (default for history, does not run on this build) | `net6.0` | `$(GamePath)/MelonLoader/net6/` | `$(GamePath)/MelonLoader/Il2CppAssemblies/` | `$(GamePath)/Mods/` |

Override the paths with `-p:GamePath=...`, `-p:LoaderLibPath=...`, `-p:GameAsmPath=...`, and
`-p:CopyToMods=false`. The old name `-p:Il2CppAssembliesPath=...` still works.

```sh
nix develop -c dotnet build src/VampireSurvivorsUx/VampireSurvivorsUx.csproj -c Release
nix develop -c dotnet build src/VampireSurvivorsUx/VampireSurvivorsUx.csproj -c Release -p:Loader=BepInEx
nix develop -c dotnet build src/VampireSurvivorsUx/VampireSurvivorsUx.csproj -c Release -p:Loader=BepInExMono
```

The source layout:

- `QuickRetryCore.cs`: Harmony patches, buttons, next-stage rule, start of the run.
- `RunSnapshot.cs`: capture and restore of the run setup.
- `PauseStageName.cs`: stage name label on the pause page.
- `GameSpeed.cs`: raises the speed limit to 5x and replaces the toggle order.
- `ArcanaDoubleClick.cs`: double click on an arcana card confirms it.
- `FrameScheduler.cs`: runs an action some frames later. The loader entry point ticks it.
- `ModLog.cs`: log sink that the loader entry point sets.
- `Interop.cs`: every IL2CPP and Mono difference. Object identity, delegate conversion, type casts, the
  private game fields, and `PartySizeField`. No other file holds a runtime conditional, except the
  `#if MELONLOADER` namespace prefix blocks.
- `Loader/MelonEntry.cs`, `Loader/BepInExEntry.cs`, `Loader/BepInExMonoEntry.cs`: entry points. Only one is compiled.

To compile the IL2CPP build without the game, run `tools/gen-interop.sh` and build with
`-p:GameAsmPath=reference/interop/Il2CppAssemblies -p:LoaderLibPath=<extracted loader zip>/... -p:CopyToMods=false`.

To compile the Mono build on a machine without the macOS game, use the reference copies:

```sh
nix develop -c dotnet build src/VampireSurvivorsUx/VampireSurvivorsUx.csproj -c Release -p:Loader=BepInExMono \
  -p:GameAsmPath=$PWD/reference/managed-mono-build25016043 \
  -p:LoaderLibPath=$PWD/reference/loaders/bepinex5/BepInEx/core -p:CopyToMods=false
```

## Game code map

Paths are relative to `reference/decompiled/VampireSurvivors.Runtime/`. Line numbers are for build `25016043`.

### Run setup

- `VampireSurvivors.Objects/PlayerOptions.cs`: `PlayerOptions.Config` is a `PlayerOptionsData` (`VampireSurvivors.Data/PlayerOptionsData.cs`). `Config` points to the online or adventure config in those modes, and to `MainGameConfig` otherwise.
- Run fields in `PlayerOptionsData`: `SelectedCharacter`, `SelectedStage`, `SelectedHyper`, `SelectedHurry`, `SelectedMazzo` (arcanas), `SelectedLimitBreak`, `SelectedInverse`, `SelectedReapers` (endless), `SelectedGoldenEggs`, `SelectedSurvarots`, `SelectedSharePassives`, `SelectedRandomEvents`, `SelectedRandomLevels`, `SelectedMaxWeapons`, `SelectedBGM`, `SelectedBGMMod`, `SelectedBGMPlayback`, `SelectedBGMSave` (track lock).
- `PlayerOptions.AutoSelectStage()` (line 268) moves `NextAutoSelectStage` into `SelectedStage`. Achievements set `NextAutoSelectStage` when a run unlocks a stage.
- Runtime access in both scenes: `SystemPlatform.Instance.PlayerOptions`, `SystemPlatform.Instance.DataManager`, `MultiplayerManager.Instance`. `GM.Core` is valid only in the gameplay scene. `AppStateMachine.Instance` is valid only in the main menu scene.
- `VampireSurvivors.Framework/CoopSlotData.cs`: each local slot has `SelectedCharacter`, `AIType`, `UnlockState`, and `RewiredPlayer`.
- `VampireSurvivors.Framework/MultiplayerManager.cs:700`: `GetLocalPlayerSlots()` returns the live list of 4 slots. `IsMultiplayer` is at line 90. `ResetMultiplayerSelections()` (line 635) sets `PartySize` to null and clears character and `AIType` of every slot. It keeps `RewiredPlayer` for connected pads. `GetLocalPlayerCount()` uses `PartySize` when set, so the snapshot must include `PartySize` and `PartyModeEnabled`.
- `VampireSurvivors.Objects.Algorithm/AIType.cs`: CPU behaviours `None`, `Aggressive`, `Defensive`, `ChaoticAF`, `MirrorInput`, `DelayedInputCopy`, and more.
- `VampireSurvivors.Data/DataManager.cs:200`: `AllCPU` maps `AIType` to `AIData`.

### Run start

- `VampireSurvivors.UI/QuickStartGameController.cs`: `Execute()` sets `Config` and the local slots. Then it fires `UISignals.QuickStartGameSignal`. Use this as the model to start a run from a known setup.
- `VampireSurvivors/AppMainMenuState.cs:124`: `QuickStartGame` fires the state event `START_GAME`.
- `VampireSurvivors/AppMainMenuState.cs:27`: `OnEnter()` calls `Multiplayer.ResetMultiplayerSelections()`. This call clears the slots. The mod must restore the slots after this call.
- `VampireSurvivors/AppStageSelectState.cs`: on `ConfirmStageSelectionSignal`, offline runs fire `START_GAME`.
- `VampireSurvivors.UI/StageSelectPage.cs`: `GetAvailableStages(DataManager, PlayerOptions)` returns the stage list.

### Run end

- `VampireSurvivors/GameStateGameOver.cs`: `ReturnToAppSignal` fires the state event `RECAP`.
- `VampireSurvivors.UI/RecapPage.cs`:
  - `_DoneButton` (line 140) is the Done button.
  - `OnShowStart` (line 362) builds the page.
  - `DoneClicked()` (line 279) gives rewards and achievements, then calls `ReturnToLanding()`.
  - `ReturnToLanding()` (line 297) calls `_playerOptions.AutoSelectStage()`, saves with a backup, and fires `RecapPageCompletedSignal`.
  - `OnShowStart` ends with `_spellsManager.RestoreCachedPlayerSettings()` (line 450). Spell runs change stage and character during the run. Take the snapshot after this call.
  - `_DoneButton` is a `Selectable`. Its click is a persistent `UnityEvent` listener from the prefab. `RemoveAllListeners()` does not remove it. Replace `onClick` with a new `ButtonClickedEvent` on the clone.
- `VampireSurvivors/GameStateRecap.cs`: on `RecapPageCompletedSignal`, it fires `RETURN_TO_LANDING` and loads `ScenePreloader`.

### Game speed

- `VampireSurvivors.Framework.Speedup/SpeedupManager.cs`: the speed-up feature of the game. `m_MaxSpeed` is
  `2f` (line 15) and `c_SpeedMultiplierSpeedupStep` is `0.5f`. `SetSpeedup(float)` (line 117) clamps to
  `m_MaxSpeed` and writes `Time.timeScale`. `Setup()` (line 42) runs once per run, from `Stage.Start`
  (`VampireSurvivors.Objects/Stage.cs:532`). `ClearSpeedupManager()` drops the instance at the end of the run,
  so the limit must be set again in every run.
- `ToggleSpeedup(InputActionEventData)` (line 76) is the Rewired handler for action 24. Its order is 1x, 1.5x,
  2x, then back to 1x, so a higher `m_MaxSpeed` alone changes nothing. The handler ignores the press while the
  player holds Rewired button 26.
- Three rules block the speed-up: the `RELIC_SPEEDUP` item is not collected, the item is in
  `PlayerOptionsData.SealedItems`, or `StageData.isSpeedupBanned` is true. Online runs are blocked too.
  `SetSpeedupBlocked(bool)` is a short block for cutscenes and boss gates.
- `VampireSurvivors.App.UI/FastForwardButton.cs`: the on-screen button. `FastForward()` (line 92) is the second
  input path and needs the same order. `CheckTimescale()` (line 72) picks one of three icons: `_icon1` below
  1.5x, `_icon2` below 2x, `_icon3` from 2x up. `Update()` (line 48) hides all three icons when the run blocks
  the speed-up. `GM.Core.MainUI.KillsText` is a public `TextMeshProUGUI` and works as a font template.

### Arcana selection

- `VampireSurvivors.UI/ArcanaCardUI.cs`: `OnClick()` (line 300) calls `ISetArcanaInfo.SetInfo(data, type, this)`
  on every click on an unlocked card that is not already active.
- `VampireSurvivors.UI/ArcanaMainSelectionPage.cs`: `SetInfo` (line 1557) returns early when the card is already
  the selected one, so it is not usable for a double click. `Select()` (line 1577) is what the GET button calls;
  it checks the unlock state itself. `_hasFinishedPopulationAnimation` and `_hasPickedRandom` gate the GET
  button. The major page, the minor page, and the Darkana page all use this one class.
- `VampireSurvivors.UI/SurvarotsSelectionPage.cs`: the same shape. Public `Select()` (line 986) and the same two
  flags.

### Pause page

- `VampireSurvivors/PausePage.cs`: `OnShowStart` (line 174) builds the page each time the game pauses. `_ResumeButton` (line 72) is a `RectTransform` with a `TextMeshProUGUI` label.
- `VampireSurvivors.Objects/Stage.cs`: `GM.Core.Stage.StageType` and `ActiveStageData` (line 353) give the current stage.
- `VampireSurvivors.Data.Stage/StageData.cs:254`: `GetLocalizedName(StageType)` returns the I2 term `stageLang/{TYPE}stageName`. `LocalizationManager.GetTranslation(term)` gives the text. `stageName` is the English fallback.

### Other references

- `VampireSurvivors.Framework/GameManager.cs:1454`: `TransitionToFoscari2()` sets `Config.SelectedStage` and calls `RestartGameScene()` (line 1472). This path skips the recap page, rewards, and the save. Do not use it for Retry.
- The save code has `LastRunBackupExists` and `RestoreLastRunBackup`. Do not change the save flow.

## Proposed design

This design is implemented in `src/VampireSurvivorsUx/`.

1. **Snapshot.** Harmony postfix on `RecapPage.OnShowStart`. Copy the `Config` run fields, every local slot (character, `AIType`, pad present), `PartySize`, and `PartyModeEnabled` into a static snapshot. A postfix is necessary because `OnShowStart` restores spell-run changes at its end. `AutoSelectStage()` runs later, in `ReturnToLanding()`.
2. **Buttons.** Same postfix. Clone `_DoneButton` two times. Replace `onClick`. Disable the `I2.Loc.Localize` component on the label and set the text to "Retry" and "Next stage". Set `SelectableUI.IsDefaultSelectedOnPage` to false on the clones. Place the clones left of Done when the parent has no layout group.
3. **Click.** Set a static `PendingAction` (`None`, `Retry`, `NextStage`). Then call `DoneClicked()`. The game gives rewards, saves, and goes to the main menu.
4. **Restore.** Harmony postfix on `AppMainMenuState.OnEnter`. If `PendingAction` is not `None`, restore `Config`, the slots, and `PartySize` from the snapshot. Call `MultiplayerManager.Refresh()`. Set `PendingAction` to `None`.
5. **Start.** Two frames later, check that `AppStateMachine.Instance.CurrentState` is `AppMainMenuState`, then call `AppStateMachine.Instance.FireEvent("START_GAME")`. This is what `QuickStartGameSignal` does. Do not call `QuickStartGameController.Execute()`, because it picks random characters and stages.
6. **Next stage.** At click time, sort the unlocked stages of `GetAvailableStages` by `StageData.order`. Skip `STAGEX`, `MACHINE`, and `MACHINE2`, as quick start does. Take the stage after the current one. Wrap to the first. BGM rule, same as the song panel: a locked track stays; else the character track; else the stage track (side B when inverse is on).
7. **Guards.** Hide the buttons if `GM.Core.StartedAsOnlineMultiplayerRun` is true, if `MultiplayerManager.Instance.IsOnlineMultiplayer` is true, or if `AdventureManager.IsInAdventureMode` is true.

## Decisions (defaults in the code, change on request)

- Next stage order: the stage select list order (`StageData.order`).
- Next stage BGM: the song panel rule (locked track, else character track, else stage track).
- Human players: the game keeps the pad on the slot when the pad is still connected. If the game removed the pad, the slot stays empty and the log shows a warning.
- Speed stops: 1x, 2x, 3x, 4x, 5x. Whole steps, five presses per full cycle. The 1.5x stop of the game is gone.
- Speed rules: the mod keeps every rule of the game. The Speed-Up relic is still necessary, a banned stage still
  blocks the speed-up, and online runs still block it. The mod only raises the limit and changes the order.
- Speed label: the game has one icon for every speed from 2x up. The mod adds the number next to the button from
  3x, so 3x, 4x, and 5x are not the same picture. The label follows `_icon3`, so every rule that hides the
  button hides the label too.

## Test results (PC, 2026-09-10, BepInEx BE 788, build 25016043)

| Test | Result |
| --- | --- |
| 1. Solo run, Retry | Pass. Same character, stage, and modifiers. Save happens in the recap page before the click. |
| 2. Party of 4, P1 plus 3 CPU `Aggressive`, Retry | Pass. `PartySize`, all slot characters, and `AIType` restored. |
| 3. Next stage | Pass. Moongolow to Green Acres, BGM set by the song panel rule. |
| 4. Done | Pass. The game returns to the landing page. No automatic start. |
| 5. Log | Pass. No `VampireSurvivorsUx` errors in `BepInEx/LogOutput.log`. |
| 6. Steam Deck | Not done. |

Facts learned in the tests:

- After the recap the app shows the landing page ("press to start"), not the main menu. The mod fires `MAIN_MENU` from a postfix on `AppLandingPageState.OnEnter`.
- The main menu background runs a 1 s pixelate tween (`BackgroundPage.OnShowStart`). `AppGameplayState.OnEnter` kills all tweens. A `START_GAME` inside that second leaves the pixelate render feature active for the whole run. The mod waits 1.6 s of unscaled time before it fires `START_GAME`.
- The interop getter for `MultiplayerManager.PartySize` (`int?`) throws when the value is empty, because IL2CPP boxes an empty `Nullable` as null. `RunSnapshot.cs` reads and writes the two struct fields with `Marshal` at the field offsets.
- The recap page label `TextMeshProUGUI` has auto-size on, but `BaseUIPage.Parse()` turns it off one frame later. The mod disables auto-size and sets a smaller font for long labels.

Notes for GUI automation with `cua-driver` on KDE Wayland:

- Per-window capture and the Steam window are not available. Use a session with `capture_scope` `auto`, escalate with reason `no_window_target`, then use `get_desktop_state` and desktop-scope `click` and `press_key`. KDE asks once for the screenshot and the remote control permission.
- Mouse clicks work on popups, quick start, the pause menu, and the recap buttons. The Confirm button on the character page and the START button on the stage page ignore mouse clicks. Use Return there.
- Escape on the stage page goes back to the character page and clears the party setup.

## macOS port

Facts checked on 2026-09-12 on macOS 26.6.2, Apple M4 Pro, game build `25016043`.

- The macOS build uses **Mono**, not IL2CPP: `Vampire_Survivors.app/Contents/MonoBleedingEdge/`,
  `Contents/Frameworks/libmonobdwgc-2.0.dylib`, `Contents/Resources/Data/Managed/` (261 assemblies).
- The main executable and `UnityPlayer.dylib` are universal binaries (`x86_64` and `arm64`).
- The managed assemblies match the Linux Mono build of the same build ID. Use
  `reference/managed-mono-build25016043/` as the reference for a build on a PC.
- The app bundle has an ad-hoc signature and no hardened runtime. `DYLD_INSERT_LIBRARIES` works.
- Loader choice: **BepInEx 5.4.23.5, `BepInEx_macos_universal`**. It is the only loader with an `arm64` Doorstop.
  The bleeding edge builds have `BepInEx-Unity.Mono-macos-x64` only.
- `UnityPlayer.dylib` does not link `libmonobdwgc-2.0.dylib`. Unity 6 loads Mono with `dlopen`. Doorstop 4.5.0
  handles this: it hooks `dlopen` and `dlsym` with `plthook`.
- The game must run as **`x86_64` under Rosetta 2**. A native `arm64` start fails in the preloader with
  `HarmonyException: IL Compile Error` and `NullReferenceException` in `DetourHelper.GetIdentifiable`. The
  preloader writes the stack to `Vampire_Survivors.app/Contents/MacOS/preloader_<date>.log`.
  The cause is not a missing `arm64` assembler: `DetourNativeARMPlatform` has an `AArch64` detour type.
  `DetourRuntimeILPlatform` runs a self-test in its constructor that detours its own methods. The write to
  the code page needs `pthread_jit_write_protect_np` on Apple Silicon, and no MonoMod build that BepInEx
  ships calls it (BepInEx 5 has 22.01.29.01, BepInEx 6 BE 788 has 22.07.31.01). The self-test throws,
  MonoMod caches the failure, and `DetourHelper.Runtime` then returns null. An upgrade to BepInEx 6 does not
  help. Note that the `BepInEx-Unity.Mono-macos-x64` Doorstop **is** universal, so the loader would inject.
- Patch `run_bepinex.sh`: `ARCHPREFERENCE="x86_64,arm64"` and `exec arch -x86_64 -e ...`. Also set
  `executable_name="Vampire_Survivors.app"`. `tools/install-loader.sh bepinex-macos` does all of it.
  The script uses `sed` with a temp file, because BSD `sed` and GNU `sed` disagree on the argument of `-i`.
  Do not add `perl` to it. `xattr` is part of macOS, is not in the dev shell, and is optional: the script
  skips it when the command is absent.
- Steam launch option: `"<game folder>/run_bepinex.sh" %command%`.
- BepInEx writes `BepInEx/LogOutput.log` in the game folder, next to the app bundle.
- The Mono assemblies keep the access level of the game fields. `RecapPage._DoneButton`,
  `RecapPage._playerOptions`, `PausePage._ResumeButton`, and `LargeMultiOptionPopup._selectedIndex` are not
  public. `Priv` in `Interop.cs` reads them with reflection. The IL2CPP interop assemblies make every field
  public, so that build reads them direct.
- `MultiplayerManager.PartySize` is a plain `int?` field on Mono. The `Marshal` work-around is for IL2CPP only.
- `StateMachine.CurrentState` is a `StateMachineState`. Mono uses `is`, IL2CPP uses `TryCast<T>()`.
- IL2CPP interop makes a new managed wrapper on every call, so the double click code compares the native
  pointer. Mono compares the object reference. `ObjId` in `Interop.cs` hides the difference.

## Test results (macOS, 2026-09-12, BepInEx 5.4.23.5, build 25016043)

| Test | Result |
| --- | --- |
| 1. BepInEx loads | Pass. `BepInEx 5.4.23.5`, `Detected Unity version: v6000.0.62f1`, `1 plugin to load`. |
| 2. Plugin loads | Pass. `Loading [VampireSurvivorsUx 0.1.1]`, `VampireSurvivorsUx 0.1.1 patched.` |
| 3. Harmony patches run | Pass. The popup and character double click log `confirming` and no error. |
| 4. Log | Pass. No `Error` or `Warning` lines in `BepInEx/LogOutput.log`. |
| 5. Pause page stage name | Pass. `Pause page: stage label added. root=View - Paused size=(1920.00, 1200.00)`. |
| 6. Recap page buttons | Pass. `Done button parent=ButtonContainer layout=HorizontalLayoutGroup`. Three clones added. |
| 7. Next new, party of 4 | Pass. `EX_MAZERELLA -> TOWERBRIDGE`, BGM `BGM_Bridge`, all 4 slots and `PartySize` restored, `START_GAME` fired. |

## Test plan

1. Play a solo run and die. Click Retry. Check the character and the stage. Check that gold and achievements are saved.
2. Set 4 local slots, all CPU, all `Aggressive`. Click Retry. Check all 4 slots and their `AIType`.
3. Click Next stage. Check that the stage is the next unlocked stage and the slots do not change.
4. Click Done. Check that the game returns to the main menu as before.
5. Read the loader log (`MelonLoader/Latest.log` or `BepInEx/LogOutput.log`). It must show no errors from `VampireSurvivorsUx`. The first recap page logs the Done button parent and layout. Use it to correct the button positions.
6. Copy `bin/Release/BepInEx/VampireSurvivorsUx.dll` to the Steam Deck. Set the same Proton tool and launch option. Repeat tests 1-3.
7. On macOS, install with `tools/install-loader.sh bepinex-macos`, copy
   `bin/Release/BepInExMono/VampireSurvivorsUx.dll` to `BepInEx/plugins/`, set the Steam launch option, and
   repeat tests 1-5.

## When the game updates

The build ID in `~/.local/share/Steam/steamapps/appmanifest_1794680.acf` changes after an update. The decompiled code can then be out of date.

1. Remove the forced Proton tool in Steam. Let Steam download the Linux build.
2. Copy the Mono DLLs and decompile them:

   ```sh
   BUILD=$(grep -oP '"buildid"\s+"\K[0-9]+' ~/.local/share/Steam/steamapps/appmanifest_1794680.acf)
   M="reference/managed-mono-build$BUILD"
   mkdir -p "$M"
   cp -a "$HOME/.local/share/Steam/steamapps/common/Vampire Survivors/VampireSurvivors_Data/Managed/." "$M/"
   for a in VampireSurvivors.Runtime Assembly-CSharp; do
     rm -rf "reference/decompiled/$a"
     nix develop -c ilspycmd -p -o "reference/decompiled/$a" -r "$M" "$M/$a.dll"
   done
   ```

3. Set the forced Proton tool again. Let Steam download the Windows build.
4. Start the game once. The loader creates new interop assemblies. Or run `tools/gen-interop.sh` without a game start.
5. Build the mod again. Run the test plan.
