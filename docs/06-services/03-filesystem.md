---
title: FileSystem
linkTitle: FileSystem
description: Files, directories, paths, and typed file-system errors as an explicit service.
---

`Axial.FileSystem` turns file access into a declared dependency with a typed failure channel. Where
`File.ReadAllText` throws one of a dozen exception types, `FileSystem.readAllText` returns
`Flow<'env, FileSystemError, string>`, so the ways it can fail are part of the signature.

```fsharp prepare
// Setup for the checked examples on this page.
open System
open System.IO
open System.Threading
open System.Threading.Tasks
open Axial
open Axial.Layers
open Axial.Console
open Axial.FileSystem
open Axial.Hosting
open Axial.Hosting.Browser
open Axial.Hosting.Node
open Axial.PlatformService
open Axial.State
open Axial.Telemetry
open Axial.Telemetry.JavaScript

/// Fails the docs test when an example's result differs from the value shown.
let shouldEqual expected actual =
    if actual <> expected then failwithf "Expected %A but got %A" expected actual
```

```fsharp
type Config = { Name: string }

let parseConfig (text: string) = { Name = text.Trim() }

let loadConfig (path: string) : Flow<#IHasFileSystem, FileSystemError, Config> =
    flow {
        let! text = FileSystem.readAllText path
        return parseConfig text
    }
```

## Supplying the service

`IFileSystem` is supplied the same way as any other explicit service:

```fsharp
type AppEnv =
    { FileSystem: IFileSystem }

    interface IHasFileSystem with
        member this.FileSystem = this.FileSystem

let live = { FileSystem = FileSystem.live }
```

The examples on this page work in a fresh temporary directory:

```fsharp
let root = Path.Combine(Path.GetTempPath(), "axial-docs-filesystem", Guid.NewGuid().ToString "N")
Directory.CreateDirectory root |> ignore
```

```fsharp run
File.WriteAllText(Path.Combine(root, "app.json"), "orders ")
loadConfig (Path.Combine(root, "app.json")) |> Flow.run live |> shouldEqual (Exit.Success { Name = "orders" })
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
| `Io (path, message)` | A general I/O failure |
| `Unsupported (path, message)` | The platform or path shape does not support the operation |
| `Unexpected (path, message)` | Anything else that escaped the operation |

Because failures are typed, recovery is a match rather than an exception filter:

```fsharp
let defaults = { Name = "default" }

let loadOrDefault (path: string) : Flow<AppEnv, FileSystemError, Config> =
    loadConfig path
    |> Flow.orElseWith (function
        | FileSystemError.FileNotFound _ -> Flow.succeed defaults
        | error -> Flow.fail error)
```

```fsharp run
loadOrDefault (Path.Combine(root, "missing.json")) |> Flow.run live |> shouldEqual (Exit.Success defaults)
```

`FileSystemError.describe` formats a case for logs and messages. `FileSystemError.fromException` performs the
classification itself, which is useful when adapting a third-party API into the same error type.

## Files

Whole-file reads and writes come in text, line, and byte forms, each with an encoding-explicit and an asynchronous
variant:

```fsharp
let notes (path: string) : Flow<AppEnv, FileSystemError, string array * int64> =
    flow {
        do! FileSystem.writeAllLines path [ "one"; "two" ]
        do! FileSystem.appendAllText path "three"
        let! lines = FileSystem.readAllLines path
        let! length = FileSystem.getFileLength path
        return lines, length
    }
