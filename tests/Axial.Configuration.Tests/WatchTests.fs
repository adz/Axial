module Axial.Configuration.Tests.WatchTests

open System
open Xunit
open Axial
open Axial.Configuration
open Axial.Configuration.Tests.Fixtures
open Axial.FileSystem
open Axial.PlatformService
open Axial.State
open Reified

let private change path =
    { Kind = FileSystemChangeKind.Changed
      Path = path
      OldPath = None }

let private write (fileSystem: IFileSystem) path text = fileSystem.WriteAllText(path, text)

let private settle () = Flow.sleep (TimeSpan.FromMilliseconds 600.0)

let private newPath () =
    System.IO.Path.Combine(System.IO.Path.GetTempPath(), "axial-config-" + Guid.NewGuid().ToString("N") + ".json")

let private timeoutError =
    [ ConfigError.SourceFailed("test", "watch did not settle before the test timeout") ]

[<Fact>]
let ``watch keeps the last valid value and publishes a later valid change`` () =
    let path = newPath ()
    let fileSystem = TestFileSystem([ path, """{"Port":"1"}""" ])
    let environment = { Config = Config.ofData (Data.Object []); FileSystem = fileSystem :> IFileSystem }
    let sources = [ ConfigSource.jsonFile path ]

    let workflow =
        flow {
            let! watch = Config.watch sources (portSchema ())
            let! initial = SubscriptionRef.get watch.Current

            write fileSystem path """{"Port":"not-an-int"}"""
            fileSystem.RaiseChange(change path)
            do! settle ()
            let! afterInvalid = SubscriptionRef.get watch.Current

            write fileSystem path """{"Port":"8"}"""
            fileSystem.RaiseChange(change path)
            do! settle ()
            let! afterValid = SubscriptionRef.get watch.Current
            let! failures =
                watch.Errors |> FlowStream.take 1 |> FlowStream.runCollect |> Flow.widenError
            return initial.Port, afterInvalid.Port, afterValid.Port, failures
        }
        |> Flow.timeout (TimeSpan.FromSeconds 10.0) timeoutError

    match Flow.run environment workflow with
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
        |> Flow.timeout (TimeSpan.FromSeconds 10.0) timeoutError

    match Flow.run environment workflow with
    | Exit.Success(initial, failures) ->
        Assert.Equal(1, initial)
        match failures with
        | [ [ ConfigError.SourceFailed(source, _) ] ] when source = "watch " + path -> ()
        | other -> failwithf "Expected one watcher setup failure, got %A" other
    | Exit.Failure cause -> failwithf "Expected success, got %A" cause

[<Fact>]
let ``a burst of file changes debounces to one reload`` () =
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
            write fileSystem path """{"Port":"bad-two"}"""
            fileSystem.RaiseChange(change path)
            do! settle ()
            let! failures =
                watch.Errors |> FlowStream.take 1 |> FlowStream.runCollect |> Flow.widenError
            return failures
        }
        |> Flow.timeout (TimeSpan.FromSeconds 10.0) timeoutError
        |> Flow.withLogSink log

    match Flow.run environment workflow with
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
    let path = newPath ()
    let fileSystem = TestFileSystem([ path, """{"Port":"2"}""" ])
    let environment = { Config = Config.ofData (Data.Object []); FileSystem = fileSystem :> IFileSystem }

    let sources =
        [ ConfigSource.jsonFile path
          ConfigSource.environmentOf None [ "Port", "1" ] ]

    let workflow =
        flow {
            let! watch = Config.watch sources (portSchema ())
            let! initial = SubscriptionRef.get watch.Current

            write fileSystem path """{"Port":"9"}"""
            fileSystem.RaiseChange(change path)
            do! settle ()
            let! after = SubscriptionRef.get watch.Current
            return initial.Port, after.Port
        }
        |> Flow.timeout (TimeSpan.FromSeconds 10.0) timeoutError

    match Flow.run environment workflow with
    | Exit.Success(initial, after) ->
        Assert.Equal(1, initial)
        Assert.Equal(1, after)
    | Exit.Failure cause -> failwithf "Expected success, got %A" cause
