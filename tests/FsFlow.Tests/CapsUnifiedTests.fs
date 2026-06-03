namespace FsFlow.Tests

open System
open System.IO
open System.Text
open System.Threading
open System.Threading.Tasks
open FsFlow
open FsFlow.Services.Console
open FsFlow.Services.FileSystem
open FsFlow.Services.Http
open FsFlow.Services.Process
open Swensen.Unquote
open Xunit

type UnifiedCaps =
    {
        Console: IConsole
        FS: IFileSystem
        Http: IHttp
        Process: IProcess
    }
    interface IHas<IConsole> with
        member this.Service = this.Console
    interface IHas<IFileSystem> with
        member this.Service = this.FS
    interface IHas<IHttp> with
        member this.Service = this.Http
    interface IHas<IProcess> with
        member this.Service = this.Process

module CapsUnifiedTests =
    type MinimalFileSystem() =
        interface IFileSystem with
            member _.ReadAllText(_) = "content"
            member _.ReadAllText(_, _) = "content"
            member _.ReadAllTextAsync(_, _) = Task.FromResult "content"
            member _.ReadAllLines(_) = [| "content" |]
            member _.ReadAllLines(_, _) = [| "content" |]
            member _.ReadAllLinesAsync(_, _) = Task.FromResult [| "content" |]
            member _.ReadAllBytes(_) = [| 1uy; 2uy |]
            member _.ReadAllBytesAsync(_, _) = Task.FromResult [| 1uy; 2uy |]
            member _.WriteAllText(_, _) = ()
            member _.WriteAllText(_, _, _) = ()
            member _.WriteAllTextAsync(_, _, _) = Task.CompletedTask
            member _.WriteAllLines(_, _) = ()
            member _.WriteAllLines(_, _, _) = ()
            member _.WriteAllLinesAsync(_, _, _) = Task.CompletedTask
            member _.WriteAllBytes(_, _) = ()
            member _.WriteAllBytesAsync(_, _, _) = Task.CompletedTask
            member _.AppendAllText(_, _) = ()
            member _.AppendAllText(_, _, _) = ()
            member _.AppendAllTextAsync(_, _, _) = Task.CompletedTask
            member _.AppendAllLines(_, _) = ()
            member _.AppendAllLines(_, _, _) = ()
            member _.FileExists(_) = true
            member _.DeleteFile(_) = ()
            member _.CopyFile(_, _, _) = ()
            member _.MoveFile(_, _, _) = ()
            member _.OpenFile(_, _) = new FileStream(Path.GetTempFileName(), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.ReadWrite)
            member _.OpenFile(_, _, _) = new FileStream(Path.GetTempFileName(), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.ReadWrite)
            member _.OpenFile(_, _, _, _) = new FileStream(Path.GetTempFileName(), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.ReadWrite)
            member _.OpenRead(_) = new MemoryStream([| 1uy; 2uy |]) :> Stream
            member _.OpenText(_) = new StreamReader(new MemoryStream(Encoding.UTF8.GetBytes "content"))
            member _.OpenWrite(_) = new MemoryStream() :> Stream
            member _.CreateFile(_) = new MemoryStream() :> Stream
            member _.CreateText(_) = new StreamWriter(new MemoryStream())
            member _.AppendText(_) = new StreamWriter(new MemoryStream())
            member _.GetFileAttributes(_) = FileAttributes.Normal
            member _.SetFileAttributes(_, _) = ()
            member _.GetFileCreationTime(_) = DateTime(2026, 1, 1)
            member _.GetFileCreationTimeUtc(_) = DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)
            member _.SetFileCreationTime(_, _) = ()
            member _.SetFileCreationTimeUtc(_, _) = ()
            member _.GetFileLastAccessTime(_) = DateTime(2026, 1, 1)
            member _.GetFileLastAccessTimeUtc(_) = DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)
            member _.SetFileLastAccessTime(_, _) = ()
            member _.SetFileLastAccessTimeUtc(_, _) = ()
            member _.GetFileLastWriteTime(_) = DateTime(2026, 1, 1)
            member _.GetFileLastWriteTimeUtc(_) = DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)
            member _.SetFileLastWriteTime(_, _) = ()
            member _.SetFileLastWriteTimeUtc(_, _) = ()
            member _.DirectoryExists(_) = true
            member _.CreateDirectory(_) = ()
            member _.DeleteDirectory(_, _) = ()
            member _.MoveDirectory(_, _) = ()
            member _.EnumerateFiles(_, _, _) = Seq.singleton "test.txt"
            member _.GetFiles(_, _, _) = [| "test.txt" |]
            member _.EnumerateDirectories(_, _, _) = Seq.singleton "folder"
            member _.GetDirectories(_, _, _) = [| "folder" |]
            member _.EnumerateFileSystemEntries(_, _, _) = seq { "test.txt"; "folder" }
            member _.GetFileSystemEntries(_, _, _) = [| "test.txt"; "folder" |]
            member _.GetLogicalDrives() = [| Path.GetPathRoot(Path.GetTempPath()) |]
            member _.GetDirectoryRoot(path) = Path.GetPathRoot path
            member _.GetParent(path) = Path.GetDirectoryName path |> Option.ofObj
            member _.GetCurrentDirectory() = "/work"
            member _.SetCurrentDirectory(_) = ()
            member _.GetDirectoryCreationTime(_) = DateTime(2026, 1, 1)
            member _.GetDirectoryCreationTimeUtc(_) = DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)
            member _.SetDirectoryCreationTime(_, _) = ()
            member _.SetDirectoryCreationTimeUtc(_, _) = ()
            member _.GetDirectoryLastAccessTime(_) = DateTime(2026, 1, 1)
            member _.GetDirectoryLastAccessTimeUtc(_) = DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)
            member _.SetDirectoryLastAccessTime(_, _) = ()
            member _.SetDirectoryLastAccessTimeUtc(_, _) = ()
            member _.GetDirectoryLastWriteTime(_) = DateTime(2026, 1, 1)
            member _.GetDirectoryLastWriteTimeUtc(_) = DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)
            member _.SetDirectoryLastWriteTime(_, _) = ()
            member _.SetDirectoryLastWriteTimeUtc(_, _) = ()
            member _.Combine(paths) = Path.Combine paths
            member _.ChangeExtension(path, extension) = Path.ChangeExtension(path, extension)
            member _.GetDirectoryName(path) = Path.GetDirectoryName path |> Option.ofObj
            member _.GetInvalidFileNameChars() = Path.GetInvalidFileNameChars()
            member _.GetInvalidPathChars() = Path.GetInvalidPathChars()
            member _.GetExtension(path) = Path.GetExtension path
            member _.GetFileName(path) = Path.GetFileName path
            member _.GetFileNameWithoutExtension(path) = Path.GetFileNameWithoutExtension path
            member _.GetFullPath(path) = Path.GetFullPath path
            member _.GetPathRoot(path) = Path.GetPathRoot path |> Option.ofObj
            member _.GetRelativePath(relativeTo, path) = Path.GetRelativePath(relativeTo, path)
            member _.GetTempPath() = Path.GetTempPath()
            member _.GetTempFileName() = Path.GetTempFileName()
            member _.GetRandomFileName() = Path.GetRandomFileName()
            member _.HasExtension(path) = Path.HasExtension path
            member _.EndsInDirectorySeparator(path) = Path.EndsInDirectorySeparator path
            member _.TrimEndingDirectorySeparator(path) = Path.TrimEndingDirectorySeparator path
            member _.IsPathFullyQualified(path) = Path.IsPathFullyQualified path
            member _.IsPathRooted(path) = Path.IsPathRooted path

    [<Fact>]
    let ``Console: read and write`` () =
        let mutable lastMsg = ""
        let caps = { 
            Console = 
                { new IConsole with 
                    member _.ReadLine() = "input"
                    member _.WriteLine(m) = lastMsg <- m }
            FS = Unchecked.defaultof<_>; Http = Unchecked.defaultof<_>; Process = Unchecked.defaultof<_>
        }
        
        let workflow = flow {
            let! input = Console.readLine
            do! Console.writeLine input
            return input
        }
        
        test <@ Flow.runSync caps workflow = Exit.Success "input" @>
        test <@ lastMsg = "input" @>

    [<Fact>]
    let ``FileSystem: exists and read`` () =
        let caps = { 
            FS = MinimalFileSystem()
            Console = Unchecked.defaultof<_>; Http = Unchecked.defaultof<_>; Process = Unchecked.defaultof<_>
        }
        
        let workflow = flow {
            let! exists = FileSystem.exists "test.txt"
            let! text = FileSystem.readAllText "test.txt"
            return exists, text
        }
        
        test <@ Flow.runSync caps workflow = Exit.Success (true, "content") @>

    [<Fact>]
    let ``Http: getString`` () =
        let caps = { 
            Http = { new IHttp with member _.GetString(_) = Task.FromResult "html" }
            Console = Unchecked.defaultof<_>; FS = Unchecked.defaultof<_>; Process = Unchecked.defaultof<_>
        }
        
        test <@ Flow.runSync caps (Http.getString "http://example.com") = Exit.Success "html" @>

    [<Fact>]
    let ``Process: execute`` () =
        let caps = { 
            Process = 
                { new IProcess with 
                    member _.Execute(_, _) = Task.FromResult { ExitCode = 0; StdOut = "out"; StdErr = "" } }
            Console = Unchecked.defaultof<_>; FS = Unchecked.defaultof<_>; Http = Unchecked.defaultof<_>
        }
        
        test <@ Flow.runSync caps (Process.execute "echo" "hi") = Exit.Success { ExitCode = 0; StdOut = "out"; StdErr = "" } @>
