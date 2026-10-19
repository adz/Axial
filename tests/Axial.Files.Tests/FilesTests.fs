namespace Axial.Files.Tests

open System
open System.IO
open System.Text
open Xunit
open Axial
open Axial.FileSystem
open Axial.Files

type private CleanupEnv =
    { FileSystem: IFileSystem; Clock: IClock }
    interface IHasFileSystem with member this.FileSystem = this.FileSystem
    interface IHasClock with member this.Clock = this.Clock

type private AtomicEnv =
    { FileSystem: IFileSystem; Guid: Axial.PlatformService.IGuid }
    interface IHasFileSystem with member this.FileSystem = this.FileSystem
    interface Axial.PlatformService.IHasGuid with member this.Guid = this.Guid

type FilesTests() = 
    [<Fact>]
    member _.``stale cleanup removes only old matching temporary files``() =
        let root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"))
        Directory.CreateDirectory root |> ignore
        try
            let path = Path.Combine(root, "data")
            let old = Path.Combine(root, ".data." + String.replicate 32 "a" + ".tmp")
            let fresh = Path.Combine(root, ".data." + String.replicate 32 "b" + ".tmp")
            let unrelated = Path.Combine(root, ".data.other.tmp")
            for file in [ old; fresh; unrelated ] do File.WriteAllText(file, "keep")
            let now = DateTimeOffset(2025, 1, 1, 0, 0, 0, TimeSpan.Zero)
            File.SetLastWriteTimeUtc(old, now.UtcDateTime.AddHours(-2.))
            File.SetLastWriteTimeUtc(fresh, now.UtcDateTime)
            let clock =
                { new IClock with
                    member _.UtcNow() = now
                    member _.Elapsed() = TimeSpan.Zero
                    member _.Sleep(_, _) = System.Threading.Tasks.Task.CompletedTask }
            let env = { FileSystem = FileSystem.live; Clock = clock }
            Assert.Equal(Exit.Success (), Files.cleanupStaleTemps path (TimeSpan.FromHours 1.) |> Flow.run env)
            Assert.False(File.Exists old)
            Assert.True(File.Exists fresh)
            Assert.True(File.Exists unrelated)
        finally Directory.Delete(root, true)


    [<Fact>]
    member _.``atomic replacement leaves complete contents and no temporary files``() =
        let root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString())
        Directory.CreateDirectory root |> ignore
        try
            let path = Path.Combine(root, "data")
            File.WriteAllText(path, String.replicate 10000 "old")
            let env =
                { FileSystem = FileSystem.live
                  Guid = Axial.PlatformService.Guid.live }
            Assert.Equal(Exit.Success (), Files.writeAllTextAtomic path "new" |> Flow.run env)
            Assert.Equal("new", File.ReadAllText path)
            Assert.Equal<string>([| "data" |], Directory.GetFiles(root) |> Array.map Path.GetFileName)
            match Files.writeAtomic (Path.Combine(root, "missing", "data")) [| 1uy |] |> Flow.run env with
            | Exit.Failure(Cause.Fail(FileSystemError.DirectoryNotFound _)) -> ()
            | other -> failwithf "%A" other
            Assert.Equal("new", File.ReadAllText path)
            Assert.Equal<string>([| "data" |], Directory.GetFiles(root) |> Array.map Path.GetFileName)
        finally Directory.Delete(root, true)

    [<Fact>]
    member _.``atomic write uses the GUID service for its temporary name``() =
        let root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"))
        Directory.CreateDirectory root |> ignore
        try
            let path = Path.Combine(root, "data")
            let first = Guid.Parse("00112233-4455-6677-8899-aabbccddeeff")
            let second = Guid.Parse("ffeeddcc-bbaa-9988-7766-554433221100")
            let predictableTemp = Path.Combine(root, ".data.00112233445566778899aabbccddeeff.tmp")
            File.WriteAllText(predictableTemp, "existing")
            let env =
                { FileSystem = FileSystem.live
                  Guid = Axial.PlatformService.Guid.fromSequence [ first; second ] }

            Assert.Equal(Exit.Success (), Files.writeAllTextAtomic path "new" |> Flow.run env)
            Assert.Equal("existing", File.ReadAllText predictableTemp)
            Assert.Equal("new", File.ReadAllText path)
        finally Directory.Delete(root, true)

    [<Fact>]
    member _.``atomic write stops after five collisions and preserves the existing temporary file``() =
        let root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"))
        Directory.CreateDirectory root |> ignore
        try
            let path = Path.Combine(root, "data")
            let fixedGuid = Guid.Parse("00112233-4455-6677-8899-aabbccddeeff")
            let temporary = Path.Combine(root, ".data.00112233445566778899aabbccddeeff.tmp")
            File.WriteAllText(temporary, "untouched")
            let mutable calls = 0
            let guid =
                { new Axial.PlatformService.IGuid with
                    member _.NewGuid() =
                        calls <- calls + 1
                        fixedGuid }
            let env = { FileSystem = FileSystem.live; Guid = guid }

            match Files.writeAtomic path [| 1uy |] |> Flow.run env with
            | Exit.Failure(Cause.Fail(_: FileSystemError)) -> ()
            | other -> failwithf "Expected a typed file-system failure, got %A" other
            Assert.Equal(5, calls)
            Assert.Equal("untouched", File.ReadAllText temporary)
            Assert.False(File.Exists path)
        finally Directory.Delete(root, true)

    [<Fact>]
    member _.``concurrent atomic replacements never mix payloads``() =
        let root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString())
        Directory.CreateDirectory root |> ignore
        try
            let path = Path.Combine(root, "data")
            let env =
                { FileSystem = FileSystem.live
                  Guid = Axial.PlatformService.Guid.live }
            let payloads = [| for n in 0 .. 15 -> Array.create 65536 (byte n) |]
            let jobs = payloads |> Array.map (fun bytes -> System.Threading.Tasks.Task.Run(fun () -> Files.writeAtomic path bytes |> Flow.run env))
            System.Threading.Tasks.Task.WaitAll(jobs |> Array.map (fun job -> job :> System.Threading.Tasks.Task))
            for job in jobs do Assert.Equal(Exit.Success (), job.Result)
            let result = File.ReadAllBytes path
            Assert.Contains(payloads, fun payload -> payload = result)
            Assert.Equal(1, Directory.GetFiles(root).Length)
        finally Directory.Delete(root, true)
    [<Fact>]
    member _.``watch delivers changes in order and take disposes``() =
        let fs = TestFileSystem()
        let environment = { new IHasFileSystem with member _.FileSystem = fs :> IFileSystem }
        let changes = [ 1; 2 ] |> List.map (fun n -> { Kind = FileSystemChangeKind.Created; Path = $"/tree/{n}"; OldPath = None })
        let producer = System.Threading.Tasks.Task.Run(fun () ->
            Assert.True(System.Threading.SpinWait.SpinUntil((fun () -> fs.ActiveWatchers = 1), 5000))
            for change in changes do fs.RaiseChange change)
        let result = Files.watch true "/tree" |> FlowStream.take 2 |> FlowStream.runCollect |> Flow.run environment
        producer.GetAwaiter().GetResult()
        Assert.Equal(Exit.Success changes, result)
        Assert.Equal(0, fs.ActiveWatchers)

    [<Fact>]
    member _.``nonrecursive watch ignores nested changes``() =
        let fs = TestFileSystem()
        let direct = { Kind = FileSystemChangeKind.Created; Path = "/tree/direct"; OldPath = None }
        let producer = System.Threading.Tasks.Task.Run(fun () ->
            Assert.True(System.Threading.SpinWait.SpinUntil((fun () -> fs.ActiveWatchers = 1), 5000))
            fs.RaiseChange { direct with Path = "/tree/nested/hidden" }
            fs.RaiseChange direct)
        let result = Files.watch false "/tree" |> FlowStream.take 1 |> FlowStream.runCollect
                     |> Flow.run { new IHasFileSystem with member _.FileSystem = fs :> IFileSystem }
        producer.GetAwaiter().GetResult()
        Assert.Equal(Exit.Success [ direct ], result)

    [<Fact>]
    member _.``watch error is typed``() =
        let fs = TestFileSystem()
        let producer = System.Threading.Tasks.Task.Run(fun () ->
            Assert.True(System.Threading.SpinWait.SpinUntil((fun () -> fs.ActiveWatchers = 1), 5000))
            fs.RaiseWatchError(IOException("broken")))
        let result = Files.watch true "/tree" |> FlowStream.runCollect |> Flow.run { new IHasFileSystem with member _.FileSystem = fs :> IFileSystem }
        producer.GetAwaiter().GetResult()
        match result with
        | Exit.Failure(Cause.Fail(FileSystemError.Io(Some "/tree", "broken"))) -> ()
        | other -> failwithf "%A" other

    [<Fact>]
    member _.``live recursive watch sees nested creation``() =
        let root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString())
        let nested = Path.Combine(root, "nested")
        Directory.CreateDirectory nested |> ignore
        try
            let target = Path.Combine(nested, "created.txt")
            let producer = System.Threading.Tasks.Task.Run(fun () ->
                System.Threading.Thread.Sleep 300
                File.WriteAllText(target, "hello"))
            let consumer = System.Threading.Tasks.Task.Run(fun () ->
                Files.watch true root
                |> FlowStream.filter (fun change -> change.Kind = FileSystemChangeKind.Created && change.Path = target)
                |> FlowStream.runTryHead
                |> Flow.run { new IHasFileSystem with member _.FileSystem = FileSystem.live })
            Assert.True(consumer.Wait(TimeSpan.FromSeconds 30.), $"Timed out waiting for creation of {target}")
            let result = consumer.Result
            producer.GetAwaiter().GetResult()
            Assert.Equal(Exit.Success(Some { Kind = FileSystemChangeKind.Created; Path = target; OldPath = None }), result)
        finally Directory.Delete(root, true)

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
