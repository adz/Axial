namespace Axial.Tests

open System
open System.IO
open System.Text
open Axial.Flow
open Axial.Flow.FileSystem
open Swensen.Unquote
open Xunit

type FileSystemTestEnv =
    { FileSystem: IFileSystem }

    interface IHas<IFileSystem> with
        member this.Service = this.FileSystem

module FileSystemServiceTests =
    let private env () : FileSystemTestEnv =
        { FileSystem = FileSystem.live }

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
