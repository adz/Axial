namespace Axial.Tests

open System
open Axial
open Axial.PlatformService
open Swensen.Unquote
open Xunit

module TestRuntimeTests =
    [<Fact>]
    let ``capturing log records entries in order`` () =
        let log = Log.capturing ()

        log.Log(LogLevel.Information, "hello")
        log.LogException(LogLevel.Error, InvalidOperationException "boom", "bad")

        test <@ log.Entries.Count = 2 @>
        test <@ log.Entries[0] = { Level = LogLevel.Information; Message = "hello"; Error = None } @>
        test <@ log.Entries[1].Level = LogLevel.Error @>
        test <@ log.Entries[1].Message = "bad" @>
        test <@ log.Entries[1].Error.IsSome @>
        test <@ log.Entries[1].Error.Value.Message = "boom" @>

        log.Clear()
        test <@ log.Entries.Count = 0 @>

    [<Fact>]
    let ``capturing log serves as the logging service`` () =
        let runtime = TestRuntime.create (DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero))
        let workflow : Flow<TestRuntime, string, unit> = Log.info "recorded"

        test <@ Flow.runSync runtime workflow = Exit.Success () @>
        test <@ runtime.Log.Entries.Count = 1 @>
        test <@ runtime.Log.Entries[0] = { Level = LogLevel.Information; Message = "recorded"; Error = None } @>

    [<Fact>]
    let ``scripted random returns values in order then throws`` () =
        let random = Random.fromSequence [ 1; 2; 3 ]

        test <@ random.Next() = 1 @>
        test <@ random.NextMax 10 = 2 @>
        test <@ random.NextInt 0 10 = 3 @>
        Assert.Throws<InvalidOperationException>(fun () -> random.Next() |> ignore) |> ignore

    [<Fact>]
    let ``scripted guid returns values in order then throws`` () =
        let first = global.System.Guid.Parse "11111111-1111-1111-1111-111111111111"
        let second = global.System.Guid.Parse "22222222-2222-2222-2222-222222222222"
        let guid = Guid.fromSequence [ first; second ]

        test <@ guid.NewGuid() = first @>
        test <@ guid.NewGuid() = second @>
        Assert.Throws<InvalidOperationException>(fun () -> guid.NewGuid() |> ignore) |> ignore

    [<Fact>]
    let ``test runtime provides a deterministic environment`` () =
        let start = DateTimeOffset(2026, 5, 10, 12, 0, 0, TimeSpan.Zero)
        let runtime = TestRuntime.create start
        runtime.Random <- Random.fromSequence [ 7 ]
        runtime.Guid <- Guid.fromSequence [ global.System.Guid.Parse "33333333-3333-3333-3333-333333333333" ]

        test <@ Flow.runSync runtime Clock.now = Exit.Success start @>
        test <@ Flow.runSync runtime Random.next = Exit.Success 7 @>
        test <@ Flow.runSync runtime Guid.newGuid = Exit.Success (global.System.Guid.Parse "33333333-3333-3333-3333-333333333333") @>
        test <@ Flow.runSync runtime (Log.info "ran") = Exit.Success () @>
        test <@ runtime.Log.Entries.Count = 1 @>

    [<Fact>]
    let ``test runtime clock advances deterministically`` () =
        let runtime = TestRuntime.create (DateTimeOffset(2026, 5, 10, 12, 0, 0, TimeSpan.Zero))

        runtime.Clock.AdvanceBy (TimeSpan.FromSeconds 5.0)

        test <@ Flow.runSync runtime Clock.now = Exit.Success (DateTimeOffset(2026, 5, 10, 12, 0, 5, TimeSpan.Zero)) @>

    [<Fact>]
    let ``test runtime can be provided as a layer`` () =
        let start = DateTimeOffset(2026, 5, 10, 12, 0, 0, TimeSpan.Zero)
        let workflow : Flow<TestRuntime, Never, DateTimeOffset> = Clock.now

        let exit =
            workflow
            |> Layer.provide (TestRuntime.layer start)
            |> Flow.runSync ()

        test <@ exit = Exit.Success start @>
