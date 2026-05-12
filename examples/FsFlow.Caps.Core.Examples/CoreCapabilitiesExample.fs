namespace FsFlow.Caps.Core.Examples

open System
open FsFlow
open FsFlow.Caps.Core

module CoreCapabilitiesExample =
    type private AppCaps =
        {
            Clock: IClock
            Random: IRandom
            Guid: IGuid
            EnvVars: IEnvironmentVariables
        }
        interface Needs<IClock> with member this.Dep = this.Clock
        interface Needs<IRandom> with member this.Dep = this.Random
        interface Needs<IGuid> with member this.Dep = this.Guid
        interface Needs<IEnvironmentVariables> with member this.Dep = this.EnvVars

    let private renderExit formatter result =
        match result with
        | Exit.Success value -> $"Ok {formatter value}"
        | Exit.Failure (Cause.Fail error) -> $"Error {EnvironmentVariableErrors.describe error}"
        | Exit.Failure cause -> $"Failure {cause}"

    let run () =
        let caps =
            {
                Clock = Clock.fromValue (DateTimeOffset(2026, 5, 10, 12, 0, 0, TimeSpan.Zero))
                Random = Random.fromValue 7
                Guid = Guid.fromValue (global.System.Guid.Parse "11111111-1111-1111-1111-111111111111")
                EnvVars =
                    EnvironmentVariables.fromPairs
                        [ "FSFLOW_CAPS_PORT", "8080"
                          "FSFLOW_CAPS_ENABLED", "true"
                          "FSFLOW_CAPS_SESSION", "22222222-2222-2222-2222-222222222222"
                          "FSFLOW_CAPS_PORT_TEXT", "abc" ]
            }

        let run flow = Flow.run caps flow |> fun t -> t.AsTask().GetAwaiter().GetResult()

        printfn "clock=%O" (run Clock.now)
        printfn "random=%d" (run (Random.nextInt 0 10) |> function Exit.Success v -> v | _ -> -1)
        printfn "guid=%O" (run Guid.newGuid)
        printfn "port=%s" (renderExit string (run (EnvironmentVariable.getInt "FSFLOW_CAPS_PORT")))
        printfn "enabled=%s" (renderExit string (run (EnvironmentVariable.getBool "FSFLOW_CAPS_ENABLED")))
        printfn "session=%s" (renderExit string (run (EnvironmentVariable.getGuid "FSFLOW_CAPS_SESSION")))
        printfn "missing=%s" (renderExit string (run (EnvironmentVariable.get "FSFLOW_CAPS_MISSING")))
        printfn "invalid=%s" (renderExit string (run (EnvironmentVariable.getInt "FSFLOW_CAPS_PORT_TEXT")))
