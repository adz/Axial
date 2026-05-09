namespace FsFlow.Caps.Core.Examples

open System
open FsFlow.Caps.Core

module CoreCapabilitiesExample =
    let private renderResult formatter result =
        match result with
        | Ok value -> $"Ok {formatter value}"
        | Error error -> $"Error {EnvironmentVariableErrors.describe error}"

    let run () =
        let clock = Clock.fromValue (DateTimeOffset(2026, 5, 10, 12, 0, 0, TimeSpan.Zero))
        let random = Random.fromValue 7
        let guid = Guid.fromValue (global.System.Guid.Parse "11111111-1111-1111-1111-111111111111")

        let environment =
            EnvironmentVariables.fromPairs
                [ "FSFLOW_CAPS_PORT", "8080"
                  "FSFLOW_CAPS_ENABLED", "true"
                  "FSFLOW_CAPS_SESSION", "22222222-2222-2222-2222-222222222222"
                  "FSFLOW_CAPS_PORT_TEXT", "abc" ]

        printfn "clock=%O" (Clock.now clock)
        printfn "random=%d" (Random.nextInt random 0 10)
        printfn "guid=%O" (Guid.newGuid guid)
        printfn "port=%s" (renderResult string (EnvironmentVariable.getInt environment "FSFLOW_CAPS_PORT"))
        printfn "enabled=%s" (renderResult string (EnvironmentVariable.getBool environment "FSFLOW_CAPS_ENABLED"))
        printfn "session=%s" (renderResult string (EnvironmentVariable.getGuid environment "FSFLOW_CAPS_SESSION"))
        printfn "missing=%s" (renderResult string (EnvironmentVariable.get environment "FSFLOW_CAPS_MISSING"))
        printfn "invalid=%s" (renderResult string (EnvironmentVariable.getInt environment "FSFLOW_CAPS_PORT_TEXT"))
