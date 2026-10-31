namespace Axial.Tests

open System
open Axial
open Axial.PlatformService
open Xunit

module PlatformPackagingTests =
    [<Fact>]
    let ``platform operations and test fixtures ship in core`` () =
        for fixture in [ typeof<TestLog>; typeof<TestPlatform> ] do
            Assert.Equal(typeof<IClock>.Assembly, fixture.Assembly)
        Assert.Same(PlatformDefaults.clock, Clock.live)
        Assert.Same(PlatformDefaults.random, Random.live)
        Assert.Same(PlatformDefaults.guid, Guid.live)
        Assert.Same(PlatformDefaults.logSink, Log.live)

    [<Fact>]
    let ``tracing exposes stable snapshots and retains fiber attribution`` () =
        let platform = TestPlatform.create ()
        platform.Trace <- true
        Clock.now |> Flow.runTestWith platform () |> ignore
        let snapshot = platform.Reads
        Random.next |> Flow.runTestWith platform () |> ignore
        Assert.Single(snapshot) |> ignore
        Assert.Equal(2, platform.Reads.Count)
        Assert.Equal("Clock.UtcNow", fst snapshot[0])

    [<Fact>]
    let ``manual and scripted platform factories retain their behavior`` () =
        let clock = Clock.fromValue DateTimeOffset.UnixEpoch
        let first = System.Guid.Parse "00000000-0000-0000-0000-000000000001"
        let scripted = Guid.sequence [ first ]
        Assert.Equal(DateTimeOffset.UnixEpoch, (clock :> IClock).UtcNow())
        Assert.Equal(first, scripted.NewGuid())
        Assert.Throws<InvalidOperationException>(fun () -> scripted.NewGuid() |> ignore) |> ignore
