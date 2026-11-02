namespace Axial.Tests

open System
open Xunit

module NamespaceSurfaceTests =
    let private types () =
        typeof<Axial.Flow<unit, string, int>>.Assembly.GetExportedTypes()
        |> Array.map (fun t -> t.FullName)
        |> Set.ofArray

    [<Theory>]
    [<InlineData("Axial.Clock")>]
    [<InlineData("Axial.Random")>]
    [<InlineData("Axial.Guid")>]
    [<InlineData("Axial.Log")>]
    [<InlineData("Axial.TestLog")>]
    [<InlineData("Axial.TestPlatform")>]
    [<InlineData("Axial.TestClock")>]
    let ``runtime helpers are exported from the Axial namespace`` name =
        Assert.Contains(name, types ())

    [<Fact>]
    let ``old PlatformService namespace is not exported`` () =
        let oldPrefix = "Axial." + "PlatformService"
        let old = types () |> Set.filter (fun name -> name.StartsWith(oldPrefix, StringComparison.Ordinal))
        Assert.True(old.IsEmpty, String.concat ", " old)

    [<Fact>]
    let ``one LogSink implementation remains`` () =
        let sinks = types () |> Set.filter (fun name -> name = "Axial.LogSink" || name.EndsWith(".LogSink", StringComparison.Ordinal))
        Assert.Equal<string>(set [ "Axial.LogSink" ], sinks)
