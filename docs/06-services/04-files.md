# Axial.Files

`Axial.Files` composes `IFileSystem` operations into lazy streams. `Files.walk descend root` lists descendants breadth-first; `descend` receives each directory path and decides whether to list its children. `Files.lines path` reads text in batches of 2048 lines, detects UTF-8 and UTF-16 BOMs, and returns an empty stream for files with NUL bytes in the first 8192 bytes (unless they have a UTF-16 BOM). Stopping a line stream early closes its reader. Both operations work with `FileSystem.test`.

`Files.watch recursive path` streams `FileChange` events for a directory or one file. Set `recursive` to `true` to include nested directories. The watcher is disposed when the stream ends, including after `FlowStream.take`. Watcher errors are typed `FileSystemError` failures. The buffer holds 1024 events; overflow fails the stream rather than silently dropping changes. Rescan the watched path after an error or overflow because filesystem notifications are not a durable log.
