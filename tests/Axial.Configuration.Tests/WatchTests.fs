module Axial.Configuration.Tests.WatchTests

open System
open System.Threading.Tasks
open Xunit
open Axial
open Axial.Configuration
open Axial.Configuration.Tests.Fixtures
open Axial.FileSystem
open Axial.State
open Reified

let private change path =
    { Kind = FileSystemChangeKind.Changed
      Path = path
      OldPath = None }

let private write (fileSystem: IFileSystem) path text = fileSystem.WriteAllText(path, text)

// Wait for registration, not elapsed wall time: a delayed worker must not lose its debounce window.
let private armed (clock: ManualClock) deadline =
    Flow.fromTask (fun token -> task {
        while clock.NextDeadline <> Some deadline do
            token.ThrowIfCancellationRequested()
            do! Task.Yield()
    })

let private updates (watch: ConfigWatch<TestEnvironment, PortSettings>) =
    flow {
        let! queue = Queue.unbounded ()
        let! _ =
            watch.Current
            |> SubscriptionRef.changes QueueStrategy.Unbounded
            |> FlowStream.runForEachFlow (fun value -> Queue.offer value queue |> Flow.ignore)
            |> Flow.fork
        // Taking the initial value also proves that the subscription is installed.
        let! initial = Dequeue.take queue
        return initial, queue
    }

let private newPath () =
    System.IO.Path.Combine(System.IO.Path.GetTempPath(), "axial-config-" + Guid.NewGuid().ToString("N") + ".json")

let private timeoutError () =
    [ ConfigError.SourceFailed("test", "watch did not settle before the test timeout") ]

let private run clock environment workflow =
    workflow
    |> Flow.withClock clock
    // Only the deadlock watchdog uses wall time; it never determines a test assertion.
    |> Flow.timeout (TimeSpan.FromSeconds 30.0) (timeoutError ())
    |> Flow.run environment

[<Fact>]
let ``watch keeps the last valid value and publishes a later valid change`` () =
    let clock = ManualClock(DateTimeOffset.UnixEpoch)
    let path = newPath ()
    let fileSystem = TestFileSystem([ path, """{"Port":"1"}""" ])
    let environment = { Config = Config.ofData (Data.Object []); FileSystem = fileSystem :> IFileSystem }
    let source = ConfigSource.jsonFile path
    let mutable reads = 0
    let sources =
        [ { source with
              Read = flow {
                  reads <- reads + 1
                  // Model a reload delayed longer than the old 600 ms wall-time settlement guess.
                  if reads = 2 then do! Flow.sleep (TimeSpan.FromSeconds 5.0)
                  return! source.Read
              } } ]

    let workflow =
        flow {
            let! watch = Config.watch sources (portSchema ())
            let! initial, changes = updates watch

            write fileSystem path """{"Port":"not-an-int"}"""
            fileSystem.RaiseChange(change path)
            do! armed clock (TimeSpan.FromMilliseconds 200.0)
            do! TestClock.advance (TimeSpan.FromMilliseconds 200.0)
            do! armed clock (TimeSpan.FromMilliseconds 5200.0)
            do! TestClock.advance (TimeSpan.FromSeconds 5.0)
            // The error is the reload's settlement signal. Do not overwrite the invalid input before it is read.
            let! failures =
                watch.Errors |> FlowStream.take 1 |> FlowStream.runCollect |> Flow.widenError
            let! afterInvalid = SubscriptionRef.get watch.Current

            write fileSystem path """{"Port":"8"}"""
            fileSystem.RaiseChange(change path)
            do! armed clock (TimeSpan.FromMilliseconds 5400.0)
            do! TestClock.advance (TimeSpan.FromMilliseconds 200.0)
            let! afterValid = Dequeue.take changes
            return initial.Port, afterInvalid.Port, afterValid.Port, failures
        }

    match run clock environment workflow with
    | Exit.Success(initial, afterInvalid, afterValid, failures) ->
        Assert.Equal(1, initial)
        Assert.Equal(1, afterInvalid)
        Assert.Equal(8, afterValid)

        match failures with
        | [ [ ConfigError.Invalid issues ] ] -> Assert.Equal(1, SchemaErrors.count issues)
        | other -> failwithf "Expected one invalid decode, got %A" other
    | Exit.Failure cause -> failwithf "Expected success, got %A" cause

