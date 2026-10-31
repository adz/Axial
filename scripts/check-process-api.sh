#!/usr/bin/env bash
# Compile public pipeline shapes and reject command settings applied to a topology.
set -euo pipefail
root_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
probe_dir="$(mktemp -d)"
trap 'rm -rf "$probe_dir"' EXIT
for package in Axial Axial.Configuration Axial.Console Axial.FileSystem Axial.Process; do
  printf '#r @"%s/artifacts/bin/%s/debug_net8.0/%s.dll"\n' "$root_dir" "$package" "$package" >> "$probe_dir/prelude.fsx"
done
cat >> "$probe_dir/prelude.fsx" <<'FSHARP'
open System
open Axial
open Axial.Process
open Axial.Process.DSL
open Axial.PlatformService
open Axial.Configuration
FSHARP
cp "$probe_dir/prelude.fsx" "$probe_dir/valid.fsx"
cat >> "$probe_dir/valid.fsx" <<'FSHARP'
type Minimal =
    {Process: IProcess}
    interface IHasProcess with member this.Process = this.Process
let qualified (env: Minimal) =
    let command = Process.command $"true"
    let spec = command |> Process.pipe command
    let workflow = command |> Process.toFlow
    Process.runWith env command, Process.runWith env spec, Process.runWith env workflow
let dsl (env: ProcessHostEnvironment) =
    let command = cmd $"true"
    let spec = command => command
    let workflow = command |> toFlow
    runWith env command, runWith env spec, runWith env workflow,
    runExitCodeWith env command, runExitCodeWith env spec, runExitCodeWith env workflow
let pureOutcome () = Flow.succeed 42 |> Process.runWith ()
let consumerErrors (env: ProcessHostEnvironment) =
    cmd $"true" |> streamStdoutLines |> ProcessStream.runForEachFlow(fun _ -> Flow.fail "consumer") |> runWith env
FSHARP
dotnet fsi --exec "$probe_dir/valid.fsx" > "$probe_dir/result" 2>&1 || { cat "$probe_dir/result"; exit 1; }
for setter in 'Process.workingDirectory "."' 'Process.environment "A" "B"' 'Process.encoding System.Text.Encoding.UTF8' 'Process.timeout (TimeSpan.FromSeconds 1.)' 'Process.withoutTimeout' 'Process.stdinFromText "input"' 'Process.stdoutToDiscard' 'Process.stderrToConsole'; do
  cp "$probe_dir/prelude.fsx" "$probe_dir/invalid.fsx"
  printf 'let invalid = cmd $"true" => cmd $"cat" |> %s\n' "$setter" >> "$probe_dir/invalid.fsx"
  if dotnet fsi --exec "$probe_dir/invalid.fsx" > "$probe_dir/result" 2>&1; then
    printf 'Topology unexpectedly accepted command setter: %s\n' "$setter" >&2
    exit 1
  fi
  rg -q 'FS0001' "$probe_dir/result" || { cat "$probe_dir/result"; exit 1; }
done
printf 'Process API compile probes passed.\n'
