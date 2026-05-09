namespace FsFlow.Tests

open System
open FsFlow.Caps.Core
open Swensen.Unquote
open Xunit

module CapsCoreTests =
    [<Fact>]
    let ``clock random guid and environment variable helpers are synchronous and deterministic when fixed`` () =
        let clock = Clock.fromValue (DateTimeOffset(2026, 5, 10, 12, 0, 0, TimeSpan.Zero))
        let random = Random.fromValue 4
        let guid = Guid.fromValue (global.System.Guid.Parse "11111111-1111-1111-1111-111111111111")
        let environment =
            EnvironmentVariables.fromPairs
                [ "FSFLOW_CAPS_PORT", "8080"
                  "FSFLOW_CAPS_ENABLED", "true"
                  "FSFLOW_CAPS_SESSION", "22222222-2222-2222-2222-222222222222" ]

        test <@ Clock.now clock = DateTimeOffset(2026, 5, 10, 12, 0, 0, TimeSpan.Zero) @>
        test <@ Random.nextInt random 0 10 = 4 @>
        test <@ Guid.newGuid guid = global.System.Guid.Parse "11111111-1111-1111-1111-111111111111" @>
        test <@ EnvironmentVariables.tryGet environment "FSFLOW_CAPS_PORT" = Some "8080" @>
        test <@ EnvironmentVariable.get environment "FSFLOW_CAPS_PORT" = Ok "8080" @>
        test <@ EnvironmentVariable.getInt environment "FSFLOW_CAPS_PORT" = Ok 8080 @>
        test <@ EnvironmentVariable.getBool environment "FSFLOW_CAPS_ENABLED" = Ok true @>
        test <@ EnvironmentVariable.getGuid environment "FSFLOW_CAPS_SESSION" = Ok (global.System.Guid.Parse "22222222-2222-2222-2222-222222222222") @>

    [<Fact>]
    let ``environment variable helpers return typed errors for missing and invalid values`` () =
        let environment =
            EnvironmentVariables.fromPairs
                [ "FSFLOW_CAPS_PORT_TEXT", "abc"
                  "FSFLOW_CAPS_ENABLED_TEXT", "maybe" ]

        test <@ EnvironmentVariable.get environment "FSFLOW_CAPS_MISSING" = Error(EnvironmentVariableError.MissingVariable "FSFLOW_CAPS_MISSING") @>
        test <@ EnvironmentVariable.getInt environment "FSFLOW_CAPS_PORT_TEXT" = Error(EnvironmentVariableError.InvalidVariable("FSFLOW_CAPS_PORT_TEXT", "abc", "an integer")) @>
        test <@ EnvironmentVariable.getBool environment "FSFLOW_CAPS_ENABLED_TEXT" = Error(EnvironmentVariableError.InvalidVariable("FSFLOW_CAPS_ENABLED_TEXT", "maybe", "a boolean")) @>

    [<Fact>]
    let ``live providers are available for production runtimes`` () =
        let timestamp = Clock.now Clock.live
        let randomValue = Random.nextInt Random.live 0 10
        let generatedGuid = Guid.newGuid Guid.live
        let envName = $"FSFLOW_CAPS_CORE_{global.System.Guid.NewGuid():N}"
        let previous = Environment.GetEnvironmentVariable envName

        try
            Environment.SetEnvironmentVariable(envName, "live-value")

            let liveEnvironment = EnvironmentVariables.live

            test <@ timestamp <= DateTimeOffset.UtcNow.AddMinutes(1.0) @>
            test <@ randomValue >= 0 && randomValue < 10 @>
            test <@ generatedGuid <> global.System.Guid.Empty @>
            test <@ EnvironmentVariable.get liveEnvironment envName = Ok "live-value" @>
        finally
            Environment.SetEnvironmentVariable(envName, previous)
