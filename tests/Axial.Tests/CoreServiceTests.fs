namespace Axial.Tests

open System
open Axial.Flow
open Axial.ErrorHandling
open Axial.Validation
open Axial.Flow.PlatformService
open Swensen.Unquote
open Xunit

module CoreServiceTests =
    let private makeRuntime clock random guid envVars : BaseRuntime =
        {
            Clock = clock
            Log = Log.live
            Random = random
            Guid = guid
            EnvironmentVariables = envVars
        }

    [<Fact>]
    let ``clock random guid and environment-variable services are deterministic when fixed`` () =
        let clock = Clock.fromValue (DateTimeOffset(2026, 5, 10, 12, 0, 0, TimeSpan.Zero))
        let random = Random.fromFixed 4 0.25 0x2Auy
        let guid = Guid.fromValue (global.System.Guid.Parse "11111111-1111-1111-1111-111111111111")
        let envVars =
            EnvironmentVariables.fromPairs
                [ "AXIAL_SERVICES_PORT", "8080"
                  "AXIAL_SERVICES_LONG", "9000000000"
                  "AXIAL_SERVICES_DOUBLE", "12.5"
                  "AXIAL_SERVICES_DECIMAL", "99.95"
                  "AXIAL_SERVICES_ENABLED", "true"
                  "AXIAL_SERVICES_SESSION", "22222222-2222-2222-2222-222222222222"
                  "AXIAL_SERVICES_URI", "https://example.com/api"
                  "AXIAL_SERVICES_TIMEOUT", "00:00:05" ]
        let runtime = makeRuntime clock random guid envVars

        test <@ Flow.runSync runtime Clock.now = Exit.Success (DateTimeOffset(2026, 5, 10, 12, 0, 0, TimeSpan.Zero)) @>
        test <@ Flow.runSync runtime Clock.utcDateTime = Exit.Success (DateTime(2026, 5, 10, 12, 0, 0, DateTimeKind.Utc)) @>
        test <@ Flow.runSync runtime Clock.unixTimeSeconds = Exit.Success 1778414400L @>
        test <@ Flow.runSync runtime Random.next = Exit.Success 4 @>
        test <@ Flow.runSync runtime (Random.nextMax 10) = Exit.Success 4 @>
        test <@ Flow.runSync runtime (Random.nextInt 0 10) = Exit.Success 4 @>
        test <@ Flow.runSync runtime Random.nextDouble = Exit.Success 0.25 @>
        test <@ Flow.runSync runtime (Random.bytes 3) = Exit.Success [| 0x2Auy; 0x2Auy; 0x2Auy |] @>
        test <@ Flow.runSync runtime Guid.newGuid = Exit.Success (global.System.Guid.Parse "11111111-1111-1111-1111-111111111111") @>
        test <@ Flow.runSync runtime (EnvironmentVariables.tryGet "AXIAL_SERVICES_PORT") = Exit.Success (Some "8080") @>
        test <@ Flow.runSync runtime EnvironmentVariables.getAll |> function Exit.Success values -> values["AXIAL_SERVICES_PORT"] = "8080" | _ -> false @>
        test <@ Flow.runSync runtime (EnvironmentVariables.expand "%AXIAL_SERVICES_PORT%") = Exit.Success "8080" @>
        test <@ Flow.runSync runtime (EnvironmentVariable.get "AXIAL_SERVICES_PORT") = Exit.Success "8080" @>
        test <@ Flow.runSync runtime (EnvironmentVariable.getInt "AXIAL_SERVICES_PORT") = Exit.Success 8080 @>
        test <@ Flow.runSync runtime (EnvironmentVariable.getInt64 "AXIAL_SERVICES_LONG") = Exit.Success 9000000000L @>
        test <@ Flow.runSync runtime (EnvironmentVariable.getDouble "AXIAL_SERVICES_DOUBLE") = Exit.Success 12.5 @>
        test <@ Flow.runSync runtime (EnvironmentVariable.getDecimal "AXIAL_SERVICES_DECIMAL") = Exit.Success 99.95M @>
        test <@ Flow.runSync runtime (EnvironmentVariable.getBool "AXIAL_SERVICES_ENABLED") = Exit.Success true @>
        test <@ Flow.runSync runtime (EnvironmentVariable.getGuid "AXIAL_SERVICES_SESSION") = Exit.Success (global.System.Guid.Parse "22222222-2222-2222-2222-222222222222") @>
        test <@ Flow.runSync runtime (EnvironmentVariable.getUri "AXIAL_SERVICES_URI") = Exit.Success (Uri "https://example.com/api") @>
        test <@ Flow.runSync runtime (EnvironmentVariable.getTimeSpan "AXIAL_SERVICES_TIMEOUT") = Exit.Success (TimeSpan.FromSeconds 5.0) @>

        test <@ Flow.runSync runtime (EnvironmentVariables.set "AXIAL_SERVICES_DYNAMIC" "set") = Exit.Success () @>
        test <@ Flow.runSync runtime (EnvironmentVariable.get "AXIAL_SERVICES_DYNAMIC") = Exit.Success "set" @>
        test <@ Flow.runSync runtime (EnvironmentVariables.clear "AXIAL_SERVICES_DYNAMIC") = Exit.Success () @>
        test <@ Flow.runSync runtime (EnvironmentVariables.tryGet "AXIAL_SERVICES_DYNAMIC") = Exit.Success None @>

    [<Fact>]
    let ``environment variable helpers return typed errors for missing and invalid values`` () =
        let envVars =
            EnvironmentVariables.fromPairs
                [ "AXIAL_SERVICES_PORT_TEXT", "abc"
                  "AXIAL_SERVICES_ENABLED_TEXT", "maybe" ]
        let runtime = makeRuntime Clock.live Random.live Guid.live envVars

        test <@ Flow.runSync runtime (EnvironmentVariable.get "AXIAL_SERVICES_MISSING") = Exit.Failure(Cause.Fail (EnvironmentVariableError.MissingVariable "AXIAL_SERVICES_MISSING")) @>
        test <@ Flow.runSync runtime (EnvironmentVariable.getInt "AXIAL_SERVICES_PORT_TEXT") = Exit.Failure(Cause.Fail (EnvironmentVariableError.InvalidVariable("AXIAL_SERVICES_PORT_TEXT", "abc", "an integer"))) @>
        test <@ Flow.runSync runtime (EnvironmentVariable.getBool "AXIAL_SERVICES_ENABLED_TEXT") = Exit.Failure(Cause.Fail (EnvironmentVariableError.InvalidVariable("AXIAL_SERVICES_ENABLED_TEXT", "maybe", "a boolean"))) @>

    [<Fact>]
    let ``live services work correctly with a live base runtime`` () =
        let runtime =
            {
                Clock = Clock.live
                Log = Log.live
                Random = Random.live
                Guid = Guid.live
                EnvironmentVariables = EnvironmentVariables.live
            }

        let timestamp = Flow.runSync runtime Clock.now |> function Exit.Success t -> t | _ -> failwith "Failed"
        let randomValue = Flow.runSync runtime (Random.nextInt 0 10) |> function Exit.Success v -> v | _ -> failwith "Failed"
        let generatedGuid = Flow.runSync runtime Guid.newGuid |> function Exit.Success g -> g | _ -> failwith "Failed"
        
        let envName = $"AXIAL_SERVICES_CORE_{global.System.Guid.NewGuid():N}"
        let previous = Environment.GetEnvironmentVariable envName

        try
            Environment.SetEnvironmentVariable(envName, "live-value")

            test <@ timestamp <= DateTimeOffset.UtcNow.AddMinutes(1.0) @>
            test <@ randomValue >= 0 && randomValue < 10 @>
            test <@ generatedGuid <> global.System.Guid.Empty @>
            test <@ Flow.runSync runtime (EnvironmentVariable.get envName) = Exit.Success "live-value" @>
        finally
            Environment.SetEnvironmentVariable(envName, previous)
