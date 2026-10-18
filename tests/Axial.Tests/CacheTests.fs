namespace Axial.Tests

open System
open System.Threading
open System.Threading.Tasks
open Axial
open Swensen.Unquote
open Xunit

module CacheTests =
    [<Fact>]
    let ``bounded cache refreshes LRU and limits count`` () =
        let calls = System.Collections.Generic.Dictionary<int, int>()
        let load key : Flow<ClockEnvironment, string, int> =
            Flow.delay (fun () ->
                let count = (if calls.ContainsKey key then calls[key] else 0) + 1
                calls[key] <- count
                Flow.ok count)
        let workflow = flow {
            let! cache = Cache.makeBounded 2 load
            let! _ = cache |> Cache.get 1
            let! _ = cache |> Cache.get 2
            let! _ = cache |> Cache.get 1
            let! _ = cache |> Cache.get 3
            let! size = Cache.count cache
            let! reloaded = cache |> Cache.get 2
            let! sizeAfter = Cache.count cache
            return size, reloaded, sizeAfter, calls[1]
        }
        test <@ Flow.runSync (TestSupport.clockEnv ()) workflow = Exit.Success(2, 2, 2, 1) @>

    [<Fact>]
    let ``recursive cache reads nested keys only once`` () =
        let calls = ref 0
        let lookup cache key : Flow<ClockEnvironment, string, int> = flow {
            if key = 0 then
                calls.Value <- calls.Value + 1
                return 10
            else
                let! inner = cache |> Cache.get 0
                let! again = cache |> Cache.get 0
                return inner + again
        }
        let workflow = flow {
            let! cache = Cache.makeRec lookup
            let! value = cache |> Cache.get 1
            return value
        }
        test <@ Flow.runSync (TestSupport.clockEnv ()) workflow = Exit.Success 20 @>
        test <@ calls.Value = 1 @>

    [<Fact>]
    let ``Cache runs one lookup for concurrent callers of the same key`` () =
        let lookups = ref 0

        let load (key: int) : Flow<ClockEnvironment, string, string> =
            flow {
                Interlocked.Increment(&lookups.contents) |> ignore
                do! Flow.sleep (TimeSpan.FromMilliseconds 30.0)
                return $"value-{key}"
            }

        let workflow =
            flow {
                let! cache = Cache.make load
                let! values = [ 1; 1; 1; 2 ] |> Flow.traversePar (Parallelism.bounded 4) (fun key -> cache |> Cache.get key)
                let! again = cache |> Cache.get 1
                let! count = Cache.count cache
                return values, again, count
            }

        test <@ Flow.runSync (TestSupport.clockEnv ()) workflow = Exit.Success([ "value-1"; "value-1"; "value-1"; "value-2" ], "value-1", 2) @>
        test <@ lookups.Value = 2 @>

    [<Fact>]
    let ``Cache does not keep failures`` () =
        let attempts = ref 0

        let load (_: string) : Flow<ClockEnvironment, string, int> =
            Flow.delay (fun () ->
                attempts.Value <- attempts.Value + 1
                if attempts.Value = 1 then Flow.fail "unavailable" else Flow.ok attempts.Value)

        let workflow =
            flow {
                let! cache = Cache.make load
                let! first = cache |> Cache.get "k" |> Flow.fold (Ok >> Flow.ok) (fun _ -> Flow.ok (Error "failed"))
                let! second = cache |> Cache.get "k"
                let! third = cache |> Cache.get "k"
                return first, second, third
            }

        test <@ Flow.runSync (TestSupport.clockEnv ()) workflow = Exit.Success(Error "failed", 2, 2) @>

    [<Fact>]
    let ``Interrupting one caller does not cancel the lookup others wait for`` () =
        let completed = ref false
        let started = TaskCompletionSource<unit>(TaskCreationOptions.RunContinuationsAsynchronously)
        let release = TaskCompletionSource<unit>(TaskCreationOptions.RunContinuationsAsynchronously)

        let load (_: int) : Flow<ClockEnvironment, string, string> =
            flow {
                started.SetResult(())
                do! Flow.fromTask (fun token -> release.Task.WaitAsync token)
                completed.Value <- true
                return "shared"
            }

        let workflow =
            flow {
                let! cache = Cache.make load
                let! impatient = Flow.fork (cache |> Cache.get 1)
                do! Flow.fromTask (fun token -> started.Task.WaitAsync(TimeSpan.FromSeconds 10.0, token))
                let! patient = Flow.fork (cache |> Cache.get 1)
                let! interrupted = Fiber.interrupt impatient
                release.SetResult(())
                let! value = Fiber.join patient
                return interrupted, value
            }

        test <@ Flow.runSync (TestSupport.clockEnv ()) workflow = Exit.Success(Exit.Failure Cause.Interrupt, "shared") @>
        test <@ completed.Value @>

    [<Fact>]
    let ``Cache invalidate forces a fresh lookup`` () =
        let lookups = ref 0

        let workflow =
            flow {
                let! cache = Cache.make (fun (_: int) -> Flow.delay (fun () -> lookups.Value <- lookups.Value + 1; Flow.ok lookups.Value))
                let! first = cache |> Cache.get 1
                do! cache |> Cache.invalidate 1
                let! second = cache |> Cache.get 1
                do! Cache.invalidateAll cache
                let! count = Cache.count cache
                return first, second, count
            }

        test <@ (Flow.runSync (TestSupport.clockEnv ()) workflow : Exit<int * int * int, string>) = Exit.Success(1, 2, 0) @>

    [<Fact>]
    let ``memoize shares one run and retries after failure`` () =
        let runs = ref 0

        let source : Flow<ClockEnvironment, string, int> =
            Flow.delay (fun () ->
                runs.Value <- runs.Value + 1
                if runs.Value = 1 then Flow.fail "cold" else Flow.ok runs.Value)

        let workflow =
            flow {
                let! shared = Flow.memoize source
                let! first = shared |> Flow.fold (Ok >> Flow.ok) (fun _ -> Flow.ok (Error "failed"))
                let! second = shared
                let! third = shared
                return first, second, third
            }

        test <@ Flow.runSync (TestSupport.clockEnv ()) workflow = Exit.Success(Error "failed", 2, 2) @>
        test <@ runs.Value = 2 @>
