#!/bin/bash
#
# Redraws Tempo's icon, its Icon Composer layers and the launch mark from the mark's one geometry,
# Lionheart/Design/ProgressionMark.swift. Run after changing that file or the palette in
# Scripts/BrandAssets/main.swift, and commit what it writes; the build does not run it.

set -euo pipefail

IOS="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
WORK="$(mktemp -d)"
trap 'rm -rf "$WORK"' EXIT

xcrun swiftc -O -o "$WORK/render" \
    "$IOS/Lionheart/Design/ProgressionMark.swift" \
    "$IOS/Scripts/BrandAssets/main.swift"

"$WORK/render" "$IOS"
