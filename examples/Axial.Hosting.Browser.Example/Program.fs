module Axial.Hosting.Browser.Example

open System
open Axial
open Axial.PlatformService
open Axial.Hosting.Browser
open Fable.Core

[<Emit("document.getElementById($0)")>]
let elementById (_id: string) : obj = jsNative

[<Emit("$0.textContent = $1")>]
let setText (_element: obj) (_text: string) : unit = jsNative

[<Emit("$0.addEventListener('click', $1)")>]
let onClick (_element: obj) (_handler: unit -> unit) : unit = jsNative

[<Emit("new AbortController()")>]
let createAbortController () : obj = jsNative

[<Emit("$0.signal")>]
let signal (_controller: obj) : AbortSignal = jsNative

[<Emit("$0.abort()")>]
let abort (_controller: obj) : unit = jsNative

let status = elementById "status"
let stopButton = elementById "stop"

type BrowserEnvironment =
    { Status: obj; Clock: IClock }
    interface IHasClock with member this.Clock = this.Clock

let application : Flow<BrowserEnvironment, string, unit> =
    flow {
        let! statusElement = Flow.envWith (fun environment -> environment.Status)
        // The owner observes this cleanup before Completion settles after an abort.
        do! Flow.scopeAsyncFinalizer (fun _ -> async { setText statusElement "Cleanup finished." })
        do! async { setText statusElement "Application is running." }
        do! Flow.sleep(TimeSpan.FromDays 1.0)
    }

let controller = createAbortController ()
// The page owns this controller; abort is an ownership event, not an unload heuristic.
let running = BrowserApp.startWithSignal (signal controller) { Status = status; Clock = Clock.live } application

onClick stopButton (fun () ->
    setText status "Stop requested; waiting for cleanup..."
    abort controller)

async {
    let! exit = running.Completion

    match exit with
    | Exit.Success () -> setText status "Application completed."
    | Exit.Failure Cause.Interrupt -> setText status "Application stopped after cleanup."
    | Exit.Failure cause -> setText status $"Application failed: {Cause.prettyPrint id cause}"
}
|> Async.StartImmediate
