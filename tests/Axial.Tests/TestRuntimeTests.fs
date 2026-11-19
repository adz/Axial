namespace Axial.Tests

open System
open Axial
open Axial
open Swensen.Unquote
open Xunit

module TestRuntimeTests =
    [<Fact>]
    let ``memory log records entries in order`` () =
        let log = Log.memory ()
        Log.info "hello" |> Flow.withLogSink log |> Flow.runSync () |> ignore
        Log.errorExn (InvalidOperationException "boom") "bad" |> Flow.withLogSink log |> Flow.runSync () |> ignore
        test <@ log.Entries.Count = 2 @>
        test <@ log.Entries[0] = { Level = LogLevel.Information; Message = "hello"; Error = None; Annotations = Map.empty; Locals = [] } @>
        test <@ log.Entries[1].Level = LogLevel.Error @>
        test <@ log.Entries[1].Message = "bad" @>
        test <@ log.Entries[1].Error.Value.Message = "boom" @>
        log.Clear()
        test <@ log.Entries.Count = 0 @>

    [<Fact>]
    let ``memory log installs as a runtime destination`` () =
        let log = Log.memory ()
        let result = Log.info "recorded" |> Flow.withLogSink log |> Flow.runSync ()
        test <@ result = Exit.Success () @>
        test <@ log.Entries.Count = 1 @>
        test <@ log.Entries[0].Message = "recorded" @>

    [<Fact>]
    let ``scripted random returns values in order then throws`` () =
        let random = Random.fromSequence [1; 2; 3]
        test <@ random.Next() = 1 @>
        test <@ random.NextMax 10 = 2 @>
        test <@ random.NextInt 0 10 = 3 @>
        Assert.Throws<InvalidOperationException>(fun () -> random.Next() |> ignore) |> ignore

    [<Fact>]
    let ``scripted guid returns values in order then throws`` () =
        let first = System.Guid.Parse "11111111-1111-1111-1111-111111111111"
        let second = System.Guid.Parse "22222222-2222-2222-2222-222222222222"
        let guid = Guid.sequence [first; second]
        test <@ guid.NewGuid() = first @>
        test <@ guid.NewGuid() = second @>
        Assert.Throws<InvalidOperationException>(fun () -> guid.NewGuid() |> ignore) |> ignore

    [<Fact>]
    let ``test platform controls services without replacing the environment`` () =
        let platform = TestPlatform.create ()
        platform.Random <- Random.fromSequence [7]
        let id = System.Guid.Parse "33333333-3333-3333-3333-333333333333"
        platform.Guid <- Guid.sequence [id]
        let workflow = flow {
            let! now = Clock.now
            let! random = Random.next
            let! guid = Guid.newGuid
            do! Log.info "ran"
            let! environment = Flow.env
            return now, random, guid, environment
        }
        test <@ workflow |> Flow.runTestWith platform "app" = Exit.Success(DateTimeOffset.UnixEpoch, 7, id, "app") @>
        test <@ platform.Log.Entries.Count = 1 @>

    [<Fact>]
    let ``test platform clock advances deterministically`` () =
        let platform = TestPlatform.create ()
        platform.Clock.AdvanceBy (TimeSpan.FromSeconds 5.)
        test <@ Clock.now |> Flow.runTestWith platform () = Exit.Success(DateTimeOffset.UnixEpoch.AddSeconds 5.) @>

    [<Fact>]
    let ``clock override composes with an application layer`` () =
        let workflow = flow {
            let! value = Flow.env
            let! now = Clock.now
            return value, now
        }
        let result = workflow |> Layer.provide (Layer.succeed "app")
                              |> Flow.withClock (Clock.fromValue DateTimeOffset.UnixEpoch) |> Flow.runSync ()
        test <@ result = Exit.Success("app", DateTimeOffset.UnixEpoch) @>

    [<Fact>]
    let ``createAt starts the manual clock at the supplied time`` () =
        let start = DateTimeOffset(2026, 1, 2, 3, 4, 5, TimeSpan.Zero)
        test <@ Clock.now |> Flow.runTestWith (TestPlatform.createAt start) () = Exit.Success start @>

    [<Fact>]
    let ``awaitSleepers waits until the sleeps are registered`` () =
        let workflow =
            flow {
                let sleeper = Flow.sleep (TimeSpan.FromMinutes 1.0)
                let! first = Flow.fork sleeper
                let! second = Flow.fork sleeper
                do! TestClock.awaitSleepers 2
                let! count = TestClock.sleepers
                do! TestClock.advanceAll
                do! Fiber.join first
                do! Fiber.join second
                return count
            }

        test <@ (workflow : Flow<unit, string, int>) |> Flow.runTest () = Exit.Success 2 @>

    [<Fact>]
    let ``driveUntilDone returns when the fiber settles without sleeping`` () =
        // The fiber waits on a deferred that real work completes, so no sleep is ever registered on the manual clock.
        let workflow =
            flow {
                let! deferred = Deferred.make<unit, string, int> ()
                let! waiting = Deferred.await deferred |> Flow.fork

                let! _ =
                    Flow.fromUnitTask (fun token -> System.Threading.Tasks.Task.Delay(20, token))
                    |> Flow.bind (fun () -> Deferred.succeed 7 deferred)
                    |> Flow.fork

                return! TestClock.driveUntilDone waiting
            }

        let running = System.Threading.Tasks.Task.Run(fun () -> workflow |> Flow.runTest ())
        test <@ running.Wait(TimeSpan.FromSeconds 10.0) @>
        test <@ running.Result = Exit.Success(Exit.Success 7) @>
