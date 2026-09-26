namespace Axial.Tests

open System
open System.Threading
open Axial
open Swensen.Unquote
open Xunit

module CacheTests =
    [<Fact>]
    let ``Cache runs one lookup for concurrent callers of the same key`` () =
        let lookups = ref 0

        let load (key: int) : Flow<unit, string, string> =
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

        test <@ Flow.runSync () workflow = Exit.Success([ "value-1"; "value-1"; "value-1"; "value-2" ], "value-1", 2) @>
        test <@ lookups.Value = 2 @>

    [<Fact>]
    let ``Cache does not keep failures`` () =
        let attempts = ref 0

        let load (_: string) : Flow<unit, string, int> =
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

        test <@ Flow.runSync () workflow = Exit.Success(Error "failed", 2, 2) @>

    [<Fact>]
    let ``Interrupting one caller does not cancel the lookup others wait for`` () =
        let completed = ref false

        let load (_: int) : Flow<unit, string, string> =
            flow {
                do! Flow.sleep (TimeSpan.FromMilliseconds 60.0)
                completed.Value <- true
                return "shared"
            }

        let workflow =
            flow {
                let! cache = Cache.make load
                let! impatient = Flow.fork (cache |> Cache.get 1)
                let! patient = Flow.fork (cache |> Cache.get 1)
                do! Flow.sleep (TimeSpan.FromMilliseconds 10.0)
                let! interrupted = Fiber.interrupt impatient
                let! value = Fiber.join patient
                return interrupted, value
            }

        test <@ Flow.runSync () workflow = Exit.Success(Exit.Failure Cause.Interrupt, "shared") @>
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

        test <@ (Flow.runSync () workflow : Exit<int * int * int, string>) = Exit.Success(1, 2, 0) @>

    [<Fact>]
    let ``memoize shares one run and retries after failure`` () =
        let runs = ref 0

        let source : Flow<unit, string, int> =
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

        test <@ Flow.runSync () workflow = Exit.Success(Error "failed", 2, 2) @>
        test <@ runs.Value = 2 @>
