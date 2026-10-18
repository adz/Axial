namespace Axial.Files.Tests

open System
open System.IO
open System.Text
open Xunit
open Axial
open Axial.FileSystem
open Axial.Files

type FilesTests() =
    [<Fact>]
    member _.``walk lists breadth first and skips excluded directories``() =
        let root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString())
        let entered = Path.Combine(root, "entered")
        let skipped = Path.Combine(root, "skipped")
        Directory.CreateDirectory entered |> ignore
        Directory.CreateDirectory skipped |> ignore
        try
            File.WriteAllText(Path.Combine(entered, "child"), "text")
            File.WriteAllText(Path.Combine(skipped, "hidden"), "text")
            let result =
                Files.walk (fun path -> path <> skipped) root
                |> FlowStream.runCollect
                |> Flow.run { new IHasFileSystem with member _.FileSystem = FileSystem.live }
            match result with
            | Exit.Success entries ->
                Assert.Contains(entries, fun entry -> entry.FullPath = entered && entry.Kind = FileSystemEntryKind.Directory)
                Assert.Contains(entries, fun entry -> entry.FullPath = skipped && entry.Kind = FileSystemEntryKind.Directory)
                Assert.Contains(entries, fun entry -> entry.Name = "child" && entry.Kind = FileSystemEntryKind.File)
                Assert.DoesNotContain(entries, fun entry -> entry.Name = "hidden")
            | other -> failwithf "%A" other
        finally Directory.Delete(root, true)

    [<Fact>]
    member _.``UTF files stream and binary files are empty``() =
        let root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString())
        Directory.CreateDirectory root |> ignore
        try
            for encoding in [ Encoding.UTF8; Encoding.Unicode ] do
                let path = Path.Combine(root, Guid.NewGuid().ToString())
                File.WriteAllText(path, "one\ntwo", encoding)
                let result = Files.lines path |> FlowStream.runCollect |> Flow.run { new IHasFileSystem with member _.FileSystem = FileSystem.live }
                match result with
                | Exit.Success values -> Assert.Equal<string list>([ "one"; "two" ], values)
                | other -> failwithf "%A" other
            let binary = Path.Combine(root, "binary")
            File.WriteAllBytes(binary, [| 1uy; 0uy; 2uy |])
            let result = Files.lines binary |> FlowStream.runCollect |> Flow.run { new IHasFileSystem with member _.FileSystem = FileSystem.live }
            match result with
            | Exit.Success values -> Assert.Empty values
            | other -> failwithf "%A" other
        finally Directory.Delete(root, true)
