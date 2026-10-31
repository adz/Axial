namespace Axial.PlatformService

open System
open Axial

#if !FABLE_COMPILER
/// A recorded read identifies the runtime service member and the fiber which read it.
type TestPlatform private () =
    let mutable nextGuid = 0
    let guidGate = obj()
    let reads = ResizeArray<string * int64>()
    member val Clock = ManualClock(DateTimeOffset.UnixEpoch)
    member val Log = TestLog()
    member val Random: IRandom = Random.seeded 0 with get, set
    member val Guid: IGuid =
        { new IGuid with
            member _.NewGuid() = lock guidGate (fun () -> nextGuid <- nextGuid + 1; System.Guid(nextGuid, 0s, 0s, Array.zeroCreate 8)) } with get, set
    /// Enable recording before running the test. Reads are available in call order.
    member val Trace = false with get, set
    /// A stable snapshot; later reads never mutate a previously captured snapshot.
    member _.Reads : System.Collections.Generic.IReadOnlyList<string * int64> =
        lock reads (fun () -> reads.ToArray() :> System.Collections.Generic.IReadOnlyList<string * int64>)
    member internal _.Record(name, id) = lock reads (fun () -> reads.Add(name, id))
    static member create () = TestPlatform()

type private TracedClock(clock: ManualClock, record: string -> unit) =
    member _.Clock = clock
    interface IClock with
        member _.UtcNow() = record "Clock.UtcNow"; (clock :> IClock).UtcNow()
        member _.Elapsed() = record "Clock.Elapsed"; (clock :> IClock).Elapsed()
        member _.Sleep(delay, token) = record "Clock.Sleep"; (clock :> IClock).Sleep(delay, token)

[<AutoOpen>]
module TestPlatformExtensions =
    module Flow =
        /// Runs with explicitly controlled platform values; it never advances time automatically.
        let runTestWith (platform: TestPlatform) (environment: 'env) (source: Flow<'env, 'error, 'value>) : Exit<'value, 'error> =
            let record name =
                if platform.Trace then
                    let id = RuntimePlatform.current().FiberId.Value
                    platform.Record(name, id)
            let random = platform.Random
            let guid = platform.Guid
            let tracedRandom =
                { new IRandom with
                    member _.Next() = record "Random.Next"; random.Next()
                    member _.NextMax maximum = record "Random.NextMax"; random.NextMax maximum
                    member _.NextInt minimum maximum = record "Random.NextInt"; random.NextInt minimum maximum
                    member _.NextDouble() = record "Random.NextDouble"; random.NextDouble()
                    member _.NextBytes buffer = record "Random.NextBytes"; random.NextBytes buffer }
            let tracedGuid = { new IGuid with member _.NewGuid() = record "Guid.NewGuid"; guid.NewGuid() }
            let installed =
                source
                |> Flow.withClock (if platform.Trace then TracedClock(platform.Clock, record) :> IClock else platform.Clock :> IClock)
                |> Flow.withRandom (if platform.Trace then tracedRandom else random)
                |> Flow.withGuid (if platform.Trace then tracedGuid else guid)
                |> Flow.withLogSink platform.Log
            installed.RunSynchronously environment

        /// A fresh platform for each run: Unix epoch, seeded random, sequential GUIDs, and a recording sink.
        let runTest (environment: 'env) (source: Flow<'env, 'error, 'value>) : Exit<'value, 'error> =
            runTestWith (TestPlatform.create ()) environment source

[<RequireQualifiedAccess>]
module TestClock =
    let private current () =
        match RuntimePlatform.current().Platform.Clock with
        | :? ManualClock as clock -> clock
        | :? TracedClock as clock -> clock.Clock
        | _ -> invalidOp "TestClock requires a ManualClock; install one with Flow.withClock or Flow.runTest."

    /// Releases due continuations before returning; never infers idleness from wall time.
    let advance (duration: TimeSpan) : Flow<'env, 'error, unit> =
        Flow.delay (fun () -> (current ()).AdvanceBy duration; Flow.ok ())

    /// Advances no later than the latest deadline registered at entry; periodic re-registration cannot extend this horizon.
    let advanceAll<'env, 'error> : Flow<'env, 'error, unit> =
        Flow.delay (fun () ->
            let clock = current ()
            let horizon = clock.LastDeadline
            let mutable draining = true
            while draining do
                match clock.NextDeadline, horizon with
                | Some next, Some last when next <= last -> clock.AdvanceToNextDeadline() |> ignore
                | _ -> draining <- false
            Flow.ok ())

    let sleepers<'env, 'error> : Flow<'env, 'error, int> =
        Flow.delay (fun () -> Flow.ok (current ()).Sleepers)
#endif
