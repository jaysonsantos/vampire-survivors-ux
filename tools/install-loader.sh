#!/usr/bin/env bash
# Installs a mod loader into the game folder (AGENTS.md step 1.6).
# Usage: tools/install-loader.sh melonloader | bepinex
# Downloads go to reference/loaders/ (not committed).
set -euo pipefail

REPO="$(cd "$(dirname "$0")/.." && pwd)"
GAME="${GAME_PATH:-$HOME/.local/share/Steam/steamapps/common/Vampire Survivors}"
DL="$REPO/reference/loaders"
mkdir -p "$DL"

case "${1:-}" in
  melonloader)
    URL="https://github.com/LavaGang/MelonLoader/releases/download/v0.7.3/MelonLoader.x64.zip"
    ZIP="$DL/MelonLoader-0.7.3.x64.zip"
    ;;
  bepinex)
    URL="https://builds.bepinex.dev/projects/bepinex_be/788/BepInEx-Unity.IL2CPP-win-x64-6.0.0-be.788%2B5b766a3.zip"
    ZIP="$DL/BepInEx-Unity.IL2CPP-win-x64-6.0.0-be.788.zip"
    ;;
  *)
    echo "usage: $0 melonloader|bepinex" >&2
    exit 2
    ;;
esac

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
