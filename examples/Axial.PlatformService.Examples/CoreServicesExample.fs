namespace Axial.PlatformService.Examples

open System
open Axial
open Axial.PlatformService

module CoreServicesExample =
    type private AppServices =
        {
            EnvVars: IEnvironmentVariables
        }
        interface IHasEnvironmentVariables with member this.EnvironmentVariables = this.EnvVars

    let private renderExit formatter result =
        match result with
        | Exit.Success value -> "Ok " + formatter value
        | Exit.Failure (Cause.Fail error) -> "Error " + EnvironmentVariableErrors.describe error
        | Exit.Failure cause -> "Failure " + cause.ToString()

    let run () =
        let services =
            {
                EnvVars =
                    EnvironmentVariables.fromPairs
                        [ "AXIAL_SERVICES_PORT", "8080"
                          "AXIAL_SERVICES_ENABLED", "true"
                          "AXIAL_SERVICES_SESSION", "22222222-2222-2222-2222-222222222222"
                          "AXIAL_SERVICES_PORT_TEXT", "abc" ]
            }

        let run (source: Flow<AppServices, 'error, 'value>) =
            source
            |> Flow.withClock (Clock.fromValue (DateTimeOffset(2026, 5, 10, 12, 0, 0, TimeSpan.Zero)))
            |> Flow.withRandom (Random.fromValue 7)
            |> Flow.withGuid (Guid.fromValue (System.Guid.Parse "11111111-1111-1111-1111-111111111111"))
            |> fun workflow -> workflow.RunSynchronously(services)

        printfn "clock=%O" (run Clock.now)
        printfn "random=%d" (run (Random.nextInt 0 10) |> function Exit.Success v -> v | _ -> -1)
        printfn "guid=%O" (run Guid.newGuid)
        printfn "port=%s" (renderExit string (run (EnvironmentVariable.getInt "AXIAL_SERVICES_PORT")))
        printfn "enabled=%s" (renderExit string (run (EnvironmentVariable.getBool "AXIAL_SERVICES_ENABLED")))
        printfn "session=%s" (renderExit string (run (EnvironmentVariable.getGuid "AXIAL_SERVICES_SESSION")))
        printfn "missing=%s" (renderExit string (run (EnvironmentVariable.get "AXIAL_SERVICES_MISSING")))
        printfn "invalid=%s" (renderExit string (run (EnvironmentVariable.getInt "AXIAL_SERVICES_PORT_TEXT")))
