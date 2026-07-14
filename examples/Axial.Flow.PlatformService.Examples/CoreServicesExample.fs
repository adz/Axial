namespace Axial.Flow.PlatformService.Examples

open System
open Axial.Flow
open Axial.Flow.PlatformService

module CoreServicesExample =
    type private AppServices =
        {
            Clock: IClock
            Random: IRandom
            Guid: IGuid
            EnvVars: IEnvironmentVariables
        }
        interface IHas<IClock> with member this.Service = this.Clock
        interface IHas<IRandom> with member this.Service = this.Random
        interface IHas<IGuid> with member this.Service = this.Guid
        interface IHas<IEnvironmentVariables> with member this.Service = this.EnvVars

    let private renderExit formatter result =
        match result with
        | Exit.Success value -> $"Ok {formatter value}"
        | Exit.Failure (Cause.Fail error) -> $"Error {EnvironmentVariableErrors.describe error}"
        | Exit.Failure cause -> $"Failure {cause}"

    let run () =
        let services =
            {
                Clock = Clock.fromValue (DateTimeOffset(2026, 5, 10, 12, 0, 0, TimeSpan.Zero))
                Random = Random.fromValue 7
                Guid = Guid.fromValue (System.Guid.Parse "11111111-1111-1111-1111-111111111111")
                EnvVars =
                    EnvironmentVariables.fromPairs
                        [ "AXIAL_SERVICES_PORT", "8080"
                          "AXIAL_SERVICES_ENABLED", "true"
                          "AXIAL_SERVICES_SESSION", "22222222-2222-2222-2222-222222222222"
                          "AXIAL_SERVICES_PORT_TEXT", "abc" ]
            }

        let run (flow: Flow<AppServices, 'error, 'value>) = flow.RunSynchronously(services)

        printfn "clock=%O" (run Clock.now)
        printfn "random=%d" (run (Random.nextInt 0 10) |> function Exit.Success v -> v | _ -> -1)
        printfn "guid=%O" (run Guid.newGuid)
        printfn "port=%s" (renderExit string (run (EnvironmentVariable.getInt "AXIAL_SERVICES_PORT")))
        printfn "enabled=%s" (renderExit string (run (EnvironmentVariable.getBool "AXIAL_SERVICES_ENABLED")))
        printfn "session=%s" (renderExit string (run (EnvironmentVariable.getGuid "AXIAL_SERVICES_SESSION")))
        printfn "missing=%s" (renderExit string (run (EnvironmentVariable.get "AXIAL_SERVICES_MISSING")))
        printfn "invalid=%s" (renderExit string (run (EnvironmentVariable.getInt "AXIAL_SERVICES_PORT_TEXT")))
