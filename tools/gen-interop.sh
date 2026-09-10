#!/usr/bin/env bash
# Makes Il2CppInterop assemblies for the Windows IL2CPP build without a game start.
# Output: reference/interop/Il2CppAssemblies/ (the reference/ folder is not committed).
# Usage: nix develop -c tools/gen-interop.sh
set -euo pipefail

REPO="$(cd "$(dirname "$0")/.." && pwd)"
GAME="${GAME_PATH:-$HOME/.local/share/Steam/steamapps/common/Vampire Survivors}"
IOP="$REPO/reference/interop"
UNITY_VERSION="${UNITY_VERSION:-6000.0.62}"
CPP2IL_VERSION="2022.1.0-pre-release.21"
INTEROP_VERSION="1.5.3"

mkdir -p "$IOP"
cd "$IOP"

if [ ! -x Cpp2IL ]; then
  curl -sSL -o Cpp2IL "https://github.com/SamboyCoding/Cpp2IL/releases/download/$CPP2IL_VERSION/Cpp2IL-$CPP2IL_VERSION-Linux"
  chmod +x Cpp2IL
fi
if [ ! -d cli ]; then
  curl -sSL -o cli.zip "https://github.com/BepInEx/Il2CppInterop/releases/download/v$INTEROP_VERSION/Il2CppInterop.CLI.$INTEROP_VERSION.zip"
  mkdir -p cli && unzip -o -q cli.zip -d cli
fi
if [ ! -d UnityDependencies ]; then
  curl -sSL -o unity.zip "https://github.com/LavaGang/MelonLoader.UnityDependencies/releases/download/$UNITY_VERSION/Managed.zip"
  mkdir -p UnityDependencies && unzip -o -q unity.zip -d UnityDependencies
fi

rm -rf cpp2il_out Il2CppAssemblies
./Cpp2IL \
  --force-binary-path "$GAME/GameAssembly.dll" \
  --force-metadata-path "$GAME/VampireSurvivors_Data/il2cpp_data/Metadata/global-metadata.dat" \
  --force-unity-version "$UNITY_VERSION" \
  --use-processor attributeanalyzer,attributeinjector \
  --output-as dummydll \
  --output-to cpp2il_out

# The CLI targets net6.0. The dev shell has the .NET 8 runtime.
DOTNET_ROLL_FORWARD=LatestMajor dotnet cli/net6.0/Il2CppInterop.CLI.dll generate \
  --input cpp2il_out \
  --output Il2CppAssemblies \
  --unity UnityDependencies \
  --game-assembly "$GAME/GameAssembly.dll" \
  --use-opt-out-prefixing

echo "Done: $IOP/Il2CppAssemblies"
ls Il2CppAssemblies | grep -E '^Il2CppVampireSurvivors\.Runtime\.dll$'
