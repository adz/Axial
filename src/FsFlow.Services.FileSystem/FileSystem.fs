namespace FsFlow.Services.FileSystem

open System
open System.IO
open System.Text
open System.Threading
open System.Threading.Tasks
open FsFlow

/// <summary>Describes a meaningful file-system failure.</summary>
[<RequireQualifiedAccess>]
type FileSystemError =
    /// <summary>A file was not found.</summary>
    | FileNotFound of path: string

    /// <summary>A directory was not found.</summary>
    | DirectoryNotFound of path: string

    /// <summary>The requested path already exists.</summary>
    | AlreadyExists of path: string

    /// <summary>The caller is not authorized to access the requested path.</summary>
    | Unauthorized of path: string option * message: string

    /// <summary>The requested path was invalid.</summary>
    | InvalidPath of path: string option * message: string

    /// <summary>The requested path was too long for the platform.</summary>
    | PathTooLong of path: string option * message: string

    /// <summary>The operation was canceled.</summary>
    | Canceled of message: string

    /// <summary>The operation failed with a general I/O error.</summary>
    | Io of path: string option * message: string

    /// <summary>The operation is not supported by the current platform or path shape.</summary>
    | Unsupported of path: string option * message: string

    /// <summary>An unexpected exception escaped a file-system operation.</summary>
    | Unexpected of path: string option * message: string

