# FileSystem

`IFileSystem` makes file access an explicit, mockable dependency. Every operation returns
`Flow<'env, FileSystemError, _>` and fails with [`FileSystemError`](/filesystem/api/Axial.FileSystem.FileSystemError.html)
rather than throwing. Read the [FileSystem guide](/filesystem/filesystem.html) for supplying the service and
representative usage; this page is the operation catalog.

## Whole-file reads and writes

Whole-file reads and writes come in text, line, and byte forms, each with an encoding-explicit and an asynchronous
variant: `readAllTextWithEncoding`, `readAllTextAsync`, `readAllBytes`, `writeAllBytes`, and the rest of the
`readAllText`, `readAllLines`, `writeAllLines`, `writeAllText`, `appendAllText`, and `appendAllLines` family.

The `Async` members on `IFileSystem` (including `ReadAllTextAsync`, `ReadAllBytesAsync`, and
`WriteAllTextAsync`) have signatures such as
`ReadAllTextAsync : path: string -> Flow<unit, FileSystemError, string>` — no token argument, and an environment
type of `unit`. The `FileSystem` module functions you call from a workflow (`FileSystem.readAllTextAsync`,
`FileSystem.readAllBytesAsync`, `FileSystem.writeAllTextAsync`, and the rest) wrap those members, so they return
`Flow<'env, FileSystemError, _>` with `when 'env :> IHasFileSystem`. The runtime passes its cancellation token to the
underlying I/O, so interrupting a workflow stops a large read in progress.

## Files and metadata

`fileExists`, `exists`, `deleteFile`, `copyFile`, and `moveFile` cover the other common operations.
`getFileLength` returns a file's size in bytes. File metadata has getters and setters for attributes and the
creation, last-access, and last-write times in both local and UTC forms, such as `getFileLastWriteTimeUtc`,
`setFileAttributes`, and so on.

Symbolic links are first class: `createFileSymbolicLink`, `createDirectorySymbolicLink`, `getSymbolicLinkTarget`
(which returns `None` when the path is not a link), and `resolveSymbolicLinkTarget`, whose boolean argument decides
whether to follow the whole chain or stop at the immediate target.

## Streams and scopes

`openRead`, `openText`, `openWrite`, `createFile`, `createText`, `appendText`, and the `openFile` family return open
handles. Acquire open handles inside a scope to ensure disposal; see
[scopes and resources](/scopes/index.html).

## Directories and paths

`createDirectory` creates missing parents. `deleteDirectory path recursive` takes the recursion flag explicitly, so a
recursive delete is visible at the call site. Listing comes in eager (`getFiles`, `getDirectories`,
`getFileSystemEntries`) and lazy (`enumerateFiles`, `enumerateDirectories`, `enumerateFileSystemEntries`) forms, each
taking a search pattern and a `SearchOption`. `enumerateFileSystemInfos directory pattern searchOption` yields
`FileSystemEntry` values with path, kind, size, and timestamps in one listing, avoiding separate metadata calls per
entry.

Path manipulation is also on the service: `combine`, `getFullPath`, `getFileName`, `getExtension`, `getRelativePath`,
`getTempPath`, `getRandomFileName`, and the rest. These are pure string operations on .NET, but routing them through
the service keeps platform-specific separator and rooting behaviour substitutable in tests.
