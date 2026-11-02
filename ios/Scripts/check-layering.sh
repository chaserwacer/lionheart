#!/bin/bash
#
# Core/ is pure values and functions: every decision a screen makes, testable without a host and
# without a renderer. A UI framework import there is how that property is lost, so it fails the
# build rather than being noticed later in review.
#
# Compiles nothing — the xcodebuild run is what proves the Swift.

set -uo pipefail

CORE="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)/Lionheart/Core"

if [[ ! -d "$CORE" ]]; then
    echo "check-layering: no Core directory at $CORE" >&2
    exit 1
fi

# Matches a real import statement at the start of a line, so the word appearing inside a comment
# or a string is not a finding.
offenders=$(grep -rnE '^[[:space:]]*(@[A-Za-z]+[[:space:]]+)?import[[:space:]]+(SwiftUI|UIKit|Charts)\b' \
    --include='*.swift' "$CORE" 2>/dev/null)

if [[ -n "$offenders" ]]; then
    echo "check-layering: Core/ must not import SwiftUI, UIKit or Charts." >&2
    echo "$offenders" >&2
    exit 1
fi

echo "check-layering: Core/ is clean."
