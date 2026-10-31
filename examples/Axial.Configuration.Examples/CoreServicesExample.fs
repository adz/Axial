namespace Axial.Configuration.Examples

open System
open Axial
open Axial.Configuration
open Axial.PlatformService
open Reified
open Reified.SchemaDSL

type private Settings =
    { Port: int
      Enabled: bool
      Name: string option }

module CoreServicesExample =
    let private settingsSchema () =
        schema<Settings> {
            field (fun (settings: Settings) -> settings.Port)
            field (fun (settings: Settings) -> settings.Enabled)
            field (fun (settings: Settings) -> settings.Name)
            construct (fun port enabled name ->
                { Port = port
                  Enabled = enabled
                  Name = name })
        }

    let private renderExit formatter result =
        match result with
        | Exit.Success value -> "Ok " + formatter value
        | Exit.Failure(Cause.Fail errors) ->
            "Error "
            + (errors
               |> List.map (ConfigError.describe (Config.ofData (Data.Object [])))
               |> String.concat "; ")
        | Exit.Failure cause -> "Failure " + cause.ToString()

    let run () =
        let sources =
            [ ConfigSource.environmentOf
                  None
                  [ "PORT", "8080"
                    "ENABLED", "true"
                    "NAME", "orders" ] ]

        let run (source: Flow<unit, 'error, 'value>) =
            source
            |> Flow.withClock (Clock.fromValue (DateTimeOffset(2026, 5, 10, 12, 0, 0, TimeSpan.Zero)))
            |> Flow.withRandom (Random.fromValue 7)
            |> Flow.withGuid (Guid.fromValue (System.Guid.Parse "11111111-1111-1111-1111-111111111111"))
            |> fun workflow -> workflow.RunSynchronously(())

        printfn "clock=%O" (run Clock.now)
        printfn "random=%d" (run (Random.nextInt 0 10) |> function Exit.Success value -> value | _ -> -1)
        printfn "guid=%O" (run Guid.newGuid)

        match run (Config.load sources (settingsSchema ())) with
        | Exit.Success settings ->
            printfn "port=%d" settings.Port
            printfn "enabled=%b" settings.Enabled
            printfn "name=%s" (defaultArg settings.Name "<missing>")
        | Exit.Failure cause -> printfn "settings=%s" (renderExit id (Exit.Failure cause))

        let fake =
            { new IHasConfig with
                member _.Config = Config.ofData (Data.Object [ "Port", Data.Number "8080" ]) }

        printfn "port text=%s" (renderExit id (run (Config.required "Port" |> Flow.mapError List.singleton |> Flow.localEnv (fun (_: unit) -> fake))))