[<Fact>]
let ``a watcher setup failure is reported without rejecting the watch`` () =
    let path = newPath ()
    let fileSystem = TestFileSystem([ path, """{"Port":"1"}""" ], watchFailure = InvalidOperationException("parent directory is missing"))
    let environment = { Config = Config.ofData (Data.Object []); FileSystem = fileSystem :> IFileSystem }
    let sources = [ ConfigSource.jsonFile path ]

    let workflow =
        flow {
            let! watch = Config.watch sources (portSchema ())
            let! initial = SubscriptionRef.get watch.Current
            let! failures =
                watch.Errors |> FlowStream.take 1 |> FlowStream.runCollect |> Flow.widenError
            return initial.Port, failures
        }
        |> Flow.timeout (TimeSpan.FromSeconds 10.0) (timeoutError ())

    match Flow.run environment workflow with
    | Exit.Success(initial, failures) ->
        Assert.Equal(1, initial)
        match failures with
        | [ [ ConfigError.SourceFailed(source, _) ] ] when source = "watch " + path -> ()
        | other -> failwithf "Expected one watcher setup failure, got %A" other
    | Exit.Failure cause -> failwithf "Expected success, got %A" cause

[<Fact>]
let ``a burst of file changes debounces to one reload`` () =
    let clock = ManualClock(DateTimeOffset.UnixEpoch)
    let path = newPath ()
    let fileSystem = TestFileSystem([ path, """{"Port":"1"}""" ])
    let environment = { Config = Config.ofData (Data.Object []); FileSystem = fileSystem :> IFileSystem }
    let log = Log.memory ()
    let sources = [ ConfigSource.jsonFile path ]

    let workflow =
        flow {
            let! watch = Config.watch sources (portSchema ())

            write fileSystem path """{"Port":"bad-one"}"""
            fileSystem.RaiseChange(change path)
            do! armed clock (TimeSpan.FromMilliseconds 200.0)
            do! TestClock.advance (TimeSpan.FromMilliseconds 100.0)
            write fileSystem path """{"Port":"bad-two"}"""
            fileSystem.RaiseChange(change path)
            // A new deadline proves the second change reached debounce, not just the watch callback.
            do! armed clock (TimeSpan.FromMilliseconds 300.0)
            do! TestClock.advance (TimeSpan.FromMilliseconds 199.0)
            Assert.Empty(log.Entries |> Seq.filter (fun entry -> entry.Level >= LogLevel.Warning))
            do! TestClock.advance (TimeSpan.FromMilliseconds 1.0)
            let! failures =
                watch.Errors |> FlowStream.take 1 |> FlowStream.runCollect |> Flow.widenError
            return failures
        }
        |> Flow.withLogSink log

    match run clock environment workflow with
    | Exit.Success failures ->
        match failures with
        | [ [ ConfigError.Invalid _ ] ] -> ()
        | other -> failwithf "Expected one debounced failure, got %A" other

        let warnings =
            log.Entries |> Seq.filter (fun entry -> entry.Level >= LogLevel.Warning) |> Seq.toList

        Assert.Equal(1, warnings.Length)
    | Exit.Failure cause -> failwithf "Expected success, got %A" cause

[<Fact>]
let ``environment sources stay static while the file changes`` () =
    let clock = ManualClock(DateTimeOffset.UnixEpoch)
    let path = newPath ()
    let fileSystem = TestFileSystem([ path, """{"Port":"2"}""" ])
    let environment = { Config = Config.ofData (Data.Object []); FileSystem = fileSystem :> IFileSystem }

    let sources =
        [ ConfigSource.jsonFile path
          ConfigSource.environmentOf None [ "Port", "1" ] ]

    let workflow =
        flow {
            let! watch = Config.watch sources (portSchema ())
            let! initial, changes = updates watch

            write fileSystem path """{"Port":"9"}"""
            fileSystem.RaiseChange(change path)
            do! armed clock (TimeSpan.FromMilliseconds 200.0)
            do! TestClock.advance (TimeSpan.FromMilliseconds 200.0)
            // Even an unchanged value must be published by an actual reload.
            let! after = Dequeue.take changes
            return initial.Port, after.Port
        }

    match run clock environment workflow with
    | Exit.Success(initial, after) ->
        Assert.Equal(1, initial)
        Assert.Equal(1, after)
    | Exit.Failure cause -> failwithf "Expected success, got %A" cause
