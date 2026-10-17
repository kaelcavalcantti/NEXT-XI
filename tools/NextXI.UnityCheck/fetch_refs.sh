#!/usr/bin/env bash
# Downloads the UnityEditor reference assembly used only for compile-checking editor scripts.
set -euo pipefail
cd "$(dirname "$0")"
mkdir -p refs tmp
curl -sSL -o tmp/ue.nupkg https://api.nuget.org/v3-flatcontainer/unity3d.unityeditor/2018.1.6-f1/unity3d.unityeditor.2018.1.6-f1.nupkg
unzip -o -q tmp/ue.nupkg -d tmp/ue
cp tmp/ue/lib/UnityEditor.dll refs/
rm -rf tmp
echo "refs/UnityEditor.dll ready"
