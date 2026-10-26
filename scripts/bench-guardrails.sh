#!/usr/bin/env bash
set -euo pipefail

# Reproducible Guardrails build-path benchmark. Take the shared lock per scenario so queueing is
# outside the measurement and other lanes get the lock between scenarios:
#   flock -o /home/adam/projects/scratch/axial-coord/limits/dotnet.lock env AXIAL_DOTNET_LOCK_HELD=1 \
#     bash scripts/bench-guardrails.sh S4

root=$(cd "$(dirname "$0")/.." && pwd)
cd "$root"
scenario_arg=${1:-}
scenarios=${scenario_arg:-${GUARDRAILS_BENCH_SCENARIOS:-S1,S2,S3,S4,S5}}
if [[ -n $scenario_arg && ! $scenario_arg =~ ^S[1-5]$ ]]; then echo 'usage: bench-guardrails.sh [S1|S2|S3|S4|S5]' >&2; exit 2; fi
default_runs=5
[[ $scenarios == S4 || $scenarios == S5 ]] && default_runs=3
runs=${GUARDRAILS_BENCH_RUNS:-$default_runs}
out=${GUARDRAILS_BENCH_OUT:-artifacts/bench/guardrails}
project=${GUARDRAILS_BENCH_PROJECT:-src/Axial/Axial.fsproj}
source=${GUARDRAILS_BENCH_SOURCE:-src/Axial/Flow.fs}
mkdir -p "$out/raw"
minimum_runs=5
[[ $scenarios == S4 || $scenarios == S5 ]] && minimum_runs=3
(( runs >= minimum_runs )) || { echo "GUARDRAILS_BENCH_RUNS must be >= $minimum_runs for $scenarios" >&2; exit 2; }
[[ ${AXIAL_DOTNET_LOCK_HELD:-} == 1 ]] || { echo 'Run while holding the shared dotnet lock (see script header).' >&2; exit 2; }
command -v /usr/bin/time >/dev/null

contains() { [[ ,$scenarios, == *,$1,* ]]; }
clear_guardrails_cache() {
  find . -path '*/obj/*' \( -name 'axial-guardrails.*.stamp' -o -name 'axial-guardrails.*.inputs' \) -delete
}
run_timed() {
  local scenario=$1 iteration=$2; shift 2
  local prefix="$out/raw/${scenario}-${iteration}"
  set +e
  /usr/bin/time -f '{"user":%U,"sys":%S,"wall_seconds":%e,"max_rss_kb":%M,"exit":%x}' \
    -o "$prefix.time.json" "$@" >"$prefix.log" 2>&1
  local status=$?
  set -e
  # Findings are expected to fail a build; preserve evidence but reject infrastructure failures.
  if [[ $status != 0 ]] && ! grep -Eq 'AXG[0-9]{3}|reported findings' "$prefix.log"; then
    tail -25 "$prefix.log" >&2
    return "$status"
  fi
  grep -Eo '^.*\([0-9]+,[0-9]+\): (error|warning) AXG[0-9]{3}: .*' "$prefix.log" \
    | sed -E 's#^\s*##' | sort -u >"$prefix.diagnostics" || true
  printf '%s\n' "$(grep -c 'Running Axial.Guardrails analysis' "$prefix.log" || true)" >"$prefix.invocations"
}
prepare_scenario() {
  case $1 in
    S1) dotnet build src/Axial.Tool/Axial.Tool.fsproj --nologo -v:q >/dev/null ;;
    S2) dotnet build "$project" --nologo -v:q >/dev/null ;;
    S3) clear_guardrails_cache ;;
    S4) : ;;
    S5) dotnet build Axial.slnx --nologo -v:q >/dev/null ;;
  esac
}
execute_scenario() {
  local scenario=$1 iteration=$2
  case $scenario in
    S1) run_timed "$scenario" "$iteration" dotnet --roll-forward Major artifacts/bin/Axial.Tool/debug/Axial.Tool.dll check ;;
    S2) run_timed "$scenario" "$iteration" dotnet build "$project" --nologo -v:minimal ;;
    S3)
      clear_guardrails_cache
      run_timed "$scenario" "$iteration" dotnet build "$project" --nologo -v:minimal \
        -p:AxialGuardrailsTimings=true ;;
    S4)
      cp "$source" "$out/raw/Flow.fs.saved"
      printf '\n// guardrails benchmark body-only edit %s\n' "$iteration" >>"$source"
      run_timed "$scenario" "$iteration" dotnet build Axial.slnx --nologo -v:minimal
      mv "$out/raw/Flow.fs.saved" "$source" ;;
    S5)
      run_timed "S5-with" "$iteration" dotnet build Axial.slnx --nologo -v:minimal
      run_timed "S5-without" "$iteration" dotnet build Axial.slnx --nologo -v:minimal -p:_AxialRepoSkipGuardrails=true ;;
  esac
}
trap 'if [[ -f "$out/raw/Flow.fs.saved" ]]; then mv "$out/raw/Flow.fs.saved" "$source"; fi' EXIT

for scenario in S1 S2 S3 S4 S5; do
  contains "$scenario" || continue
  echo "warmup $scenario" >&2
  prepare_scenario "$scenario"
  execute_scenario "$scenario" 0
  rm -f "$out/raw/${scenario}-0."* "$out/raw/S5-with-0."* "$out/raw/S5-without-0."*
  for iteration in $(seq 1 "$runs"); do
    echo "$scenario $iteration/$runs" >&2
    execute_scenario "$scenario" "$iteration"
  done
done

python3 - "$out" "$runs" "$scenarios" <<'PY'
import datetime, glob, json, os, platform, statistics, subprocess, sys
out, runs, scenarios = sys.argv[1], int(sys.argv[2]), sys.argv[3]
def percentile(xs, p):
    xs=sorted(xs); return xs[max(0, min(len(xs)-1, int((len(xs)-1)*p+.999999)))]
def result(name):
    rows=[]
    for path in sorted(glob.glob(f'{out}/raw/{name}-[1-9]*.time.json')):
        with open(path) as f:
            lines=[line for line in f if line.lstrip().startswith('{')]
        if not lines: continue
        row=json.loads(lines[-1])
        row['cpu_seconds']=row['user']+row['sys']
        stem=path[:-len('.time.json')]
        row['analyzer_invocations']=int(open(stem+'.invocations').read())
        rows.append(row)
    if not rows: return None
    return {'runs':rows, 'median':{'wall_seconds':statistics.median(r['wall_seconds'] for r in rows),'cpu_seconds':statistics.median(r['cpu_seconds'] for r in rows)},'p90':{'wall_seconds':percentile([r['wall_seconds'] for r in rows],.9),'cpu_seconds':percentile([r['cpu_seconds'] for r in rows],.9)},'median_analyzer_invocations':statistics.median(r['analyzer_invocations'] for r in rows)}
names=['S1','S2','S3','S4','S5-with','S5-without']
data={'schema':1,'created_utc':datetime.datetime.now(datetime.timezone.utc).isoformat(),'git':subprocess.check_output(['git','rev-parse','HEAD'],text=True).strip(),'runs_per_scenario':runs,'requested_scenarios':scenarios,'machine':{'node':platform.node(),'platform':platform.platform(),'cpu_count':os.cpu_count(),'uptime':open('/proc/uptime').read().strip(),'loadavg':open('/proc/loadavg').read().strip()},'results':{n:r for n in names if (r:=result(n))}}
path=f'{out}/summary.json'
with open(path,'w') as f: json.dump(data,f,indent=2); f.write('\n')
print(path)
PY
