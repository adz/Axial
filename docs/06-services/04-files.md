---
title: Files
linkTitle: Files
description: Streaming traversal, atomic writes, and the append log as first-use operations over IFileSystem.
project: src/Axial.Files/Axial.Files.fsproj
---

# Axial.Files

Install `Axial.Files`; it composes `IFileSystem` operations into lazy streams:

```bash
dotnet add package Axial.Files
```

`Files.walk descend root` lists descendants breadth-first; `descend` receives each directory path and decides whether to list its children. `Files.lines path` reads text in batches of 2048 lines, detects UTF-8 and UTF-16 BOMs, and returns an empty stream for files with NUL bytes in the first 8192 bytes (unless they have a UTF-16 BOM). Stopping a line stream early closes its reader. Both operations work with `FileSystem.test`.

`Files.writeAtomic path bytes` writes the full contents to a sibling temporary file in the same directory, flushes it
to disk, then renames it over the destination with `MoveFile(overwrite = true)`. Because the temporary file lives in
the same directory as the destination, the rename stays on one filesystem and is atomic: readers see either the
previous contents — or no file, when the destination did not exist yet — or the complete new contents, never a
partially written destination. A crash before the rename leaves the destination untouched and may leave a
`.name.*.tmp` sibling, which `cleanupStaleTemps` removes. `Files.writeAllTextAtomic path text` uses UTF-8;
`Files.writeAllTextAtomicWithEncoding path text encoding` selects an encoding. Failures return `FileSystemError` and
attempt to remove the temporary file. Power-loss durability on some filesystems also requires fsync of the parent
directory after rename, which this API does not perform.

## AppendLog

`AppendLog.openLog path maxBytes` opens a scoped, single-writer log. `AppendLog.append record log` writes an 8-byte length/CRC32 header followed by the payload and flushes before acknowledging the append. On open, recovery scans frames and truncates the current file at the first incomplete or corrupt frame. `AppendLog.readAll log` replays valid records from `<path>.1` followed by `<path>` in order. When the next frame would exceed `maxBytes`, append rotates the current file to `<path>.1`, replacing the previous rotated file; each file stays within the cap. This is a bounded two-file log, not unlimited history.

Concurrent appends to one log are serialised. Recovery rejects frame lengths larger than `maxBytes - 8` before allocating. If a kill occurs after rotation's rename but before creation of the new current file, opening the log keeps `<path>.1` and creates an empty current file.

```fsharp prepare
open System
open System.IO
open System.Text
open Axial
open Axial.FileSystem
open Axial.Files

let shouldEqual expected actual =
    if actual <> expected then failwithf "Expected %A but got %A" expected actual
```

```fsharp
open System.Text
open Axial
open Axial.FileSystem
open Axial.Files

let fileSystemEnv = { new IHasFileSystem with member _.FileSystem = FileSystem.live }

let appendAndReplay (path: string) : Flow<IHasFileSystem, FileSystemError, byte array list> =
    flow {
        // Open the log, append two records, and let the scope close it.
        let! log = AppendLog.openLog path 1024L |> Flow.scopeResource
        do! [ "first"; "second" ]
            |> List.map Encoding.ASCII.GetBytes
            |> List.fold (fun step record -> step |> Flow.bind (fun () -> AppendLog.append record log)) (Flow.succeed ())

        // Reopen and replay the acknowledged records in order.
        let! log = AppendLog.openLog path 1024L |> Flow.scopeResource
        return! AppendLog.readAll log |> FlowStream.runCollect
    }
```

```fsharp run
let path = Path.Combine(Path.GetTempPath(), "axial-docs-append-log")
for candidate in [ path; path + ".1" ] do
    if File.Exists candidate then File.Delete candidate
try
    appendAndReplay path
    |> Flow.run fileSystemEnv
    |> Exit.map (List.map (fun bytes -> System.Text.Encoding.ASCII.GetString bytes))
    |> shouldEqual (Exit.Success [ "first"; "second" ])
finally
    for candidate in [ path; path + ".1" ] do
        if File.Exists candidate then File.Delete candidate
```

`AppendLog.openLog` returns a `Resource`, so the scope acquires and later closes the log; `AppendLog.readAll`
replays the records written by the earlier append.

`Flush(true)` makes acknowledged record contents durable. Process kills lose no acknowledged records; an unacknowledged tail can be discarded on recovery. A newly created or renamed file's directory entry is durable across power loss only when the directory is fsynced; this package does not fsync directories.

`Files.watch recursive path` streams `FileSystemChange` events for a directory or one file. Set `recursive` to `true` to include nested directories. The watcher is disposed when the stream ends, including after `FlowStream.take`. Watcher errors are typed `FileSystemError` failures. The buffer holds 1024 events; overflow fails the stream rather than silently dropping changes. Rescan the watched path after an error or overflow because filesystem notifications are not a durable log.
