namespace Axial.Files

open System
open System.Collections.Generic
open System.IO
open System.Text
open Axial
open Axial.FileSystem

[<Sealed>]
type private PrefixedStream(prefix: byte array, length: int, rest: Stream) =
    inherit Stream()
    let mutable position = 0
    override _.CanRead = true
    override _.CanSeek = false
    override _.CanWrite = false
    override _.Length = raise (NotSupportedException())
    override _.Position with get () = raise (NotSupportedException()) and set _ = raise (NotSupportedException())
    override _.Flush() = ()
    override _.Seek(_, _) = raise (NotSupportedException())
    override _.SetLength _ = raise (NotSupportedException())
    override _.Write(_, _, _) = raise (NotSupportedException())
    override _.Read(buffer, offset, count) =
        if position < length then
            let copied = min count (length - position)
            Buffer.BlockCopy(prefix, position, buffer, offset, copied)
            position <- position + copied
            copied
        else rest.Read(buffer, offset, count)
    override _.Dispose(disposing) =
        if disposing then rest.Dispose()
        base.Dispose(disposing)

[<RequireQualifiedAccess>]
module Files =
    let private sniffLength = 8192
    let private batchSize = 2048

    /// <summary>Watches a directory (optionally recursively) or one file until consumption ends.</summary>
    /// <remarks>The 1024-event buffer fails on overflow rather than silently losing changes. Rescan after any failure.</remarks>
    let watch (recursive: bool) (path: string) : FlowStream<'env, FileSystemError, FileChange> when 'env :> IHasFileSystem =
        FlowStream.fromCallbackWith 1024 (CallbackOverflow.Fail (FileSystemError.Io(Some path, "Watcher buffer overflow; rescan required")))
            (fun emit ->
                flow {
                    let! fs = FileSystem.service
                    return fs.Watch(path, recursive, (fun change -> emit.Value change |> ignore),
                                    (fun error -> emit.Fail (FileSystemError.fromException (Some path) error)))
                }
                |> Flow.catch (FileSystemError.fromException (Some path)))

    /// <summary>Walks the children of root breadth-first. Directories are listed only when pulled and permitted by descend.</summary>
    let walk (descend: string -> bool) (root: string) : FlowStream<'env, FileSystemError, FileSystemEntry> when 'env :> IHasFileSystem =
        let rec step (pending: string list, entries: FileSystemEntry list) =
            (
                match entries, pending with
                | entry :: tail, _ ->
                    let next =
                        if entry.Kind = FileSystemEntryKind.Directory && descend entry.FullPath then
                            pending @ [ entry.FullPath ]
                        else pending
                    Flow.succeed (Some(entry, (next, tail)))
                | [], directory :: remaining ->
                    FileSystem.enumerateFileSystemInfos directory "*" SearchOption.TopDirectoryOnly
                    |> Flow.bind (fun found -> step (remaining, List.ofSeq found))
                | [], [] -> Flow.succeed None)
        FlowStream.unfoldFlow step ([ root ], [])

    /// <summary>Opens a BOM-aware text reader, or None if the first 8192 bytes contain a NUL (except UTF-16 with a BOM).</summary>
    let reader (path: string) : Resource<'env, FileSystemError, TextReader option> when 'env :> IHasFileSystem =
        Resource.create
            (flow {
                let! raw = FileSystem.openRead path
                return!
                    Flow.attemptBlocking (fun _ ->
                        // Sniffing failures are mapped to FileSystemError by attemptBlocking.
                        try
                            let prefix = Array.zeroCreate<byte> sniffLength
                            let mutable length = 0
                            let mutable reading = true
                            while reading && length < sniffLength do
                                let count = raw.Read(prefix, length, sniffLength - length)
                                if count = 0 then reading <- false else length <- length + count
                            let utf16 = length >= 2 &&
                                        ((prefix[0] = 0xFFuy && prefix[1] = 0xFEuy) ||
                                         (prefix[0] = 0xFEuy && prefix[1] = 0xFFuy))
                            if not utf16 && Array.IndexOf(prefix, 0uy, 0, length) >= 0 then
                                raw.Dispose()
                                None
                            else
                                let replay = new PrefixedStream(prefix, length, raw)
                                Some(new StreamReader(replay, Encoding.UTF8, true) :> TextReader)
                        with error ->
                            raw.Dispose()
                            System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(error).Throw()
                            Unchecked.defaultof<_>)
                    |> Flow.mapError (FileSystemError.fromException (Some path))
            })
            (fun text -> Flow.delay (fun () -> text |> Option.iter _.Dispose(); Flow.succeed ()))

    /// <summary>Streams text in 2048-line batches. Binary files produce no lines; stopping early closes the reader.</summary>
    let lines (path: string) : FlowStream<'env, FileSystemError, string> when 'env :> IHasFileSystem =
        FlowStream.using (reader path) (function
            | None -> FlowStream.empty
            | Some text ->
                FlowStream.repeatFlow (
                    Flow.attemptBlocking (fun _ ->
                        let batch = ResizeArray<string>(batchSize)
                        let mutable line = text.ReadLine()
                        while not (isNull line) && batch.Count < batchSize - 1 do
                            batch.Add line
                            line <- text.ReadLine()
                        if not (isNull line) then batch.Add line
                        batch.ToArray())
                    |> Flow.mapError (FileSystemError.fromException (Some path)))
                |> FlowStream.takeWhile (fun batch -> batch.Length > 0)
                |> FlowStream.collect FlowStream.fromSeq)
