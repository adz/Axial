namespace Axial.Tests

open System
open System.IO
open Axial
open Axial.PlatformService
open Xunit

module PlatformScopeTests =
    let private environment () = ()

    let private signallingClock (clock: IClock) (registered: Threading.SemaphoreSlim) =
        { new IClock with
            member _.UtcNow() = clock.UtcNow()
            member _.Elapsed() = clock.Elapsed()
            member _.Sleep(delay, token) =
                let pending = clock.Sleep(delay, token)
                registered.Release() |> ignore
                pending }

    let private awaitRegistration (registered: Threading.SemaphoreSlim) =
        Flow.fromTask (fun token -> task {
            let! signalled = registered.WaitAsync(TimeSpan.FromSeconds 5., token)
            Assert.True signalled
            return ()
        })
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
        use registered = new Threading.SemaphoreSlim(0)
        let notifying = signallingClock platform.Clock registered
        let mutable completed = false
        let workflow = flow {
            let! fiber = (Flow.sleep (TimeSpan.FromSeconds 5.) |> Flow.map (fun () -> completed <- true))
                         |> Flow.fork |> Flow.withClock notifying
            do! awaitRegistration registered
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

    [<Theory>]
    [<InlineData("plain")>]
    [<InlineData("race")>]
    [<InlineData("zipPar")>]
    [<InlineData("disconnect")>]
    [<InlineData("runOn")>]
    let ``log sink overrides reach forks and restore outer scope`` (branch: string) =
        let outer = ResizeArray<Axial.LogEntry>()
        let inner = ResizeArray<Axial.LogEntry>()
        let forked = flow {
            let! child = Log.info "inner" |> Flow.fork
            do! Fiber.join child
        }
        let descendant =
            match branch with
            | "race" -> Flow.race forked Flow.never
            | "zipPar" -> Flow.zipPar forked (Flow.ok ()) |> Flow.map fst
            | "disconnect" -> Flow.disconnect forked
            | "runOn" -> Flow.runOn RunOn.ThreadPool forked
            | _ -> forked
        let workflow = flow {
            do! descendant |> Flow.withLogSink (LogSink.fromFunction inner.Add)
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
            do! Flow.ok () |> Flow.annotate "inner" "yes"
                           |> Axial.Telemetry.Context.withEndUserId "inner-user"
                           |> Flow.withTelemetry (sink inner)
            do! Flow.ok () |> Flow.annotate "outer" "yes"
                           |> Axial.Telemetry.Context.withEndUserId "outer-user"
        }
        Assert.Equal(Exit.Success (), workflow |> Flow.withTelemetry (sink outer) |> Flow.runSync (environment ()))
        Assert.Equal<string list>(["enduser.id"; "inner"], List.ofSeq inner)
        Assert.Equal<string list>(["enduser.id"; "outer"], List.ofSeq outer)

    [<Fact>]
    let ``tee guards each destination independently including a throwing gate`` () =
        let entries = ResizeArray<Axial.LogEntry>()
        let gate = { new ILogSink with
                        member _.IsEnabled _ = failwith "gate"
                        member _.Write _ = failwith "unreachable" }
        let write = LogSink.fromFunction (fun _ -> failwith "write")
        let sink = LogSink.tee gate (LogSink.tee write (LogSink.fromFunction entries.Add))
        Assert.Equal(Exit.Success (), Log.info "survived" |> Flow.withLogSink sink |> Flow.runSync ())
        Assert.Equal("survived", (Assert.Single entries).Message)

    [<Fact>]
    let ``sink decorators compose without changing the outer destination`` () =
        let entries = ResizeArray<Axial.LogEntry>()
        let workflow = flow {
            do! Log.debug "disabled"
            do! Log.info "drop"
            do! Log.info "keep" |> Flow.mapLogSink (LogSink.map (fun entry -> { entry with Message = "mapped:" + entry.Message }))
            do! Log.info "outside"
        }
        let sink = LogSink.fromFunction entries.Add |> LogSink.filter (fun entry -> entry.Message <> "drop") |> LogSink.minLevel LogLevel.Information
        Assert.Equal(Exit.Success (), workflow |> Flow.withLogSink sink |> Flow.runSync ())
        Assert.Equal<string list>(["mapped:keep"; "outside"], entries |> Seq.map _.Message |> Seq.toList)

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
    let ``advanceAll explicitly drains already registered deadlines`` () =
        let platform = TestPlatform.create ()
        use registered = new Threading.SemaphoreSlim(0)
        let notifying = signallingClock platform.Clock registered
        let workflow = flow {
            let! first = Flow.sleep (TimeSpan.FromSeconds 5.) |> Flow.fork |> Flow.withClock notifying
            let! second = Flow.sleep (TimeSpan.FromSeconds 7.) |> Flow.fork |> Flow.withClock notifying
            do! awaitRegistration registered
            do! awaitRegistration registered
            do! TestClock.advanceAll
            do! Fiber.join first
            do! Fiber.join second
            return! Clock.elapsed
        }
        Assert.Equal(Exit.Success(TimeSpan.FromSeconds 7.), workflow |> Flow.runTestWith platform ())

    [<Fact>]
    let ``advanceAll stops when a sleeper registers beyond its entry horizon`` () =
        let platform = TestPlatform.create ()
        use registered = new Threading.SemaphoreSlim(0)
        let notifying = signallingClock platform.Clock registered
        let workflow = flow {
            let! child =
                (flow {
                    do! Flow.sleep (TimeSpan.FromSeconds 1.)
                    do! Flow.sleep (TimeSpan.FromSeconds 1.)
                }) |> Flow.fork |> Flow.withClock notifying
            do! awaitRegistration registered
            do! TestClock.advanceAll
            let! elapsed = Clock.elapsed
            do! Fiber.interrupt child |> Flow.ignore
            return elapsed
        }
        Assert.Equal(Exit.Success(TimeSpan.FromSeconds 1.), workflow |> Flow.runTestWith platform ())

    [<Fact>]
    let ``fixed clock sleeps until explicitly advanced`` () =
        let clock = Clock.``fixed`` DateTimeOffset.UnixEpoch
        let pending = (clock :> IClock).Sleep(TimeSpan.FromSeconds 1., Threading.CancellationToken.None)
        Assert.False(pending.IsCompleted)
        clock.AdvanceBy(TimeSpan.FromSeconds 1.)
        Assert.True(pending.IsCompletedSuccessfully)

    [<Fact>]
    let ``mapped log level is checked before writing`` () =
        let log = TestLog()
        let gated =
            { new ILogSink with
                member _.IsEnabled level = level >= LogLevel.Warning
                member _.Write entry = (log :> ILogSink).Write entry }
        let sink = gated |> LogSink.map (fun entry -> { entry with Level = LogLevel.Debug })
        sink.Write { Level = LogLevel.Error; Message = "mapped"; Error = None; Annotations = Map.empty; Locals = [] }
        Assert.Empty(log.Entries)

    [<Fact>]
    let ``recording log entries are snapshots`` () =
        let log = TestLog()
        log.Log(LogLevel.Information, "first")
        let snapshot = log.Entries
        log.Clear()
        log.Log(LogLevel.Information, "second")
        Assert.Single(snapshot) |> ignore
        Assert.Equal("first", snapshot.[0].Message)

    [<Fact>]
    let ``watchdog stall dump names a sleeper when nobody advances time`` () =
        let platform = TestPlatform.create ()
        let registry = FiberRegistry()
        use registered = new Threading.SemaphoreSlim(0)
        let notifying = signallingClock platform.Clock registered
        let workflow = flow {
            let! child = Flow.sleep (TimeSpan.FromSeconds 5.) |> Flow.forkNamed "unadvanced-sleeper" |> Flow.withClock notifying
            do! awaitRegistration registered
            // A real-time watchdog observes the stalled manual-time fiber without joining it.
            do! Flow.sleep (TimeSpan.FromMilliseconds 20.) |> Flow.withClock Clock.live
            let dump = registry.DumpAt(platform.Clock)
            let stalled = platform.Clock.Sleepers = 1 && registry.LiveFiberCount > 0
            do! Fiber.interrupt child |> Flow.ignore
            return stalled, dump
        }
        match workflow |> Flow.withFiberRegistry registry |> Flow.runTestWith platform () with
        | Exit.Success(true, dump) -> Assert.Contains("unadvanced-sleeper", dump)
        | other -> failwithf "Expected watchdog stall, got %A" other

    [<Fact>]
    let ``TestClock rejects a live clock with a clear defect`` () =
        match TestClock.advance (TimeSpan.FromSeconds 1.) |> Flow.runSync () with
        | Exit.Failure(Cause.Die error) -> Assert.Contains("TestClock requires a ManualClock", error.Message)
        | other -> failwithf "Expected defect, got %A" other

    [<Fact>]
    let ``test platform trace identifies service reads and fibers`` () =
        let platform = TestPlatform.create ()
        platform.trace <- true
        let workflow = flow {
            let! _ = reads ()
            let! fiber = Flow.fork (reads ())
            return! Fiber.join fiber
        }
        workflow |> Flow.runTestWith platform (environment ()) |> ignore
        Assert.True(platform.Reads |> Seq.map snd |> Seq.distinct |> Seq.length |> fun count -> count >= 2)
        Assert.Contains(platform.Reads, fun (service, _) -> service = "Clock.UtcNow")
        Assert.Contains(platform.Reads, fun (service, _) -> service = "Random.Next")
        Assert.Contains(platform.Reads, fun (service, _) -> service = "Guid.NewGuid")
