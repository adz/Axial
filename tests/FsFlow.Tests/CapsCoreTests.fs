namespace FsFlow.Tests

open System
open FsFlow
open FsFlow.Services.Core
open Swensen.Unquote
open Xunit

module CapsCoreTests =
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
        let random = Random.fromValue 4
        let guid = Guid.fromValue (global.System.Guid.Parse "11111111-1111-1111-1111-111111111111")
        let envVars =
            EnvironmentVariables.fromPairs
                [ "FSFLOW_CAPS_PORT", "8080"
                  "FSFLOW_CAPS_ENABLED", "true"
                  "FSFLOW_CAPS_SESSION", "22222222-2222-2222-2222-222222222222" ]
        let runtime = makeRuntime clock random guid envVars

        test <@ Flow.runSync runtime Clock.now = Exit.Success (DateTimeOffset(2026, 5, 10, 12, 0, 0, TimeSpan.Zero)) @>
        test <@ Flow.runSync runtime (Random.nextInt 0 10) = Exit.Success 4 @>
        test <@ Flow.runSync runtime Guid.newGuid = Exit.Success (global.System.Guid.Parse "11111111-1111-1111-1111-111111111111") @>
        test <@ Flow.runSync runtime (EnvironmentVariables.tryGet "FSFLOW_CAPS_PORT") = Exit.Success (Some "8080") @>
        test <@ Flow.runSync runtime (EnvironmentVariable.get "FSFLOW_CAPS_PORT") = Exit.Success "8080" @>
        test <@ Flow.runSync runtime (EnvironmentVariable.getInt "FSFLOW_CAPS_PORT") = Exit.Success 8080 @>
        test <@ Flow.runSync runtime (EnvironmentVariable.getBool "FSFLOW_CAPS_ENABLED") = Exit.Success true @>
        test <@ Flow.runSync runtime (EnvironmentVariable.getGuid "FSFLOW_CAPS_SESSION") = Exit.Success (global.System.Guid.Parse "22222222-2222-2222-2222-222222222222") @>

    [<Fact>]
    let ``environment variable helpers return typed errors for missing and invalid values`` () =
        let envVars =
            EnvironmentVariables.fromPairs
                [ "FSFLOW_CAPS_PORT_TEXT", "abc"
                  "FSFLOW_CAPS_ENABLED_TEXT", "maybe" ]
        let runtime = makeRuntime Clock.live Random.live Guid.live envVars

        test <@ Flow.runSync runtime (EnvironmentVariable.get "FSFLOW_CAPS_MISSING") = Exit.Failure(Cause.Fail (EnvironmentVariableError.MissingVariable "FSFLOW_CAPS_MISSING")) @>
        test <@ Flow.runSync runtime (EnvironmentVariable.getInt "FSFLOW_CAPS_PORT_TEXT") = Exit.Failure(Cause.Fail (EnvironmentVariableError.InvalidVariable("FSFLOW_CAPS_PORT_TEXT", "abc", "an integer"))) @>
        test <@ Flow.runSync runtime (EnvironmentVariable.getBool "FSFLOW_CAPS_ENABLED_TEXT") = Exit.Failure(Cause.Fail (EnvironmentVariableError.InvalidVariable("FSFLOW_CAPS_ENABLED_TEXT", "maybe", "a boolean"))) @>

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
        
        let envName = $"FSFLOW_CAPS_CORE_{global.System.Guid.NewGuid():N}"
        let previous = Environment.GetEnvironmentVariable envName

        try
            Environment.SetEnvironmentVariable(envName, "live-value")

            test <@ timestamp <= DateTimeOffset.UtcNow.AddMinutes(1.0) @>
            test <@ randomValue >= 0 && randomValue < 10 @>
            test <@ generatedGuid <> global.System.Guid.Empty @>
            test <@ Flow.runSync runtime (EnvironmentVariable.get envName) = Exit.Success "live-value" @>
        finally
            Environment.SetEnvironmentVariable(envName, previous)
