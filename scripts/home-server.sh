#!/usr/bin/env bash
# Runs the backend as a per-user launchd service on the Mac that serves the phone.
#
#   scripts/home-server.sh deploy     publish the checked-out code and (re)start the service
#   scripts/home-server.sh status     whether it is running, and the last log lines
#   scripts/home-server.sh stop       unload the service; deploy loads it again
#
# The service's working directory is always the main checkout, whichever worktree this is called
# from: the database path is relative (./Data/lionheart.db) and .env is found from the working
# directory, and both live only there. Its content root is the published build, which holds the
# built web app and sits outside the repo, so rebuilding the source never swaps files under a
# running server.
set -euo pipefail

label="com.chasebalmer.lionheart.backend"
port="${LIONHEART_PORT:-7025}"
here="$(cd "$(dirname "$0")/.." && pwd)"
main_checkout="$(dirname "$(git -C "$here" rev-parse --path-format=absolute --git-common-dir)")"
publish_dir="$HOME/Library/Application Support/Lionheart/server"
log_dir="$HOME/Library/Logs/Lionheart"
plist="$HOME/Library/LaunchAgents/$label.plist"
domain="gui/$(id -u)"

write_plist() {
  mkdir -p "$log_dir" "$(dirname "$plist")"
  # Production, not Development: Development regenerates the web client into the source tree and
  # starts the Svelte dev server, neither of which a phone's server should do. Bound to every
  # interface so a phone on the home Wi-Fi reaches it directly as well as through Tailscale.
  cat > "$plist" <<EOF
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
  <key>Label</key><string>$label</string>
  <key>ProgramArguments</key>
  <array>
    <string>$publish_dir/lionheart</string>
    <string>--urls</string><string>http://0.0.0.0:$port</string>
    <string>--contentRoot</string><string>$publish_dir</string>
  </array>
  <key>WorkingDirectory</key><string>$main_checkout</string>
  <key>EnvironmentVariables</key>
  <dict><key>ASPNETCORE_ENVIRONMENT</key><string>Production</string></dict>
  <key>RunAtLoad</key><true/>
  <key>KeepAlive</key><true/>
  <key>ThrottleInterval</key><integer>10</integer>
  <key>StandardOutPath</key><string>$log_dir/backend.log</string>
  <key>StandardErrorPath</key><string>$log_dir/backend.log</string>
</dict>
</plist>
EOF
}

case "${1:-}" in
  deploy)
    dotnet publish "$main_checkout/lionheart.csproj" -c Release -o "$publish_dir" --nologo -v quiet
    write_plist
    # Unloaded and loaded again rather than kickstarted: launchd keeps the plist it was loaded
    # with, so a restart alone would miss any change write_plist just made.
    # bootout returns before the job is gone, and bootstrapping over it fails with an I/O error.
    launchctl bootout "$domain/$label" 2>/dev/null || true
    for _ in $(seq 1 50); do
      launchctl print "$domain/$label" >/dev/null 2>&1 || break
      sleep 0.2
    done
    launchctl bootstrap "$domain" "$plist"
    echo "Deployed $(git -C "$main_checkout" log -1 --format='%h %s') on port $port."
    ;;
  status)
    launchctl print "$domain/$label" 2>/dev/null | grep -E "state =|pid =|last exit code" || echo "Not loaded."
    tail -n 15 "$log_dir/backend.log" 2>/dev/null || true
    ;;
  stop)
    launchctl bootout "$domain/$label" 2>/dev/null && echo "Stopped." || echo "Not loaded."
    ;;
  *)
    sed -n '2,7p' "$0" | sed 's/^# \{0,1\}//'
    exit 64
    ;;
esac
