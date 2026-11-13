/// Runs a few flows through each Fable-compatible package as a NuGet consumer would, prints one line per check, and
/// exits non-zero (through NodeApp's exit code) when any check fails.
module Axial.Fable.PackageTests.Program

open System
open Axial
open Axial.Configuration
open Axial.Hosting.Node
open Axial.Telemetry.JavaScript
open Fable.Core

[<Emit("console.log($0)")>]
let private print (_line: string) : unit = jsNative

[<Emit("(() => { const spans=[]; const tracer={ startSpan(name){ const span={name,tags:{},setAttribute(k,v){this.tags[k]=v;},setStatus(s){this.status=s;},spanContext(){return {};},end(){this.ended=true;}}; spans.push(span); return span; }}; return {spans, trace:{getTracer(){return tracer;},setSpan(c,s){return {span:s};}},context:{active(){return {};},with(c,f){return f();}}}; })()")>]
let private fakeOtel () : OpenTelemetryApi = jsNative

[<Emit("$0.spans.map(s => s.name + (s.ended ? ':ended' : ':open')).join(',')")>]
let private spans (_api: OpenTelemetryApi) : string = jsNative

let private exitOf (source: Flow<'env, 'error, 'value>) : Flow<'env, 'none, Exit<'value, 'error>> =
    source |> Flow.foldCause (fun value -> Flow.ok (Exit.Success value)) (fun cause -> Flow.ok (Exit.Failure cause))

let private check (name: string) (passed: bool) = name, passed

let private forkAndJoin: Flow<unit, Never, (string * bool) list> = flow {
    let worker (delay: int) (value: int) = flow {
        do! Flow.sleep (TimeSpan.FromMilliseconds(float delay))
        return value
    }

    let! slow = worker 20 1 |> Flow.fork
    let! fast = worker 5 2 |> Flow.fork
    let! a = Fiber.join slow
    let! b = Fiber.join fast
    return [ check "Axial: forked fibers sleep and join" (a + b = 3) ]
}

let private typedFailure: Flow<unit, Never, (string * bool) list> = flow {
    let! exit = Flow.fail "boom" |> Flow.map (fun () -> 0) |> exitOf

    let failed =
        match exit with
        | Exit.Failure(Cause.Fail "boom") -> true
        | _ -> false

    return [ check "Axial: a typed failure is a value at the edge" failed ]
}

let private configuration: Flow<unit, Never, (string * bool) list> = flow {
    let! exit =
        Config.loadData
            [ ConfigSource.environmentOf None [ "Port", "1"; "Name", "env" ]
              ConfigSource.commandLine [ "--Port=2" ] ]
        |> exitOf

    let port =
        match exit with
        | Exit.Success config ->
            match Config.tryFind "Port" config with
            | Ok value -> value
            | Error _ -> None
        | Exit.Failure _ -> None

    return [ check "Axial.Configuration: later sources win" (port = Some "2") ]
}

let private telemetry: Flow<unit, Never, (string * bool) list> = flow {
    let api = fakeOtel ()
    Otel.installNamed api "package-test"

    try
        let! value = Flow.ok 41 |> Flow.map ((+) 1) |> Otel.trace "package-span"
        return [ check "Axial.Telemetry.JavaScript: a traced flow ends its span" (value = 42 && spans api = "package-span:ended") ]
    finally
        Otel.uninstall ()
}

let private application: Flow<unit, string, unit> = flow {
    let! results =
        [ forkAndJoin; typedFailure; configuration; telemetry ]
        |> List.map (Flow.mapError Never.absurd)
        |> Flow.sequence

    let checks = List.concat results

    for name, passed in checks do
        print ((if passed then "  pass " else "  FAIL ") + name)

    let failed = checks |> List.filter (snd >> not) |> List.length
    print $"{List.length checks} package checks, {failed} failed"

    if failed > 0 then
        return! Flow.fail $"{failed} package checks failed"
}

NodeApp.start id () application |> ignore