/// <summary>Provides access to common file, directory, and path operations.</summary>
type IFileSystem =
    /// <summary>Opens a text file, reads all text, and then closes the file.</summary>
    abstract ReadAllText : path: string -> string

    /// <summary>Opens a text file with the specified encoding, reads all text, and then closes the file.</summary>
    abstract ReadAllText : path: string * encoding: Encoding -> string

    /// <summary>Asynchronously opens a text file, reads all text, and then closes the file.</summary>
    abstract ReadAllTextAsync : path: string * cancellationToken: CancellationToken -> Task<string>

    /// <summary>Opens a text file, reads all lines, and then closes the file.</summary>
    abstract ReadAllLines : path: string -> string array

    /// <summary>Opens a text file with the specified encoding, reads all lines, and then closes the file.</summary>
    abstract ReadAllLines : path: string * encoding: Encoding -> string array

    /// <summary>Asynchronously opens a text file, reads all lines, and then closes the file.</summary>
    abstract ReadAllLinesAsync : path: string * cancellationToken: CancellationToken -> Task<string array>

    /// <summary>Opens a binary file, reads all bytes, and then closes the file.</summary>
    abstract ReadAllBytes : path: string -> byte array

    /// <summary>Asynchronously opens a binary file, reads all bytes, and then closes the file.</summary>
    abstract ReadAllBytesAsync : path: string * cancellationToken: CancellationToken -> Task<byte array>

    /// <summary>Creates or overwrites a text file, writes all text, and then closes the file.</summary>
    abstract WriteAllText : path: string * contents: string -> unit

    /// <summary>Creates or overwrites a text file with the specified encoding, writes all text, and then closes the file.</summary>
    abstract WriteAllText : path: string * contents: string * encoding: Encoding -> unit

    /// <summary>Asynchronously creates or overwrites a text file, writes all text, and then closes the file.</summary>
    abstract WriteAllTextAsync : path: string * contents: string * cancellationToken: CancellationToken -> Task

    /// <summary>Creates or overwrites a text file, writes all lines, and then closes the file.</summary>
    abstract WriteAllLines : path: string * contents: seq<string> -> unit

    /// <summary>Creates or overwrites a text file with the specified encoding, writes all lines, and then closes the file.</summary>
    abstract WriteAllLines : path: string * contents: seq<string> * encoding: Encoding -> unit

    /// <summary>Asynchronously creates or overwrites a text file, writes all lines, and then closes the file.</summary>
    abstract WriteAllLinesAsync : path: string * contents: seq<string> * cancellationToken: CancellationToken -> Task

    /// <summary>Creates or overwrites a binary file, writes all bytes, and then closes the file.</summary>
    abstract WriteAllBytes : path: string * contents: byte array -> unit

    /// <summary>Asynchronously creates or overwrites a binary file, writes all bytes, and then closes the file.</summary>
    abstract WriteAllBytesAsync : path: string * contents: byte array * cancellationToken: CancellationToken -> Task

    /// <summary>Appends text to a file, creating the file if needed.</summary>
    abstract AppendAllText : path: string * contents: string -> unit

    /// <summary>Appends text to a file with the specified encoding, creating the file if needed.</summary>
    abstract AppendAllText : path: string * contents: string * encoding: Encoding -> unit

    /// <summary>Asynchronously appends text to a file, creating the file if needed.</summary>
    abstract AppendAllTextAsync : path: string * contents: string * cancellationToken: CancellationToken -> Task

    /// <summary>Appends lines to a file, creating the file if needed.</summary>
    abstract AppendAllLines : path: string * contents: seq<string> -> unit

    /// <summary>Appends lines to a file with the specified encoding, creating the file if needed.</summary>
    abstract AppendAllLines : path: string * contents: seq<string> * encoding: Encoding -> unit

    /// <summary>Determines whether the specified file exists.</summary>
    abstract FileExists : path: string -> bool

    /// <summary>Deletes the specified file if it exists.</summary>
    abstract DeleteFile : path: string -> unit

    /// <summary>Copies a file to a destination path.</summary>
    abstract CopyFile : sourcePath: string * destinationPath: string * overwrite: bool -> unit

    /// <summary>Moves a file to a destination path.</summary>
    abstract MoveFile : sourcePath: string * destinationPath: string * overwrite: bool -> unit

    /// <summary>Opens a file with the specified mode.</summary>
    abstract OpenFile : path: string * mode: FileMode -> FileStream

    /// <summary>Opens a file with the specified mode and access.</summary>
    abstract OpenFile : path: string * mode: FileMode * access: FileAccess -> FileStream

    /// <summary>Opens a file with the specified mode, access, and sharing behavior.</summary>
    abstract OpenFile : path: string * mode: FileMode * access: FileAccess * share: FileShare -> FileStream

    /// <summary>Opens an existing file for reading.</summary>
    abstract OpenRead : path: string -> Stream

    /// <summary>Opens an existing UTF-8 text file for reading.</summary>
    abstract OpenText : path: string -> StreamReader

    /// <summary>Opens a file for writing, creating it if needed.</summary>
    abstract OpenWrite : path: string -> Stream

    /// <summary>Creates or overwrites a file and returns the open stream.</summary>
    abstract CreateFile : path: string -> Stream

    /// <summary>Creates or overwrites a UTF-8 text file and returns the open writer.</summary>
    abstract CreateText : path: string -> StreamWriter

    /// <summary>Opens a UTF-8 text writer that appends to a file, creating the file if needed.</summary>
    abstract AppendText : path: string -> StreamWriter

    /// <summary>Gets file attributes.</summary>
    abstract GetFileAttributes : path: string -> FileAttributes

    /// <summary>Sets file attributes.</summary>
    abstract SetFileAttributes : path: string * attributes: FileAttributes -> unit

    /// <summary>Gets file creation time.</summary>
    abstract GetFileCreationTime : path: string -> DateTime

    /// <summary>Gets file creation time in UTC.</summary>
    abstract GetFileCreationTimeUtc : path: string -> DateTime

    /// <summary>Sets file creation time.</summary>
    abstract SetFileCreationTime : path: string * creationTime: DateTime -> unit

    /// <summary>Sets file creation time in UTC.</summary>
    abstract SetFileCreationTimeUtc : path: string * creationTimeUtc: DateTime -> unit

    /// <summary>Gets file last access time.</summary>
    abstract GetFileLastAccessTime : path: string -> DateTime

    /// <summary>Gets file last access time in UTC.</summary>
    abstract GetFileLastAccessTimeUtc : path: string -> DateTime

    /// <summary>Sets file last access time.</summary>
    abstract SetFileLastAccessTime : path: string * lastAccessTime: DateTime -> unit

    /// <summary>Sets file last access time in UTC.</summary>
    abstract SetFileLastAccessTimeUtc : path: string * lastAccessTimeUtc: DateTime -> unit

    /// <summary>Gets file last write time.</summary>
    abstract GetFileLastWriteTime : path: string -> DateTime

    /// <summary>Gets file last write time in UTC.</summary>
    abstract GetFileLastWriteTimeUtc : path: string -> DateTime

    /// <summary>Sets file last write time.</summary>
    abstract SetFileLastWriteTime : path: string * lastWriteTime: DateTime -> unit

    /// <summary>Sets file last write time in UTC.</summary>
    abstract SetFileLastWriteTimeUtc : path: string * lastWriteTimeUtc: DateTime -> unit

    /// <summary>Determines whether the specified directory exists.</summary>
    abstract DirectoryExists : path: string -> bool

    /// <summary>Creates a directory and any missing parent directories.</summary>
    abstract CreateDirectory : path: string -> unit

    /// <summary>Deletes a directory.</summary>
    abstract DeleteDirectory : path: string * recursive: bool -> unit

    /// <summary>Moves a directory to a destination path.</summary>
    abstract MoveDirectory : sourcePath: string * destinationPath: string -> unit

    /// <summary>Enumerates files in a directory.</summary>
    abstract EnumerateFiles : path: string * searchPattern: string * searchOption: SearchOption -> seq<string>

    /// <summary>Gets files in a directory.</summary>
    abstract GetFiles : path: string * searchPattern: string * searchOption: SearchOption -> string array

    /// <summary>Enumerates directories in a directory.</summary>
    abstract EnumerateDirectories : path: string * searchPattern: string * searchOption: SearchOption -> seq<string>

    /// <summary>Gets directories in a directory.</summary>
    abstract GetDirectories : path: string * searchPattern: string * searchOption: SearchOption -> string array

    /// <summary>Enumerates files and directories in a directory.</summary>
    abstract EnumerateFileSystemEntries : path: string * searchPattern: string * searchOption: SearchOption -> seq<string>

    /// <summary>Gets files and directories in a directory.</summary>
    abstract GetFileSystemEntries : path: string * searchPattern: string * searchOption: SearchOption -> string array

    /// <summary>Gets logical drives for the current machine.</summary>
    abstract GetLogicalDrives : unit -> string array

    /// <summary>Gets the root portion of a directory path.</summary>
    abstract GetDirectoryRoot : path: string -> string

    /// <summary>Gets the parent directory path.</summary>
    abstract GetParent : path: string -> string option

    /// <summary>Gets the current working directory.</summary>
    abstract GetCurrentDirectory : unit -> string

    /// <summary>Sets the current working directory.</summary>
    abstract SetCurrentDirectory : path: string -> unit

    /// <summary>Gets directory creation time.</summary>
    abstract GetDirectoryCreationTime : path: string -> DateTime

    /// <summary>Gets directory creation time in UTC.</summary>
    abstract GetDirectoryCreationTimeUtc : path: string -> DateTime

    /// <summary>Sets directory creation time.</summary>
    abstract SetDirectoryCreationTime : path: string * creationTime: DateTime -> unit

    /// <summary>Sets directory creation time in UTC.</summary>
    abstract SetDirectoryCreationTimeUtc : path: string * creationTimeUtc: DateTime -> unit

    /// <summary>Gets directory last access time.</summary>
    abstract GetDirectoryLastAccessTime : path: string -> DateTime

    /// <summary>Gets directory last access time in UTC.</summary>
    abstract GetDirectoryLastAccessTimeUtc : path: string -> DateTime

    /// <summary>Sets directory last access time.</summary>
    abstract SetDirectoryLastAccessTime : path: string * lastAccessTime: DateTime -> unit

    /// <summary>Sets directory last access time in UTC.</summary>
    abstract SetDirectoryLastAccessTimeUtc : path: string * lastAccessTimeUtc: DateTime -> unit

    /// <summary>Gets directory last write time.</summary>
    abstract GetDirectoryLastWriteTime : path: string -> DateTime

    /// <summary>Gets directory last write time in UTC.</summary>
    abstract GetDirectoryLastWriteTimeUtc : path: string -> DateTime

    /// <summary>Sets directory last write time.</summary>
    abstract SetDirectoryLastWriteTime : path: string * lastWriteTime: DateTime -> unit

    /// <summary>Sets directory last write time in UTC.</summary>
    abstract SetDirectoryLastWriteTimeUtc : path: string * lastWriteTimeUtc: DateTime -> unit

    /// <summary>Combines path segments into one path.</summary>
    abstract Combine : paths: string array -> string

    /// <summary>Changes the extension of a path.</summary>
    abstract ChangeExtension : path: string * extension: string -> string

    /// <summary>Returns the directory name for a path.</summary>
    abstract GetDirectoryName : path: string -> string option

    /// <summary>Returns invalid file-name characters for the platform.</summary>
    abstract GetInvalidFileNameChars : unit -> char array

    /// <summary>Returns invalid path characters for the platform.</summary>
    abstract GetInvalidPathChars : unit -> char array

    /// <summary>Returns the extension for a path.</summary>
    abstract GetExtension : path: string -> string

    /// <summary>Returns the file name for a path.</summary>
    abstract GetFileName : path: string -> string

    /// <summary>Returns the file name without extension for a path.</summary>
    abstract GetFileNameWithoutExtension : path: string -> string

    /// <summary>Returns the full path for a path.</summary>
    abstract GetFullPath : path: string -> string

    /// <summary>Returns the root portion of a path.</summary>
    abstract GetPathRoot : path: string -> string option

    /// <summary>Returns a path relative to another path.</summary>
    abstract GetRelativePath : relativeTo: string * path: string -> string

    /// <summary>Returns the temporary directory path.</summary>
    abstract GetTempPath : unit -> string

    /// <summary>Creates a uniquely named zero-byte temporary file and returns its path.</summary>
    abstract GetTempFileName : unit -> string

    /// <summary>Returns a random file or directory name.</summary>
    abstract GetRandomFileName : unit -> string

    /// <summary>Returns whether a path has an extension.</summary>
    abstract HasExtension : path: string -> bool

    /// <summary>Returns whether a path ends in a directory separator.</summary>
    abstract EndsInDirectorySeparator : path: string -> bool

    /// <summary>Trims one trailing directory separator from a path.</summary>
    abstract TrimEndingDirectorySeparator : path: string -> string

    /// <summary>Returns whether a path is fully qualified.</summary>
    abstract IsPathFullyQualified : path: string -> bool

    /// <summary>Returns whether a path contains a root.</summary>
    abstract IsPathRooted : path: string -> bool

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
[<RequireQualifiedAccess>]
module FileSystemError =
    let private describePath path =
        defaultArg path "<unknown>"

    /// <summary>Classifies an exception raised by a file-system operation.</summary>
    let fromException (path: string option) (error: exn) : FileSystemError =
        match error with
        | :? FileNotFoundException as fileNotFound ->
            FileSystemError.FileNotFound(defaultArg (Option.ofObj fileNotFound.FileName) (defaultArg path ""))
        | :? DirectoryNotFoundException ->
            FileSystemError.DirectoryNotFound(defaultArg path "")
        | :? UnauthorizedAccessException ->
            FileSystemError.Unauthorized(path, error.Message)
        | :? PathTooLongException ->
            FileSystemError.PathTooLong(path, error.Message)
        | :? OperationCanceledException ->
            FileSystemError.Canceled error.Message
        | :? ArgumentException ->
            FileSystemError.InvalidPath(path, error.Message)
        | :? NotSupportedException ->
            FileSystemError.Unsupported(path, error.Message)
        | :? IOException ->
            FileSystemError.Io(path, error.Message)
        | _ ->
            FileSystemError.Unexpected(path, error.Message)

    /// <summary>Formats a human-readable description for a file-system error.</summary>
    let describe =
        function
        | FileSystemError.FileNotFound path -> $"File not found: {path}"
        | FileSystemError.DirectoryNotFound path -> $"Directory not found: {path}"
        | FileSystemError.AlreadyExists path -> $"Path already exists: {path}"
        | FileSystemError.Unauthorized(path, message) -> $"Unauthorized file-system access at {describePath path}: {message}"
        | FileSystemError.InvalidPath(path, message) -> $"Invalid path {describePath path}: {message}"
        | FileSystemError.PathTooLong(path, message) -> $"Path too long {describePath path}: {message}"
        | FileSystemError.Canceled message -> $"File-system operation canceled: {message}"
        | FileSystemError.Io(path, message) -> $"File-system I/O error at {describePath path}: {message}"
        | FileSystemError.Unsupported(path, message) -> $"Unsupported file-system operation at {describePath path}: {message}"
        | FileSystemError.Unexpected(path, message) -> $"Unexpected file-system error at {describePath path}: {message}"

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
[<RequireQualifiedAccess>]
module FileSystem =
    let private directorySeparators =
        [| Path.DirectorySeparatorChar; Path.AltDirectorySeparatorChar |]

    let private endsInDirectorySeparatorRaw (path: string) =
        not (String.IsNullOrEmpty path)
        && Array.contains path[path.Length - 1] directorySeparators

    let private trimEndingDirectorySeparatorRaw (path: string) =
        if endsInDirectorySeparatorRaw path then
            path.Substring(0, path.Length - 1)
        else
            path

    let private protect path flow =
        flow |> Flow.catch (FileSystemError.fromException path)

    let private withService
        (path: string option)
        (operation: IFileSystem -> 'value)
        : Flow<'env, FileSystemError, 'value>
        when 'env :> IHas<IFileSystem> =
        flow {
            let! fileSystem = Service<IFileSystem>.get()
            return operation fileSystem
        }
        |> protect path

    let private withServiceAsync
        (path: string option)
        (operation: IFileSystem -> CancellationToken -> Task<'value>)
        : Flow<'env, FileSystemError, 'value>
        when 'env :> IHas<IFileSystem> =
        flow {
            let! fileSystem = Service<IFileSystem>.get()
            let! cancellationToken = Flow.Runtime.cancellationToken
            return! operation fileSystem cancellationToken
        }
        |> protect path

    let private withServiceTask
        (path: string option)
        (operation: IFileSystem -> CancellationToken -> Task)
        : Flow<'env, FileSystemError, unit>
        when 'env :> IHas<IFileSystem> =
        flow {
            let! fileSystem = Service<IFileSystem>.get()
            let! cancellationToken = Flow.Runtime.cancellationToken
            do! operation fileSystem cancellationToken
        }
        |> protect path

    /// <summary>Reads all text through an explicit file-system service.</summary>
    let readAllText<'env when 'env :> IHas<IFileSystem>>
        (path: string)
        : Flow<'env, FileSystemError, string> =
        withService (Some path) (fun fileSystem -> fileSystem.ReadAllText path)

    /// <summary>Reads all text with the specified encoding through an explicit file-system service.</summary>
    let readAllTextWithEncoding<'env when 'env :> IHas<IFileSystem>>
        (encoding: Encoding)
        (path: string)
        : Flow<'env, FileSystemError, string> =
        withService (Some path) (fun fileSystem -> fileSystem.ReadAllText(path, encoding))

    /// <summary>Asynchronously reads all text through an explicit file-system service.</summary>
    let readAllTextAsync<'env when 'env :> IHas<IFileSystem>>
        (path: string)
        : Flow<'env, FileSystemError, string> =
        withServiceAsync (Some path) (fun fileSystem cancellationToken -> fileSystem.ReadAllTextAsync(path, cancellationToken))

    /// <summary>Reads all lines through an explicit file-system service.</summary>
    let readAllLines<'env when 'env :> IHas<IFileSystem>>
        (path: string)
        : Flow<'env, FileSystemError, string array> =
        withService (Some path) (fun fileSystem -> fileSystem.ReadAllLines path)

    /// <summary>Reads all lines with the specified encoding through an explicit file-system service.</summary>
    let readAllLinesWithEncoding<'env when 'env :> IHas<IFileSystem>>
        (encoding: Encoding)
        (path: string)
        : Flow<'env, FileSystemError, string array> =
        withService (Some path) (fun fileSystem -> fileSystem.ReadAllLines(path, encoding))

    /// <summary>Asynchronously reads all lines through an explicit file-system service.</summary>
    let readAllLinesAsync<'env when 'env :> IHas<IFileSystem>>
        (path: string)
        : Flow<'env, FileSystemError, string array> =
        withServiceAsync (Some path) (fun fileSystem cancellationToken -> fileSystem.ReadAllLinesAsync(path, cancellationToken))

    /// <summary>Reads all bytes through an explicit file-system service.</summary>
    let readAllBytes<'env when 'env :> IHas<IFileSystem>>
        (path: string)
        : Flow<'env, FileSystemError, byte array> =
        withService (Some path) (fun fileSystem -> fileSystem.ReadAllBytes path)

    /// <summary>Asynchronously reads all bytes through an explicit file-system service.</summary>
    let readAllBytesAsync<'env when 'env :> IHas<IFileSystem>>
        (path: string)
        : Flow<'env, FileSystemError, byte array> =
        withServiceAsync (Some path) (fun fileSystem cancellationToken -> fileSystem.ReadAllBytesAsync(path, cancellationToken))

    /// <summary>Writes all text through an explicit file-system service.</summary>
    let writeAllText<'env when 'env :> IHas<IFileSystem>>
        (path: string)
        (contents: string)
        : Flow<'env, FileSystemError, unit> =
        withService (Some path) (fun fileSystem -> fileSystem.WriteAllText(path, contents))

    /// <summary>Writes all text with the specified encoding through an explicit file-system service.</summary>
    let writeAllTextWithEncoding<'env when 'env :> IHas<IFileSystem>>
        (encoding: Encoding)
        (path: string)
        (contents: string)
        : Flow<'env, FileSystemError, unit> =
        withService (Some path) (fun fileSystem -> fileSystem.WriteAllText(path, contents, encoding))

    /// <summary>Asynchronously writes all text through an explicit file-system service.</summary>
    let writeAllTextAsync<'env when 'env :> IHas<IFileSystem>>
        (path: string)
        (contents: string)
        : Flow<'env, FileSystemError, unit> =
        withServiceTask (Some path) (fun fileSystem cancellationToken -> fileSystem.WriteAllTextAsync(path, contents, cancellationToken))

    /// <summary>Writes all lines through an explicit file-system service.</summary>
    let writeAllLines<'env when 'env :> IHas<IFileSystem>>
        (path: string)
        (contents: seq<string>)
        : Flow<'env, FileSystemError, unit> =
        withService (Some path) (fun fileSystem -> fileSystem.WriteAllLines(path, contents))

    /// <summary>Writes all lines with the specified encoding through an explicit file-system service.</summary>
    let writeAllLinesWithEncoding<'env when 'env :> IHas<IFileSystem>>
        (encoding: Encoding)
        (path: string)
        (contents: seq<string>)
        : Flow<'env, FileSystemError, unit> =
        withService (Some path) (fun fileSystem -> fileSystem.WriteAllLines(path, contents, encoding))

    /// <summary>Asynchronously writes all lines through an explicit file-system service.</summary>
    let writeAllLinesAsync<'env when 'env :> IHas<IFileSystem>>
        (path: string)
        (contents: seq<string>)
        : Flow<'env, FileSystemError, unit> =
        withServiceTask (Some path) (fun fileSystem cancellationToken -> fileSystem.WriteAllLinesAsync(path, contents, cancellationToken))

    /// <summary>Writes all bytes through an explicit file-system service.</summary>
    let writeAllBytes<'env when 'env :> IHas<IFileSystem>>
        (path: string)
        (contents: byte array)
        : Flow<'env, FileSystemError, unit> =
        withService (Some path) (fun fileSystem -> fileSystem.WriteAllBytes(path, contents))

    /// <summary>Asynchronously writes all bytes through an explicit file-system service.</summary>
    let writeAllBytesAsync<'env when 'env :> IHas<IFileSystem>>
        (path: string)
        (contents: byte array)
        : Flow<'env, FileSystemError, unit> =
        withServiceTask (Some path) (fun fileSystem cancellationToken -> fileSystem.WriteAllBytesAsync(path, contents, cancellationToken))

    /// <summary>Appends all text through an explicit file-system service.</summary>
    let appendAllText<'env when 'env :> IHas<IFileSystem>>
        (path: string)
        (contents: string)
        : Flow<'env, FileSystemError, unit> =
        withService (Some path) (fun fileSystem -> fileSystem.AppendAllText(path, contents))

    /// <summary>Appends all text with the specified encoding through an explicit file-system service.</summary>
    let appendAllTextWithEncoding<'env when 'env :> IHas<IFileSystem>>
        (encoding: Encoding)
        (path: string)
        (contents: string)
        : Flow<'env, FileSystemError, unit> =
        withService (Some path) (fun fileSystem -> fileSystem.AppendAllText(path, contents, encoding))

    /// <summary>Asynchronously appends all text through an explicit file-system service.</summary>
    let appendAllTextAsync<'env when 'env :> IHas<IFileSystem>>
        (path: string)
        (contents: string)
        : Flow<'env, FileSystemError, unit> =
        withServiceTask (Some path) (fun fileSystem cancellationToken -> fileSystem.AppendAllTextAsync(path, contents, cancellationToken))

    /// <summary>Appends all lines through an explicit file-system service.</summary>
    let appendAllLines<'env when 'env :> IHas<IFileSystem>>
        (path: string)
        (contents: seq<string>)
        : Flow<'env, FileSystemError, unit> =
        withService (Some path) (fun fileSystem -> fileSystem.AppendAllLines(path, contents))

    /// <summary>Appends all lines with the specified encoding through an explicit file-system service.</summary>
    let appendAllLinesWithEncoding<'env when 'env :> IHas<IFileSystem>>
        (encoding: Encoding)
        (path: string)
        (contents: seq<string>)
        : Flow<'env, FileSystemError, unit> =
        withService (Some path) (fun fileSystem -> fileSystem.AppendAllLines(path, contents, encoding))

    /// <summary>Checks file existence through an explicit file-system service.</summary>
    let fileExists<'env when 'env :> IHas<IFileSystem>>
        (path: string)
        : Flow<'env, FileSystemError, bool> =
        withService (Some path) (fun fileSystem -> fileSystem.FileExists path)

    /// <summary>Checks file existence through an explicit file-system service.</summary>
    let exists<'env when 'env :> IHas<IFileSystem>>
        (path: string)
        : Flow<'env, FileSystemError, bool> =
        fileExists path

    /// <summary>Deletes a file through an explicit file-system service.</summary>
    let deleteFile<'env when 'env :> IHas<IFileSystem>>
        (path: string)
        : Flow<'env, FileSystemError, unit> =
        withService (Some path) (fun fileSystem -> fileSystem.DeleteFile path)

    /// <summary>Copies a file through an explicit file-system service.</summary>
    let copyFile<'env when 'env :> IHas<IFileSystem>>
        (sourcePath: string)
        (destinationPath: string)
        (overwrite: bool)
        : Flow<'env, FileSystemError, unit> =
        withService (Some sourcePath) (fun fileSystem -> fileSystem.CopyFile(sourcePath, destinationPath, overwrite))

    /// <summary>Moves a file through an explicit file-system service.</summary>
    let moveFile<'env when 'env :> IHas<IFileSystem>>
        (sourcePath: string)
        (destinationPath: string)
        (overwrite: bool)
        : Flow<'env, FileSystemError, unit> =
        withService (Some sourcePath) (fun fileSystem -> fileSystem.MoveFile(sourcePath, destinationPath, overwrite))

    /// <summary>Opens a file with the specified mode through an explicit file-system service.</summary>
    let openFile<'env when 'env :> IHas<IFileSystem>>
        (mode: FileMode)
        (path: string)
        : Flow<'env, FileSystemError, FileStream> =
        withService (Some path) (fun fileSystem -> fileSystem.OpenFile(path, mode))

    /// <summary>Opens a file with the specified mode and access through an explicit file-system service.</summary>
    let openFileWithAccess<'env when 'env :> IHas<IFileSystem>>
        (mode: FileMode)
        (access: FileAccess)
        (path: string)
        : Flow<'env, FileSystemError, FileStream> =
        withService (Some path) (fun fileSystem -> fileSystem.OpenFile(path, mode, access))

    /// <summary>Opens a file with the specified mode, access, and sharing behavior through an explicit file-system service.</summary>
    let openFileWithShare<'env when 'env :> IHas<IFileSystem>>
        (mode: FileMode)
        (access: FileAccess)
        (share: FileShare)
        (path: string)
        : Flow<'env, FileSystemError, FileStream> =
        withService (Some path) (fun fileSystem -> fileSystem.OpenFile(path, mode, access, share))

    /// <summary>Opens a file for reading through an explicit file-system service.</summary>
    let openRead<'env when 'env :> IHas<IFileSystem>>
        (path: string)
        : Flow<'env, FileSystemError, Stream> =
        withService (Some path) (fun fileSystem -> fileSystem.OpenRead path)

    /// <summary>Opens a text reader through an explicit file-system service.</summary>
    let openText<'env when 'env :> IHas<IFileSystem>>
        (path: string)
        : Flow<'env, FileSystemError, StreamReader> =
        withService (Some path) (fun fileSystem -> fileSystem.OpenText path)

    /// <summary>Opens a file for writing through an explicit file-system service.</summary>
    let openWrite<'env when 'env :> IHas<IFileSystem>>
        (path: string)
        : Flow<'env, FileSystemError, Stream> =
        withService (Some path) (fun fileSystem -> fileSystem.OpenWrite path)

    /// <summary>Creates or overwrites a file through an explicit file-system service.</summary>
    let createFile<'env when 'env :> IHas<IFileSystem>>
        (path: string)
        : Flow<'env, FileSystemError, Stream> =
        withService (Some path) (fun fileSystem -> fileSystem.CreateFile path)

    /// <summary>Creates a text writer through an explicit file-system service.</summary>
    let createText<'env when 'env :> IHas<IFileSystem>>
        (path: string)
        : Flow<'env, FileSystemError, StreamWriter> =
        withService (Some path) (fun fileSystem -> fileSystem.CreateText path)

    /// <summary>Creates an append text writer through an explicit file-system service.</summary>
    let appendText<'env when 'env :> IHas<IFileSystem>>
        (path: string)
        : Flow<'env, FileSystemError, StreamWriter> =
        withService (Some path) (fun fileSystem -> fileSystem.AppendText path)

    /// <summary>Gets file attributes through an explicit file-system service.</summary>
    let getFileAttributes<'env when 'env :> IHas<IFileSystem>>
        (path: string)
        : Flow<'env, FileSystemError, FileAttributes> =
        withService (Some path) (fun fileSystem -> fileSystem.GetFileAttributes path)

    /// <summary>Sets file attributes through an explicit file-system service.</summary>
    let setFileAttributes<'env when 'env :> IHas<IFileSystem>>
        (path: string)
        (attributes: FileAttributes)
        : Flow<'env, FileSystemError, unit> =
        withService (Some path) (fun fileSystem -> fileSystem.SetFileAttributes(path, attributes))

    /// <summary>Gets file creation time through an explicit file-system service.</summary>
    let getFileCreationTime<'env when 'env :> IHas<IFileSystem>> (path: string) : Flow<'env, FileSystemError, DateTime> =
        withService (Some path) (fun fileSystem -> fileSystem.GetFileCreationTime path)

    /// <summary>Gets file creation time in UTC through an explicit file-system service.</summary>
    let getFileCreationTimeUtc<'env when 'env :> IHas<IFileSystem>> (path: string) : Flow<'env, FileSystemError, DateTime> =
        withService (Some path) (fun fileSystem -> fileSystem.GetFileCreationTimeUtc path)

    /// <summary>Sets file creation time through an explicit file-system service.</summary>
    let setFileCreationTime<'env when 'env :> IHas<IFileSystem>> (path: string) (time: DateTime) : Flow<'env, FileSystemError, unit> =
        withService (Some path) (fun fileSystem -> fileSystem.SetFileCreationTime(path, time))

    /// <summary>Sets file creation time in UTC through an explicit file-system service.</summary>
    let setFileCreationTimeUtc<'env when 'env :> IHas<IFileSystem>> (path: string) (time: DateTime) : Flow<'env, FileSystemError, unit> =
        withService (Some path) (fun fileSystem -> fileSystem.SetFileCreationTimeUtc(path, time))

    /// <summary>Gets file last access time through an explicit file-system service.</summary>
    let getFileLastAccessTime<'env when 'env :> IHas<IFileSystem>> (path: string) : Flow<'env, FileSystemError, DateTime> =
        withService (Some path) (fun fileSystem -> fileSystem.GetFileLastAccessTime path)

    /// <summary>Gets file last access time in UTC through an explicit file-system service.</summary>
    let getFileLastAccessTimeUtc<'env when 'env :> IHas<IFileSystem>> (path: string) : Flow<'env, FileSystemError, DateTime> =
        withService (Some path) (fun fileSystem -> fileSystem.GetFileLastAccessTimeUtc path)

    /// <summary>Sets file last access time through an explicit file-system service.</summary>
    let setFileLastAccessTime<'env when 'env :> IHas<IFileSystem>> (path: string) (time: DateTime) : Flow<'env, FileSystemError, unit> =
        withService (Some path) (fun fileSystem -> fileSystem.SetFileLastAccessTime(path, time))

    /// <summary>Sets file last access time in UTC through an explicit file-system service.</summary>
    let setFileLastAccessTimeUtc<'env when 'env :> IHas<IFileSystem>> (path: string) (time: DateTime) : Flow<'env, FileSystemError, unit> =
        withService (Some path) (fun fileSystem -> fileSystem.SetFileLastAccessTimeUtc(path, time))

    /// <summary>Gets file last write time through an explicit file-system service.</summary>
    let getFileLastWriteTime<'env when 'env :> IHas<IFileSystem>> (path: string) : Flow<'env, FileSystemError, DateTime> =
        withService (Some path) (fun fileSystem -> fileSystem.GetFileLastWriteTime path)

    /// <summary>Gets file last write time in UTC through an explicit file-system service.</summary>
    let getFileLastWriteTimeUtc<'env when 'env :> IHas<IFileSystem>> (path: string) : Flow<'env, FileSystemError, DateTime> =
        withService (Some path) (fun fileSystem -> fileSystem.GetFileLastWriteTimeUtc path)

    /// <summary>Sets file last write time through an explicit file-system service.</summary>
    let setFileLastWriteTime<'env when 'env :> IHas<IFileSystem>> (path: string) (time: DateTime) : Flow<'env, FileSystemError, unit> =
        withService (Some path) (fun fileSystem -> fileSystem.SetFileLastWriteTime(path, time))

    /// <summary>Sets file last write time in UTC through an explicit file-system service.</summary>
    let setFileLastWriteTimeUtc<'env when 'env :> IHas<IFileSystem>> (path: string) (time: DateTime) : Flow<'env, FileSystemError, unit> =
        withService (Some path) (fun fileSystem -> fileSystem.SetFileLastWriteTimeUtc(path, time))

    /// <summary>Checks directory existence through an explicit file-system service.</summary>
    let directoryExists<'env when 'env :> IHas<IFileSystem>>
        (path: string)
        : Flow<'env, FileSystemError, bool> =
        withService (Some path) (fun fileSystem -> fileSystem.DirectoryExists path)

    /// <summary>Creates a directory through an explicit file-system service.</summary>
    let createDirectory<'env when 'env :> IHas<IFileSystem>>
        (path: string)
        : Flow<'env, FileSystemError, unit> =
        withService (Some path) (fun fileSystem -> fileSystem.CreateDirectory path)

    /// <summary>Deletes a directory through an explicit file-system service.</summary>
    let deleteDirectory<'env when 'env :> IHas<IFileSystem>>
        (path: string)
        (recursive: bool)
        : Flow<'env, FileSystemError, unit> =
        withService (Some path) (fun fileSystem -> fileSystem.DeleteDirectory(path, recursive))

    /// <summary>Moves a directory through an explicit file-system service.</summary>
    let moveDirectory<'env when 'env :> IHas<IFileSystem>>
        (sourcePath: string)
        (destinationPath: string)
        : Flow<'env, FileSystemError, unit> =
        withService (Some sourcePath) (fun fileSystem -> fileSystem.MoveDirectory(sourcePath, destinationPath))

    /// <summary>Enumerates files through an explicit file-system service.</summary>
    let enumerateFiles<'env when 'env :> IHas<IFileSystem>>
        (path: string)
        (searchPattern: string)
        (searchOption: SearchOption)
        : Flow<'env, FileSystemError, seq<string>> =
        withService (Some path) (fun fileSystem -> fileSystem.EnumerateFiles(path, searchPattern, searchOption))

    /// <summary>Gets files through an explicit file-system service.</summary>
    let getFiles<'env when 'env :> IHas<IFileSystem>>
        (path: string)
        (searchPattern: string)
        (searchOption: SearchOption)
        : Flow<'env, FileSystemError, string array> =
        withService (Some path) (fun fileSystem -> fileSystem.GetFiles(path, searchPattern, searchOption))

    /// <summary>Enumerates directories through an explicit file-system service.</summary>
    let enumerateDirectories<'env when 'env :> IHas<IFileSystem>>
        (path: string)
        (searchPattern: string)
        (searchOption: SearchOption)
        : Flow<'env, FileSystemError, seq<string>> =
        withService (Some path) (fun fileSystem -> fileSystem.EnumerateDirectories(path, searchPattern, searchOption))

    /// <summary>Gets directories through an explicit file-system service.</summary>
    let getDirectories<'env when 'env :> IHas<IFileSystem>>
        (path: string)
        (searchPattern: string)
        (searchOption: SearchOption)
        : Flow<'env, FileSystemError, string array> =
        withService (Some path) (fun fileSystem -> fileSystem.GetDirectories(path, searchPattern, searchOption))

    /// <summary>Enumerates files and directories through an explicit file-system service.</summary>
    let enumerateFileSystemEntries<'env when 'env :> IHas<IFileSystem>>
        (path: string)
        (searchPattern: string)
        (searchOption: SearchOption)
        : Flow<'env, FileSystemError, seq<string>> =
        withService (Some path) (fun fileSystem -> fileSystem.EnumerateFileSystemEntries(path, searchPattern, searchOption))

    /// <summary>Gets files and directories through an explicit file-system service.</summary>
    let getFileSystemEntries<'env when 'env :> IHas<IFileSystem>>
        (path: string)
        (searchPattern: string)
        (searchOption: SearchOption)
        : Flow<'env, FileSystemError, string array> =
        withService (Some path) (fun fileSystem -> fileSystem.GetFileSystemEntries(path, searchPattern, searchOption))

    /// <summary>Gets logical drives through an explicit file-system service.</summary>
    let getLogicalDrives<'env when 'env :> IHas<IFileSystem>>
        : Flow<'env, FileSystemError, string array> =
        withService None (fun fileSystem -> fileSystem.GetLogicalDrives())

    /// <summary>Gets the directory root through an explicit file-system service.</summary>
    let getDirectoryRoot<'env when 'env :> IHas<IFileSystem>>
        (path: string)
        : Flow<'env, FileSystemError, string> =
        withService (Some path) (fun fileSystem -> fileSystem.GetDirectoryRoot path)

    /// <summary>Gets the parent directory through an explicit file-system service.</summary>
    let getParent<'env when 'env :> IHas<IFileSystem>>
        (path: string)
        : Flow<'env, FileSystemError, string option> =
        withService (Some path) (fun fileSystem -> fileSystem.GetParent path)

    /// <summary>Gets the current working directory through an explicit file-system service.</summary>
    let getCurrentDirectory<'env when 'env :> IHas<IFileSystem>>
        : Flow<'env, FileSystemError, string> =
        withService None (fun fileSystem -> fileSystem.GetCurrentDirectory())

    /// <summary>Sets the current working directory through an explicit file-system service.</summary>
    let setCurrentDirectory<'env when 'env :> IHas<IFileSystem>>
        (path: string)
        : Flow<'env, FileSystemError, unit> =
        withService (Some path) (fun fileSystem -> fileSystem.SetCurrentDirectory path)

    /// <summary>Gets directory creation time through an explicit file-system service.</summary>
    let getDirectoryCreationTime<'env when 'env :> IHas<IFileSystem>> (path: string) : Flow<'env, FileSystemError, DateTime> =
        withService (Some path) (fun fileSystem -> fileSystem.GetDirectoryCreationTime path)

    /// <summary>Gets directory creation time in UTC through an explicit file-system service.</summary>
    let getDirectoryCreationTimeUtc<'env when 'env :> IHas<IFileSystem>> (path: string) : Flow<'env, FileSystemError, DateTime> =
        withService (Some path) (fun fileSystem -> fileSystem.GetDirectoryCreationTimeUtc path)

    /// <summary>Sets directory creation time through an explicit file-system service.</summary>
    let setDirectoryCreationTime<'env when 'env :> IHas<IFileSystem>> (path: string) (time: DateTime) : Flow<'env, FileSystemError, unit> =
        withService (Some path) (fun fileSystem -> fileSystem.SetDirectoryCreationTime(path, time))

    /// <summary>Sets directory creation time in UTC through an explicit file-system service.</summary>
    let setDirectoryCreationTimeUtc<'env when 'env :> IHas<IFileSystem>> (path: string) (time: DateTime) : Flow<'env, FileSystemError, unit> =
        withService (Some path) (fun fileSystem -> fileSystem.SetDirectoryCreationTimeUtc(path, time))

    /// <summary>Gets directory last access time through an explicit file-system service.</summary>
    let getDirectoryLastAccessTime<'env when 'env :> IHas<IFileSystem>> (path: string) : Flow<'env, FileSystemError, DateTime> =
        withService (Some path) (fun fileSystem -> fileSystem.GetDirectoryLastAccessTime path)

    /// <summary>Gets directory last access time in UTC through an explicit file-system service.</summary>
    let getDirectoryLastAccessTimeUtc<'env when 'env :> IHas<IFileSystem>> (path: string) : Flow<'env, FileSystemError, DateTime> =
        withService (Some path) (fun fileSystem -> fileSystem.GetDirectoryLastAccessTimeUtc path)

    /// <summary>Sets directory last access time through an explicit file-system service.</summary>
    let setDirectoryLastAccessTime<'env when 'env :> IHas<IFileSystem>> (path: string) (time: DateTime) : Flow<'env, FileSystemError, unit> =
        withService (Some path) (fun fileSystem -> fileSystem.SetDirectoryLastAccessTime(path, time))

    /// <summary>Sets directory last access time in UTC through an explicit file-system service.</summary>
    let setDirectoryLastAccessTimeUtc<'env when 'env :> IHas<IFileSystem>> (path: string) (time: DateTime) : Flow<'env, FileSystemError, unit> =
        withService (Some path) (fun fileSystem -> fileSystem.SetDirectoryLastAccessTimeUtc(path, time))

    /// <summary>Gets directory last write time through an explicit file-system service.</summary>
    let getDirectoryLastWriteTime<'env when 'env :> IHas<IFileSystem>> (path: string) : Flow<'env, FileSystemError, DateTime> =
        withService (Some path) (fun fileSystem -> fileSystem.GetDirectoryLastWriteTime path)

    /// <summary>Gets directory last write time in UTC through an explicit file-system service.</summary>
    let getDirectoryLastWriteTimeUtc<'env when 'env :> IHas<IFileSystem>> (path: string) : Flow<'env, FileSystemError, DateTime> =
        withService (Some path) (fun fileSystem -> fileSystem.GetDirectoryLastWriteTimeUtc path)

    /// <summary>Sets directory last write time through an explicit file-system service.</summary>
    let setDirectoryLastWriteTime<'env when 'env :> IHas<IFileSystem>> (path: string) (time: DateTime) : Flow<'env, FileSystemError, unit> =
        withService (Some path) (fun fileSystem -> fileSystem.SetDirectoryLastWriteTime(path, time))

    /// <summary>Sets directory last write time in UTC through an explicit file-system service.</summary>
    let setDirectoryLastWriteTimeUtc<'env when 'env :> IHas<IFileSystem>> (path: string) (time: DateTime) : Flow<'env, FileSystemError, unit> =
        withService (Some path) (fun fileSystem -> fileSystem.SetDirectoryLastWriteTimeUtc(path, time))

    /// <summary>Combines path segments through an explicit file-system service.</summary>
    let combine<'env when 'env :> IHas<IFileSystem>>
        (paths: string array)
        : Flow<'env, FileSystemError, string> =
        withService None (fun fileSystem -> fileSystem.Combine paths)

    /// <summary>Changes a path extension through an explicit file-system service.</summary>
    let changeExtension<'env when 'env :> IHas<IFileSystem>>
        (path: string)
        (extension: string)
        : Flow<'env, FileSystemError, string> =
        withService (Some path) (fun fileSystem -> fileSystem.ChangeExtension(path, extension))

    /// <summary>Gets the directory name for a path through an explicit file-system service.</summary>
    let getDirectoryName<'env when 'env :> IHas<IFileSystem>>
        (path: string)
        : Flow<'env, FileSystemError, string option> =
        withService (Some path) (fun fileSystem -> fileSystem.GetDirectoryName path)

    /// <summary>Gets invalid file-name characters through an explicit file-system service.</summary>
    let getInvalidFileNameChars<'env when 'env :> IHas<IFileSystem>>
        : Flow<'env, FileSystemError, char array> =
        withService None (fun fileSystem -> fileSystem.GetInvalidFileNameChars())

    /// <summary>Gets invalid path characters through an explicit file-system service.</summary>
    let getInvalidPathChars<'env when 'env :> IHas<IFileSystem>>
        : Flow<'env, FileSystemError, char array> =
        withService None (fun fileSystem -> fileSystem.GetInvalidPathChars())

    /// <summary>Gets the extension for a path through an explicit file-system service.</summary>
    let getExtension<'env when 'env :> IHas<IFileSystem>>
        (path: string)
        : Flow<'env, FileSystemError, string> =
        withService (Some path) (fun fileSystem -> fileSystem.GetExtension path)

    /// <summary>Gets the file name for a path through an explicit file-system service.</summary>
    let getFileName<'env when 'env :> IHas<IFileSystem>>
        (path: string)
        : Flow<'env, FileSystemError, string> =
        withService (Some path) (fun fileSystem -> fileSystem.GetFileName path)

    /// <summary>Gets the file name without extension for a path through an explicit file-system service.</summary>
    let getFileNameWithoutExtension<'env when 'env :> IHas<IFileSystem>>
        (path: string)
        : Flow<'env, FileSystemError, string> =
        withService (Some path) (fun fileSystem -> fileSystem.GetFileNameWithoutExtension path)

    /// <summary>Gets the full path through an explicit file-system service.</summary>
    let getFullPath<'env when 'env :> IHas<IFileSystem>>
        (path: string)
        : Flow<'env, FileSystemError, string> =
        withService (Some path) (fun fileSystem -> fileSystem.GetFullPath path)

    /// <summary>Gets the path root through an explicit file-system service.</summary>
    let getPathRoot<'env when 'env :> IHas<IFileSystem>>
        (path: string)
        : Flow<'env, FileSystemError, string option> =
        withService (Some path) (fun fileSystem -> fileSystem.GetPathRoot path)

    /// <summary>Gets a relative path through an explicit file-system service.</summary>
    let getRelativePath<'env when 'env :> IHas<IFileSystem>>
        (relativeTo: string)
        (path: string)
        : Flow<'env, FileSystemError, string> =
        withService (Some path) (fun fileSystem -> fileSystem.GetRelativePath(relativeTo, path))

    /// <summary>Gets the temporary directory path through an explicit file-system service.</summary>
    let getTempPath<'env when 'env :> IHas<IFileSystem>>
        : Flow<'env, FileSystemError, string> =
        withService None (fun fileSystem -> fileSystem.GetTempPath())

    /// <summary>Creates a temporary file through an explicit file-system service and returns its path.</summary>
    let getTempFileName<'env when 'env :> IHas<IFileSystem>>
        : Flow<'env, FileSystemError, string> =
        withService None (fun fileSystem -> fileSystem.GetTempFileName())

    /// <summary>Gets a random file name through an explicit file-system service.</summary>
    let getRandomFileName<'env when 'env :> IHas<IFileSystem>>
        : Flow<'env, FileSystemError, string> =
        withService None (fun fileSystem -> fileSystem.GetRandomFileName())

    /// <summary>Checks whether a path has an extension through an explicit file-system service.</summary>
    let hasExtension<'env when 'env :> IHas<IFileSystem>>
        (path: string)
        : Flow<'env, FileSystemError, bool> =
        withService (Some path) (fun fileSystem -> fileSystem.HasExtension path)

    /// <summary>Checks whether a path ends in a directory separator through an explicit file-system service.</summary>
    let endsInDirectorySeparator<'env when 'env :> IHas<IFileSystem>>
        (path: string)
        : Flow<'env, FileSystemError, bool> =
        withService (Some path) (fun fileSystem -> fileSystem.EndsInDirectorySeparator path)

    /// <summary>Trims one trailing directory separator through an explicit file-system service.</summary>
    let trimEndingDirectorySeparator<'env when 'env :> IHas<IFileSystem>>
        (path: string)
        : Flow<'env, FileSystemError, string> =
        withService (Some path) (fun fileSystem -> fileSystem.TrimEndingDirectorySeparator path)

    /// <summary>Checks whether a path is fully qualified through an explicit file-system service.</summary>
    let isPathFullyQualified<'env when 'env :> IHas<IFileSystem>>
        (path: string)
        : Flow<'env, FileSystemError, bool> =
        withService (Some path) (fun fileSystem -> fileSystem.IsPathFullyQualified path)

    /// <summary>Checks whether a path is rooted through an explicit file-system service.</summary>
    let isPathRooted<'env when 'env :> IHas<IFileSystem>>
        (path: string)
        : Flow<'env, FileSystemError, bool> =
        withService (Some path) (fun fileSystem -> fileSystem.IsPathRooted path)

#if !FABLE_COMPILER
    /// <summary>Creates a live file-system service backed by <see cref="T:System.IO.File" />, <see cref="T:System.IO.Directory" />, and <see cref="T:System.IO.Path" />.</summary>
    let live : IFileSystem =
        { new IFileSystem with
            member _.ReadAllText(path) = File.ReadAllText(path)
            member _.ReadAllText(path, encoding) = File.ReadAllText(path, encoding)
            member _.ReadAllTextAsync(path, cancellationToken) = File.ReadAllTextAsync(path, cancellationToken)
            member _.ReadAllLines(path) = File.ReadAllLines(path)
            member _.ReadAllLines(path, encoding) = File.ReadAllLines(path, encoding)
            member _.ReadAllLinesAsync(path, cancellationToken) = File.ReadAllLinesAsync(path, cancellationToken)
            member _.ReadAllBytes(path) = File.ReadAllBytes(path)
            member _.ReadAllBytesAsync(path, cancellationToken) = File.ReadAllBytesAsync(path, cancellationToken)
            member _.WriteAllText(path, contents) = File.WriteAllText(path, contents)
            member _.WriteAllText(path, contents, encoding) = File.WriteAllText(path, contents, encoding)
            member _.WriteAllTextAsync(path, contents, cancellationToken) = File.WriteAllTextAsync(path, contents, cancellationToken)
            member _.WriteAllLines(path, contents) = File.WriteAllLines(path, contents)
            member _.WriteAllLines(path, contents, encoding) = File.WriteAllLines(path, contents, encoding)
            member _.WriteAllLinesAsync(path, contents, cancellationToken) = File.WriteAllLinesAsync(path, contents, cancellationToken)
            member _.WriteAllBytes(path, contents) = File.WriteAllBytes(path, contents)
            member _.WriteAllBytesAsync(path, contents, cancellationToken) = File.WriteAllBytesAsync(path, contents, cancellationToken)
            member _.AppendAllText(path, contents) = File.AppendAllText(path, contents)
            member _.AppendAllText(path, contents, encoding) = File.AppendAllText(path, contents, encoding)
            member _.AppendAllTextAsync(path, contents, cancellationToken) = File.AppendAllTextAsync(path, contents, cancellationToken)
            member _.AppendAllLines(path, contents) = File.AppendAllLines(path, contents)
            member _.AppendAllLines(path, contents, encoding) = File.AppendAllLines(path, contents, encoding)
            member _.FileExists(path) = File.Exists(path)
            member _.DeleteFile(path) = File.Delete(path)
            member _.CopyFile(sourcePath, destinationPath, overwrite) = File.Copy(sourcePath, destinationPath, overwrite)
            member _.MoveFile(sourcePath, destinationPath, overwrite) =
                if overwrite && File.Exists destinationPath then
                    File.Delete destinationPath

                File.Move(sourcePath, destinationPath)
            member _.OpenFile(path, mode) = File.Open(path, mode)
            member _.OpenFile(path, mode, access) = File.Open(path, mode, access)
            member _.OpenFile(path, mode, access, share) = File.Open(path, mode, access, share)
            member _.OpenRead(path) = File.OpenRead(path) :> Stream
            member _.OpenText(path) = File.OpenText(path)
            member _.OpenWrite(path) = File.OpenWrite(path) :> Stream
            member _.CreateFile(path) = File.Create(path) :> Stream
            member _.CreateText(path) = File.CreateText(path)
            member _.AppendText(path) = File.AppendText(path)
            member _.GetFileAttributes(path) = File.GetAttributes(path)
            member _.SetFileAttributes(path, attributes) = File.SetAttributes(path, attributes)
            member _.GetFileCreationTime(path) = File.GetCreationTime(path)
            member _.GetFileCreationTimeUtc(path) = File.GetCreationTimeUtc(path)
            member _.SetFileCreationTime(path, creationTime) = File.SetCreationTime(path, creationTime)
            member _.SetFileCreationTimeUtc(path, creationTimeUtc) = File.SetCreationTimeUtc(path, creationTimeUtc)
            member _.GetFileLastAccessTime(path) = File.GetLastAccessTime(path)
            member _.GetFileLastAccessTimeUtc(path) = File.GetLastAccessTimeUtc(path)
            member _.SetFileLastAccessTime(path, lastAccessTime) = File.SetLastAccessTime(path, lastAccessTime)
            member _.SetFileLastAccessTimeUtc(path, lastAccessTimeUtc) = File.SetLastAccessTimeUtc(path, lastAccessTimeUtc)
            member _.GetFileLastWriteTime(path) = File.GetLastWriteTime(path)
            member _.GetFileLastWriteTimeUtc(path) = File.GetLastWriteTimeUtc(path)
            member _.SetFileLastWriteTime(path, lastWriteTime) = File.SetLastWriteTime(path, lastWriteTime)
            member _.SetFileLastWriteTimeUtc(path, lastWriteTimeUtc) = File.SetLastWriteTimeUtc(path, lastWriteTimeUtc)
            member _.DirectoryExists(path) = Directory.Exists(path)
            member _.CreateDirectory(path) = Directory.CreateDirectory(path) |> ignore
            member _.DeleteDirectory(path, recursive) = Directory.Delete(path, recursive)
            member _.MoveDirectory(sourcePath, destinationPath) = Directory.Move(sourcePath, destinationPath)
            member _.EnumerateFiles(path, searchPattern, searchOption) = Directory.EnumerateFiles(path, searchPattern, searchOption)
            member _.GetFiles(path, searchPattern, searchOption) = Directory.GetFiles(path, searchPattern, searchOption)
            member _.EnumerateDirectories(path, searchPattern, searchOption) = Directory.EnumerateDirectories(path, searchPattern, searchOption)
            member _.GetDirectories(path, searchPattern, searchOption) = Directory.GetDirectories(path, searchPattern, searchOption)
            member _.EnumerateFileSystemEntries(path, searchPattern, searchOption) = Directory.EnumerateFileSystemEntries(path, searchPattern, searchOption)
            member _.GetFileSystemEntries(path, searchPattern, searchOption) = Directory.GetFileSystemEntries(path, searchPattern, searchOption)
            member _.GetLogicalDrives() = Directory.GetLogicalDrives()
            member _.GetDirectoryRoot(path) = Directory.GetDirectoryRoot(path)
            member _.GetParent(path) = Directory.GetParent(path) |> Option.ofObj |> Option.map _.FullName
            member _.GetCurrentDirectory() = Directory.GetCurrentDirectory()
            member _.SetCurrentDirectory(path) = Directory.SetCurrentDirectory(path)
            member _.GetDirectoryCreationTime(path) = Directory.GetCreationTime(path)
            member _.GetDirectoryCreationTimeUtc(path) = Directory.GetCreationTimeUtc(path)
            member _.SetDirectoryCreationTime(path, creationTime) = Directory.SetCreationTime(path, creationTime)
            member _.SetDirectoryCreationTimeUtc(path, creationTimeUtc) = Directory.SetCreationTimeUtc(path, creationTimeUtc)
            member _.GetDirectoryLastAccessTime(path) = Directory.GetLastAccessTime(path)
            member _.GetDirectoryLastAccessTimeUtc(path) = Directory.GetLastAccessTimeUtc(path)
            member _.SetDirectoryLastAccessTime(path, lastAccessTime) = Directory.SetLastAccessTime(path, lastAccessTime)
            member _.SetDirectoryLastAccessTimeUtc(path, lastAccessTimeUtc) = Directory.SetLastAccessTimeUtc(path, lastAccessTimeUtc)
            member _.GetDirectoryLastWriteTime(path) = Directory.GetLastWriteTime(path)
            member _.GetDirectoryLastWriteTimeUtc(path) = Directory.GetLastWriteTimeUtc(path)
            member _.SetDirectoryLastWriteTime(path, lastWriteTime) = Directory.SetLastWriteTime(path, lastWriteTime)
            member _.SetDirectoryLastWriteTimeUtc(path, lastWriteTimeUtc) = Directory.SetLastWriteTimeUtc(path, lastWriteTimeUtc)
            member _.Combine(paths) = Path.Combine(paths)
            member _.ChangeExtension(path, extension) = Path.ChangeExtension(path, extension)
            member _.GetDirectoryName(path) = Path.GetDirectoryName(path) |> Option.ofObj
            member _.GetInvalidFileNameChars() = Path.GetInvalidFileNameChars()
            member _.GetInvalidPathChars() = Path.GetInvalidPathChars()
            member _.GetExtension(path) = Path.GetExtension(path)
            member _.GetFileName(path) = Path.GetFileName(path)
            member _.GetFileNameWithoutExtension(path) = Path.GetFileNameWithoutExtension(path)
            member _.GetFullPath(path) = Path.GetFullPath(path)
            member _.GetPathRoot(path) = Path.GetPathRoot(path) |> Option.ofObj
            member _.GetRelativePath(relativeTo, path) = Path.GetRelativePath(relativeTo, path)
            member _.GetTempPath() = Path.GetTempPath()
            member _.GetTempFileName() = Path.GetTempFileName()
            member _.GetRandomFileName() = Path.GetRandomFileName()
            member _.HasExtension(path) = Path.HasExtension(path)
            member _.EndsInDirectorySeparator(path) = endsInDirectorySeparatorRaw path
            member _.TrimEndingDirectorySeparator(path) = trimEndingDirectorySeparatorRaw path
            member _.IsPathFullyQualified(path) = Path.IsPathFullyQualified(path)
            member _.IsPathRooted(path) = Path.IsPathRooted(path) }
#endif

    /// <summary>Builds the live file-system service as a layer.</summary>
    let layer : Layer<unit, Never, IFileSystem> =
#if FABLE_COMPILER
        Layer.effect (fun _ _ -> async { return Exit.Failure (Cause.Die (PlatformNotSupportedException("File-system services are not supported on Fable."))) })
#else
        Layer.succeed live
#endif
