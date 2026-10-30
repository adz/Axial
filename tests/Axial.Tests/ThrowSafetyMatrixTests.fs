namespace Axial.Tests

open System
open System.Collections.Generic
open System.Reflection
open System.Threading
open System.Threading.Tasks
open Axial
open Xunit

// T30: the throw-safety matrix. The library invariant: an exception thrown by user code inside a flow is observed
// as Cause.Die by every combinator. It never hangs, never skips compensation, never completes twice and never leaks
// a resource. Every public combinator that runs user code (a function, a flow, a task, an async, a sequence, a
// disposable, a schedule step) has cells here, each run in two modes:
//   Sync        the user code throws as soon as it is called (a Flow.delay factory, a mapper, a task factory);
//   AfterAwait  the user code throws on a continuation after a real suspension, so the combinator observes a faulted
//               execution rather than a thrown one (`flow { do! hop; return raise boom }`, a faulting Task/Async).
// Each cell checks: the run ends within 5 s (a hang is a named failure, never a stalled suite); the outcome is
// Cause.Die carrying the original exception exactly once (or the documented recovery value); compensation ran exactly
// once; every acquired resource was released exactly once; a sibling fiber waiting on the shared signal was released.

/// The exception every cell throws from user code; one instance per cell run, compared by reference.
type ThrowSafetyBoom(cell: string) =
    inherit Exception($"boom from {cell}")

type ThrowMode =
    | Sync
    | AfterAwait

