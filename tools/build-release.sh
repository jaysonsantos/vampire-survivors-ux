#!/usr/bin/env bash
# Builds the release files for every supported platform (AGENTS.md step 2).
# Usage: tools/build-release.sh [output folder]
# Output (default dist/):
#   VampireSurvivorsUx.dll       Windows, Linux, and Steam Deck. BepInEx 6 on IL2CPP.
#   VampireSurvivorsUx-mono.dll  macOS. BepInEx 5 on Mono.
#
# The build needs the game assemblies, because the mod patches game types.
# Those files belong to poncle and are not in the repository, so a build server
# cannot make them. This script runs on a computer that has the game.
#
# IL2CPP assemblies come from the game folder (BepInEx/interop) or from
# tools/gen-interop.sh. Mono assemblies come from the macOS or Linux game folder,
# or from a local copy in reference/managed-mono-build<BUILD>/.
set -euo pipefail

REPO="$(cd "$(dirname "$0")/.." && pwd)"
OUT="${1:-$REPO/dist}"
PROJ="$REPO/src/VampireSurvivorsUx/VampireSurvivorsUx.csproj"
BIN="$REPO/src/VampireSurvivorsUx/bin/Release"

# First folder of the list that exists.
pick() {
  for d in "$@"; do
    if [ -d "$d" ]; then
      printf '%s' "$d"
      return 0
    fi
  done
  return 1
}

# The reference/managed-mono-build<BUILD> folders, newest build ID first.
# A plain glob gives lexical order, so it would select an old build after a game update.
mono_refs() {
  local d
  for d in "$REPO"/reference/managed-mono-build*; do
    [ -d "$d" ] && printf '%s\n' "$d"
  done | sort -V -r
}

GAME_IL2CPP="${GAME_PATH:-$HOME/.local/share/Steam/steamapps/common/Vampire Survivors}"
GAME_MONO="${MACOS_GAME_PATH:-$HOME/Library/Application Support/Steam/steamapps/common/Vampire Survivors}"

IL2CPP_ASM="${IL2CPP_ASM:-}"
if [ -z "$IL2CPP_ASM" ]; then
  IL2CPP_ASM="$(pick \
    "$GAME_IL2CPP/BepInEx/interop" \
    "$REPO/reference/interop/Il2CppAssemblies" || true)"
fi

MONO_ASM="${MONO_ASM:-}"
if [ -z "$MONO_ASM" ]; then
  MONO_REFS=()
  while IFS= read -r d; do
    MONO_REFS+=("$d")
  done < <(mono_refs)
  if [ "${#MONO_REFS[@]}" -gt 1 ]; then
    echo "More than one reference/managed-mono-build* folder. This build uses ${MONO_REFS[0]}." >&2
    echo "Set MONO_ASM to select another folder." >&2
  fi
  MONO_ASM="$(pick \
    "$GAME_MONO/Vampire_Survivors.app/Contents/Resources/Data/Managed" \
    "$GAME_IL2CPP/VampireSurvivors_Data/Managed" \
    ${MONO_REFS[@]+"${MONO_REFS[@]}"} || true)"
fi

MONO_LIB="${MONO_LIB:-}"
if [ -z "$MONO_LIB" ]; then
  MONO_LIB="$(pick \
    "$GAME_MONO/BepInEx/core" \
    "$REPO/reference/loaders/bepinex5/BepInEx/core" || true)"
fi

fail=0
if [ -z "$IL2CPP_ASM" ]; then
  echo "No IL2CPP assemblies. Start the game once with BepInEx, or run tools/gen-interop.sh." >&2
  fail=1
fi
if [ -z "$MONO_ASM" ]; then
  echo "No Mono assemblies. Copy VampireSurvivors_Data/Managed to reference/managed-mono-build<BUILD>/." >&2
  fail=1
fi
if [ -z "$MONO_LIB" ]; then
  echo "No BepInEx 5 libraries. Run tools/install-loader.sh bepinex-macos, or unzip the loader to" >&2
  echo "reference/loaders/bepinex5/." >&2
  fail=1
fi
[ "$fail" -eq 0 ] || exit 1

mkdir -p "$OUT"

echo "== BepInEx (IL2CPP): $IL2CPP_ASM"
dotnet build "$PROJ" -c Release -p:Loader=BepInEx \
  -p:GameAsmPath="$IL2CPP_ASM" -p:CopyToMods=false
cp "$BIN/BepInEx/VampireSurvivorsUx.dll" "$OUT/VampireSurvivorsUx.dll"

echo "== BepInExMono (macOS): $MONO_ASM"
dotnet build "$PROJ" -c Release -p:Loader=BepInExMono \
  -p:GameAsmPath="$MONO_ASM" -p:LoaderLibPath="$MONO_LIB" -p:CopyToMods=false
cp "$BIN/BepInExMono/VampireSurvivorsUx.dll" "$OUT/VampireSurvivorsUx-mono.dll"

echo
echo "Release files in $OUT:"
ls -l "$OUT"
