open System
open System.IO
open System.Threading
open Axial
open Axial.FileSystem

type ReadmeEnv =
    { Root: string
      FileSystem: IFileSystem }

    interface IHasFileSystem with
        member this.FileSystem = this.FileSystem

type FileReadError =
    | NotFound of path: string

let readTextFile (path: string) : Flow<ReadmeEnv, FileReadError, string> =
    flow {
        // In production, map access and path exceptions separately at the boundary.
        let! exists = FileSystem.exists path |> Bind.mapError (FileSystemError.describe >> NotFound)
        do! if exists then Flow.succeed () else Flow.fail (NotFound path)
        return! FileSystem.readAllTextAsync path |> Flow.mapError (FileSystemError.describe >> NotFound)
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
    let fileSystem = FileSystem.live
    let root = Path.Combine(fileSystem.GetTempPath(), "Axial.ReadmeExample", fileSystem.GetRandomFileName())
    let settingsPath = Path.Combine(root, "settings.json")
    let featureFlagsPath = Path.Combine(root, "feature-flags.json")

    let readPairResult () =
        program.RunSynchronously({ Root = root; FileSystem = fileSystem })

    printfn "Config pair result (before files exist): %s" ((readPairResult ()).ToString())
    // Config pair result: Failure (Fail (NotFound ".../settings.json"))

    fileSystem.CreateDirectory root
    fileSystem.WriteAllText(settingsPath, """{"name":"Ada"}""")
    fileSystem.WriteAllText(featureFlagsPath, """{"darkMode":true}""")

    printfn "Config pair result (after files created): %s" ((readPairResult ()).ToString())
    // Config pair result: Success ("{\"name\":\"Ada\"}", "{\"darkMode\":true}")

    0