/// Supplies a cell's throwing user code for its mode, plus the counters and the sibling signal it checks.
type MatrixCtx(cell: string, mode: ThrowMode) =
    let boom = ThrowSafetyBoom cell :> exn
    // 0 compensations, 1 acquisitions, 2 releases, 3 handler calls.
    let counts = Array.zeroCreate<int> 4
    let sibling = TaskCompletionSource<unit>(TaskCreationOptions.RunContinuationsAsynchronously)
    let mutable siblingStarted = 0
    let bump index = Interlocked.Increment(&counts[index]) |> ignore
    let read index = Volatile.Read(&counts[index])

    member _.Mode = mode
    member _.Boom = boom
    member _.IsBoom(error: exn) = obj.ReferenceEquals(error, boom)
    member _.Throw<'a>() : 'a = raise boom
    member _.Fn<'a, 'b>() : 'a -> 'b = fun _ -> raise boom

    /// A real suspension in AfterAwait mode, nothing in Sync mode.
    member _.HopOf<'env, 'e>() : Flow<'env, 'e, unit> =
        match mode with
        | Sync -> Flow.ok ()
        | AfterAwait -> Flow.fromTask (fun _ -> task { do! Task.Yield() })

    member this.Hop: Flow<unit, string, unit> = this.HopOf()
    member this.SrcOf<'env, 'e, 'v>(value: 'v) : Flow<'env, 'e, 'v> = this.HopOf<'env, 'e>() |> Flow.map (fun () -> value)
    member this.Src(value: int) : Flow<unit, string, int> = this.SrcOf value

    /// A typed failure, reached after the hop.
    member this.SrcFailOf<'env, 'v>() : Flow<'env, string, 'v> = this.HopOf<'env, string>() |> Flow.bind (fun () -> Flow.fail "typed")
    member this.SrcFail: Flow<unit, string, int> = this.SrcFailOf()

    /// A user flow that throws: from its Flow.delay factory (Sync), or from a bind continuation after the hop (AfterAwait).
    member this.BadOf<'env, 'e, 'v>() : Flow<'env, 'e, 'v> =
        match mode with
        | Sync -> Flow.delay (fun () -> raise boom)
        | AfterAwait -> this.HopOf<'env, 'e>() |> Flow.bind (fun () -> raise boom)

    member this.Bad: Flow<unit, string, int> = this.BadOf()

    /// Runs a whole combinator after the hop, for user callbacks that cannot await themselves (a mapper, a Dispose).
    member this.Later(flow: Flow<'env, 'e, 'v>) : Flow<'env, 'e, 'v> = this.HopOf<'env, 'e>() |> Flow.bind (fun () -> flow)

    member _.BadTask<'v>() : CancellationToken -> Task<'v> =
        match mode with
        | Sync -> fun _ -> raise boom
        | AfterAwait ->
            fun _ ->
                task {
                    do! Task.Yield()
                    return (raise boom: 'v)
                }

    member this.BadValueTask<'v>() : CancellationToken -> ValueTask<'v> =
        match mode with
        | Sync -> fun _ -> raise boom
        | AfterAwait -> fun token -> ValueTask<'v>(this.BadTask<'v>() token)

    /// A task already started by the user: faulted from the start (Sync) or faulting after a yield (AfterAwait).
    member _.StartedBadTask<'v>() : Task<'v> =
        match mode with
        | Sync -> Task.FromException<'v> boom
        | AfterAwait ->
            task {
                do! Task.Yield()
                return (raise boom: 'v)
            }

    member _.BadAsync<'v>() : Async<'v> =
        match mode with
        | Sync -> async { return (raise boom: 'v) }
        | AfterAwait ->
            async {
                do! Async.SwitchToThreadPool()
                return (raise boom: 'v)
            }

    /// Throws on the first MoveNext (Sync), or on the second, after the consumer suspended (AfterAwait).
    member _.BadSeq: seq<int> =
        match mode with
        | Sync ->
            seq {
                raise boom
                yield 1
            }
        | AfterAwait ->
            seq {
                yield 1
                raise boom
            }

    member _.BadDisposable: IDisposable = { new IDisposable with member _.Dispose() = raise boom }

    member _.BadAsyncDisposable: IAsyncDisposable =
        { new IAsyncDisposable with
            member _.DisposeAsync() =
                match mode with
                | Sync -> raise boom
                | AfterAwait ->
                    ValueTask(
                        task {
                            do! Task.Yield()
                            return (raise boom: unit)
                        }
                        :> Task
                    ) }

    member _.BadAsyncEnumerable: IAsyncEnumerable<int> =
        { new IAsyncEnumerable<int> with
            member _.GetAsyncEnumerator(_) =
                let mutable yielded = false

                { new IAsyncEnumerator<int> with
                    member _.Current = 1
                    member _.MoveNextAsync() =
                        match mode with
                        | Sync -> raise boom
                        | AfterAwait ->
                            if not yielded then
                                yielded <- true
                                ValueTask<bool>(true)
                            else
                                ValueTask<bool>(
                                    task {
                                        do! Task.Yield()
                                        return (raise boom: bool)
                                    }
                                )
                    member _.DisposeAsync() = ValueTask.CompletedTask } }

    member _.Compensate() = bump 0
    member _.Acquire() = bump 1
    member _.Release() = bump 2
    member _.Handle() = bump 3
    member _.Compensations = read 0
    member _.Acquired = read 1
    member _.Released = read 2
    member _.HandlerCalls = read 3

    member this.FinalizerOf<'env>() : Flow<'env, Never, unit> =
        Flow.delay (fun () ->
            this.Compensate()
            Flow.ok ())

    member this.Finalizer: Flow<unit, Never, unit> = this.FinalizerOf()

    member this.AcquireOf<'env, 'e>() : Flow<'env, 'e, int> =
        Flow.delay (fun () ->
            this.Acquire()
            Flow.ok 7)

    member this.ReleaseOf<'env>() : int -> Flow<'env, Never, unit> =
        fun _ ->
            Flow.delay (fun () ->
                this.Release()
                Flow.ok ())

    /// A release that is counted when called, then throws.
    member this.BadReleaseOf<'env>() : int -> Flow<'env, Never, unit> =
        fun _ ->
            Flow.delay (fun () ->
                this.Release()
                this.BadOf())

    member this.Resource: Resource<unit, string, int> = Resource.create (this.AcquireOf()) (this.ReleaseOf())
    member this.BadAcquireResource: Resource<unit, string, int> = Resource.create (this.BadOf()) (this.ReleaseOf())
    member this.BadReleaseResource: Resource<unit, string, int> = Resource.create (this.AcquireOf()) (this.BadReleaseOf())

    /// A disposable counted as acquired when created and released when disposed.
    member this.CountedDisposable() : IDisposable =
        this.Acquire()
        { new IDisposable with member _.Dispose() = this.Release() }

    /// Marks the sibling as released.
    member _.SignalOf<'env>() : Flow<'env, Never, unit> =
        Flow.delay (fun () ->
            sibling.TrySetResult() |> ignore
            Flow.ok ())

    member this.Signal: Flow<unit, Never, unit> = this.SignalOf()

    /// Runs a flow as the cell's sibling: marks it started when it runs, and released when it ends in any way.
    /// A sibling interrupted before it ever ran has nothing to release, so only a started sibling is checked.
    member this.AsSibling(flow: Flow<'env, 'e, 'v>) : Flow<'env, 'e, 'v> =
        Flow.delay (fun () ->
            Volatile.Write(&siblingStarted, 1)
            flow)
        |> Flow.ensuring (this.SignalOf())

    /// A sibling that waits forever and signals when it is released (interrupted).
    member this.WaiterOf<'env, 'e, 'v>() : Flow<'env, 'e, 'v> = this.AsSibling Flow.never
    member this.Waiter: Flow<unit, string, int> = this.WaiterOf()
    member _.SiblingReleased: Task = sibling.Task :> Task
    member _.SiblingStarted = Volatile.Read(&siblingStarted) = 1

    member this.SrcStream: FlowStream<unit, string, int> =
        match mode with
        | Sync -> FlowStream.fromSeq [ 1; 2; 3 ]
        | AfterAwait ->
            FlowStream.unfoldFlow
                (fun state -> if state > 3 then Flow.ok None else this.Hop |> Flow.map (fun () -> Some(state, state + 1)))
                1

    member this.BadStreamOf<'env, 'e, 'v>() : FlowStream<'env, 'e, 'v> = FlowStream.fromFlow (this.BadOf())
    member this.BadStream: FlowStream<unit, string, int> = this.BadStreamOf()

type Expect =
    | ExpectDie
    | ExpectFailBoom
    | ExpectFail of obj
    | ExpectValue of obj

type MatrixCell =
    { Name: string
      Expect: Expect
      Compensations: int option
      Resources: bool
      Sibling: bool
      HandlerCalls: int option
      Run: MatrixCtx -> CancellationToken -> Task<Exit<obj, obj>> }

/// Public members exempt from cells: they run no user code, or another member's cells cover them.
type Exemption =
    | NoUserCode
    | CoveredBy of string

module ThrowSafetyMatrix =
    let private runFlow (build: MatrixCtx -> Flow<unit, 'e, 'v>) : MatrixCtx -> CancellationToken -> Task<Exit<obj, obj>> =
        fun ctx token ->
            let flow = build ctx

            task {
                let! exit = flow.StartAsTask((), token)
                return Exit.mapBoth box (Cause.map box) exit
            }

    let private cell name expect build =
        { Name = name
          Expect = expect
          Compensations = None
          Resources = false
          Sibling = false
          HandlerCalls = None
          Run = runFlow build }

    let private die name (build: MatrixCtx -> Flow<unit, 'e, 'v>) = cell name ExpectDie build
    let private failBoom name (build: MatrixCtx -> Flow<unit, exn, 'v>) = cell name ExpectFailBoom build
    let private fails name (error: 'e) (build: MatrixCtx -> Flow<unit, 'e, 'v>) = cell name (ExpectFail(box error)) build
    let private value name (expected: 'v) (build: MatrixCtx -> Flow<unit, 'e, 'v>) = cell name (ExpectValue(box expected)) build
    let private comp count (c: MatrixCell) = { c with Compensations = Some count }
    let private res (c: MatrixCell) = { c with Resources = true }
    let private sib (c: MatrixCell) = { c with Sibling = true }
    let private calls count (c: MatrixCell) = { c with HandlerCalls = Some count }

    let private ms (value: float) = TimeSpan.FromMilliseconds value
    let private long () = TimeSpan.FromSeconds 30.0
    let private hasBoom (c: MatrixCtx) (cause: Cause<'e>) = Cause.defects cause |> List.exists c.IsBoom

    let private clock () : IClock =
        { new IClock with
            member _.UtcNow() = DateTimeOffset.UtcNow
            member _.Elapsed() = TimeSpan.Zero
            member _.Sleep(delay, token) = Task.Delay(delay, token) }

    let private guid () : IGuid = { new IGuid with member _.NewGuid() = Guid.NewGuid() }

    let private random () : IRandom =
        { new IRandom with
            member _.Next() = 4
            member _.NextMax _ = 0
            member _.NextInt low _ = low
            member _.NextDouble() = 0.5
            member _.NextBytes _ = () }

    let private telemetry () : ITelemetry =
        { new ITelemetry with
            member _.Annotate(_, _) = ()
            member _.Attribute _ = () }

    // Starts a user flow through a fork variant, joins it, and has a detached sibling await the same fiber.
    let private forkCell name (start: MatrixCtx -> Flow<unit, string, int> -> Flow<unit, string, Fiber<string, int>>) =
        die name (fun c ->
            flow {
                let! fiber = start c c.Bad
                let! _ = Flow.forkDetached (Fiber.awaitExit fiber |> c.AsSibling)
                return! Fiber.join fiber
            })
        |> sib

    let private provideUnit (layer: Layer<unit, string, int>) : Flow<unit, string, int> = Layer.provide layer Flow.env<int, string>

    let private srcLayer (c: MatrixCtx) (value: int) : Layer<unit, string, int> = Layer.fromFlow (c.Src value)
    let private badLayer (c: MatrixCtx) : Layer<unit, string, int> = Layer.fromFlow c.Bad

    // A schedule step that throws, used through Flow.repeat over a succeeding source.
    let private repeatWith (c: MatrixCtx) (schedule: Schedule<unit, int, 'o>) : Flow<unit, string, int> =
        c.Src 1 |> Flow.repeat schedule

    let private flowCells () : MatrixCell list =
        [ die "Flow.addAnnotationSink/flow" (fun c -> c.Bad |> Flow.addAnnotationSink (fun _ _ -> ()))
          // Documented exception: "Sinks are guarded: one that throws cannot fail the workflow."
          value "Flow.addAnnotationSink/sink" 1 (fun c ->
              c.Later(Flow.ok 1 |> Flow.annotate "k" "v") |> Flow.addAnnotationSink (fun _ _ -> c.Throw()))
          die "Flow.addFiberObserver/flow" (fun c -> c.Bad |> Flow.addFiberObserver FiberObserver.none)
          die "Flow.annotate/flow" (fun c -> c.Bad |> Flow.annotate "k" "v")
          die "Flow.apply/fn" (fun c -> Flow.apply (c.SrcOf(fun (_: int) -> c.Throw<int>())) (c.Src 1))
          die "Flow.apply/flow" (fun c -> Flow.apply (c.BadOf<unit, string, int -> int>()) (c.Src 1))
          failBoom "Flow.attemptAsync/async" (fun c -> Flow.attemptAsync (c.BadAsync<int>()))
          failBoom "Flow.attemptBlocking/fn" (fun c -> c.Later(Flow.attemptBlocking (fun _ -> c.Throw<int>())))
          failBoom "Flow.attemptStartedTask/task" (fun c -> Flow.delay (fun () -> Flow.attemptStartedTask (c.StartedBadTask<int>())))
          failBoom "Flow.attemptStartedValueTask/task" (fun c ->
              Flow.delay (fun () -> Flow.attemptStartedValueTask (ValueTask<int>(c.StartedBadTask<int>()))))
          failBoom "Flow.attemptTask/factory" (fun c -> Flow.attemptTask (c.BadTask<int>()))
          failBoom "Flow.attemptValueTask/factory" (fun c -> Flow.attemptValueTask (c.BadValueTask<int>()))
          die "Flow.awaitStartedTask/task" (fun c -> Flow.delay (fun () -> Flow.awaitStartedTask (c.StartedBadTask<int>())) : Flow<unit, string, int>)
          die "Flow.awaitStartedTaskResult/task" (fun c ->
              Flow.delay (fun () -> Flow.awaitStartedTaskResult (c.StartedBadTask<Result<int, string>>())))
          die "Flow.awaitStartedValueTask/task" (fun c ->
              Flow.delay (fun () -> Flow.awaitStartedValueTask (ValueTask<int>(c.StartedBadTask<int>()))) : Flow<unit, string, int>)
          die "Flow.awaitStartedValueTaskResult/task" (fun c ->
              Flow.delay (fun () ->
                  Flow.awaitStartedValueTaskResult (ValueTask<Result<int, string>>(c.StartedBadTask<Result<int, string>>()))))
          die "Flow.bind/fn" (fun c -> c.Src 1 |> Flow.bind (c.Fn<int, Flow<unit, string, int>>()))
          die "Flow.bind/flow" (fun c -> c.Src 1 |> Flow.bind (fun _ -> c.Bad))
          die "Flow.bind/source" (fun c -> c.Bad |> Flow.bind Flow.ok)
          fails "Flow.catch/source" "caught" (fun c ->
              c.Bad
              |> Flow.catch (fun error ->
                  c.Handle()
                  if c.IsBoom error then "caught" else "another exception"))
          |> calls 1
          die "Flow.catch/handler" (fun c ->
              c.Bad
              |> Flow.catch (fun _ ->
                  c.Handle()
                  c.Throw<string>()))
          |> calls 1
          die "Flow.catchCancellation/source" (fun c ->
              c.Bad
              |> Flow.catchCancellation (fun _ ->
                  c.Handle()
                  "cancelled"))
          |> calls 0
          die "Flow.delay/factory" (fun c -> c.Later(Flow.delay (fun () -> c.Throw<Flow<unit, string, int>>())))
          die "Flow.delay/flow" (fun c -> Flow.delay (fun () -> c.Bad))
          die "Flow.disconnect/flow" (fun c -> c.Bad |> Flow.disconnect)
          die "Flow.ensuring/source" (fun c -> c.Bad |> Flow.ensuring c.Finalizer) |> comp 1
          die "Flow.ensuring/finalizer" (fun c -> c.Src 1 |> Flow.ensuring (c.BadOf()))
          die "Flow.envWith/fn" (fun c -> c.Later(Flow.envWith (fun () -> c.Throw<int>())) : Flow<unit, string, int>)
          die "Flow.fold/source" (fun c ->
              c.Bad
              |> Flow.fold
                  (fun value ->
                      c.Handle()
                      Flow.ok value)
                  (fun _ ->
                      c.Handle()
                      Flow.ok 0)
              : Flow<unit, string, int>)
          |> calls 0
          die "Flow.fold/onSuccess" (fun c -> c.Src 1 |> Flow.fold (c.Fn<int, Flow<unit, string, int>>()) (fun _ -> Flow.ok 0))
          die "Flow.fold/onError" (fun c -> c.SrcFail |> Flow.fold Flow.ok (c.Fn<string, Flow<unit, string, int>>()))
          value "Flow.foldCause/source" 42 (fun c ->
              c.Bad
              |> Flow.foldCause
                  (fun _ -> Flow.ok 0)
                  (fun cause ->
                      c.Handle()
                      Flow.ok (if hasBoom c cause then 42 else -1))
              : Flow<unit, string, int>)
          |> calls 1
          die "Flow.foldCause/onSuccess" (fun c ->
              c.Src 1
              |> Flow.foldCause
                  (c.Fn<int, Flow<unit, string, int>>())
                  (fun _ ->
                      c.Handle()
                      Flow.ok 0))
          |> calls 0
          die "Flow.foldCause/onFailure" (fun c ->
              c.SrcFail
              |> Flow.foldCause Flow.ok (fun _ ->
                  c.Handle()
                  c.Throw<Flow<unit, string, int>>()))
          |> calls 1
          die "Flow.forEachPar/fn" (fun c ->
              c.Later(
                  [ 2; 1 ]
                  |> Flow.forEachPar (Parallelism.bounded 2) (fun item ->
                      if item = 1 then c.Throw() else c.WaiterOf<unit, string, unit>())
              ))
          |> sib
          die "Flow.forEachPar/flow" (fun c ->
              [ 2; 1 ]
              |> Flow.forEachPar (Parallelism.bounded 2) (fun item ->
                  if item = 1 then c.BadOf<unit, string, unit>() else c.WaiterOf()))
          |> sib
          die "Flow.forEachParUsing/fn" (fun c ->
              c.Later(
                  [ 2; 1 ]
                  |> Flow.forEachParUsing (Parallelism.bounded 2) c.Resource (fun _ item ->
                      if item = 1 then c.Throw() else c.WaiterOf<unit, string, unit>())
              ))
          |> sib
          |> res
          die "Flow.forEachParUsing/flow" (fun c ->
              [ 2; 1 ]
              |> Flow.forEachParUsing (Parallelism.bounded 2) c.Resource (fun _ item ->
                  if item = 1 then c.BadOf<unit, string, unit>() else c.WaiterOf()))
          |> sib
          |> res
          die "Flow.forEachParUsing/acquire" (fun c ->
              [ 1 ] |> Flow.forEachParUsing (Parallelism.bounded 2) c.BadAcquireResource (fun _ _ -> Flow.ok ()))
          die "Flow.forEachParUsing/release" (fun c ->
              [ 1 ] |> Flow.forEachParUsing (Parallelism.bounded 2) c.BadReleaseResource (fun _ _ -> c.HopOf()))
          |> res
          forkCell "Flow.fork/flow" (fun _ bad -> Flow.fork bad)
          forkCell "Flow.forkDetached/flow" (fun _ bad -> Flow.forkDetached bad)
          forkCell "Flow.forkNamed/flow" (fun _ bad -> Flow.forkNamed "matrix" bad)
          forkCell "Flow.forkGraceful/flow" (fun c bad -> Flow.forkGraceful c.Finalizer (ms 10.0) bad)
          forkCell "Flow.forkReplacing/flow" (fun _ bad -> FiberSlot.make () |> Flow.bind (fun slot -> Flow.forkReplacing slot bad))
          forkCell "Flow.forkReplacingKey/flow" (fun _ bad ->
              FiberSlot.makeKeyed () |> Flow.bind (fun slots -> Flow.forkReplacingKey "key" slots bad))
          die "Flow.forkGraceful/stop" (fun c ->
              Flow.scoped (
                  flow {
                      let! _ = Flow.forkGraceful (c.BadOf()) (ms 10.0) (Flow.never: Flow<unit, string, int>)
                      return 1
                  }
              ))
          die "Flow.forkResource/acquire" (fun c -> Flow.forkResource c.BadAcquireResource |> Flow.bind Fiber.join)
          die "Flow.forkResource/release" (fun c -> Flow.scoped (Flow.forkResource c.BadReleaseResource |> Flow.bind Fiber.join))
          |> res
          die "Flow.fromAsync/async" (fun c -> Flow.fromAsync (c.BadAsync<int>()) : Flow<unit, string, int>)
          die "Flow.fromAsyncResult/async" (fun c -> Flow.fromAsyncResult (c.BadAsync<Result<int, string>>()))
          die "Flow.fromBlocking/fn" (fun c -> c.Later(Flow.fromBlocking (fun _ -> c.Throw<int>())) : Flow<unit, string, int>)
          die "Flow.fromBlockingResult/fn" (fun c -> c.Later(Flow.fromBlockingResult (fun _ -> c.Throw<Result<int, string>>())))
          die "Flow.fromColdTask/task" (fun c -> Flow.fromColdTask (ColdTask.create (c.BadTask<int>())) : Flow<unit, string, int>)
          die "Flow.fromColdTaskResult/task" (fun c -> Flow.fromColdTaskResult (ColdTask.create (c.BadTask<Result<int, string>>())))
          die "Flow.fromTask/factory" (fun c -> Flow.fromTask (c.BadTask<int>()) : Flow<unit, string, int>)
          die "Flow.fromTaskResult/factory" (fun c -> Flow.fromTaskResult (c.BadTask<Result<int, string>>()))
          die "Flow.fromValueTask/factory" (fun c -> Flow.fromValueTask (c.BadValueTask<int>()) : Flow<unit, string, int>)
          die "Flow.fromValueTaskResult/factory" (fun c -> Flow.fromValueTaskResult (c.BadValueTask<Result<int, string>>()))
          die "Flow.ignore/flow" (fun c -> c.Bad |> Flow.ignore)
          die "Flow.localEnv/fn" (fun c -> c.Later(Flow.env<int, string> |> Flow.localEnv (fun () -> c.Throw<int>())))
          die "Flow.localEnv/flow" (fun c -> c.BadOf<int, string, int>() |> Flow.localEnv (fun () -> 5))
          die "Flow.map/fn" (fun c -> c.Src 1 |> Flow.map (c.Fn<int, int>()))
          die "Flow.map/source" (fun c -> c.Bad |> Flow.map id)
          die "Flow.map2/fn" (fun c -> Flow.map2 (fun _ _ -> c.Throw<int>()) (c.Src 1) (c.Src 2))
          die "Flow.map3/fn" (fun c -> Flow.map3 (fun _ _ _ -> c.Throw<int>()) (c.Src 1) (c.Src 2) (c.Src 3))
          die "Flow.mapBoth/source" (fun c ->
              c.Bad
              |> Flow.mapBoth id (fun cause ->
                  c.Handle()
                  cause))
          |> calls 1
          die "Flow.mapBoth/onSuccess" (fun c ->
              c.Src 1
              |> Flow.mapBoth (c.Fn<int, int>()) (fun (cause: Cause<string>) ->
                  c.Handle()
                  cause))
          |> calls 0
          die "Flow.mapBoth/onFailure" (fun c ->
              c.SrcFail
              |> Flow.mapBoth id (fun (_: Cause<string>) ->
                  c.Handle()
                  c.Throw<Cause<string>>()))
          |> calls 1
          die "Flow.mapError/fn" (fun c -> c.SrcFail |> Flow.mapError (c.Fn<string, string>()))
          die "Flow.mapError/source" (fun c -> c.Bad |> Flow.mapError (fun (error: string) -> error.Length))
          die "Flow.mapLogSink/fn" (fun c -> c.Later(c.Src 1 |> Flow.mapLogSink (c.Fn<ILogSink, ILogSink>())))
          die "Flow.mapLogSink/flow" (fun c -> c.Bad |> Flow.mapLogSink id)
          die "Flow.memoize/flow" (fun c ->
              flow {
                  let! memo = Flow.memoize c.Bad
                  let! _ = Flow.forkDetached (memo |> c.AsSibling)
                  return! memo
              })
          |> sib
          die "Flow.onExit/source" (fun c ->
              c.Bad
              |> Flow.onExit (fun exit ->
                  c.Handle()

                  match exit with
                  | Exit.Failure cause when hasBoom c cause -> c.Compensate()
                  | _ -> ()

                  Flow.ok ()))
          |> calls 1
          |> comp 1
          die "Flow.onExit/handler" (fun c -> c.Src 1 |> Flow.onExit (fun _ -> c.Throw<Flow<unit, Never, unit>>()))
          die "Flow.onExit/handlerFlow" (fun c -> c.Src 1 |> Flow.onExit (fun _ -> c.BadOf()))
          die "Flow.onInterrupt/source" (fun c -> c.Bad |> Flow.onInterrupt c.Finalizer) |> comp 0
          die "Flow.onInterrupt/handler" (fun c ->
              (Flow.never: Flow<unit, string, int>)
              |> Flow.onInterrupt (
                  Flow.delay (fun () ->
                      c.Compensate()
                      c.BadOf())
              )
              |> Flow.timeoutToOk (ms 20.0) 0)
          |> comp 1
          die "Flow.orElse/source" (fun c -> c.Bad |> Flow.orElse (Flow.ok 0))
          die "Flow.orElse/fallback" (fun c -> c.SrcFail |> Flow.orElse c.Bad)
          die "Flow.orElseFlow/flow" (fun c -> Flow.orElseFlow (c.BadOf<unit, string, string>()) (Error() : Result<int, unit>))
          die "Flow.orElseWith/source" (fun c -> c.Bad |> Flow.orElseWith (fun _ -> Flow.ok 0))
          die "Flow.orElseWith/fn" (fun c -> c.SrcFail |> Flow.orElseWith (c.Fn<string, Flow<unit, string, int>>()))
          die "Flow.orElseWith/flow" (fun c -> c.SrcFail |> Flow.orElseWith (fun _ -> c.Bad))
          die "Flow.race/left" (fun c -> Flow.race c.Bad c.Waiter) |> sib
          die "Flow.race/right" (fun c -> Flow.race c.Waiter c.Bad) |> sib
          die "Flow.recover/source" (fun c -> c.Bad |> Flow.recover (fun _ -> Flow.ok 0) : Flow<unit, string, int>)
          die "Flow.recover/fn" (fun c -> c.SrcFail |> Flow.recover (c.Fn<string, Flow<unit, string, int>>()))
          die "Flow.recover/flow" (fun c -> c.SrcFail |> Flow.recover (fun _ -> c.Bad))
          die "Flow.repeat/flow" (fun c -> c.Bad |> Flow.repeat (Schedule.recurs 2))
          die "Flow.retry/flow" (fun c -> c.Bad |> Flow.retry (Schedule.recurs 2))
          die "Flow.run/flow" (fun c -> c.Later(Flow.delay (fun () -> Flow.ofExit (Flow.run () c.Bad))))
          die "Flow.runOn/flow" (fun c -> c.Bad |> Flow.runOn RunOn.ThreadPool)
          die "Flow.scopeAcquireRelease/acquire" (fun c ->
              Flow.scoped (Flow.scopeAcquireRelease c.Bad (fun _ -> c.FinalizerOf())))
          |> comp 0
          die "Flow.scopeAcquireRelease/release" (fun c -> Flow.scoped (Flow.scopeAcquireRelease (c.AcquireOf()) (c.BadReleaseOf())))
          |> res
          die "Flow.scopeAcquireRelease/use" (fun c ->
              Flow.scoped (Flow.scopeAcquireRelease (c.AcquireOf()) (c.ReleaseOf()) |> Flow.bind (fun _ -> c.Bad)))
          |> res
          die "Flow.scopeAsyncDisposable/dispose" (fun c ->
              Flow.scoped (
                  flow {
                      do! Flow.scopeFinalizer c.Finalizer
                      do! Flow.scopeAsyncDisposable c.BadAsyncDisposable
                      return! c.Src 1
                  }
              ))
          |> comp 1
          die "Flow.scopeDisposable/dispose" (fun c ->
              Flow.scoped (
                  flow {
                      do! Flow.scopeFinalizer c.Finalizer
                      do! Flow.scopeDisposable c.BadDisposable
                      return! c.Src 1
                  }
              ))
          |> comp 1
          die "Flow.scopeFinalizer/finalizer" (fun c ->
              Flow.scoped (
                  flow {
                      do! Flow.scopeFinalizer c.Finalizer
                      do! Flow.scopeFinalizer (c.BadOf())
                      return! c.Src 1
                  }
              ))
          |> comp 1
          die "Flow.scopeResource/use" (fun c -> Flow.scoped (Flow.scopeResource c.Resource |> Flow.bind (fun _ -> c.Bad))) |> res
          die "Flow.scoped/flow" (fun c ->
              Flow.scoped (
                  flow {
                      do! Flow.scopeFinalizer c.Finalizer
                      return! c.Bad
                  }
              ))
          |> comp 1
          die "Flow.sequence/item" (fun c -> Flow.sequence [ c.Src 1; c.Bad ])
          die "Flow.sequencePar/item" (fun c -> Flow.sequencePar [ c.Waiter; c.Bad ]) |> sib
          die "Flow.sequenceParWith/item" (fun c -> Flow.sequenceParWith (Parallelism.bounded 2) [ c.Waiter; c.Bad ]) |> sib
          die "Flow.startTask/flow" (fun c ->
              c.Later(Flow.delay (fun () -> Flow.awaitStartedTask (Flow.startTask () c.Bad) |> Flow.bind Flow.ofExit)))
          die "Flow.supervise/flow" (fun c -> c.Bad |> Flow.supervise (Schedule.recurs 1))
          die "Flow.tap/fn" (fun c -> c.Src 1 |> Flow.tap (c.Fn<int, Flow<unit, string, unit>>()))
          die "Flow.tap/flow" (fun c -> c.Src 1 |> Flow.tap (fun _ -> c.BadOf()))
          die "Flow.tapError/fn" (fun c -> c.SrcFail |> Flow.tapError (c.Fn<string, Flow<unit, string, unit>>()))
          die "Flow.tapError/source" (fun c -> c.Bad |> Flow.tapError (fun _ -> Flow.ok ()))
          die "Flow.timeout/flow" (fun c -> c.Bad |> Flow.timeout (long ()) "late")
          die "Flow.timeoutToError/flow" (fun c -> c.Bad |> Flow.timeoutToError (long ()) "late")
          die "Flow.timeoutToOk/flow" (fun c -> c.Bad |> Flow.timeoutToOk (long ()) 0)
          die "Flow.timeoutWith/flow" (fun c -> c.Bad |> Flow.timeoutWith (long ()) (fun () -> Flow.ok 0))
          die "Flow.timeoutWith/fallback" (fun c ->
              (Flow.never: Flow<unit, string, int>) |> Flow.timeoutWith (ms 10.0) (c.Fn<unit, Flow<unit, string, int>>()))
          die "Flow.timeoutWith/fallbackFlow" (fun c -> (Flow.never: Flow<unit, string, int>) |> Flow.timeoutWith (ms 10.0) (fun () -> c.Bad))
          die "Flow.toAsync/flow" (fun c ->
              c.Later(Flow.delay (fun () -> Flow.fromAsync (Flow.toAsync () c.Bad) |> Flow.bind Flow.ofExit)))
          die "Flow.tracedError/flow" (fun c -> c.Bad |> Flow.tracedError "matrix")
          die "Flow.traverse/fn" (fun c -> c.Later([ 1; 2 ] |> Flow.traverse (fun item -> if item = 2 then c.Throw() else Flow.ok item)))
          die "Flow.traverse/flow" (fun c -> [ 1; 2 ] |> Flow.traverse (fun item -> if item = 2 then c.Bad else Flow.ok item))
          die "Flow.traversePar/fn" (fun c ->
              c.Later([ 1; 2 ] |> Flow.traversePar (Parallelism.bounded 2) (fun item -> if item = 2 then c.Throw() else c.Waiter)))
          |> sib
          die "Flow.traversePar/flow" (fun c ->
              [ 1; 2 ] |> Flow.traversePar (Parallelism.bounded 2) (fun item -> if item = 2 then c.Bad else c.Waiter))
          |> sib
          die "Flow.traverseParUsing/fn" (fun c ->
              c.Later(
                  [ 1; 2 ]
                  |> Flow.traverseParUsing (Parallelism.bounded 2) c.Resource (fun _ item -> if item = 2 then c.Throw() else c.Waiter)
              ))
          |> sib
          |> res
          die "Flow.traverseParUsing/flow" (fun c ->
              [ 1; 2 ]
              |> Flow.traverseParUsing (Parallelism.bounded 2) c.Resource (fun _ item -> if item = 2 then c.Bad else c.Waiter))
          |> sib
          |> res
          die "Flow.traverseParUsing/acquire" (fun c ->
              [ 1 ] |> Flow.traverseParUsing (Parallelism.bounded 2) c.BadAcquireResource (fun _ item -> Flow.ok item))
          die "Flow.traverseParUsing/release" (fun c ->
              [ 1 ] |> Flow.traverseParUsing (Parallelism.bounded 2) c.BadReleaseResource (fun _ item -> c.Src item))
          |> res
          die "Flow.verify/fn" (fun c -> c.Later(Flow.verify (fun () _ -> c.Throw<Result<int, string>>()) 1))
          die "Flow.widenError/flow" (fun c -> Flow.widenError (c.BadOf<unit, Never, int>()) : Flow<unit, string, int>)
          die "Flow.withAnnotationSink/flow" (fun c -> c.Bad |> Flow.withAnnotationSink (fun _ _ -> ()))
          // Documented exception, as for addAnnotationSink: a throwing sink cannot fail the workflow.
          value "Flow.withAnnotationSink/sink" 1 (fun c ->
              c.Later(Flow.ok 1 |> Flow.annotate "k" "v") |> Flow.withAnnotationSink (fun _ _ -> c.Throw()))
          die "Flow.withClock/flow" (fun c -> c.Bad |> Flow.withClock (clock ()))
          die "Flow.withFiberObserver/flow" (fun c -> c.Bad |> Flow.withFiberObserver FiberObserver.none)
          die "Flow.withFiberRegistry/flow" (fun c -> c.Bad |> Flow.withFiberRegistry (FiberRegistry()))
          die "Flow.withGuid/flow" (fun c -> c.Bad |> Flow.withGuid (guid ()))
          die "Flow.withLogSink/flow" (fun c -> c.Bad |> Flow.withLogSink LogSink.none)
          die "Flow.withRandom/flow" (fun c -> c.Bad |> Flow.withRandom (random ()))
          die "Flow.withTelemetry/flow" (fun c -> c.Bad |> Flow.withTelemetry (telemetry ()))
          die "Flow.withTraceId/flow" (fun c -> c.Bad |> Flow.withTraceId "trace")
          die "Flow.zip/left" (fun c -> Flow.zip c.Bad (c.Src 2))
          die "Flow.zip/right" (fun c -> Flow.zip (c.Src 1) c.Bad)
          die "Flow.zipPar/left" (fun c -> Flow.zipPar c.Bad c.Waiter) |> sib
          die "Flow.zipPar/right" (fun c -> Flow.zipPar c.Waiter c.Bad) |> sib ]

    let private resourceAndScopeCells () : MatrixCell list =
        [ die "Resource.create/acquire" (fun c -> Flow.scoped (Flow.scopeResource c.BadAcquireResource))
          die "Resource.create/release" (fun c -> Flow.scoped (Flow.scopeResource c.BadReleaseResource |> Flow.bind c.Src)) |> res
          die "Resource.finalizer/flow" (fun c ->
              Flow.scoped (
                  flow {
                      do! Flow.scopeFinalizer c.Finalizer
                      do! Flow.scopeResource (Resource.finalizer (c.BadOf()))
                      return! c.Src 1
                  }
              ))
          |> comp 1
          die "Scope.AddFinalizer/finalizer" (fun c ->
              Flow.scoped (
                  flow {
                      let! scope = Flow.scope

                      scope.AddFinalizer(fun _ ->
                          c.Compensate()
                          Task.CompletedTask)

                      scope.AddFinalizer(fun token -> c.BadTask<unit>() token :> Task)
                      return! c.Src 1
                  }
              ))
          |> comp 1
          die "Scope.AddDisposable/dispose" (fun c ->
              Flow.scoped (
                  flow {
                      let! scope = Flow.scope
                      do! Flow.scopeFinalizer c.Finalizer
                      scope.AddDisposable c.BadDisposable
                      return! c.Src 1
                  }
              ))
          |> comp 1
          die "Scope.AddAsyncDisposable/dispose" (fun c ->
              Flow.scoped (
                  flow {
                      let! scope = Flow.scope
                      do! Flow.scopeFinalizer c.Finalizer
                      scope.AddAsyncDisposable c.BadAsyncDisposable
                      return! c.Src 1
                  }
              ))
          |> comp 1 ]

    let private scheduleCells () : MatrixCell list =
        let bad (c: MatrixCtx) = Schedule.recurs 3 |> Schedule.whileOutput (c.Fn<int, bool>())

        [ die "Schedule.whileInput/fn" (fun c -> repeatWith c (Schedule.recurs 3 |> Schedule.whileInput (c.Fn<int, bool>())))
          die "Schedule.untilInput/fn" (fun c -> repeatWith c (Schedule.recurs 3 |> Schedule.untilInput (c.Fn<int, bool>())))
          die "Schedule.whileOutput/fn" (fun c -> repeatWith c (bad c))
          die "Schedule.untilOutput/fn" (fun c -> repeatWith c (Schedule.recurs 3 |> Schedule.untilOutput (c.Fn<int, bool>())))
          die "Schedule.map/fn" (fun c -> repeatWith c (Schedule.recurs 3 |> Schedule.map (c.Fn<int, int>())))
          die "Schedule.jitteredWith/fn" (fun c ->
              repeatWith c (Schedule.spaced (ms 1.0) |> Schedule.recursAtMost 2 |> Schedule.jitteredWith (c.Fn<unit, float>())))
          die "Schedule.andThen/inner" (fun c -> repeatWith c (bad c |> Schedule.andThen (Schedule.recurs 1)))
          die "Schedule.intersect/inner" (fun c -> repeatWith c (bad c |> Schedule.intersect (Schedule.recurs 5)))
          die "Schedule.union/inner" (fun c -> repeatWith c (bad c |> Schedule.union (Schedule.recurs 5)))
          die "Schedule.recursAtMost/inner" (fun c -> repeatWith c (bad c |> Schedule.recursAtMost 5))
          die "Schedule.resetAfter/inner" (fun c -> repeatWith c (bad c |> Schedule.resetAfter (long ())))
          die "Schedule.upTo/inner" (fun c -> repeatWith c (bad c |> Schedule.upTo (long ())))
          die "Flow.retry/schedule" (fun c -> c.SrcFail |> Flow.retry (Schedule.recurs 3 |> Schedule.whileInput (c.Fn<string, bool>())))
          die "Flow.supervise/schedule" (fun c -> c.Bad |> Flow.supervise (Schedule.recurs 3 |> Schedule.whileInput (fun (_: exn) -> c.Throw<bool>())))
          die "Retry.schedule/when" (fun c ->
              c.SrcFail |> Flow.retry (Retry.schedule { Retry.defaults with When = c.Fn<string, bool>() })) ]

    let private streamCells () : MatrixCell list =
        let collect (stream: FlowStream<unit, string, 'v>) = FlowStream.runCollect stream

        [ die "FlowStream.append/first" (fun c -> FlowStream.append c.SrcStream c.BadStream |> collect)
          die "FlowStream.append/second" (fun c -> FlowStream.append c.BadStream c.SrcStream |> collect)
          die "FlowStream.buffer/upstream" (fun c -> c.BadStream |> FlowStream.buffer (QueueStrategy.BackPressure 2) |> collect)
          die "FlowStream.choose/fn" (fun c -> c.SrcStream |> FlowStream.choose (c.Fn<int, int option>()) |> collect)
          die "FlowStream.chunkBySize/upstream" (fun c -> c.BadStream |> FlowStream.chunkBySize 2 |> collect)
          die "FlowStream.collect/fn" (fun c -> c.SrcStream |> FlowStream.collect (c.Fn<int, FlowStream<unit, string, int>>()) |> collect)
          die "FlowStream.collect/inner" (fun c -> c.SrcStream |> FlowStream.collect (fun _ -> c.BadStream) |> collect)
          die "FlowStream.debounce/upstream" (fun c -> c.BadStream |> FlowStream.debounce (ms 5.0) |> collect)
          die "FlowStream.distinctUntilChangedBy/fn" (fun c -> c.SrcStream |> FlowStream.distinctUntilChangedBy (c.Fn<int, int>()) |> collect)
          die "FlowStream.filter/fn" (fun c -> c.SrcStream |> FlowStream.filter (c.Fn<int, bool>()) |> collect)
          die "FlowStream.fromCallback/register" (fun c ->
              c.Later(FlowStream.fromCallback 4 (c.Fn<Emit<string, int>, Flow<unit, string, IDisposable>>()) |> collect))
          die "FlowStream.fromCallback/registerFlow" (fun c -> FlowStream.fromCallback 4 (fun _ -> c.BadOf<unit, string, IDisposable>()) |> collect)
          die "FlowStream.fromCallback/dispose" (fun c ->
              c.Later(
                  FlowStream.fromCallback 4 (fun (emit: Emit<string, int>) ->
                      emit.Value 1 |> ignore
                      emit.End()
                      Flow.ok c.BadDisposable)
                  |> collect
              ))
          die "FlowStream.fromCallbackWith/register" (fun c ->
              c.Later(
                  FlowStream.fromCallbackWith 4 CallbackOverflow.Dropping (c.Fn<Emit<string, int>, Flow<unit, string, IDisposable>>())
                  |> collect
              ))
          die "FlowStream.fromFlow/flow" (fun c -> c.BadStream |> collect)
          die "FlowStream.fromSchedule/schedule" (fun c ->
              c.Later(FlowStream.fromSchedule (Schedule.recurs 3 |> Schedule.map (c.Fn<int, int>())) |> FlowStream.runCollect)
              : Flow<unit, string, int list>)
          die "FlowStream.fromSeq/seq" (fun c -> FlowStream.fromSeq c.BadSeq |> FlowStream.runForEachFlow (fun _ -> c.Hop))
          die "FlowStream.groupedWithin/upstream" (fun c -> c.BadStream |> FlowStream.groupedWithin 2 (ms 5.0) |> collect)
          die "FlowStream.indexed/upstream" (fun c -> c.BadStream |> FlowStream.indexed |> collect)
          die "FlowStream.localEnv/fn" (fun c ->
              c.Later(FlowStream.fromSeq [ 1 ] |> FlowStream.localEnv (fun () -> c.Throw<int>()) |> collect))
          die "FlowStream.localEnv/upstream" (fun c -> c.BadStreamOf<int, string, int>() |> FlowStream.localEnv (fun () -> 1) |> collect)
          die "FlowStream.map/fn" (fun c -> c.SrcStream |> FlowStream.map (c.Fn<int, int>()) |> collect)
          die "FlowStream.mapError/fn" (fun c -> FlowStream.fromFlow c.SrcFail |> FlowStream.mapError (c.Fn<string, string>()) |> collect)
          die "FlowStream.mapFlow/fn" (fun c -> c.SrcStream |> FlowStream.mapFlow (c.Fn<int, Flow<unit, string, int>>()) |> collect)
          die "FlowStream.mapFlow/flow" (fun c -> c.SrcStream |> FlowStream.mapFlow (fun _ -> c.Bad) |> collect)
          die "FlowStream.mapFlowPar/fn" (fun c ->
              c.SrcStream |> FlowStream.mapFlowPar (Parallelism.bounded 2) (fun item -> if item = 2 then c.Throw() else c.Src item) |> collect)
          die "FlowStream.mapFlowPar/flow" (fun c ->
              c.SrcStream |> FlowStream.mapFlowPar (Parallelism.bounded 2) (fun item -> if item = 2 then c.Bad else c.Waiter) |> collect)
          |> sib
          die "FlowStream.mapFlowParUsing/fn" (fun c ->
              c.SrcStream
              |> FlowStream.mapFlowParUsing (Parallelism.bounded 2) c.Resource (fun _ item -> if item = 2 then c.Throw() else c.Src item)
              |> collect)
          |> res
          die "FlowStream.mapFlowParUsing/flow" (fun c ->
              c.SrcStream
              |> FlowStream.mapFlowParUsing (Parallelism.bounded 2) c.Resource (fun _ item -> if item = 2 then c.Bad else c.Src item)
              |> collect)
          |> res
          die "FlowStream.mergePar/stream" (fun c -> FlowStream.mergePar [ FlowStream.fromFlow c.Waiter; c.BadStream ] |> collect) |> sib
          die "FlowStream.ofAsyncEnumerable/enumerator" (fun c -> FlowStream.ofAsyncEnumerable c.BadAsyncEnumerable |> collect)
          die "FlowStream.reissueOnWake/work" (fun c -> FlowStream.reissueOnWake (FlowStream.fromSeq [ () ]) c.Bad)
          die "FlowStream.repeatFlow/flow" (fun c -> FlowStream.repeatFlow c.Bad |> FlowStream.take 3 |> collect)
          die "FlowStream.runCollect/upstream" (fun c -> c.BadStream |> FlowStream.runCollect)
          die "FlowStream.runCount/upstream" (fun c -> c.BadStream |> FlowStream.runCount)
          die "FlowStream.runDrain/upstream" (fun c -> c.BadStream |> FlowStream.runDrain)
          die "FlowStream.runFold/fn" (fun c -> c.SrcStream |> FlowStream.runFold (fun _ _ -> c.Throw<int>()) 0)
          die "FlowStream.runForEach/fn" (fun c -> c.SrcStream |> FlowStream.runForEach (c.Fn<int, unit>()))
          die "FlowStream.runForEachFlow/fn" (fun c -> c.SrcStream |> FlowStream.runForEachFlow (c.Fn<int, Flow<unit, string, unit>>()))
          die "FlowStream.runForEachFlow/flow" (fun c -> c.SrcStream |> FlowStream.runForEachFlow (fun _ -> c.BadOf()))
          die "FlowStream.runIntoHub/upstream" (fun c -> Hub.make () |> Flow.bind (fun hub -> c.BadStream |> FlowStream.runIntoHub hub))
          die "FlowStream.runIntoQueue/upstream" (fun c ->
              Queue.unbounded () |> Flow.bind (fun queue -> c.BadStream |> FlowStream.runIntoQueue queue))
          die "FlowStream.runTryHead/upstream" (fun c -> c.BadStream |> FlowStream.runTryHead)
          die "FlowStream.runTryLast/upstream" (fun c -> c.BadStream |> FlowStream.runTryLast)
          die "FlowStream.scan/fn" (fun c -> c.SrcStream |> FlowStream.scan (fun _ _ -> c.Throw<int>()) 0 |> collect)
          die "FlowStream.skip/upstream" (fun c -> c.BadStream |> FlowStream.skip 1 |> collect)
          die "FlowStream.skipWhile/fn" (fun c -> c.SrcStream |> FlowStream.skipWhile (c.Fn<int, bool>()) |> collect)
          die "FlowStream.switchMapFlow/fn" (fun c -> c.SrcStream |> FlowStream.switchMapFlow (c.Fn<int, Flow<unit, string, int>>()) |> collect)
          die "FlowStream.switchMapFlow/flow" (fun c -> c.SrcStream |> FlowStream.switchMapFlow (fun _ -> c.Bad) |> collect)
          die "FlowStream.take/upstream" (fun c -> c.BadStream |> FlowStream.take 2 |> collect)
          die "FlowStream.takeWhile/fn" (fun c -> c.SrcStream |> FlowStream.takeWhile (c.Fn<int, bool>()) |> collect)
          die "FlowStream.tapFlow/fn" (fun c -> c.SrcStream |> FlowStream.tapFlow (c.Fn<int, Flow<unit, string, unit>>()) |> collect)
          die "FlowStream.tapFlow/flow" (fun c -> c.SrcStream |> FlowStream.tapFlow (fun _ -> c.BadOf()) |> collect)
          die "FlowStream.throttle/upstream" (fun c -> c.BadStream |> FlowStream.throttle (ms 5.0) |> collect)
          die "FlowStream.unfoldFlow/fn" (fun c ->
              c.Later(FlowStream.unfoldFlow (c.Fn<int, Flow<unit, string, (int * int) option>>()) 0 |> FlowStream.runCollect)
              : Flow<unit, string, int list>)
          die "FlowStream.unfoldFlow/flow" (fun c ->
              FlowStream.unfoldFlow (fun _ -> c.BadOf<unit, string, (int * int) option>()) 0 |> FlowStream.runCollect
              : Flow<unit, string, int list>)
          die "FlowStream.using/acquire" (fun c -> FlowStream.using c.BadAcquireResource (fun _ -> c.SrcStream) |> collect)
          die "FlowStream.using/release" (fun c -> FlowStream.using c.BadReleaseResource (fun _ -> c.SrcStream) |> collect) |> res
          die "FlowStream.using/body" (fun c ->
              c.Later(FlowStream.using c.Resource (c.Fn<int, FlowStream<unit, string, int>>()) |> collect))
          |> res
          die "FlowStream.using/stream" (fun c -> FlowStream.using c.Resource (fun _ -> c.BadStream) |> collect) |> res
          die "FlowStream.zip/left" (fun c -> FlowStream.zip c.BadStream c.SrcStream |> collect)
          die "FlowStream.zip/right" (fun c -> FlowStream.zip c.SrcStream c.BadStream |> collect) ]

    // Consumers of Hub and Queue. The sibling is a detached producer blocked by back-pressure: it must be released
    // when the consumer throws (the scoped hub or queue shuts down as the consuming scope closes).
    let private hubAndQueueCells () : MatrixCell list =
        let publishUntilShutdown (c: MatrixCtx) (hub: Hub<int>) =
            let running = ref true

            flow {
                while running.Value do
                    let! _ = Hub.publish 1 hub
                    do! Flow.sleep (ms 1.0)
                    let! stopped = Hub.isShutdown hub
                    running.Value <- not stopped
            }
            |> c.AsSibling

        let offerMany (c: MatrixCtx) (queue: Queue<int>) =
            flow {
                for item in 1..50 do
                    let! _ = Queue.offer item queue
                    ()
            }
            |> c.AsSibling

        [ die "Hub.subscribe/consumer" (fun c ->
              Flow.scoped (
                  flow {
                      let! hub = Hub.makeScoped ()
                      let! subscription = hub |> Hub.subscribe (QueueStrategy.BackPressure 1)
                      let! _ = Flow.forkDetached (publishUntilShutdown c hub)
                      return! FlowStream.fromDequeue subscription |> FlowStream.runForEachFlow (fun _ -> c.BadOf())
                  }
              ))
          |> sib
          die "Hub.publish/consumer" (fun c ->
              Flow.scoped (
                  flow {
                      let! hub = Hub.makeScoped ()
                      let! _ = Flow.forkDetached (publishUntilShutdown c hub)
                      return! FlowStream.fromHub (QueueStrategy.BackPressure 1) hub |> FlowStream.runForEachFlow (fun _ -> c.BadOf())
                  }
              ))
          |> sib
          die "Queue.offer/consumer" (fun c ->
              Flow.scoped (
                  flow {
                      let! queue = Queue.makeScoped (QueueStrategy.BackPressure 1)
                      let! _ = Flow.forkDetached (offerMany c queue)
                      return! FlowStream.fromDequeue queue |> FlowStream.runForEachFlow (fun _ -> c.BadOf())
                  }
              ))
          |> sib
          die "Dequeue.take/consumer" (fun c ->
              Flow.scoped (
                  flow {
                      let! queue = Queue.makeScoped (QueueStrategy.BackPressure 1)
                      let! _ = Flow.forkDetached (offerMany c queue)

                      while true do
                          let! item = Dequeue.take queue
                          do! (if item = 2 then c.BadOf() else c.Hop)
                  }
              ))
          |> sib ]

    let private cacheCells () : MatrixCell list =
        // A second, detached caller waits on the same key: it must be released when the lookup throws.
        let getTwice (c: MatrixCtx) (cache: Cache<int, string, int>) =
            flow {
                let! _ = Flow.forkDetached (Cache.get 1 cache |> c.AsSibling)
                return! Cache.get 1 cache
            }

        let lookupFn (c: MatrixCtx) : int -> Flow<unit, string, int> = c.Fn()
        let lookupFlow (c: MatrixCtx) : int -> Flow<unit, string, int> = fun _ -> c.Bad

        [ die "Cache.make/lookup" (fun c -> c.Later(Cache.make (lookupFn c) |> Flow.bind (getTwice c))) |> sib
          die "Cache.make/lookupFlow" (fun c -> Cache.make (lookupFlow c) |> Flow.bind (getTwice c)) |> sib
          die "Cache.makeBounded/lookup" (fun c -> c.Later(Cache.makeBounded 4 (lookupFn c) |> Flow.bind (getTwice c))) |> sib
          die "Cache.makeBounded/lookupFlow" (fun c -> Cache.makeBounded 4 (lookupFlow c) |> Flow.bind (getTwice c)) |> sib
          die "Cache.makeBoundedWithExpiry/lookupFlow" (fun c ->
              Cache.makeBoundedWithExpiry 4 (long ()) (lookupFlow c) |> Flow.bind (getTwice c))
          |> sib
          die "Cache.makeRec/lookupFlow" (fun c -> Cache.makeRec (fun _ -> lookupFlow c) |> Flow.bind (getTwice c)) |> sib ]

    let private layerCells () : MatrixCell list =
        [ die "Layer.acquireRelease/acquire" (fun c -> provideUnit (Layer.acquireRelease (badLayer c) (fun _ -> c.FinalizerOf())))
          |> comp 0
          die "Layer.acquireRelease/release" (fun c ->
              provideUnit (Layer.acquireRelease (Layer.fromFlow (c.AcquireOf())) (c.BadReleaseOf())))
          |> res
          die "Layer.acquireRelease/use" (fun c ->
              Layer.provide (Layer.acquireRelease (Layer.fromFlow (c.AcquireOf())) (c.ReleaseOf())) (c.BadOf<int, string, int>()))
          |> res
          die "Layer.addFinalizer/finalizer" (fun c ->
              Layer.provide (Layer.addFinalizer (c.BadOf())) (Flow.ok 1 : Flow<unit, string, int>))
          die "Layer.apply/fn" (fun c -> provideUnit (Layer.apply (Layer.succeed (fun (_: int) -> c.Throw<int>())) (srcLayer c 1)))
          die "Layer.apply/layer" (fun c -> provideUnit (Layer.apply (Layer.fromFlow (c.BadOf<unit, string, int -> int>())) (srcLayer c 1)))
          die "Layer.bind/fn" (fun c -> provideUnit (srcLayer c 1 |> Layer.bind (c.Fn<int, Layer<unit, string, int>>())))
          die "Layer.bind/layer" (fun c -> provideUnit (srcLayer c 1 |> Layer.bind (fun _ -> badLayer c)))
          die "Layer.envWith/fn" (fun c -> c.Later(provideUnit (Layer.envWith (c.Fn<unit, int>()))))
          die "Layer.fromFlow/flow" (fun c -> provideUnit (badLayer c))
          die "Layer.map/fn" (fun c -> provideUnit (srcLayer c 1 |> Layer.map (c.Fn<int, int>())))
          die "Layer.map2/fn" (fun c -> provideUnit (Layer.map2 (fun _ _ -> c.Throw<int>()) (srcLayer c 1) (srcLayer c 2)))
          die "Layer.map3/fn" (fun c -> provideUnit (Layer.map3 (fun _ _ _ -> c.Throw<int>()) (srcLayer c 1) (srcLayer c 2) (srcLayer c 3)))
          die "Layer.mapError/fn" (fun c -> provideUnit (Layer.fromFlow c.SrcFail |> Layer.mapError (c.Fn<string, string>())))
          die "Layer.merge/left" (fun c -> Layer.provide (Layer.merge (badLayer c) (srcLayer c 2)) (Flow.envWith fst : Flow<int * int, string, int>))
          die "Layer.merge/right" (fun c -> Layer.provide (Layer.merge (srcLayer c 1) (badLayer c)) (Flow.envWith fst : Flow<int * int, string, int>))
          die "Layer.pool/fn" (fun c ->
              c.Later(Layer.provide (Layer.pool 2 (c.Fn<int, Layer<unit, string, int>>())) (Flow.ok 1 : Flow<Pool<int>, string, int>)))
          die "Layer.pool/layer" (fun c -> Layer.provide (Layer.pool 2 (fun _ -> badLayer c)) (Flow.ok 1 : Flow<Pool<int>, string, int>))
          die "Layer.provide/flow" (fun c -> Layer.provide (Layer.succeed 1) (c.BadOf<int, string, int>()))
          die "Layer.provide/layer" (fun c -> provideUnit (badLayer c))
          die "Layer.widenError/layer" (fun c -> provideUnit (Layer.widenError (Layer.fromFlow (c.BadOf<unit, Never, int>()))))
          die "Layer.zip/left" (fun c -> Layer.provide (Layer.zip (badLayer c) (srcLayer c 2)) (Flow.envWith fst : Flow<int * int, string, int>))
          die "Layer.zip/right" (fun c -> Layer.provide (Layer.zip (srcLayer c 1) (badLayer c)) (Flow.envWith fst : Flow<int * int, string, int>))
          die "Layer.zipPar/left" (fun c ->
              Layer.provide (Layer.zipPar (badLayer c) (Layer.fromFlow c.Waiter)) (Flow.envWith fst : Flow<int * int, string, int>))
          |> sib
          die "Layer.zipPar/right" (fun c ->
              Layer.provide (Layer.zipPar (Layer.fromFlow c.Waiter) (badLayer c)) (Flow.envWith fst : Flow<int * int, string, int>))
          |> sib ]

    let private builderCells () : MatrixCell list =
        [ die "Builder.let!/source" (fun c ->
              flow {
                  let! value = c.Bad
                  return value
              })
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
          die "Builder.use/dispose" (fun c ->
              flow {
                  use _ = c.BadDisposable
                  return! c.Src 1
              })
          die "Builder.try-finally/body" (fun c ->
              flow {
                  try
                      return! c.Bad
                  finally
                      c.Compensate()
              })
          |> comp 1
          die "Builder.try-finally/compensation" (fun c ->
              flow {
                  try
                      return! c.Src 1
                  finally
                      c.Throw<unit>()
              })
          value "Builder.try-with/body" 42 (fun c ->
              flow {
                  try
                      return! c.Bad
                  with error ->
                      c.Handle()
                      return (if c.IsBoom error then 42 else -1)
              })
          |> calls 1
          die "Builder.try-with/handler" (fun c ->
              flow {
                  try
                      return! c.Bad
                  with _ ->
                      c.Handle()
                      return c.Throw<int>()
              })
          |> calls 1
          die "Builder.for/body" (fun c ->
              flow {
                  for item in [ 1; 2 ] do
                      do! (if item = 2 then c.BadOf() else c.Hop)

                  return 1
              })
          die "Builder.for/seq" (fun c ->
              flow {
                  for _ in c.BadSeq do
                      do! c.Hop

                  return 1
              })
          die "Builder.while/body" (fun c ->
              flow {
                  let turns = ref 0

                  while turns.Value < 2 do
                      turns.Value <- turns.Value + 1
                      do! (if turns.Value = 2 then c.BadOf() else c.Hop)

                  return 1
              })
          die "Builder.while/guard" (fun c ->
              flow {
                  let turns = ref 0

                  while (turns.Value <- turns.Value + 1
                         if turns.Value = 2 then c.Throw() else true) do
                      do! c.Hop

                  return 1
              }) ]

    // Every value here is a function: this test assembly never runs F# file initializers for computed module values.
    let cells () : MatrixCell list =
        flowCells () @ resourceAndScopeCells () @ scheduleCells () @ streamCells () @ hubAndQueueCells () @ cacheCells ()
        @ layerCells () @ builderCells ()

    let modes () = [ Sync; AfterAwait ]
    let modeName mode =
        match mode with
        | Sync -> "Sync"
        | AfterAwait -> "AfterAwait"

    let caseId (cell: MatrixCell) (mode: ThrowMode) = $"{cell.Name} [{modeName mode}]"

    /// Cells that fail at the current HEAD, with the observed failure. Asserted EQUAL to the failing set: a fixed gap
    /// fails the suite until it is removed here, and a new failure fails it until it is fixed (or recorded).
    /// "throw-specific" gaps hold when the user flow returns Flow.die instead of throwing, so converting throws at the
    /// user-code call site (the planned leaf wrapper, T30) fixes them; "combinator" gaps fail with Flow.die too.
    let knownGaps () : Map<string, string> =
        let parallelLeak =
            "throw-specific: a user flow faulting after an await ends the combinator with Die but the running sibling "
            + "is never interrupted (leaked fiber)"

        let bufferHang = "throw-specific: the buffer producer never offers the failure for a thrown/faulted upstream; the consumer hangs"
        let mergeHang = "throw-specific: mergePar never delivers the failure of a stream whose pull throws; the run hangs"

        let layerZipParHang =
            "combinator: Layer.zipPar waits for both branches (zipParAllExecution) and never interrupts the other one, "
            + "so a failing branch beside a long-running one hangs, with Flow.die as well"

        let timeoutSwallow =
            "combinator: the onInterrupt handler runs (compensation 1) but timeoutToOk returns its value and drops the "
            + "handler's defect, with Flow.die as well"

        Map
            [ "Flow.forEachPar/flow [AfterAwait]", parallelLeak
              "Flow.sequencePar/item [AfterAwait]", parallelLeak
              "Flow.sequenceParWith/item [AfterAwait]", parallelLeak
              "Flow.traversePar/flow [AfterAwait]", parallelLeak
              "Flow.zipPar/left [AfterAwait]", parallelLeak
              "Flow.zipPar/right [AfterAwait]", parallelLeak
              "FlowStream.buffer/upstream [Sync]", bufferHang
              "FlowStream.buffer/upstream [AfterAwait]", bufferHang
              "FlowStream.mergePar/stream [Sync]", mergeHang
              "FlowStream.mergePar/stream [AfterAwait]", mergeHang
              "Layer.zipPar/left [Sync]", layerZipParHang
              "Layer.zipPar/left [AfterAwait]", layerZipParHang
              "Layer.zipPar/right [Sync]", layerZipParHang
              "Layer.zipPar/right [AfterAwait]", layerZipParHang
              "Flow.onInterrupt/handler [Sync]", timeoutSwallow
              "Flow.onInterrupt/handler [AfterAwait]", timeoutSwallow ]

    /// Members of the checked modules that have no cells of their own, and why.
    let exemptions () : Map<string, Exemption> =
        Map
            [ for name in
                  [ "annotations"; "cancellationToken"; "die"; "ensureNotCanceled"; "env"; "error"; "fail"; "fiberId"
                    "fromOption"; "fromResult"; "fromValueOption"; "never"; "ofExit"; "ok"; "scope"; "sleep"; "succeed"
                    "traceId" ] do
                  $"Flow.{name}", NoUserCode
              "Flow.op_GreaterGreaterEquals", CoveredBy "Flow.bind"
              "Flow.op_LessBangGreater", CoveredBy "Flow.map"
              "Flow.op_LessMultiplyGreater", CoveredBy "Flow.apply"
              for name in [ "empty"; "singleton" ] do
                  $"FlowStream.{name}", NoUserCode
              "FlowStream.fromDequeue", CoveredBy "Queue.offer"
              "FlowStream.fromHub", CoveredBy "Hub.publish"
              "Layer.succeed", NoUserCode
              "Cache.get", CoveredBy "Cache.make"
              for name in [ "count"; "invalidate"; "invalidateAll" ] do
                  $"Cache.{name}", NoUserCode
              for name in [ "AddChild"; "Close" ] do
                  $"Scope.{name}", NoUserCode
              for name in
                  [ "awaitShutdown"; "isShutdown"; "make"; "makeScoped"; "publishAll"; "shutdown"; "subscriberCount"
                    "tryPublish"; "tryPublishNow" ] do
                  $"Hub.{name}", NoUserCode
              for name in
                  [ "bounded"; "dropping"; "droppingNow"; "make"; "makeScoped"; "offerAll"; "shutdownNow"; "sliding"
                    "tryOffer"; "unbounded" ] do
                  $"Queue.{name}", NoUserCode
              for name in
                  [ "awaitShutdown"; "capacity"; "isShutdown"; "poll"; "shutdown"; "size"; "stats"; "takeAll"
                    "takeBetween"; "takeUpTo" ] do
                  $"Dequeue.{name}", NoUserCode
              for name in [ "elapsed"; "exponential"; "fixedRate"; "recurs"; "spaced" ] do
                  $"Schedule.{name}", NoUserCode
              "Retry.defaults", NoUserCode ]

    /// Cell prefixes that are not module members: computation-expression constructs.
    let pseudoTargets () = set [ "Builder" ]

    [<Literal>]
    let private boundSeconds = 5.0

    let private bound () = TimeSpan.FromSeconds boundSeconds

    let private describeCause (c: MatrixCtx) (cause: Cause<obj>) =
        let rec go (cause: Cause<obj>) : string =
            match cause with
            | Cause.Fail error ->
                match error with
                | :? exn as e when c.IsBoom e -> "Fail boom"
                | _ -> $"Fail {error}"
            | Cause.Die error when c.IsBoom error -> "Die boom"
            | Cause.Die error -> $"Die {error.GetType().Name}({error.Message})"
            | Cause.Interrupt -> "Interrupt"
            | Cause.Then(left, right) -> $"Then({go left}, {go right})"
            | Cause.Both(left, right) -> $"Both({go left}, {go right})"
            | Cause.Traced(inner, _) -> go inner

        go cause

    let private describeExit (c: MatrixCtx) (exit: Exit<obj, obj>) =
        match exit with
        | Exit.Success value -> $"Success {value}"
        | Exit.Failure cause -> describeCause c cause

    let private checkOutcome (cell: MatrixCell) (c: MatrixCtx) (exit: Exit<obj, obj>) : string list =
        let wrong expected = [ $"expected {expected}, got {describeExit c exit}" ]

        match cell.Expect, exit with
        | ExpectDie, Exit.Failure cause ->
            let defects = Cause.defects cause

            if List.isEmpty (Cause.failures cause) && defects.Length = 1 && c.IsBoom defects.Head then []
            else wrong "Die boom"
        | ExpectDie, _ -> wrong "Die boom"
        | ExpectFailBoom, Exit.Failure cause ->
            match Cause.failures cause, Cause.defects cause with
            | [ (:? exn as error) ], [] when c.IsBoom error -> []
            | _ -> wrong "Fail boom"
        | ExpectFailBoom, _ -> wrong "Fail boom"
        | ExpectFail expected, Exit.Failure cause ->
            match Cause.failures cause, Cause.defects cause with
            | [ error ], [] when error = expected -> []
            | _ -> wrong $"Fail {expected}"
        | ExpectFail expected, _ -> wrong $"Fail {expected}"
        | ExpectValue expected, Exit.Success actual when actual = expected -> []
        | ExpectValue expected, _ -> wrong $"Success {expected}"

    let private checkCounters (cell: MatrixCell) (c: MatrixCtx) : string list =
        [ match cell.Compensations with
          | Some expected when c.Compensations <> expected -> $"compensation ran {c.Compensations} times, expected {expected}"
          | _ -> ()
          match cell.HandlerCalls with
          | Some expected when c.HandlerCalls <> expected -> $"handler ran {c.HandlerCalls} times, expected {expected}"
          | _ -> ()
          if cell.Resources && (c.Acquired = 0 || c.Released <> c.Acquired) then
              $"acquired {c.Acquired} resources, released {c.Released}" ]

    /// Runs one cell in one mode on the thread pool (a cell that blocks its caller cannot stall the harness) and
    /// returns its failures; empty means the cell holds the invariant.
    let evaluate (cell: MatrixCell) (mode: ThrowMode) : Task<string list> =
        task {
            let c = MatrixCtx(cell.Name, mode)
            let cancellation = new CancellationTokenSource()

            let running =
                Task.Run<Exit<obj, obj>>(fun () -> cell.Run c cancellation.Token)

            let! outcome =
                task {
                    try
                        let! exit = running.WaitAsync(bound ())
                        return Choice1Of3 exit
                    with
                    | :? TimeoutException -> return Choice2Of3()
                    | error -> return Choice3Of3 error
                }

            match outcome with
            | Choice2Of3() ->
                // Interrupt the hung run; never wait for it, it may ignore interruption.
                cancellation.Cancel()
                return [ $"hang: no exit within {boundSeconds} s" ]
            | Choice3Of3 error ->
                let shown = if c.IsBoom error then "boom" else $"{error.GetType().Name}({error.Message})"
                return [ $"the exception escaped the flow as a raw {shown}" ]
            | Choice1Of3 exit ->
                cancellation.Dispose()
                let outcomeFailures = checkOutcome cell c exit
                let counterFailures = checkCounters cell c

                let! siblingFailures =
                    task {
                        if not cell.Sibling then
                            return []
                        else
                            try
                                do! c.SiblingReleased.WaitAsync(bound ())
                                return []
                            with :? TimeoutException ->
                                if c.SiblingStarted then
                                    return [ $"sibling not released within {boundSeconds} s" ]
                                else
                                    return []
                    }

                return outcomeFailures @ counterFailures @ siblingFailures
        }

    let mutable private cached: Task<IReadOnlyDictionary<string, string list>> = null

    let private runAll () : Task<IReadOnlyDictionary<string, string list>> =
        task {
            use gate = new SemaphoreSlim(16)

            let runOne (cell: MatrixCell) (mode: ThrowMode) =
                task {
                    do! gate.WaitAsync()

                    try
                        let! failures = evaluate cell mode
                        return KeyValuePair(caseId cell mode, failures)
                    finally
                        gate.Release() |> ignore
                }

            let! all = Task.WhenAll [ for cell in cells () do for mode in modes () -> runOne cell mode ]
            return Dictionary<string, string list>(all) :> IReadOnlyDictionary<string, string list>
        }

    /// Every cell in both modes, run once and concurrently (at most 16 at a time) so hung cells cost one bound each
    /// slot, not one each in sequence.
    let results () : IReadOnlyDictionary<string, string list> =
        let pending =
            lock typeof<MatrixCell> (fun () ->
                if isNull cached then cached <- runAll ()
                cached)

        pending.Result

    let failingCases () =
        results ()
        |> Seq.filter (fun pair -> not pair.Value.IsEmpty)
        |> Seq.map (fun pair -> pair.Key, String.concat "; " pair.Value)
        |> Map.ofSeq

    // The public members of each checked module, by reflection (.NET only; Fable keeps its own explicit list).
    let private moduleTypes () : (string * Type) list =
        let assembly = typeof<Scope>.Assembly

        [ "Flow", assembly.GetType("Axial.Flow", true)
          "FlowStream", assembly.GetType("Axial.FlowStreamModule", true)
          "Layer", assembly.GetType("Axial.LayerModule", true)
          "Cache", assembly.GetType("Axial.CacheModule", true)
          "Hub", assembly.GetType("Axial.HubModule", true)
          "Queue", assembly.GetType("Axial.QueueModule", true)
          "Dequeue", assembly.GetType("Axial.DequeueModule", true)
          "Schedule", assembly.GetType("Axial.ScheduleModule", true)
          "Retry", assembly.GetType("Axial.RetryModule", true)
          "Resource", assembly.GetType("Axial.ResourceModule", true) ]

    let publicMembers () : Set<string> =
        let fromModules =
            [ for name, moduleType in moduleTypes () do
                  for methodInfo in moduleType.GetMethods(BindingFlags.Public ||| BindingFlags.Static ||| BindingFlags.DeclaredOnly) do
                      if not (methodInfo.Name.Contains "<") && not (methodInfo.Name.StartsWith "get_") then
                          $"{name}.{methodInfo.Name}"
                  for property in moduleType.GetProperties(BindingFlags.Public ||| BindingFlags.Static ||| BindingFlags.DeclaredOnly) do
                      $"{name}.{property.Name}" ]

        let fromScope =
            [ for methodInfo in typeof<Scope>.GetMethods(BindingFlags.Public ||| BindingFlags.Instance ||| BindingFlags.DeclaredOnly) do
                  if not methodInfo.IsSpecialName then
                      $"Scope.{methodInfo.Name}" ]

        Set.ofList (fromModules @ fromScope)

    let cellTarget (cell: MatrixCell) = cell.Name.Split('/')[0]

module ThrowSafetyMatrixTests =
    open ThrowSafetyMatrix

    let cases () : obj array seq =
        seq {
            for cell in cells () do
                for mode in modes () do
                    let id = caseId cell mode

                    let status =
                        match knownGaps().TryFind id with
                        | Some _ -> "known gap"
                        | None -> "holds"

                    yield [| box id; box status |]
        }

    /// One named case per cell and mode. A known gap passes while it still fails and fails once it is fixed.
    [<Theory>]
    [<MemberData(nameof cases)>]
    let ``throw-safety cell`` (id: string, status: string) =
        let failures = (results ())[id]

        match knownGaps().TryFind id, failures with
        | None, [] -> ()
        | None, failures ->
            let reasons = String.concat "; " failures
            failwith $"{id}: {reasons}"
        | Some _, _ :: _ -> Assert.Equal("known gap", status)
        | Some gap, [] -> failwith $"{id} now holds the invariant (recorded gap: {gap}); remove it from knownGaps"

    [<Fact>]
    let ``the known-gap list equals the set of failing cells`` () =
        let failing = failingCases ()
        let unexpected = failing |> Map.filter (fun id _ -> not (knownGaps().ContainsKey id))
        let fixedGaps = knownGaps () |> Map.filter (fun id _ -> not (failing.ContainsKey id))

        let describe (entries: Map<string, string>) =
            entries |> Seq.map (fun pair -> $"  {pair.Key}: {pair.Value}") |> String.concat Environment.NewLine

        if not unexpected.IsEmpty || not fixedGaps.IsEmpty then
            failwith (
                $"{failing.Count} failing of {(cells ()).Length * (modes ()).Length} cases.{Environment.NewLine}"
                + $"Failing but not in knownGaps:{Environment.NewLine}{describe unexpected}{Environment.NewLine}"
                + $"In knownGaps but holding now:{Environment.NewLine}{describe fixedGaps}"
            )

    [<Fact>]
    let ``every cell name is unique and every known gap names a case`` () =
        let names = cells () |> List.map _.Name
        let duplicates = names |> List.countBy id |> List.filter (fun (_, count) -> count > 1) |> List.map fst
        let ids = set [ for cell in cells () do for mode in modes () -> caseId cell mode ]
        let strayGaps = knownGaps () |> Map.toList |> List.map fst |> List.filter (fun id -> not (ids.Contains id))
        Assert.Equal<string list>([], duplicates)
        Assert.Equal<string list>([], strayGaps)

    /// Fails when a public member of Flow, FlowStream, Layer, Cache, Scope, Hub, Queue, Dequeue, Schedule, Retry or
    /// Resource has neither a cell nor an exemption, or when a cell or exemption names a member that does not exist.
    [<Fact>]
    let ``every public combinator is in the throw-safety table`` () =
        let members = publicMembers ()
        let targets = cells () |> List.map cellTarget |> Set.ofList
        let missing = members - targets - (exemptions () |> Map.keys |> Set.ofSeq)

        let strayCells =
            targets |> Set.filter (fun target -> not (members.Contains target) && not (pseudoTargets().Contains(target.Split('.')[0])))

        let strayExemptions = exemptions () |> Map.keys |> Set.ofSeq |> Set.filter (fun name -> not (members.Contains name))
        let doubled = Set.intersect targets (exemptions () |> Map.keys |> Set.ofSeq)

        let coveredByMissing =
            exemptions ()
            |> Map.toList
            |> List.choose (fun (name, exemption) ->
                match exemption with
                | CoveredBy target when not (targets.Contains target) -> Some $"{name} -> {target}"
                | _ -> None)

        Assert.Equal<string list>([], Set.toList missing)
        Assert.Equal<string list>([], Set.toList strayCells)
        Assert.Equal<string list>([], Set.toList strayExemptions)
        Assert.Equal<string list>([], Set.toList doubled)
        Assert.Equal<string list>([], coveredByMissing)
