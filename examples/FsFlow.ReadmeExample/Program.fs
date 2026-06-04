open System
open System.IO
open System.Threading
open FsFlow

type ReadmeEnv =
    { Root: string }

type FileReadError =
    | NotFound of path: string

let readTextFile (path: string) : Flow<ReadmeEnv, FileReadError, string> =
    flow {
        // In production, map access and path exceptions separately at the boundary.
        do! Check.okIf (File.Exists path) |> Check.orError (NotFound path)

        return! ColdTask(fun ct -> File.ReadAllTextAsync(path, ct))
    }

let program : Flow<ReadmeEnv, FileReadError, string * string> =
    flow {
        let! root = Flow.read _.Root // ReadmeEnv.Root -> string
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
        Path.Combine(Path.GetTempPath(), "FsFlow.ReadmeExample", Guid.NewGuid().ToString "N")
    let settingsPath = Path.Combine(root, "settings.json")
    let featureFlagsPath = Path.Combine(root, "feature-flags.json")

    let readPairResult () =
        program.RunSynchronously({ Root = root })

    printfn "Config pair result (before files exist): %A" (readPairResult ())
    // Config pair result: Failure (Fail (NotFound ".../settings.json"))

    Directory.CreateDirectory root |> ignore
    File.WriteAllText(settingsPath, """{"name":"Ada"}""")
    File.WriteAllText(featureFlagsPath, """{"darkMode":true}""")

    printfn "Config pair result (after files created): %A" (readPairResult ())
    // Config pair result: Success ("{\"name\":\"Ada\"}", "{\"darkMode\":true}")

    0
