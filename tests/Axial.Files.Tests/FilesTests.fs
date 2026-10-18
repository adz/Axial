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
    member _.``walk uses in-memory file system and respects descend``() =
        let fs = FileSystem.test () :> IFileSystem
        fs.CreateDirectory "/tree"
        fs.CreateDirectory "/tree/enter"
        fs.CreateDirectory "/tree/skip"
        fs.WriteAllText("/tree/root.txt", "root")
        fs.WriteAllText("/tree/enter/visible.txt", "visible")
        fs.WriteAllText("/tree/skip/hidden.txt", "hidden")
        let result =
            Files.walk (fun path -> path <> "/tree/skip") "/tree"
            |> FlowStream.runCollect
            |> Flow.run { new IHasFileSystem with member _.FileSystem = fs }
        match result with
        | Exit.Success entries ->
            Assert.Equal<string list>(
                [ "/tree/enter"; "/tree/root.txt"; "/tree/skip"; "/tree/enter/visible.txt" ] |> List.sort,
                entries |> List.map _.FullPath |> List.sort)
        | other -> failwithf "%A" other

    [<Fact>]
    member _.``walk without descent lists only direct children``() =
        let fs = FileSystem.test () :> IFileSystem
        fs.CreateDirectory "/tree"
        fs.CreateDirectory "/tree/child"
        fs.WriteAllText("/tree/file", "a")
        fs.WriteAllText("/tree/child/grandchild", "b")
        let result =
            Files.walk (fun _ -> false) "/tree"
            |> FlowStream.runCollect
            |> Flow.run { new IHasFileSystem with member _.FileSystem = fs }
        match result with
        | Exit.Success entries ->
            Assert.Equal<string list>([ "/tree/child"; "/tree/file" ], entries |> List.map _.FullPath |> List.sort)
        | other -> failwithf "%A" other

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

    [<Fact>]
    member _.``lines reads UTF-16 LE BOM``() =
        let path = Path.GetTempFileName()
        try
            File.WriteAllBytes(path, Array.append [| 0xFFuy; 0xFEuy |] (Encoding.Unicode.GetBytes("alpha\nbeta")))
            let result = Files.lines path |> FlowStream.runCollect |> Flow.run { new IHasFileSystem with member _.FileSystem = FileSystem.live }
            match result with
            | Exit.Success lines -> Assert.Equal<string list>([ "alpha"; "beta" ], lines)
            | other -> failwithf "%A" other
        finally File.Delete path

    [<Fact>]
    member _.``lines skips binary content containing NUL``() =
        let path = Path.GetTempFileName()
        try
            File.WriteAllBytes(path, [| 65uy; 0uy; 66uy |])
            let result = Files.lines path |> FlowStream.runCollect |> Flow.run { new IHasFileSystem with member _.FileSystem = FileSystem.live }
            match result with
            | Exit.Success lines -> Assert.Empty lines
            | other -> failwithf "%A" other
        finally File.Delete path

    [<Fact>]
    member _.``taking one line releases the reader``() =
        let path = Path.GetTempFileName()
        try
            File.WriteAllText(path, "first\nsecond")
            let result =
                Files.lines path
                |> FlowStream.take 1
                |> FlowStream.runCollect
                |> Flow.run { new IHasFileSystem with member _.FileSystem = FileSystem.live }
            match result with
            | Exit.Success lines -> Assert.Equal<string list>([ "first" ], lines)
            | other -> failwithf "%A" other
            use exclusive = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None)
            Assert.True(exclusive.CanRead)
        finally File.Delete path

    [<Fact>]
    member _.``lines streams ten thousand lines in order``() =
        let path = Path.GetTempFileName()
        try
            let expected = [ for i in 0 .. 9999 -> string i ]
            File.WriteAllLines(path, expected)
            let result = Files.lines path |> FlowStream.runCollect |> Flow.run { new IHasFileSystem with member _.FileSystem = FileSystem.live }
            match result with
            | Exit.Success lines -> Assert.Equal<string list>(expected, lines)
            | other -> failwithf "%A" other
        finally File.Delete path

    [<Fact>]
    member _.``lines reports missing file as typed error``() =
        let path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString())
        let result = Files.lines path |> FlowStream.runCollect |> Flow.run { new IHasFileSystem with member _.FileSystem = FileSystem.live }
        match result with
        | Exit.Failure(Cause.Fail(FileSystemError.FileNotFound missing)) -> Assert.Equal(path, missing)
        | other -> failwithf "Expected FileNotFound, got %A" other
