namespace Axial.PlatformService

open System
open Axial

#if !FABLE_COMPILER
/// Explicitly controlled test services. Time advances only when the test requests it.
type TestPlatform private () =
    member val Clock = ManualClock(DateTimeOffset.UnixEpoch)
    member val Log = TestLog()
    member val Random: IRandom = Random.fromValue 0 with get, set
    member val Guid: IGuid = Guid.fromValue System.Guid.Empty with get, set
    member val trace = false with get, set
    member val Reads = ResizeArray<string * int64>()
    static member create () = TestPlatform()

[<AutoOpen>]
module TestPlatformExtensions =
    module Flow =
        let runTestWith (platform: TestPlatform) (environment: 'env) (source: Flow<'env, 'error, 'value>) : Exit<'value, 'error> =
            failwith "T25a: not implemented"
        let runTest (environment: 'env) (source: Flow<'env, 'error, 'value>) : Exit<'value, 'error> =
            failwith "T25a: not implemented"

[<RequireQualifiedAccess>]
module TestClock =
    let advance (duration: TimeSpan) : Flow<'env, 'error, unit> = Flow.delay (fun () -> failwith "T25a: not implemented")
    let advanceAll<'env, 'error> : Flow<'env, 'error, unit> = Flow.delay (fun () -> failwith "T25a: not implemented")
    let sleepers<'env, 'error> : Flow<'env, 'error, int> = Flow.delay (fun () -> failwith "T25a: not implemented")
#endif
