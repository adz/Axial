namespace Axial.Tests

open System
open System.IO
open Axial
open Axial.PlatformService
open Xunit

module PlatformScopeTests =
    let private environment () = ()
    let private reads () : Flow<unit, string, DateTimeOffset * int * System.Guid> =
        flow {
            let! now = Clock.now
            let! random = Random.next
            let! guid = Guid.newGuid
            return now, random, guid
        }

    [<Fact>]
    let ``runTest installs deterministic nonambient defaults`` () =
        let first = reads () |> Flow.runTest (environment ())
        let second = reads () |> Flow.runTest (environment ())
        Assert.Equal(first, second)
        match first with
        | Exit.Success(now, _, _) -> Assert.Equal(DateTimeOffset.UnixEpoch, now)
        | other -> failwithf "%A" other

    [<Theory>]
    [<InlineData("plain")>]
    [<InlineData("race")>]
    [<InlineData("zipPar")>]
    [<InlineData("disconnect")>]
    [<InlineData("runOn")>]
    let ``platform overrides reach descendants and restore outer scope`` (branch: string) =
        let epoch = DateTimeOffset.UnixEpoch
        let innerTime = epoch.AddDays 7.
        let innerId = System.Guid.Parse "00000000-0000-0000-0000-000000000042"
        let forked = flow {
            let! child = reads () |> Flow.fork
            return! Fiber.join child
        }
        let descendant =
            match branch with
            | "race" -> Flow.race forked Flow.never
            | "zipPar" -> Flow.zipPar forked (Flow.ok ()) |> Flow.map fst
            | "disconnect" -> Flow.disconnect forked
            | "runOn" -> Flow.runOn RunOn.ThreadPool forked
            | _ -> forked
        let workflow = flow {
            let! inside = descendant
                          |> Flow.withClock (ManualClock(innerTime))
                          |> Flow.withRandom (Random.fromValue 42)
                          |> Flow.withGuid (Guid.fromValue innerId)
            let! outside = reads ()
            return inside, outside
        }
        match workflow |> Flow.runTest (environment ()) with
        | Exit.Success(inside, (outsideTime, outsideRandom, outsideId)) ->
            Assert.Equal((innerTime, 42, innerId), inside)
            Assert.Equal(epoch, outsideTime)
            Assert.NotEqual(42, outsideRandom)
            Assert.NotEqual(innerId, outsideId)
        | other -> failwithf "%A" other

    [<Fact>]
    let ``explicit stepping wakes a sleeper only at its deadline`` () =
        let platform = TestPlatform.create ()
        let mutable completed = false
        let workflow = flow {
            let! fiber = (Flow.sleep (TimeSpan.FromSeconds 5.) |> Flow.map (fun () -> completed <- true)) |> Flow.fork
            // Await the registration fact, not elapsed wall time.
            do! Flow.fromTask (fun _ -> task {
                Assert.True(System.Threading.SpinWait.SpinUntil((fun () -> platform.Clock.Sleepers = 1), 5000))
                return ()
            })
            do! TestClock.advance (TimeSpan.FromSeconds 4.)
            do! Flow.delay (fun () -> Assert.False completed; Flow.ok ())
            do! TestClock.advance (TimeSpan.FromSeconds 1.)
            do! Fiber.join fiber
            return completed
        }
        Assert.Equal(Exit.Success true, workflow |> Flow.runTestWith platform (environment ()))

    [<Fact>]
    let ``throwing observation slots cannot change the result`` () =
        let sink = { new ILogSink with
                        member _.IsEnabled _ = true
                        member _.Write _ = failwith "sink" }
        let telemetry = { new ITelemetry with
                            member _.Annotate(_, _) = failwith "annotation"
                            member _.Attribute _ = failwith "attribute" }
        let workflow = flow {
            do! Log.info "observed"
            return 42
        }
        let actual = workflow |> Flow.annotate "trace_id" "test"
                              |> Axial.Telemetry.Context.withEndUserId "user"
                              |> Flow.withLogSink sink |> Flow.withTelemetry telemetry
        Assert.Equal(Exit.Success 42, actual |> Flow.runSync (environment ()))

    [<Fact>]
    let ``disabled sink does not receive an entry`` () =
        let mutable writes = 0
        let sink = { new ILogSink with
                        member _.IsEnabled _ = false
                        member _.Write _ = writes <- writes + 1; failwith "disabled Write" }
        let result = Log.info "disabled" |> Flow.withLogSink sink |> Flow.runSync (environment ())
        Assert.Equal(Exit.Success (), result)
        Assert.Equal(0, writes)

    [<Fact>]
    let ``log entries snapshot annotations and named locals`` () =
        let entries = ResizeArray<Axial.LogEntry>()
        let local = FiberLocal.named "request" "root"
        let workflow = Log.info "snapshot"
                       |> FiberLocal.locally "scoped" local
                       |> Flow.annotate "trace_id" "abc"
                       |> Flow.withLogSink (LogSink.fromFunction entries.Add)
        Assert.Equal(Exit.Success (), workflow |> Flow.runSync (environment ()))
        Assert.Single entries |> ignore
        Assert.Equal("abc", entries[0].Annotations["trace_id"])
        Assert.Contains(("request", box "scoped"), entries[0].Locals)

    [<Fact>]
    let ``log sink overrides reach forks and restore outer scope`` () =
        let outer = ResizeArray<Axial.LogEntry>()
        let inner = ResizeArray<Axial.LogEntry>()
        let workflow = flow {
            let! child = Log.info "inner" |> Flow.fork |> Flow.withLogSink (LogSink.fromFunction inner.Add)
            do! Fiber.join child
            do! Log.info "outer"
        }
        Assert.Equal(Exit.Success (), workflow |> Flow.withLogSink (LogSink.fromFunction outer.Add) |> Flow.runSync (environment ()))
        Assert.Equal("inner", (Assert.Single inner).Message)
        Assert.Equal("outer", (Assert.Single outer).Message)

    [<Fact>]
    let ``default console sink writes info and above only to stderr`` () =
        let originalOut, originalError = Console.Out, Console.Error
        use stdout = new StringWriter()
        use stderr = new StringWriter()
        try
            Console.SetOut stdout
            Console.SetError stderr
            let workflow = flow {
                do! Log.debug "hidden"
                do! Log.info "visible"
            }
            Assert.Equal(Exit.Success (), workflow |> Flow.runSync (environment ()))
            Assert.Equal("", stdout.ToString())
            Assert.Contains("visible", stderr.ToString())
            Assert.DoesNotContain("hidden", stderr.ToString())
        finally
            Console.SetOut originalOut
            Console.SetError originalError

    [<Fact>]
    let ``telemetry overrides replace both slots and restore the outer slots`` () =
        let outer, inner = ResizeArray<string>(), ResizeArray<string>()
        let sink (entries: ResizeArray<string>) =
            { new ITelemetry with
                member _.Annotate(name, _) = entries.Add name
                member _.Attribute attribute = entries.Add attribute.Name }
        let workflow = flow {
            do! Flow.ok () |> Flow.annotate "inner" "yes" |> Flow.withTelemetry (sink inner)
            do! Flow.ok () |> Flow.annotate "outer" "yes"
        }
        Assert.Equal(Exit.Success (), workflow |> Flow.withTelemetry (sink outer) |> Flow.runSync (environment ()))
        Assert.Equal<string list>(["inner"], List.ofSeq inner)
        Assert.Equal<string list>(["outer"], List.ofSeq outer)

    [<Fact>]
    let ``disabled logging execution allocates no entries or execution outcomes`` () =
        let sink = { new ILogSink with
                        member _.IsEnabled _ = false
                        member _.Write _ = failwith "disabled" }
        let runtime = { RuntimeContext.create (new Scope()) with LogSink = sink }
        let source: Flow<unit, string, unit> = Log.info "disabled"
        // Measure the primitive's execution, excluding Invoke's existing stack-guard closure.
        let execute = match source with Flow operation -> operation | _ -> failwith "Expected primitive log flow"
        RuntimeState.withRuntime runtime (fun () ->
            for _ in 1 .. 100 do execute () Threading.CancellationToken.None |> ignore
            let before = GC.GetAllocatedBytesForCurrentThread()
            for _ in 1 .. 1000 do execute () Threading.CancellationToken.None |> ignore
            let allocated = GC.GetAllocatedBytesForCurrentThread() - before
            Assert.Equal(0L, allocated))

    [<Fact>]
    let ``timed primitives compile and run under unit without a clock interface`` () =
        let workflow: Flow<unit, string, unit> =
            Flow.sleep TimeSpan.Zero
            |> Flow.timeout (TimeSpan.FromHours 1.) "timeout"
            |> Flow.retry (Schedule.recurs 0)
            |> Flow.repeat (Schedule.recurs 0)
            |> Flow.supervise (Schedule.recurs 0)
        Assert.Equal(Exit.Success (), workflow |> Flow.runTest ())

    [<Fact>]
    let ``test platform trace identifies service reads and fibers`` () =
        let platform = TestPlatform.create ()
        platform.trace <- true
        reads () |> Flow.runTestWith platform (environment ()) |> ignore
        Assert.Contains(platform.Reads, fun (service, _) -> service = "Clock.UtcNow")
        Assert.Contains(platform.Reads, fun (service, _) -> service = "Random.Next")
        Assert.Contains(platform.Reads, fun (service, _) -> service = "Guid.NewGuid")
