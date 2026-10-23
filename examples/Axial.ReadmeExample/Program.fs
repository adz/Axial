open System
open System.IO
open System.Threading
open Axial

type ReadmeEnv =
    { Root: string }

type FileReadError =
    | NotFound of path: string

let readTextFile (path: string) : Flow<ReadmeEnv, FileReadError, string> =
    flow {
        // In production, map access and path exceptions separately at the boundary.
        do!
            // executable host boundary performs this effect.
            // axial-allow-AXG001: intentional filesystem host or live-service boundary
            if File.Exists path then
                Ok()
            else
                Error(NotFound path)

        // executable host boundary performs this effect.

        // axial-allow-AXG001: intentional filesystem host or live-service boundary
        return! ColdTask(fun ct -> File.ReadAllTextAsync(path, ct))
    }

let program : Flow<ReadmeEnv, FileReadError, string * string> =
    flow {
        let! root = Flow.envWith _.Root // ReadmeEnv.Root -> string
        let settingsFile = Path.Combine(root, "settings.json")
        let featureFlagsFile = Path.Combine(root, "feature-flags.json")

        // The cancellation token is passed implicitly through both file reads.
        let! settings = readTextFile settingsFile // Flow<ReadmeEnv, FileReadError, string>
        let! featureFlags = readTextFile featureFlagsFile // Flow<ReadmeEnv, FileReadError, string>

        return settings, featureFlags // Flow<ReadmeEnv, FileReadError, string * string>
    }

[<EntryPoint>]
let main _ =
    let root =
        // executable host boundary performs this effect.
        // axial-allow-AXG001: intentional guid host or live-service boundary
        Path.Combine(Path.GetTempPath(), "Axial.ReadmeExample", Guid.NewGuid().ToString "N")
    let settingsPath = Path.Combine(root, "settings.json")
    let featureFlagsPath = Path.Combine(root, "feature-flags.json")

    let readPairResult () =
        program.RunSynchronously({ Root = root })

    printfn "Config pair result (before files exist): %s" ((readPairResult ()).ToString())
    // Config pair result: Failure (Fail (NotFound ".../settings.json"))

    // executable host boundary performs this effect.

    // axial-allow-AXG001: intentional filesystem host or live-service boundary
    Directory.CreateDirectory root |> ignore
    // executable host boundary performs this effect.
    // axial-allow-AXG001: intentional filesystem host or live-service boundary
    File.WriteAllText(settingsPath, """{"name":"Ada"}""")
    // executable host boundary performs this effect.
    // axial-allow-AXG001: intentional filesystem host or live-service boundary
    File.WriteAllText(featureFlagsPath, """{"darkMode":true}""")

    printfn "Config pair result (after files created): %s" ((readPairResult ()).ToString())
    // Config pair result: Success ("{\"name\":\"Ada\"}", "{\"darkMode\":true}")

    0
