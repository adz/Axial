namespace Axial.Tests

open System
open Axial
open Axial.PlatformService
open Swensen.Unquote
open Xunit

module CoreServiceTests =
    type private VariablesEnvironment =
        { Variables: IEnvironmentVariables }
        interface IHasEnvironmentVariables with member this.EnvironmentVariables = this.Variables

    [<Fact>]
    let ``runtime contracts belong to core and authority remains optional`` () =
        test <@ typeof<IClock>.Assembly.GetName().Name = "Axial" @>
        test <@ typeof<IRandom>.Assembly.GetName().Name = "Axial" @>
        test <@ typeof<IGuid>.Assembly.GetName().Name = "Axial" @>
        test <@ typeof<ILogSink>.Assembly.GetName().Name = "Axial" @>
        test <@ typeof<IEnvironmentVariables>.Assembly.GetName().Name = "Axial.PlatformService" @>

    [<Fact>]
    let ``clock random and guid reads use scoped overrides under unit`` () =
        let instant = DateTimeOffset(2026, 5, 10, 12, 0, 0, TimeSpan.Zero)
        let id = System.Guid.Parse "11111111-1111-1111-1111-111111111111"
        let workflow = flow {
            let! now = Clock.now
            let! utc = Clock.utcDateTime
            let! seconds = Clock.unixTimeSeconds
            let! next = Random.next
            let! maximum = Random.nextMax 10
            let! ranged = Random.nextInt 0 10
            let! fraction = Random.nextDouble
            let! bytes = Random.bytes 3
            let! guid = Guid.newGuid
            return now, utc, seconds, next, maximum, ranged, fraction, bytes, guid
        }
        let actual = workflow |> Flow.withClock (Clock.fromValue instant)
                              |> Flow.withRandom (Random.fromFixed 4 0.25 0x2Auy)
                              |> Flow.withGuid (Guid.fromValue id)
                              |> Flow.runSync ()
        Assert.Equal(Exit.Success(instant, instant.UtcDateTime, 1778414400L, 4, 4, 4, 0.25, [|0x2Auy; 0x2Auy; 0x2Auy|], id), actual)

    [<Fact>]
    let ``environment-variable parsing retains explicit authority`` () =
        let environment = { Variables = EnvironmentVariables.fromPairs [
            "PORT", "8080"; "LONG", "9000000000"; "DOUBLE", "12.5"; "DECIMAL", "99.95"
            "ENABLED", "true"; "SESSION", "22222222-2222-2222-2222-222222222222"
            "URI", "https://example.com/api"; "TIMEOUT", "00:00:05" ] }
        test <@ EnvironmentVariables.tryGet "PORT" |> Flow.runSync environment = Exit.Success(Some "8080") @>
        test <@ EnvironmentVariables.getAll |> Flow.runSync environment |> function Exit.Success values -> values["PORT"] = "8080" | _ -> false @>
        test <@ EnvironmentVariables.expand "%PORT%" |> Flow.runSync environment = Exit.Success "8080" @>
        test <@ EnvironmentVariable.get "PORT" |> Flow.runSync environment = Exit.Success "8080" @>
        test <@ EnvironmentVariable.getInt "PORT" |> Flow.runSync environment = Exit.Success 8080 @>
        test <@ EnvironmentVariable.getInt64 "LONG" |> Flow.runSync environment = Exit.Success 9000000000L @>
        test <@ EnvironmentVariable.getDouble "DOUBLE" |> Flow.runSync environment = Exit.Success 12.5 @>
        test <@ EnvironmentVariable.getDecimal "DECIMAL" |> Flow.runSync environment = Exit.Success 99.95M @>
        test <@ EnvironmentVariable.getBool "ENABLED" |> Flow.runSync environment = Exit.Success true @>
        test <@ EnvironmentVariable.getGuid "SESSION" |> Flow.runSync environment = Exit.Success(System.Guid.Parse "22222222-2222-2222-2222-222222222222") @>
        test <@ EnvironmentVariable.getUri "URI" |> Flow.runSync environment = Exit.Success(Uri "https://example.com/api") @>
        test <@ EnvironmentVariable.getTimeSpan "TIMEOUT" |> Flow.runSync environment = Exit.Success(TimeSpan.FromSeconds 5.) @>
        test <@ EnvironmentVariables.set "DYNAMIC" "set" |> Flow.runSync environment = Exit.Success () @>
        test <@ EnvironmentVariable.get "DYNAMIC" |> Flow.runSync environment = Exit.Success "set" @>
        test <@ EnvironmentVariables.clear "DYNAMIC" |> Flow.runSync environment = Exit.Success () @>
        test <@ EnvironmentVariables.tryGet "DYNAMIC" |> Flow.runSync environment = Exit.Success None @>

    [<Fact>]
    let ``environment variable helpers return typed errors for missing and invalid values`` () =
        let environment = { Variables = EnvironmentVariables.fromPairs ["PORT", "abc"; "ENABLED", "maybe"] }
        test <@ EnvironmentVariable.get "MISSING" |> Flow.runSync environment = Exit.Failure(Cause.Fail(EnvironmentVariableError.MissingVariable "MISSING")) @>
        test <@ EnvironmentVariable.getInt "PORT" |> Flow.runSync environment = Exit.Failure(Cause.Fail(EnvironmentVariableError.InvalidVariable("PORT", "abc", "an integer"))) @>
        test <@ EnvironmentVariable.getBool "ENABLED" |> Flow.runSync environment = Exit.Failure(Cause.Fail(EnvironmentVariableError.InvalidVariable("ENABLED", "maybe", "a boolean"))) @>

    [<Fact>]
    let ``live platform defaults work without an environment`` () =
        let timestamp = Clock.now |> Flow.runSync () |> function Exit.Success value -> value | _ -> failwith "clock"
        let random = Random.nextInt 0 10 |> Flow.runSync () |> function Exit.Success value -> value | _ -> failwith "random"
        let guid = Guid.newGuid |> Flow.runSync () |> function Exit.Success value -> value | _ -> failwith "guid"
        Assert.True(timestamp <= DateTimeOffset.UtcNow.AddMinutes 1.)
        Assert.InRange(random, 0, 9)
        Assert.NotEqual(System.Guid.Empty, guid)

    [<Fact>]
    let ``Clock timed measures with the installed monotonic timer`` () =
        let mutable readings = 0
        let clock = { new IClock with
                        member _.UtcNow() = DateTimeOffset.UnixEpoch
                        member _.Elapsed() = readings <- readings + 1; TimeSpan.FromMilliseconds(float (readings * 250))
                        member _.Sleep(_, _) = System.Threading.Tasks.Task.CompletedTask }
        let actual = Flow.ok "done" |> Clock.timed |> Flow.withClock clock |> Flow.runSync ()
        test <@ actual = Exit.Success("done", TimeSpan.FromMilliseconds 250.) @>
        let fixedResult = Flow.ok 1 |> Clock.timed |> Flow.withClock (Clock.fromValue DateTimeOffset.UnixEpoch) |> Flow.runSync ()
        test <@ fixedResult = Exit.Success(1, TimeSpan.Zero) @>
