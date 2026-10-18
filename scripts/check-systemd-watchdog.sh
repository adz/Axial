#!/usr/bin/env bash
set -euo pipefail
if [[ $(uname -s) != Linux ]] || ! systemctl --user is-system-running >/dev/null 2>&1; then
  echo 'skipped: systemd user session unavailable'
  exit 0
fi
cd "$(dirname "$0")/.."
dotnet build examples/Axial.Hosting.WatchdogProbe -c Release --nologo -v quiet >/dev/null
probe="$(pwd)/artifacts/bin/Axial.Hosting.WatchdogProbe/release/Axial.Hosting.WatchdogProbe.dll"
unit="axial-watchdog-probe-$RANDOM-$RANDOM"
units=()
cleanup() {
  local u
  for u in "${units[@]}"; do
    systemctl --user stop "$u" >/dev/null || echo "FAIL: stop $u" >&2
    if systemctl --user show "$u" -p LoadState --value 2>/dev/null | grep -qv not-found; then
      systemctl --user reset-failed "$u" >/dev/null || echo "FAIL: reset-failed $u" >&2
    fi
  done
}
trap cleanup EXIT
trap 'exit 130' INT
trap 'exit 143' TERM
failed=0
check() { if "$@"; then echo "PASS: $label"; else echo "FAIL: $label"; failed=1; fi; }
state() { systemctl --user show "$unit" -p "$1" --value; }
active_zero() { [[ $(state ActiveState) == active && $(state NRestarts) == 0 ]]; }
restarted() { [[ $(state ActiveState) == active && $(state NRestarts) -ge 1 ]]; }
wait_for() { local deadline=$((SECONDS + $1)); until "${@:2}"; do (( SECONDS >= deadline )) && return 1; sleep 0.25; done; }
start() {
  units+=("$unit")
  systemd-run --user --unit="$unit" -p Type=notify -p WatchdogSec=3 -p Restart=always -p RestartSec=1 -p TimeoutStopSec=2 -p NotifyAccess=main dotnet "$probe" "$@" >/dev/null
}
start run
sleep 8
label='run remains active, NRestarts=0'; check active_zero
pid=$(state MainPID)
if [[ $pid =~ ^[1-9][0-9]*$ ]]; then kill -9 "$pid"; fi
label='SIGKILL restarts within 5s'; check wait_for 5 restarted
unit="axial-watchdog-probe-$RANDOM-$RANDOM"
hang_start=$(date '+%Y-%m-%d %H:%M:%S')
start hang-after 2
watchdog_fired() {
  journalctl --user -u "$unit" --since "$hang_start" --no-pager 2>/dev/null | grep -qi 'Watchdog timeout'
}
watchdog_restart() { watchdog_fired && [[ $(state NRestarts) -ge 1 ]]; }
label='hang triggers watchdog and restarts within 25s'; check wait_for 25 watchdog_restart
exit "$failed"
