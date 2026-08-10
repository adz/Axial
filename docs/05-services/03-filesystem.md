---
title: FileSystem
linkTitle: FileSystem
description: Files, directories, paths, and typed file-system errors as an explicit service.
---

`Axial.FileSystem` turns file access into a declared dependency with a typed failure channel. Where
`File.ReadAllText` throws one of a dozen exception types, `FileSystem.readAllText` returns
`Flow<'env, FileSystemError, string>` — the ways it can fail are part of the signature.

```fsharp no-check reason="Application-specific fixtures are described in the surrounding prose"
open Axial.FileSystem

let loadConfig path : Flow<#IHasFileSystem, FileSystemError, Config> =
    flow {
        let! text = FileSystem.readAllText path
        return parseConfig text
    }
```

## Supplying the service

`IFileSystem` is supplied the same way as any other explicit service:

```fsharp no-check reason="Shown independently; surrounding application context is intentionally omitted"
type AppEnv =
    { FileSystem: IFileSystem }

    interface IHasFileSystem with
        member this.FileSystem = this.FileSystem

let! exit = (loadConfig "app.json").StartAsTask({ FileSystem = FileSystem.live })
```

For a runtime assembled with [layers](/layers/index.html), wrap it: `Layer.succeed FileSystem.live`.

## Typed errors

Every operation fails with `FileSystemError`, a union that classifies what went wrong:

| Case | Raised when |
| --- | --- |
| `FileNotFound path` | The file does not exist |
| `DirectoryNotFound path` | A directory in the path does not exist |
| `AlreadyExists path` | The target path is already taken |
| `Unauthorized (path, message)` | The process lacks permission |
| `InvalidPath (path, message)` | The path is malformed |
| `PathTooLong (path, message)` | The platform rejected the path length |
| `Canceled message` | The operation was interrupted |
| `Io (path, message)` | A general I/O failure |
| `Unsupported (path, message)` | The platform or path shape does not support the operation |
| `Unexpected (path, message)` | Anything else that escaped the operation |

Because failures are typed, recovery is a match rather than an exception filter:

```fsharp no-check reason="Application-specific fixtures are described in the surrounding prose"
let loadOrDefault path =
    loadConfig path
    |> Flow.orElseWith (function
        | FileSystemError.FileNotFound _ -> Flow.succeed Config.defaults
        | error -> Flow.fail error)
```

`FileSystemError.describe` formats a case for logs and messages. `FileSystemError.fromException` performs the
classification itself, which is useful when adapting a third-party API into the same error type.

## Files

Whole-file reads and writes come in text, line, and byte forms, each with an encoding-explicit and an asynchronous
variant:

```fsharp no-check reason="Illustrative fragment is intentionally abbreviated"
FileSystem.readAllText path
FileSystem.readAllTextWithEncoding Encoding.UTF8 path
FileSystem.readAllTextAsync path
FileSystem.readAllLines path
FileSystem.readAllBytes path

FileSystem.writeAllText path contents
FileSystem.writeAllLines path lines
FileSystem.writeAllBytes path bytes
FileSystem.appendAllText path contents
```

The `Async` variants pass the flow's cancellation token to the underlying call, so an interrupted workflow stops a
large read in progress rather than after it. Prefer them for anything that is not small.

`fileExists`, `exists`, `deleteFile`, `copyFile`, and `moveFile` cover the rest of the common surface, and file
metadata has getters and setters for attributes and the creation, last-access, and last-write times in both local and
UTC forms — `getFileLastWriteTimeUtc`, `setFileAttributes`, and so on.

Symbolic links are first class: `createFileSymbolicLink`, `createDirectorySymbolicLink`, `getSymbolicLinkTarget`
(which returns `None` when the path is not a link), and `resolveSymbolicLinkTarget`, whose boolean argument decides
whether to follow the whole chain or stop at the immediate target.

## Streams and scopes

`openRead`, `openText`, `openWrite`, `createFile`, `createText`, `appendText`, and the `openFile` family return open
handles. An open handle is a resource, so acquire it inside a scope rather than trusting a later `Dispose`:

```fsharp no-check reason="Illustrative fragment is intentionally abbreviated"
let copyThrough source destination =
    Flow.acquireReleaseWith
        (FileSystem.openRead source)
        (fun stream _ ->
            stream.Dispose()
            Task.CompletedTask)
        (fun stream -> readAndTransform stream destination)
```

Cleanup then runs whether the workflow succeeds, fails, defects, or is interrupted. See
[scopes and resources](/dependencies/scopes-and-resources.html).

## Directories and paths

`createDirectory` creates missing parents. `deleteDirectory path recursive` takes the recursion flag explicitly, so a
recursive delete is visible at the call site. Listing comes in eager (`getFiles`, `getDirectories`,
`getFileSystemEntries`) and lazy (`enumerateFiles`, `enumerateDirectories`, `enumerateFileSystemEntries`) forms, each
taking a search pattern and a `SearchOption`:

```fsharp no-check reason="Shown independently; surrounding application context is intentionally omitted"
let fsharpSources root =
    FileSystem.enumerateFiles root "*.fs" SearchOption.AllDirectories
```

Path manipulation is also on the service — `combine`, `getFullPath`, `getFileName`, `getExtension`, `getRelativePath`,
`getTempPath`, `getRandomFileName`, and the rest. These are pure string operations on .NET, but routing them through
the service keeps platform-specific separator and rooting behaviour substitutable in tests.

## Testing

`IFileSystem` is a wide interface, and implementing it in full to fake three calls is rarely worth it. Two approaches
work better:

**Use `FileSystem.live` against a temporary directory.** This is what Axial's own tests do. The workflow exercises
real I/O, and the test owns cleanup:

```fsharp no-check reason="Illustrative fragment is intentionally abbreviated"
let root = Path.Combine(Path.GetTempPath(), "my-tests", Guid.NewGuid().ToString "N")
Directory.CreateDirectory root |> ignore

try
    let exit = (workflow root).RunSynchronously({ FileSystem = FileSystem.live })
    test <@ exit = Exit.Success expected @>
finally
    Directory.Delete(root, true)
```

**Wrap `FileSystem.live` to inject one failure.** When the point of the test is error handling, delegate every member
to the live service and override the one that should fail — that keeps the fake honest about everything else.

## Fable

`FileSystem.live` is not compiled for Fable, and `Layer.succeed FileSystem.live` fails with `PlatformNotSupportedException` there.
See [packages and platforms](/notes/packages-and-platforms.html).

## Related

- [Explicit services](/dependencies/explicit-services.html) — how a package declares the service it needs.
- [Scopes and resources](/dependencies/scopes-and-resources.html) — deterministic cleanup for open handles.
- [Error handling](/error-handling/index.html) — expected failures against defects.
