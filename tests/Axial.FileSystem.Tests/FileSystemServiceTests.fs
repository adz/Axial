namespace Axial.Tests

open System
open System.IO
open System.Text
open System.Threading.Tasks
open Axial
open Axial.FileSystem
open Swensen.Unquote
open Xunit

type FileSystemTestEnv =
    { FileSystem: IFileSystem
      Clock: IClock }

    interface IHasClock with
        member this.Clock = this.Clock

    interface IHasFileSystem with
        member this.FileSystem = this.FileSystem

module FileSystemServiceTests =
    [<Fact>]
    let ``isIoFailure includes missing access length and IO but excludes other errors`` () =
        let io =
            [ FileSystemError.FileNotFound "a"
              FileSystemError.DirectoryNotFound "a"
              FileSystemError.Unauthorized(None, "denied")
              FileSystemError.PathTooLong(None, "long")
              FileSystemError.Io(None, "failed") ]
        let other =
            [ FileSystemError.AlreadyExists "a"
              FileSystemError.InvalidPath(None, "invalid")
              FileSystemError.Unsupported(None, "unsupported")
              FileSystemError.Unexpected(None, "unexpected") ]
        Assert.All(io, fun error -> Assert.True(FileSystemError.isIoFailure error))
        Assert.All(other, fun error -> Assert.False(FileSystemError.isIoFailure error))

    [<Fact>]
    let ``live overwrite never exposes a missing destination`` () =
        let root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"))
        Directory.CreateDirectory root |> ignore
        try
            let destination = Path.Combine(root, "destination")
            let source = Path.Combine(root, "source")
            File.WriteAllText(destination, "initial")
            use stop = new System.Threading.CancellationTokenSource()
            let mutable missing = 0
            let reader = Task.Run(fun () ->
                while not stop.IsCancellationRequested do
                    try
                        use input = File.Open(destination, FileMode.Open, FileAccess.Read, FileShare.ReadWrite ||| FileShare.Delete)
                        input.ReadByte() |> ignore
                    with :? FileNotFoundException ->
                        System.Threading.Interlocked.Increment(&missing) |> ignore)
            let until = System.Diagnostics.Stopwatch.StartNew()
            try
                while until.Elapsed < TimeSpan.FromSeconds 1. do
                    File.WriteAllText(source, "next")
                    FileSystem.live.MoveFile(source, destination, true)
            finally
                stop.Cancel()
                reader.GetAwaiter().GetResult()
            Assert.Equal(0, missing)
        finally Directory.Delete(root, true)

    let private clock () : IClock =
        { new IClock with
            member _.UtcNow() = DateTimeOffset.UtcNow
            member _.Elapsed() = TimeSpan.FromMilliseconds(Environment.TickCount64 |> float)
            member _.Sleep(delay, token) = Task.Delay(delay, token) :> Task }

    let private env () : FileSystemTestEnv =
        { FileSystem = FileSystem.live; Clock = clock () }

    let private tempRoot () =
        Path.Combine(Path.GetTempPath(), "axial-tests", Guid.NewGuid().ToString("N"))

    let private withTempRoot test =
        let root = tempRoot ()
        Directory.CreateDirectory root |> ignore

        try
            test root
        finally
            if Directory.Exists root then
                Directory.Delete(root, true)

    let private requireSuccess =
        function
        | Exit.Success value -> value
        | Exit.Failure cause -> failwithf "Expected success, got %A" cause

    [<Fact>]
    let ``listing metadata reports file and directory for live and test services`` () =
        withTempRoot (fun root ->
            for service in [ FileSystem.live; FileSystem.test () :> IFileSystem ] do
                let file = Path.Combine(root, "sample.txt")
                let directory = Path.Combine(root, "child")
                service.CreateDirectory directory
                service.WriteAllText(file, "hello")
                let entries = service.EnumerateFileSystemInfos(root, "*", SearchOption.TopDirectoryOnly) |> Seq.toArray
                test <@ entries.Length = 2 @>
                let text = entries |> Array.find (fun entry -> entry.Name = "sample.txt")
                let child = entries |> Array.find (fun entry -> entry.Name = "child")
                test <@ text.FullPath = file && text.Kind = FileSystemEntryKind.File && text.Size = 5L @>
                test <@ child.FullPath = directory && child.Kind = FileSystemEntryKind.Directory && child.Size = 0L @>)

    [<Fact>]
    let ``metadata listing maps missing directory during enumeration`` () =
        withTempRoot (fun root ->
            let missing = Path.Combine(root, "missing")
            match FileSystem.enumerateFileSystemInfos missing "*" SearchOption.TopDirectoryOnly |> Flow.runSync (env ()) with
            | Exit.Failure(Cause.Fail(FileSystemError.DirectoryNotFound path)) -> test <@ path = missing @>
            | other -> failwithf "Expected DirectoryNotFound, got %A" other)

    [<Fact>]
    let ``test metadata listing snapshots sizes before deletion`` () =
        withTempRoot (fun root ->
            let service = FileSystem.test () :> IFileSystem
            let path = Path.Combine(root, "sample.txt")
            service.WriteAllText(path, "hello")
            let entries = service.EnumerateFileSystemInfos(root, "*", SearchOption.TopDirectoryOnly)
            service.DeleteFile path
            test <@ entries |> Seq.map (fun entry -> entry.Size) |> Seq.toList = [ 5L ] @>)

    [<Fact>]
    let ``async file operations roundtrip and missing reads are typed`` () =
        withTempRoot (fun root ->
            let path = Path.Combine(root, "sample.txt")
            let lines = Path.Combine(root, "lines.txt")
            let bytes = Path.Combine(root, "bytes.bin")
            let workflow = flow {
                do! FileSystem.writeAllTextAsync path "a"
                do! FileSystem.appendAllTextAsync path "b"
                let! text = FileSystem.readAllTextAsync path
                do! FileSystem.writeAllLinesAsync lines [ "one"; "two" ]
                let! readLines = FileSystem.readAllLinesAsync lines
                do! FileSystem.writeAllBytesAsync bytes [| 1uy; 2uy |]
                let! readBytes = FileSystem.readAllBytesAsync bytes
                return text, readLines, readBytes
            }
            let text, readLines, readBytes = Flow.runSync (env ()) workflow |> requireSuccess
            test <@ text = "ab" && readLines = [| "one"; "two" |] && readBytes = [| 1uy; 2uy |] @>
            match FileSystem.readAllTextAsync (Path.Combine(root, "missing")) |> Flow.runSync (env ()) with
            | Exit.Failure (Cause.Fail (FileSystemError.FileNotFound _)) -> ()
            | other -> failwithf "Expected FileNotFound, got %A" other)

    // A live file read cannot be held open portably: FIFO opens can block synchronously before
    // cancellation is observed, while large regular-file reads may finish before interruption.
    [<Fact>]
    let ``interrupting an in-progress file read is not a file-system error`` () =
        let started = TaskCompletionSource<unit>(TaskCreationOptions.RunContinuationsAsynchronously)
        let gate = TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously)
        let service = TestFileSystem(readAllTextAsync = (fun _ ->
            Flow.fromTask (fun cancellationToken ->
                started.TrySetResult(()) |> ignore
                gate.Task.WaitAsync(cancellationToken))))
        let workflow = flow {
            let! fiber = FileSystem.readAllTextAsync "blocked.txt" |> Flow.fork
            do! Flow.fromTask (fun token -> started.Task.WaitAsync(token))
            return! Fiber.interrupt fiber
        }
        let result = Flow.runSync { FileSystem = service :> IFileSystem; Clock = clock () } workflow
        match result with
        | Exit.Success (Exit.Failure Cause.Interrupt) -> ()
        | other -> failwithf "Expected interrupted read, got %A" other

    [<Fact>]
    let ``live file-system service creates and resolves file and directory symbolic links`` () =
        withTempRoot (fun root ->
            let targetFile = Path.Combine(root, "target.txt")
            let fileLink = Path.Combine(root, "file-link")
            let targetDirectory = Path.Combine(root, "target-directory")
            let directoryLink = Path.Combine(root, "directory-link")
            File.WriteAllText(targetFile, "target")
            Directory.CreateDirectory targetDirectory |> ignore

            let workflow =
                flow {
                    do! FileSystem.createFileSymbolicLink fileLink targetFile
                    do! FileSystem.createDirectorySymbolicLink directoryLink targetDirectory
                    let! immediate = FileSystem.getSymbolicLinkTarget fileLink
                    let! finalFile = FileSystem.resolveSymbolicLinkTarget true fileLink
                    let! finalDirectory = FileSystem.resolveSymbolicLinkTarget true directoryLink
                    return immediate, finalFile, finalDirectory
                }

            let immediate, finalFile, finalDirectory = workflow |> Flow.runSync (env ()) |> requireSuccess
            test <@ immediate = Some targetFile @>
            test <@ finalFile = Some targetFile @>
            test <@ finalDirectory = Some targetDirectory @>)

    [<Fact>]
    let ``live file-system service covers common text line byte and stream operations`` () =
        withTempRoot (fun root ->
            let textPath = Path.Combine(root, "text.txt")
            let linePath = Path.Combine(root, "lines.txt")
            let bytesPath = Path.Combine(root, "bytes.bin")
            let streamPath = Path.Combine(root, "stream.txt")

            let workflow =
                flow {
                    do! FileSystem.writeAllTextWithEncoding Encoding.UTF8 textPath "hello"
                    do! FileSystem.appendAllText textPath " world"
                    let! text = FileSystem.readAllTextWithEncoding Encoding.UTF8 textPath

                    do! FileSystem.writeAllLines linePath [ "a"; "b" ]
                    do! FileSystem.appendAllLinesWithEncoding Encoding.UTF8 linePath [ "c" ]
                    let! lines = FileSystem.readAllLines linePath

                    do! FileSystem.writeAllBytesAsync bytesPath [| 1uy; 2uy; 3uy |]
                    let! bytes = FileSystem.readAllBytesAsync bytesPath

                    let! writeStream = FileSystem.openFileWithAccess FileMode.Create FileAccess.Write streamPath
                    do
                        use stream = writeStream
                        stream.Write([| 4uy; 5uy |], 0, 2)

                    let! readStream = FileSystem.openRead streamPath
                    let streamBytes =
                        use stream = readStream
                        use memory = new MemoryStream()
                        stream.CopyTo memory
                        memory.ToArray()

                    let! textReader = FileSystem.openText textPath
                    let firstText =
                        use reader = textReader
                        reader.ReadToEnd()

                    let! textWriter = FileSystem.appendText textPath
                    do
                        use writer = textWriter
                        writer.Write "!"

                    let! finalText = FileSystem.readAllText textPath
                    let! exists = FileSystem.fileExists textPath

                    return text, lines, bytes, streamBytes, firstText, finalText, exists
                }

            let text, lines, bytes, streamBytes, firstText, finalText, exists =
                Flow.runSync (env ()) workflow |> requireSuccess

            test <@ text = "hello world" @>
            test <@ lines = [| "a"; "b"; "c" |] @>
            test <@ bytes = [| 1uy; 2uy; 3uy |] @>
            test <@ streamBytes = [| 4uy; 5uy |] @>
            test <@ firstText = "hello world" @>
            test <@ finalText = "hello world!" @>
            test <@ exists @>)

    [<Fact>]
    let ``live file-system service covers directory enumeration move copy delete and metadata`` () =
        withTempRoot (fun root ->
            let sourceDir = Path.Combine(root, "source")
            let movedDir = Path.Combine(root, "moved")
            let sourceFile = Path.Combine(sourceDir, "source.txt")
            let copyFile = Path.Combine(sourceDir, "copy.txt")
            let movedFile = Path.Combine(sourceDir, "moved.txt")
            let timestamp = DateTime(2026, 1, 2, 3, 4, 5, DateTimeKind.Utc)

            let workflow =
                flow {
                    do! FileSystem.createDirectory sourceDir
                    do! FileSystem.writeAllText sourceFile "content"
                    do! FileSystem.copyFile sourceFile copyFile true
                    do! FileSystem.moveFile copyFile movedFile true

                    let! files = FileSystem.getFiles sourceDir "*.txt" SearchOption.TopDirectoryOnly
                    let! enumeratedFiles = FileSystem.enumerateFiles root "*.txt" SearchOption.AllDirectories
                    let enumerated = Seq.toArray enumeratedFiles
                    let! entries = FileSystem.getFileSystemEntries sourceDir "*" SearchOption.TopDirectoryOnly
                    let! rootExists = FileSystem.directoryExists sourceDir

                    do! FileSystem.setFileLastWriteTimeUtc movedFile timestamp
                    let! fileWriteTime = FileSystem.getFileLastWriteTimeUtc movedFile
                    let! attributes = FileSystem.getFileAttributes movedFile
                    do! FileSystem.setFileAttributes movedFile attributes

                    do! FileSystem.setDirectoryLastWriteTimeUtc sourceDir timestamp
                    let! directoryWriteTime = FileSystem.getDirectoryLastWriteTimeUtc sourceDir
                    let! parent = FileSystem.getParent sourceDir
                    let! directoryRoot = FileSystem.getDirectoryRoot sourceDir
                    let! logicalDrives = FileSystem.getLogicalDrives

                    do! FileSystem.moveDirectory sourceDir movedDir
                    let! movedExists = FileSystem.directoryExists movedDir
                    do! FileSystem.deleteDirectory movedDir true
                    let! deleted = FileSystem.directoryExists movedDir

                    return
                        files,
                        enumerated,
                        entries,
                        rootExists,
                        fileWriteTime,
                        directoryWriteTime,
                        parent,
                        directoryRoot,
                        logicalDrives,
                        movedExists,
                        deleted
                }

            let files, enumerated, entries, rootExists, fileWriteTime, directoryWriteTime, parent, directoryRoot, logicalDrives, movedExists, deleted =
                Flow.runSync (env ()) workflow |> requireSuccess

            test <@ files |> Array.map Path.GetFileName |> Set.ofArray = set [ "source.txt"; "moved.txt" ] @>
            test <@ enumerated |> Array.map Path.GetFileName |> Set.ofArray = set [ "source.txt"; "moved.txt" ] @>
            test <@ entries |> Array.length = 2 @>
            test <@ rootExists @>
            test <@ fileWriteTime = timestamp @>
            test <@ directoryWriteTime = timestamp @>
            test <@ parent = Some root @>
            test <@ not (String.IsNullOrWhiteSpace directoryRoot) @>
            test <@ logicalDrives.Length > 0 @>
            test <@ movedExists @>
            test <@ not deleted @>)

    [<Fact>]
    let ``live file-system service covers path helpers`` () =
        withTempRoot (fun root ->
            let workflow =
                flow {
                    let! combined = FileSystem.combine [| root; "child"; "file.txt" |]
                    let! changed = FileSystem.changeExtension combined ".md"
                    let! directory = FileSystem.getDirectoryName changed
                    let! extension = FileSystem.getExtension changed
                    let! fileName = FileSystem.getFileName changed
                    let! fileNameWithoutExtension = FileSystem.getFileNameWithoutExtension changed
                    let! fullPath = FileSystem.getFullPath changed
                    let! relativePath = FileSystem.getRelativePath root changed
                    let! pathRoot = FileSystem.getPathRoot changed
                    let! tempPath = FileSystem.getTempPath
                    let! tempFileName = FileSystem.getTempFileName
                    let! randomName = FileSystem.getRandomFileName
                    let! hasExtension = FileSystem.hasExtension changed
                    let! endsWithSeparator = FileSystem.endsInDirectorySeparator (root + string Path.DirectorySeparatorChar)
                    let! trimmed = FileSystem.trimEndingDirectorySeparator (root + string Path.DirectorySeparatorChar)
                    let! rooted = FileSystem.isPathRooted changed
                    let! fullyQualified = FileSystem.isPathFullyQualified changed
                    let! invalidFileNameChars = FileSystem.getInvalidFileNameChars
                    let! invalidPathChars = FileSystem.getInvalidPathChars
                    return
                        combined,
                        changed,
                        directory,
                        extension,
                        fileName,
                        fileNameWithoutExtension,
                        fullPath,
                        relativePath,
                        pathRoot,
                        tempPath,
                        tempFileName,
                        randomName,
                        hasExtension,
                        endsWithSeparator,
                        trimmed,
                        rooted,
                        fullyQualified,
                        invalidFileNameChars,
                        invalidPathChars
                }

            let combined, changed, directory, extension, fileName, fileNameWithoutExtension, fullPath, relativePath, pathRoot, tempPath, tempFileName, randomName, hasExtension, endsWithSeparator, trimmed, rooted, fullyQualified, invalidFileNameChars, invalidPathChars =
                Flow.runSync (env ()) workflow |> requireSuccess

            try
                test <@ combined.EndsWith(Path.Combine("child", "file.txt"), StringComparison.Ordinal) @>
                test <@ changed.EndsWith(Path.Combine("child", "file.md"), StringComparison.Ordinal) @>
                test <@ directory = Some(Path.Combine(root, "child")) @>
                test <@ extension = ".md" @>
                test <@ fileName = "file.md" @>
                test <@ fileNameWithoutExtension = "file" @>
                test <@ fullPath = changed @>
                test <@ relativePath = Path.Combine("child", "file.md") @>
                test <@ pathRoot.IsSome @>
                test <@ Directory.Exists tempPath @>
                test <@ File.Exists tempFileName @>
                test <@ not (String.IsNullOrWhiteSpace randomName) @>
                test <@ hasExtension @>
                test <@ endsWithSeparator @>
                test <@ trimmed = root @>
                test <@ rooted @>
                test <@ fullyQualified @>
                test <@ invalidFileNameChars.Length > 0 @>
                test <@ invalidPathChars.Length > 0 @>
            finally
                if File.Exists tempFileName then
                    File.Delete tempFileName)

    [<Fact>]
    let ``missing files are reported as typed file-system failures`` () =
        withTempRoot (fun root ->
            let missing = Path.Combine(root, "missing.txt")

            match Flow.runSync (env ()) (FileSystem.readAllText missing) with
            | Exit.Failure(Cause.Fail(FileSystemError.FileNotFound path)) ->
                test <@ path = missing @>
            | other ->
                failwithf "Expected FileNotFound failure, got %A" other)

    [<Fact>]
    let ``live file-system service reports file length and missing files`` () =
        withTempRoot (fun root ->
            let path = Path.Combine(root, "sized.bin")
            File.WriteAllBytes(path, Array.zeroCreate 1234)

            let length = FileSystem.getFileLength path |> Flow.runSync (env ()) |> requireSuccess
            let missing = FileSystem.getFileLength (Path.Combine(root, "missing.bin")) |> Flow.runSync (env ())

            test <@ length = 1234L @>
            test <@ match missing with Exit.Failure(Cause.Fail(FileSystemError.FileNotFound _)) -> true | _ -> false @>)

    [<Fact>]
    let ``async service members run through flows`` () =
        withTempRoot (fun root ->
            for fileSystem in [ FileSystem.live; FileSystem.test () :> IFileSystem ] do
                let run computation = computation |> Flow.runSync () |> requireSuccess
                let text = Path.Combine(root, "text.txt")
                let lines = Path.Combine(root, "lines.txt")
                let bytes = Path.Combine(root, "bytes.bin")
                fileSystem.WriteAllTextAsync(text, "one") |> run
                fileSystem.AppendAllTextAsync(text, " two") |> run
                let actual = fileSystem.ReadAllTextAsync(text) |> run
                test <@ actual = "one two" @>
                fileSystem.WriteAllLinesAsync(lines, [ "first"; "second" ]) |> run
                let actual = fileSystem.ReadAllLinesAsync(lines) |> run
                test <@ actual = [| "first"; "second" |] @>
                fileSystem.WriteAllBytesAsync(bytes, [| 1uy; 2uy |]) |> run
                let actual = fileSystem.ReadAllBytesAsync(bytes) |> run
                test <@ actual = [| 1uy; 2uy |] @>
                let missing = fileSystem.ReadAllTextAsync(Path.Combine(root, "missing.txt")) |> Flow.runSync ()
                test <@ match missing with Exit.Failure(Cause.Fail(FileSystemError.FileNotFound _)) -> true | _ -> false @>)
