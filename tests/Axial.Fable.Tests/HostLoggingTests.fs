module Axial.Fable.Tests.HostLoggingTests

open System
open Axial
open Axial.PlatformService
open Axial.Hosting.Node
open Axial.Hosting.Browser
open Fable.Core
open Axial.Fable.Tests.Harness

[<Emit("(() => { const methods=['debug','info','warn','error']; const saved=Object.fromEntries(methods.map(k=>[k,console[k]])); const calls=[]; const win=globalThis.window, doc=globalThis.document, code=process.exitCode; globalThis.window={}; globalThis.document={}; for(const k of methods) console[k]=(...args)=>calls.push([k,args]); return { calls, restore(){ for(const k of methods) console[k]=saved[k]; if(win===undefined) delete globalThis.window; else globalThis.window=win; if(doc===undefined) delete globalThis.document; else globalThis.document=doc; process.exitCode=code; } }; })()")>]
let private capture () : obj = jsNative

[<Emit("$0.calls")>]
let private calls (_state: obj) : (string * obj array) array = jsNative

[<Emit("$0.restore()")>]
let private restore (_state: obj) : unit = jsNative

[<Emit("new AbortController().signal")>]
let private signal () : AbortSignal = jsNative

let private start kind (source: Flow<unit, Never, unit>) =
    match kind with
    | 0 -> NodeApp.start Never.absurd () source
    | 1 -> BrowserApp.mount () source
    | _ -> BrowserApp.startWithSignal (signal ()) () source

let private startWithSink kind sink (source: Flow<unit, Never, unit>) =
    match kind with
    | 0 -> NodeApp.startWithOptions { LogSink = Some sink } Never.absurd () source
    | 1 -> BrowserApp.mountWithOptions { LogSink = Some sink } () source
    | _ -> BrowserApp.startWithSignalOptions { LogSink = Some sink } (signal ()) () source

let private label = function 0 -> "Node" | 1 -> "Browser mount" | _ -> "Browser signal"

let private metadata (args: obj array) =
    args.Length = 4 &&
    (unbox<(string * string) array> args.[2] |> Array.contains ("request", "js-request")) &&
    (unbox<(string * obj) array> args.[3] |> Array.contains ("job", box "js-job"))

let tests =
    [ for kind in 0 .. 2 do
          test ("host logging " + label kind + " root and fork console metadata") (flow {
              let state = capture ()
              try
                  let local = FiberLocal.named "job" "unset"
                  let error = InvalidOperationException("js-error")
                  let source =
                      (flow {
                          do! Log.debug "disabled"
                          do! Log.info "root-message"
                          let! child = Log.errorExn error "child-message" |> Flow.forkNamed "js-log-child"
                          do! Fiber.join child
                      }) |> Flow.annotate "request" "js-request" |> FiberLocal.locally "js-job" local
                  let running = start kind source
                  let! exit = Flow.fromAsync running.Completion
                  let captured = calls state
                  match captured with
                  | [| (firstMethod, first); (secondMethod, second) |] ->
                      return [ equal "successful root" (Exit.Success ()) exit
                               equal "info method" (if kind = 0 then "error" else "info") firstMethod
                               equal "error method" "error" secondMethod
                               isTrue "info level and message" ((string first.[0]).Contains "Information: root-message")
                               isTrue "error level and message" ((string second.[0]).Contains "Error: child-message")
                               isTrue "info metadata snapshot" (metadata first)
                               isTrue "fork metadata snapshot" (metadata second)
                               equal "exception" "js-error" (unbox<exn> second.[1]).Message ]
                  | _ -> return [ equal "two entries, no disabled entry" 2 captured.Length ]
              finally restore state
          })
          test ("host logging " + label kind + " minimum gate and inner override") (flow {
              let state = capture ()
              try
                  let sink = if kind = 0 then NodeLogging.sink LogLevel.Warning else BrowserLogging.sink LogLevel.Warning
                  let memory = Log.memory ()
                  let source = flow {
                      do! Log.debug "disabled-debug"
                      do! Log.info "disabled-info"
                      do! Log.info "inner" |> Flow.withLogSink memory
                  }
                  let running = startWithSink kind sink source
                  let! _ = Flow.fromAsync running.Completion
                  return [ isTrue "level gate rejects info" (not (sink.IsEnabled LogLevel.Information))
                           equal "no console entry built for disabled levels" 0 (calls state).Length
                           equal "inner scoped sink wins" ["inner"] (memory.Entries |> Seq.map _.Message |> Seq.toList) ]
              finally restore state
          })
          test ("host logging " + label kind + " routes every level") (flow {
              let state = capture ()
              try
                  let sink = if kind = 0 then NodeLogging.sink LogLevel.Trace else BrowserLogging.sink LogLevel.Trace
                  let source = [ Log.trace "trace"; Log.debug "debug"; Log.info "info"; Log.warning "warning"; Log.error "error"; Log.critical "critical" ] |> Flow.sequence |> Flow.ignore
                  let completion =
                      if kind = 0 then NodeApp.runWithOptions { LogSink = Some sink } Never.absurd () source
                      else (startWithSink kind sink source).Completion
                  let! _ = Flow.fromAsync completion
                  let expected = if kind = 0 then List.replicate 6 "error" else ["debug"; "debug"; "info"; "warn"; "error"; "error"]
                  return [ equal "console methods" expected (calls state |> Array.map fst |> Array.toList) ]
              finally restore state
          })
          test ("host logging " + label kind + " can be silenced without hiding an inner sink") (flow {
              let state = capture ()
              try
                  let memory = Log.memory ()
                  let source = flow {
                      do! Log.error "silenced"
                      do! Log.info "inner" |> Flow.withLogSink memory
                  }
                  let running = startWithSink kind LogSink.none source
                  let! _ = Flow.fromAsync running.Completion
                  return [ equal "no console write" 0 (calls state).Length
                           equal "inner sink wins" ["inner"] (memory.Entries |> Seq.map _.Message |> Seq.toList) ]
              finally restore state
          }) ]
