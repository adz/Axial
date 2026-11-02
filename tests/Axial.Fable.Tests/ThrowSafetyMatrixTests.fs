/// T30: the reduced throw-safety matrix on JavaScript. The full matrix, with the reflection check that every public
/// combinator is in its table, is tests/Axial.Tests/ThrowSafetyMatrixTests.fs; this one keeps an explicit table of the
/// combinators whose JavaScript paths differ (fibers, scopes, timers, signals, streams) and runs each cell in the same
/// two modes: the user code throws as soon as it is called, or after a real suspension.
module Axial.Fable.Tests.ThrowSafetyMatrixTests

open System
open System.Threading
open Axial
open Axial.Fable.Tests.Harness
open Fable.Core

type private Mode =
    | Sync
    | AfterAwait

type private Expect =
    | ExpectDie
    | ExpectFail of string
    | ExpectValue of int

/// Supplies a cell's throwing user code for its mode, plus the counters and the sibling signal it checks. JavaScript
/// is single-threaded, so the counters are plain fields.
type private Ctx(cell: string, mode: Mode, signal: Deferred<Never, unit>) =
    let boom = Exception $"boom from {cell}"
    let mutable compensations = 0
    let mutable acquired = 0
    let mutable released = 0
    let mutable handlerCalls = 0
    let mutable siblingStarted = false

    member _.IsBoom(error: exn) = LanguagePrimitives.PhysicalEquality error boom
    member _.Throw<'a>() : 'a = raise boom
    member _.Fn<'a, 'b>() : 'a -> 'b = fun _ -> raise boom

    member _.HopOf<'env, 'e>() : Flow<'env, 'e, unit> =
        match mode with
        | Sync -> Flow.ok ()
        | AfterAwait -> Flow.sleep (TimeSpan.FromMilliseconds 1.0)

    member this.Hop: Flow<unit, string, unit> = this.HopOf()
    member this.Src(value: int) : Flow<unit, string, int> = this.Hop |> Flow.map (fun () -> value)
    member this.SrcFail: Flow<unit, string, int> = this.Hop |> Flow.bind (fun () -> Flow.fail "typed")

    member this.BadOf<'env, 'e, 'v>() : Flow<'env, 'e, 'v> =
        match mode with
        | Sync -> Flow.delay (fun () -> raise boom)
        | AfterAwait -> this.HopOf<'env, 'e>() |> Flow.bind (fun () -> raise boom)

    member this.Bad: Flow<unit, string, int> = this.BadOf()
    member this.Later(flow: Flow<'env, 'e, 'v>) : Flow<'env, 'e, 'v> = this.HopOf<'env, 'e>() |> Flow.bind (fun () -> flow)

    member _.Compensate() = compensations <- compensations + 1
    member _.Handle() = handlerCalls <- handlerCalls + 1
    member _.Compensations = compensations
    member _.HandlerCalls = handlerCalls
    member _.Acquired = acquired
    member _.Released = released

    member this.FinalizerOf<'env>() : Flow<'env, Never, unit> =
        Flow.delay (fun () ->
            this.Compensate()
            Flow.ok ())

    member _.AcquireOf<'env, 'e>() : Flow<'env, 'e, int> =
        Flow.delay (fun () ->
            acquired <- acquired + 1
            Flow.ok 7)

    member _.ReleaseOf<'env>() : int -> Flow<'env, Never, unit> =
        fun _ ->
            Flow.delay (fun () ->
                released <- released + 1
                Flow.ok ())

    member this.CountedDisposable() : IDisposable =
        acquired <- acquired + 1
        { new IDisposable with member _.Dispose() = released <- released + 1 }

    member _.SiblingStarted = siblingStarted

    member _.AsSibling(flow: Flow<'env, 'e, 'v>) : Flow<'env, 'e, 'v> =
        Flow.delay (fun () ->
            siblingStarted <- true
            flow)
        |> Flow.ensuring (
            Flow.delay (fun () ->
                Deferred.succeedNow () signal |> ignore
                Flow.ok ())
        )

    member this.Waiter: Flow<unit, string, int> = this.AsSibling Flow.never

    member this.SrcStream: FlowStream<unit, string, int> =
        FlowStream.unfoldFlow (fun state -> if state > 3 then Flow.ok None else this.Hop |> Flow.map (fun () -> Some(state, state + 1))) 1

    member this.BadStream: FlowStream<unit, string, int> = FlowStream.fromFlow this.Bad

type private Cell =
    { Name: string
      Expect: Expect
      Compensations: int option
      Resources: bool
      Sibling: bool
      HandlerCalls: int option
      Build: Ctx -> Flow<unit, string, int> }

let private cell name expect build =
    { Name = name
      Expect = expect
      Compensations = None
      Resources = false
      Sibling = false
      HandlerCalls = None
      Build = build }

let private die name build = cell name ExpectDie build
let private comp count (c: Cell) = { c with Compensations = Some count }
let private res (c: Cell) = { c with Resources = true }
let private sib (c: Cell) = { c with Sibling = true }
let private calls count (c: Cell) = { c with HandlerCalls = Some count }
let private ms (value: float) = TimeSpan.FromMilliseconds value
let private count (stream: FlowStream<unit, string, 'v>) : Flow<unit, string, int> = stream |> FlowStream.runCollect |> Flow.map List.length
let private pairFirst : Flow<int * int, string, int> = Flow.envWith fst

let private cells () : Cell list =
    [ die "Flow.map/fn" (fun c -> c.Src 1 |> Flow.map (c.Fn<int, int>()))
      die "Flow.bind/fn" (fun c -> c.Src 1 |> Flow.bind (c.Fn<int, Flow<unit, string, int>>()))
      die "Flow.bind/flow" (fun c -> c.Src 1 |> Flow.bind (fun _ -> c.Bad))
      cell "Flow.catch/source" (ExpectFail "caught") (fun c ->
          c.Bad
          |> Flow.catch (fun error ->
              c.Handle()
              if c.IsBoom error then "caught" else "another exception"))
      |> calls 1
      cell "Flow.foldCause/source" (ExpectValue 42) (fun c ->
          c.Bad
          |> Flow.foldCause
              (fun _ -> Flow.ok 0)
              (fun cause ->
                  c.Handle()
                  Flow.ok (if Cause.defects cause |> List.exists c.IsBoom then 42 else -1)))
      |> calls 1
      die "Flow.foldCause/onFailure" (fun c ->
          c.SrcFail
          |> Flow.foldCause Flow.ok (fun _ ->
              c.Handle()
              c.Throw<Flow<unit, string, int>>()))
      |> calls 1
      die "Flow.mapBoth/source" (fun c ->
          c.Bad
          |> Flow.mapBoth id (fun cause ->
              c.Handle()
              cause))
      |> calls 1
      die "Flow.fold/onSuccess" (fun c -> c.Src 1 |> Flow.fold (c.Fn<int, Flow<unit, string, int>>()) (fun _ -> Flow.ok 0))
      die "Flow.recover/source" (fun c -> c.Bad |> Flow.recover (fun _ -> Flow.ok 0))
      die "Flow.orElseWith/fn" (fun c -> c.SrcFail |> Flow.orElseWith (c.Fn<string, Flow<unit, string, int>>()))
      die "Flow.tap/fn" (fun c -> c.Src 1 |> Flow.tap (c.Fn<int, Flow<unit, string, unit>>()))
      die "Flow.tapError/fn" (fun c -> c.SrcFail |> Flow.tapError (c.Fn<string, Flow<unit, string, unit>>()))
      die "Flow.ensuring/source" (fun c -> c.Bad |> Flow.ensuring (c.FinalizerOf())) |> comp 1
      die "Flow.ensuring/finalizer" (fun c -> c.Src 1 |> Flow.ensuring (c.BadOf()))
      die "Flow.onExit/source" (fun c ->
          c.Bad
          |> Flow.onExit (fun _ ->
              c.Handle()
              Flow.ok ()))
      |> calls 1
      die "Flow.onExit/handler" (fun c -> c.Src 1 |> Flow.onExit (fun _ -> c.Throw<Flow<unit, Never, unit>>()))
      die "Flow.retry/flow" (fun c -> c.Bad |> Flow.retry (Schedule.recurs 2))
      die "Flow.retry/schedule" (fun c -> c.SrcFail |> Flow.retry (Schedule.recurs 3 |> Schedule.whileInput (c.Fn<string, bool>())))
      die "Flow.repeat/flow" (fun c -> c.Bad |> Flow.repeat (Schedule.recurs 2))
      die "Flow.supervise/flow" (fun c -> c.Bad |> Flow.supervise (Schedule.recurs 1))
      die "Flow.timeout/flow" (fun c -> c.Bad |> Flow.timeout (TimeSpan.FromSeconds 30.0) "late")
      die "Flow.timeoutWith/fallback" (fun c ->
          (Flow.never: Flow<unit, string, int>) |> Flow.timeoutWith (ms 10.0) (c.Fn<unit, Flow<unit, string, int>>()))
      die "Flow.onInterrupt/handler" (fun c ->
          (Flow.never: Flow<unit, string, int>)
          |> Flow.onInterrupt (
              Flow.delay (fun () ->
                  c.Compensate()
                  c.BadOf())
          )
          |> Flow.timeoutToOk (ms 20.0) 0)
      |> comp 1
      die "Flow.race/left" (fun c -> Flow.race c.Bad c.Waiter) |> sib
      die "Flow.zipPar/left" (fun c -> Flow.zipPar c.Bad c.Waiter |> Flow.map fst) |> sib
      die "Flow.zipPar/right" (fun c -> Flow.zipPar c.Waiter c.Bad |> Flow.map fst) |> sib
      die "Flow.sequencePar/item" (fun c -> Flow.sequencePar [ c.Waiter; c.Bad ] |> Flow.map List.length) |> sib
      die "Flow.traversePar/flow" (fun c ->
          [ 1; 2 ] |> Flow.traversePar (Parallelism.bounded 2) (fun item -> if item = 2 then c.Bad else c.Waiter) |> Flow.map List.length)
      |> sib
      die "Flow.fork/flow" (fun c ->
          flow {
              let! fiber = Flow.fork c.Bad
              let! _ = Flow.forkDetached (Fiber.awaitExit fiber |> c.AsSibling)
              return! Fiber.join fiber
          })
      |> sib
      die "Flow.memoize/flow" (fun c ->
          flow {
              let! memo = Flow.memoize c.Bad
              let! _ = Flow.forkDetached (memo |> c.AsSibling)
              return! memo
          })
      |> sib
      die "Flow.scoped/flow" (fun c ->
          Flow.scoped (
              flow {
                  do! Flow.scopeFinalizer (c.FinalizerOf())
                  return! c.Bad
              }
          ))
      |> comp 1
      die "Flow.scopeFinalizer/finalizer" (fun c ->
          Flow.scoped (
              flow {
                  do! Flow.scopeFinalizer (c.FinalizerOf())
                  do! Flow.scopeFinalizer (c.BadOf())
                  return! c.Src 1
              }
          ))
      |> comp 1
      die "Flow.scopeAcquireRelease/use" (fun c ->
          Flow.scoped (Flow.scopeAcquireRelease (c.AcquireOf()) (c.ReleaseOf()) |> Flow.bind (fun _ -> c.Bad)))
      |> res
      die "Cache.make/lookupFlow" (fun c ->
          flow {
              let! cache = Cache.make (fun (_: int) -> c.Bad)
              let! _ = Flow.forkDetached (Cache.get 1 cache |> c.AsSibling)
              return! Cache.get 1 cache
          })
      |> sib
      die "Layer.acquireRelease/use" (fun c ->
          Layer.provide (Layer.acquireRelease (Layer.fromFlow (c.AcquireOf())) (c.ReleaseOf())) (c.BadOf<int, string, int>()))
      |> res
      die "Layer.provide/flow" (fun c -> Layer.provide (Layer.succeed 1) (c.BadOf<int, string, int>()))
      die "Layer.zipPar/left" (fun c -> Layer.provide (Layer.zipPar (Layer.fromFlow c.Bad) (Layer.fromFlow c.Waiter)) pairFirst) |> sib
      die "FlowStream.map/fn" (fun c -> c.SrcStream |> FlowStream.map (c.Fn<int, int>()) |> count)
      die "FlowStream.mapFlow/flow" (fun c -> c.SrcStream |> FlowStream.mapFlow (fun _ -> c.Bad) |> count)
      die "FlowStream.filter/fn" (fun c -> c.SrcStream |> FlowStream.filter (c.Fn<int, bool>()) |> count)
      die "FlowStream.scan/fn" (fun c -> c.SrcStream |> FlowStream.scan (fun _ _ -> c.Throw<int>()) 0 |> count)
      die "FlowStream.debounce/upstream" (fun c -> c.BadStream |> FlowStream.debounce (ms 5.0) |> count)
      die "FlowStream.throttle/upstream" (fun c -> c.BadStream |> FlowStream.throttle (ms 5.0) |> count)
      die "FlowStream.groupedWithin/upstream" (fun c -> c.BadStream |> FlowStream.groupedWithin 2 (ms 5.0) |> count)
      die "FlowStream.switchMapFlow/flow" (fun c -> c.SrcStream |> FlowStream.switchMapFlow (fun _ -> c.Bad) |> count)
      die "FlowStream.buffer/upstream" (fun c -> c.BadStream |> FlowStream.buffer (QueueStrategy.BackPressure 2) |> count)
      die "FlowStream.mergePar/stream" (fun c -> FlowStream.mergePar [ FlowStream.fromFlow c.Waiter; c.BadStream ] |> count) |> sib
      die "FlowStream.zip/left" (fun c -> FlowStream.zip c.BadStream c.SrcStream |> count)
      die "FlowStream.runForEachFlow/flow" (fun c -> c.SrcStream |> FlowStream.runForEachFlow (fun _ -> c.BadOf()) |> Flow.map (fun () -> 0))
      die "Queue.offer/consumer" (fun c ->
          Flow.scoped (
              flow {
                  let! queue = Queue.makeScoped (QueueStrategy.BackPressure 1)

                  let producer =
                      flow {
                          for item in 1..50 do
                              let! _ = Queue.offer item queue
                              ()
                      }

                  let! _ = Flow.forkDetached (producer |> c.AsSibling)
                  do! FlowStream.fromDequeue queue |> FlowStream.runForEachFlow (fun _ -> c.BadOf())
                  return 0
              }
          ))
      |> sib
      die "Builder.let!/continuation" (fun c ->
          flow {
              let! _ = c.Src 1
              return c.Throw<int>()
          })
      die "Builder.use/body" (fun c ->
          flow {
              use _ = c.CountedDisposable()
              return! c.Bad
          })
      |> res
      die "Builder.try-finally/body" (fun c ->
          flow {
              try
                  return! c.Bad
              finally
                  c.Compensate()
          })
      |> comp 1
      cell "Builder.try-with/body" (ExpectValue 42) (fun c ->
          flow {
              try
                  return! c.Bad
              with error ->
                  c.Handle()
                  return (if c.IsBoom error then 42 else -1)
          })
      |> calls 1
      die "Builder.for/body" (fun c ->
          flow {
              for item in [ 1; 2 ] do
                  do! (if item = 2 then c.BadOf() else c.Hop)

              return 1
          })
      die "Builder.while/body" (fun c ->
          flow {
              let turns = ref 0

              while turns.Value < 2 do
                  turns.Value <- turns.Value + 1
                  do! (if turns.Value = 2 then c.BadOf() else c.Hop)

              return 1
          }) ]

/// Cells that fail on JavaScript at the current HEAD. A cell in this list passes while it still fails and fails once
/// it holds, so fixing a gap forces removing it here; a cell not in it must hold the invariant.
let private knownGaps () : Map<string, string> =
    Map.empty

[<Literal>]
let private boundMilliseconds = 5000

[<Emit("setTimeout($0, $1)")>]
let private setTimer (_callback: unit -> unit) (_milliseconds: int) : obj = jsNative

[<Emit("clearTimeout($0)")>]
let private clearTimer (_timer: obj) : unit = jsNative

type private Bounded<'value, 'error> =
    | Exited of Exit<'value, 'error>
    | Escaped of exn
    | TimedOut

/// Runs a flow to its exit within the bound. The bound is a plain JavaScript timer, independent of the runtime under
/// test (a Flow timeout waits for the interrupted flow), so a hang, even one that ignores interruption, cannot stall
/// the run; the abandoned run is cancelled and never awaited.
let private bounded (work: Flow<unit, 'error, 'value>) : Flow<unit, Never, Bounded<'value, 'error>> =
    Flow.fromAsync (
        Async.FromContinuations(fun (resolve, _, _) ->
            let cancellation = new CancellationTokenSource()
            let mutable settled = false

            let settle outcome =
                if not settled then
                    settled <- true
                    resolve outcome

            let timer =
                setTimer
                    (fun () ->
                        settle TimedOut
                        cancellation.Cancel())
                    boundMilliseconds

            Async.StartWithContinuations(
                Flow.toAsync () work,
                (fun exit ->
                    clearTimer timer
                    settle (Exited exit)),
                (fun error ->
                    clearTimer timer
                    settle (Escaped error)),
                (fun _ -> ()),
                cancellation.Token
            ))
    )

let private describe (c: Ctx) (exit: Exit<int, string>) =
    let rec go (cause: Cause<string>) : string =
        match cause with
        | Cause.Fail error -> $"Fail {error}"
        | Cause.Die error when c.IsBoom error -> "Die boom"
        | Cause.Die error -> $"Die ({error.Message})"
        | Cause.Interrupt -> "Interrupt"
        | Cause.Then(left, right) -> $"Then({go left}, {go right})"
        | Cause.Both(left, right) -> $"Both({go left}, {go right})"
        | Cause.Traced(inner, _) -> go inner

    match exit with
    | Exit.Success value -> $"Success {value}"
    | Exit.Failure cause -> go cause

let private outcomeFailures (cell: Cell) (c: Ctx) (exit: Exit<int, string>) : string list =
    let held =
        match cell.Expect, exit with
        | ExpectDie, Exit.Failure cause ->
            let defects = Cause.defects cause
            List.isEmpty (Cause.failures cause) && defects.Length = 1 && c.IsBoom defects.Head
        | ExpectFail expected, Exit.Failure cause -> Cause.failures cause = [ expected ] && List.isEmpty (Cause.defects cause)
        | ExpectValue expected, Exit.Success actual -> actual = expected
        | _ -> false

    let expected =
        match cell.Expect with
        | ExpectDie -> "Die boom"
        | ExpectFail error -> $"Fail {error}"
        | ExpectValue value -> $"Success {value}"

    if held then [] else [ $"expected {expected}, got {describe c exit}" ]

let private counterFailures (cell: Cell) (c: Ctx) : string list =
    [ match cell.Compensations with
      | Some expected when c.Compensations <> expected -> $"compensation ran {c.Compensations} times, expected {expected}"
      | _ -> ()
      match cell.HandlerCalls with
      | Some expected when c.HandlerCalls <> expected -> $"handler ran {c.HandlerCalls} times, expected {expected}"
      | _ -> ()
      if cell.Resources && (c.Acquired = 0 || c.Released <> c.Acquired) then
          $"acquired {c.Acquired} resources, released {c.Released}" ]

/// Runs one cell in one mode. The run is disconnected and bounded, so a hang is a failure naming the cell rather than
/// a stalled run.
let private evaluate (cell: Cell) (mode: Mode) : Flow<unit, Never, string list> =
    flow {
        let! signal = Deferred.make<unit, Never, unit> ()
        let c = Ctx(cell.Name, mode, signal)

        let! outcome = bounded (Flow.delay (fun () -> cell.Build c))

        match outcome with
        | TimedOut -> return [ "hang: no exit within 5 s" ]
        | Escaped error -> return [ $"the exception escaped the flow as a raw exception ({error.Message})" ]
        | Exited exit ->
            let! released =
                if cell.Sibling then
                    bounded (Deferred.await signal : Flow<unit, Never, unit>)
                    |> Flow.map (function
                        | Exited _ -> true
                        | _ -> false)
                else
                    Flow.ok true

            let siblingFailures =
                if released || not c.SiblingStarted then [] else [ "sibling not released within 5 s" ]

            return outcomeFailures cell c exit @ counterFailures cell c @ siblingFailures
    }

let tests: Test list =
    [ for cell in cells () do
          for mode in [ Sync; AfterAwait ] do
              let modeName =
                  match mode with
                  | Sync -> "Sync"
                  | AfterAwait -> "AfterAwait"

              let id = $"throw-safety {cell.Name} [{modeName}]"

              test id (
                  flow {
                      let! failures = evaluate cell mode
                      let reasons = String.concat "; " failures

                      return
                          [ match knownGaps().TryFind id, failures with
                            | None, [] -> isTrue "holds the invariant" true
                            | None, _ -> { Label = "holds the invariant"; Passed = false; Detail = reasons }
                            | Some _, _ :: _ -> isTrue "known gap still fails" true
                            | Some gap, [] ->
                                { Label = "known gap still fails"
                                  Passed = false
                                  Detail = $"now holds (recorded gap: {gap}); remove it from knownGaps" } ]
                  }
              ) ]
