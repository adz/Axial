namespace Axial.FileSystem

open System
open Axial

/// <summary>Bounds for the rolling file log sink.</summary>
type FileLogOptions =
    {
        /// <summary>Roll the current file when it exceeds this size. Zero disables size-based rolling.</summary>
        MaxFileSizeBytes: int64
        /// <summary>Keep this many numbered archives after the current file. Zero keeps only the current file.</summary>
        MaxArchives: int
    }

/// <summary>A log sink that appends to one file and rolls it into numbered archives when it grows too large.</summary>
/// <remarks>
/// The current file is <c>path</c>. When a write would push it over <c>MaxFileSizeBytes</c>, it is moved to
/// <c>path.1</c>, the older archives shift up one number, and the oldest archive is deleted. This is write-only
/// observation: a failed write or roll is swallowed, never thrown back into the workflow.
/// </remarks>
[<RequireQualifiedAccess>]
module FileLog =
    let private render (entry: LogEntry) : string =
        match entry.Error with
        | Some error -> entry.Level.ToString() + ": " + entry.Message + "\n" + error.ToString()
        | None -> entry.Level.ToString() + ": " + entry.Message

    let private validate (options: FileLogOptions) =
        if options.MaxFileSizeBytes < 0L then
            invalidArg (nameof options) "MaxFileSizeBytes must be non-negative."

        if options.MaxArchives < 0 then
            invalidArg (nameof options) "MaxArchives must be non-negative."

        options

    /// <summary>Builds a rolling file sink that appends to <paramref name="path" /> through the supplied file system.</summary>
    let sink (fileSystem: IFileSystem) (path: string) (options: FileLogOptions) : ILogSink =
        if String.IsNullOrWhiteSpace path then invalidArg (nameof path) "A log path must not be empty."
        validate options |> ignore

        let gate = obj ()
        let archivePath (index: int) = path + "." + string index

        { new ILogSink with
            member _.IsEnabled _ = true
            member _.Write entry =
                lock gate (fun () ->
                    try
                        let line = render entry + "\n"

                        if options.MaxFileSizeBytes > 0L && fileSystem.FileExists path then
                            let length = fileSystem.GetFileLength path

                            if length + int64 line.Length > options.MaxFileSizeBytes && options.MaxArchives = 0 then
                                fileSystem.DeleteFile path
                            elif length + int64 line.Length > options.MaxFileSizeBytes then
                                let oldest = archivePath options.MaxArchives

                                if fileSystem.FileExists oldest then
                                    fileSystem.DeleteFile oldest

                                for index in options.MaxArchives .. -1 .. 2 do
                                    let from = archivePath (index - 1)
                                    let destination = archivePath index

                                    if fileSystem.FileExists from then
                                        fileSystem.MoveFile(from, destination, true)

                                if fileSystem.FileExists path then
                                    fileSystem.MoveFile(path, archivePath 1, true)

                        fileSystem.AppendAllText(path, line)
                    with _ -> ()) }
