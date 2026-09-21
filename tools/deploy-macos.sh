#!/usr/bin/env bash
# Builds the macOS (Mono) file on this computer and installs it on a Mac through SSH.
# Usage: tools/deploy-macos.sh <user@host> [--loader]
#   --loader  Install BepInEx 5 on the Mac again. Without it, the loader is installed only when absent.
# Environment:
#   REMOTE_GAME_PATH  Game folder on the Mac. Default: the Steam library in the home folder.
#   MONO_ASM, MONO_LIB  See tools/build-release.sh.
#
# The build uses the local Mono assemblies (reference/managed-mono-build<BUILD>/), so the Mac needs no
# repository and no .NET SDK. The Mac needs SSH access with a key, plus curl and unzip for the loader.
set -euo pipefail

REPO="$(cd "$(dirname "$0")/.." && pwd)"
PLUGIN=VampireSurvivorsUx.dll

HOST=""
FORCE_LOADER=0
for arg in "$@"; do
  case "$arg" in
    --loader) FORCE_LOADER=1 ;;
    -*)
      echo "usage: $0 <user@host> [--loader]" >&2
      exit 2
      ;;
    *) HOST="$arg" ;;
  esac
done
if [ -z "$HOST" ]; then
  echo "usage: $0 <user@host> [--loader]" >&2
  exit 2
fi

# The remote login shell reads the command line, so quote the path for it. An empty value selects the
# default of the remote script.
RGAME_Q="$(printf '%q' "${REMOTE_GAME_PATH:-}")"
SSH=(ssh -o BatchMode=yes -o ConnectTimeout=10 "$HOST")

# Every remote script starts with this line. $1 is the game folder.
# shellcheck disable=SC2016
REMOTE_HEAD='GAME="${1:-$HOME/Library/Application Support/Steam/steamapps/common/Vampire Survivors}"'

echo "== Check the Mac: $HOST"
STATE="$("${SSH[@]}" "bash -s -- $RGAME_Q" <<EOF
set -eu
$REMOTE_HEAD
if [ ! -d "\$GAME/Vampire_Survivors.app" ]; then
  echo "missing-game \$GAME"
elif [ ! -d "\$GAME/BepInEx/core" ] || [ ! -f "\$GAME/run_bepinex.sh" ]; then
  echo "missing-loader \$GAME"
else
  echo "ok \$GAME"
fi
EOF
)"
RGAME="${STATE#* }"
case "$STATE" in
  missing-game*)
    echo "The macOS build is not installed in: $RGAME" >&2
    exit 1
    ;;
  missing-loader*) FORCE_LOADER=1 ;;
esac
echo "Game folder: $RGAME"

if [ "$FORCE_LOADER" -eq 1 ]; then
  # unzip writes into the loader files that a game process has open.
  if "${SSH[@]}" "pgrep -f 'Vampire_Survivors.app/Contents/MacOS' > /dev/null"; then
    echo "The game runs on the Mac. Close it before the loader install." >&2
    exit 1
  fi
  echo "== Install BepInEx 5 on the Mac"
  # shellcheck disable=SC2016
  "${SSH[@]}" "GAME_PATH=$(printf '%q' "$RGAME")"' LOADER_DL="$HOME/Library/Caches/vampire-survivors-ux" bash -s -- bepinex-macos' \
    < "$REPO/tools/install-loader.sh"
fi

OUT="$(mktemp -d)"
trap 'rm -rf "$OUT"' EXIT
ONLY=mono "$REPO/tools/build-release.sh" "$OUT"
DLL="$OUT/VampireSurvivorsUx-mono.dll"
LOCAL_SUM="$(sha256sum "$DLL" | cut -d' ' -f1)"

echo "== Copy $PLUGIN to the Mac"
# cat through SSH has no quote problem with the space in the game path, and scp has.
# shellcheck disable=SC2016
"${SSH[@]}" 'cat > "$HOME/.vampire-survivors-ux.part"' < "$DLL"

# mv replaces the file in one step, so a game that runs keeps the old file until its next start.
RESULT="$("${SSH[@]}" "bash -s -- $RGAME_Q" <<EOF
set -eu
$REMOTE_HEAD
mkdir -p "\$GAME/BepInEx/plugins"
mv "\$HOME/.vampire-survivors-ux.part" "\$GAME/BepInEx/plugins/$PLUGIN"
shasum -a 256 "\$GAME/BepInEx/plugins/$PLUGIN" | cut -d' ' -f1
if pgrep -f 'Vampire_Survivors.app/Contents/MacOS' > /dev/null; then echo running; else echo stopped; fi
EOF
)"
REMOTE_SUM="$(printf '%s\n' "$RESULT" | sed -n 1p)"
GAME_STATE="$(printf '%s\n' "$RESULT" | sed -n 2p)"

if [ "$LOCAL_SUM" != "$REMOTE_SUM" ]; then
  echo "Checksum mismatch. Local: $LOCAL_SUM. Mac: $REMOTE_SUM." >&2
  exit 1
fi

echo "Installed: $RGAME/BepInEx/plugins/$PLUGIN"
echo "sha256: $REMOTE_SUM"
if [ "$GAME_STATE" = running ]; then
  echo "The game runs on the Mac. Start it again to load the new file."
fi
echo "Steam launch option on the Mac:"
echo "  \"$RGAME/run_bepinex.sh\" %command%"
