namespace Axial.Files

open System
open System.IO
open Axial
open Axial.FileSystem

/// <summary>A bounded framed file log retaining two generations. Dispose the resource before opening another writer.</summary>
[<Sealed>]
type AppendLog internal (path: string, maxBytes: int64, fs: IFileSystem, stream: FileStream) =
    let mutable current = stream
    let gate = Semaphore.make 1 |> Flow.run () |> function Exit.Success value -> value | _ -> failwith "Invalid semaphore"
    let mutable failed = false
    member internal _.Gate = gate
    member internal _.Failed with get () = failed and set value = failed <- value
    member internal _.Path = path
    member internal _.MaxBytes = maxBytes
    member internal _.FileSystem = fs
    member internal _.Stream with get () = current and set value = current <- value
    override _.ToString() = $"AppendLog({path})"

[<RequireQualifiedAccess>]
module AppendLog =
    let private table =
        Array.init 256 (fun index ->
            let mutable value = uint32 index
            for _ in 1 .. 8 do
                value <- if value &&& 1u = 1u then (value >>> 1) ^^^ 0xEDB88320u else value >>> 1
            value)

    let private crc (bytes: byte array) =
        let mutable value = 0xFFFFFFFFu
        for b in bytes do
            value <- table[int ((value ^^^ uint32 b) &&& 0xFFu)] ^^^ (value >>> 8)
        ~~~value

    let private scan (maxBytes: int64) (stream: FileStream) =
        stream.Position <- 0L
        let header = Array.zeroCreate<byte> 8
        let records = ResizeArray<byte array>()
        let mutable valid = 0L
        let mutable scanning = true
        while scanning && stream.Length - stream.Position >= 8L do
            let mutable count = 0
            while count < 8 do
                let n = stream.Read(header, count, 8 - count)
                if n = 0 then raise (EndOfStreamException())
                count <- count + n
            let length = BitConverter.ToInt32(header, 0)
            if length < 0 || int64 length > maxBytes - 8L || int64 length > stream.Length - stream.Position then
                scanning <- false
            else
                let payload = Array.zeroCreate<byte> length
                let mutable offset = 0
                while offset < length do
                    let n = stream.Read(payload, offset, length - offset)
                    if n = 0 then raise (EndOfStreamException())
                    offset <- offset + n
                if crc payload <> BitConverter.ToUInt32(header, 4) then scanning <- false
                else
                    valid <- stream.Position
                    records.Add payload
        valid, records.ToArray()

    /// <summary>Opens a log, preserving the rotated generation when the current file is missing, and truncates a corrupt tail. Frame lengths are bounded by maxBytes.</summary>
    let openLog (path: string) (maxBytes: int64) : Resource<'env, FileSystemError, AppendLog> when 'env :> IHasFileSystem =
        Resource.create
            (flow {
                let! fs = FileSystem.service
                if maxBytes < 8L then
                    return! Flow.fail (FileSystemError.InvalidPath(Some path, "Log cap must fit a frame header"))
                return!
                    Flow.attemptBlocking (fun _ ->
                        let stream = fs.OpenFile(path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.Read)
                        try
                            let valid, _ = scan maxBytes stream
                            if valid <> stream.Length then
                                stream.SetLength valid
                                stream.Flush(true)
                            stream.Position <- valid
                            AppendLog(path, maxBytes, fs, stream)
                        with error ->
                            stream.Dispose()
                            System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(error).Throw()
                            Unchecked.defaultof<_>)
                    |> Flow.mapError (FileSystemError.fromException (Some path))
            })
            (fun log -> Flow.delay (fun () -> log.Stream.Dispose(); Flow.succeed ()))

    /// <summary>Serialises appends and flushes record contents to storage before acknowledgement. Rotation retains two generations; directory entries are not fsynced for power-loss durability.</summary>
    let append (record: byte array) (log: AppendLog) : Flow<'env, FileSystemError, unit> =
        (Flow.attemptBlocking (fun _ ->
            if log.Failed then raise (IOException("AppendLog is unavailable after failed rotation"))
            let size = int64 record.Length + 8L
            if size > log.MaxBytes then invalidArg (nameof record) "Record exceeds log cap"
            let stream = log.Stream
            if stream.Length + size > log.MaxBytes then
                stream.Flush(true)
                stream.Dispose()
                try
                    log.FileSystem.MoveFile(log.Path, log.Path + ".1", true)
                    log.Stream <- log.FileSystem.OpenFile(log.Path, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.Read)
                with error ->
                    try
                        if log.FileSystem.FileExists log.Path then
                            log.Stream <- log.FileSystem.OpenFile(log.Path, FileMode.Open, FileAccess.ReadWrite, FileShare.Read)
                            log.Stream.Position <- log.Stream.Length
                        else log.Failed <- true
                    // axial-note AXG010: failed recovery poisons the log; rethrow the original write failure below rather than replace it with the reopen failure.
                    with _ -> log.Failed <- true
                    System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(error).Throw()
            let stream = log.Stream
            let header = Array.zeroCreate<byte> 8
            Buffer.BlockCopy(BitConverter.GetBytes(record.Length), 0, header, 0, 4)
            Buffer.BlockCopy(BitConverter.GetBytes(crc record), 0, header, 4, 4)
            stream.Write(header, 0, 8)
            stream.Write(record, 0, record.Length)
            stream.Flush(true))
        |> Flow.mapError (FileSystemError.fromException (Some log.Path)))
        |> Semaphore.withPermit log.Gate

    /// <summary>Replays valid frames from the current file.</summary>
    let readAll (log: AppendLog) : FlowStream<'env, FileSystemError, byte array> =
        FlowStream.fromFlow (
            Flow.attemptBlocking (fun _ ->
                let read path =
                    if log.FileSystem.FileExists path then
                        use stream = log.FileSystem.OpenFile(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite)
                        scan log.MaxBytes stream |> snd
                    else [||]
                Array.append (read (log.Path + ".1")) (read log.Path))
            |> Flow.mapError (FileSystemError.fromException (Some log.Path)))
        |> FlowStream.collect FlowStream.fromSeq
