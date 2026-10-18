# Axial.Files

`Axial.Files` composes `IFileSystem` operations into lazy streams. `Files.walk descend root` lists descendants breadth-first; `descend` receives each directory path and decides whether to list its children. `Files.lines path` reads text in batches of 2048 lines, detects UTF-8 and UTF-16 BOMs, and returns an empty stream for files with NUL bytes in the first 8192 bytes (unless they have a UTF-16 BOM). Stopping a line stream early closes its reader. Both operations work with `FileSystem.test`.

`Files.writeAtomic path bytes` writes to a sibling temporary file, flushes it to disk, then renames it over the destination. `Files.writeAllTextAtomic path text` uses UTF-8; `Files.writeAllTextAtomicWithEncoding path text encoding` selects an encoding. Failures return `FileSystemError` and attempt to remove the temporary file. A process crash cannot expose a partially written destination. Power-loss durability on some filesystems also requires fsync of the parent directory after rename, which this API does not perform.

## AppendLog

`AppendLog.openLog path maxBytes` opens a scoped, single-writer log. `AppendLog.append record log` writes an 8-byte length/CRC32 header followed by the payload and flushes before acknowledging the append. On open, recovery scans frames and truncates the current file at the first incomplete or corrupt frame. `AppendLog.readAll log` replays valid records from `<path>.1` followed by `<path>` in order. When the next frame would exceed `maxBytes`, append rotates the current file to `<path>.1`, replacing the previous rotated file; each file stays within the cap. This is a bounded two-file log, not unlimited history.

Concurrent appends to one log are serialised. Recovery rejects frame lengths larger than `maxBytes - 8` before allocating. If a kill occurs after rotation's rename but before creation of the new current file, opening the log keeps `<path>.1` and creates an empty current file.

`Flush(true)` makes acknowledged record contents durable. Process kills lose no acknowledged records; an unacknowledged tail can be discarded on recovery. A newly created or renamed file's directory entry is durable across power loss only when the directory is fsynced; this package does not fsync directories.

`Files.watch recursive path` streams `FileChange` events for a directory or one file. Set `recursive` to `true` to include nested directories. The watcher is disposed when the stream ends, including after `FlowStream.take`. Watcher errors are typed `FileSystemError` failures. The buffer holds 1024 events; overflow fails the stream rather than silently dropping changes. Rescan the watched path after an error or overflow because filesystem notifications are not a durable log.
