#!/usr/bin/env bash
# Installs a mod loader into the game folder (AGENTS.md step 1.6).
# Usage: tools/install-loader.sh melonloader | bepinex | bepinex-macos
# Downloads go to reference/loaders/ (not committed). Set LOADER_DL to select another folder.
# tools/deploy-macos.sh sets it, because it runs this script on the Mac without the repository.
set -euo pipefail

REPO="$(cd "$(dirname "$0")/.." && pwd)"
DL="${LOADER_DL:-$REPO/reference/loaders}"
mkdir -p "$DL"

case "${1:-}" in
  melonloader)
    GAME="${GAME_PATH:-$HOME/.local/share/Steam/steamapps/common/Vampire Survivors}"
    URL="https://github.com/LavaGang/MelonLoader/releases/download/v0.7.3/MelonLoader.x64.zip"
    ZIP="$DL/MelonLoader-0.7.3.x64.zip"
    ;;
  bepinex)
    GAME="${GAME_PATH:-$HOME/.local/share/Steam/steamapps/common/Vampire Survivors}"
    URL="https://builds.bepinex.dev/projects/bepinex_be/788/BepInEx-Unity.IL2CPP-win-x64-6.0.0-be.788%2B5b766a3.zip"
    ZIP="$DL/BepInEx-Unity.IL2CPP-win-x64-6.0.0-be.788.zip"
    ;;
  bepinex-macos)
    GAME="${GAME_PATH:-$HOME/Library/Application Support/Steam/steamapps/common/Vampire Survivors}"
    URL="https://github.com/BepInEx/BepInEx/releases/download/v5.4.23.5/BepInEx_macos_universal_5.4.23.5.zip"
    ZIP="$DL/BepInEx_macos_universal_5.4.23.5.zip"
    ;;
  *)
    echo "usage: $0 melonloader|bepinex|bepinex-macos" >&2
    exit 2
    ;;
esac

if [ "$1" = bepinex-macos ]; then
  APP="$GAME/Vampire_Survivors.app"
  if [ ! -d "$APP" ]; then
    echo "$APP not found. The macOS build is not installed in: $GAME" >&2
    exit 1
  fi
  [ -f "$ZIP" ] || curl -sSL -o "$ZIP" "$URL"
  unzip -o -q "$ZIP" -d "$GAME"
  mkdir -p "$GAME/BepInEx/plugins"

  SH="$GAME/run_bepinex.sh"
  # BSD sed and GNU sed disagree on the argument of -i, so write to a temp file.
  edit() {
    sed "$1" "$SH" > "$SH.new" && mv "$SH.new" "$SH"
  }
  # The loader needs the x86_64 slice. MonoMod in BepInEx 5 cannot detour arm64 code, so a native arm64
  # start fails in the preloader with a null DetourHelper.Runtime. Rosetta 2 must be installed.
  edit 's|^    export ARCHPREFERENCE="arm64,x86_64"$|    export ARCHPREFERENCE="x86_64,arm64"|'
  # arch drops the DYLD variables, so run_bepinex.sh must pass them again. ${DYLD_LIBRARY_PATH} stays
  # literal here, because run_bepinex.sh expands it, not this script.
  # shellcheck disable=SC2016
  edit 's|^    exec arch -e DYLD_INSERT_LIBRARIES=|    exec arch -x86_64 -e DYLD_LIBRARY_PATH="${DYLD_LIBRARY_PATH}" -e DYLD_INSERT_LIBRARIES=|'
  edit 's|^executable_name=""$|executable_name="Vampire_Survivors.app"|'
  chmod +x "$SH"

  for marker in 'exec arch -x86_64' 'ARCHPREFERENCE="x86_64,arm64"' 'executable_name="Vampire_Survivors.app"'; do
    grep -q "$marker" "$SH" || { echo "Failed to patch run_bepinex.sh: $marker is missing." >&2; exit 1; }
  done

  # xattr is part of macOS and is not in the dev shell. The quarantine flag is only set on a download
  # through a browser, so skip this step when the command is absent.
  if command -v xattr > /dev/null; then
    xattr -cr "$GAME/libdoorstop.dylib" "$SH" "$GAME/BepInEx" 2>/dev/null || true
  fi

  echo "Installed BepInEx 5 (Mono) into: $GAME"
  echo "Steam launch option:"
  echo "  \"$SH\" %command%"
  exit 0
fi

if ! file -b "$GAME/VampireSurvivors.exe" | grep -q 'PE32+'; then
  echo "VampireSurvivors.exe is not the Windows build. Force Proton in Steam first." >&2
  exit 1
fi

[ -f "$ZIP" ] || curl -sSL -o "$ZIP" "$URL"
unzip -o -q "$ZIP" -d "$GAME"
# Doorstop exports the version.dll functions too. The copy works with the version override.
[ "$1" = bepinex ] && cp "$GAME/winhttp.dll" "$GAME/version.dll"
echo "Installed $1 into: $GAME"
echo 'Launch option: WINEDLLOVERRIDES="version=n,b" %command%'