```

```fsharp run
let lines, length = notes (Path.Combine(root, "notes.txt")) |> Flow.run live |> Exit.toResult |> Result.defaultWith (failwithf "%A")
lines |> shouldEqual [| "one"; "two"; "three" |]
length |> shouldEqual (int64 ("one" + Environment.NewLine + "two" + Environment.NewLine + "three").Length)
```

The same family has `readAllTextWithEncoding`, `readAllTextAsync`, `readAllBytes`, `writeAllBytes`, and the other
encoding-explicit and asynchronous forms.

The `Async` variants pass the flow's cancellation token to the underlying call, so an interrupted workflow stops a
large read in progress rather than after it. Prefer them for anything that is not small.

`fileExists`, `exists`, `deleteFile`, `copyFile`, and `moveFile` cover the other common operations.
`getFileLength` returns a file's size in bytes, and file metadata has getters and setters for attributes and the creation, last-access, and last-write times in both local and
UTC forms, such as `getFileLastWriteTimeUtc`, `setFileAttributes`, and so on.

Symbolic links are first class: `createFileSymbolicLink`, `createDirectorySymbolicLink`, `getSymbolicLinkTarget`
(which returns `None` when the path is not a link), and `resolveSymbolicLinkTarget`, whose boolean argument decides
whether to follow the whole chain or stop at the immediate target.

## Streams and scopes

`openRead`, `openText`, `openWrite`, `createFile`, `createText`, `appendText`, and the `openFile` family return open
handles. An open handle is a resource, so acquire it inside a scope rather than trusting a later `Dispose`:

```fsharp
let readAndTransform (stream: Stream) (destination: string) : Flow<AppEnv, FileSystemError, unit> =
    use reader = new StreamReader(stream)
    FileSystem.writeAllText destination (reader.ReadToEnd().ToUpperInvariant())

let copyThrough (source: string) (destination: string) : Flow<AppEnv, FileSystemError, unit> =
    Flow.scopeAcquireRelease
        (FileSystem.openRead source)
        (fun stream _ ->
            stream.Dispose()
            Task.CompletedTask)
    |> Flow.bind (fun stream -> readAndTransform stream destination)
    |> Flow.scoped
```


Cleanup then runs whether the workflow succeeds, fails, defects, or is interrupted. See
[scopes and resources](/scopes/index.html).

## Directories and paths

`createDirectory` creates missing parents. `deleteDirectory path recursive` takes the recursion flag explicitly, so a
recursive delete is visible at the call site. Listing comes in eager (`getFiles`, `getDirectories`,
`getFileSystemEntries`) and lazy (`enumerateFiles`, `enumerateDirectories`, `enumerateFileSystemEntries`) forms, each
taking a search pattern and a `SearchOption`:

```fsharp
let fsharpSources (directory: string) : Flow<AppEnv, FileSystemError, string list> =
    flow {
        let! files = FileSystem.enumerateFiles directory "*.fs" SearchOption.AllDirectories
        return files |> Seq.map Path.GetFileName |> Seq.sort |> List.ofSeq
    }
```

```fsharp run
Directory.CreateDirectory(Path.Combine(root, "src", "nested")) |> ignore
File.WriteAllText(Path.Combine(root, "src", "Program.fs"), "")
File.WriteAllText(Path.Combine(root, "src", "nested", "Library.fs"), "")
File.WriteAllText(Path.Combine(root, "src", "notes.md"), "")

fsharpSources (Path.Combine(root, "src")) |> Flow.run live |> shouldEqual (Exit.Success [ "Library.fs"; "Program.fs" ])
```

Path manipulation is also on the service: `combine`, `getFullPath`, `getFileName`, `getExtension`, `getRelativePath`,
`getTempPath`, `getRandomFileName`, and the rest. These are pure string operations on .NET, but routing them through
the service keeps platform-specific separator and rooting behaviour substitutable in tests.

## Testing

`IFileSystem` is a wide interface, and implementing it in full to fake three calls is rarely worth it. Two approaches
work better:

**Use `FileSystem.live` against a temporary directory.** This is what Axial's own tests do, and what the examples on
this page do. The workflow exercises real I/O, and the test owns cleanup:

```fsharp run
try
    File.WriteAllText(Path.Combine(root, "app.json"), "orders")
    loadOrDefault (Path.Combine(root, "app.json")) |> Flow.run live |> shouldEqual (Exit.Success { Name = "orders" })
finally
    Directory.Delete(root, true)
```

**Wrap `FileSystem.live` to inject one failure.** When the point of the test is error handling, delegate every member
to the live service and override the one that should fail, so every other member behaves as in production.

## Fable

`FileSystem.live` is not compiled for Fable, and `Layer.succeed FileSystem.live` fails with `PlatformNotSupportedException` there.
See [packages and platforms](/notes/packages-and-platforms.html).

## Related

- [Service contracts](/dependencies/service-contracts.html): how a package declares the service it needs.
- [Scopes and resources](/scopes/index.html): deterministic cleanup for open handles.
- [Error handling](/error-handling/index.html): expected failures against defects.
