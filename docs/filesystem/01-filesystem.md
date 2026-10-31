---
title: FileSystem
linkTitle: FileSystem
description: Files, directories, paths, and typed file-system errors as an explicit service.
project: src/Axial.FileSystem/Axial.FileSystem.fsproj
---

`Axial.FileSystem` makes file access an explicit dependency. `FileSystem.readAllText` returns
`Flow<'env, FileSystemError, string>` instead of throwing the exceptions raised by `File.ReadAllText`.

```fsharp prepare
// Setup for the checked examples on this page.
open System
open System.IO
open System.Threading
open System.Threading.Tasks
open Axial
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

Use `FileSystemError.isIoFailure` to recognize missing files or directories, `Unauthorized`, path length errors, and general I/O errors together; it excludes invalid paths, existing targets, unsupported operations, and unexpected failures. `Unauthorized` includes access denial from `UnauthorizedAccessException`, which is not an `IOException`. Code migrating from `catch IOException` therefore handles access denial too. To exclude it, match `FileSystemError.Unauthorized _` before applying `isIoFailure`:

```fsharp
let isIoFailureWithoutAccessDenial error =
    match error with
    | FileSystemError.Unauthorized _ -> false
    | other -> FileSystemError.isIoFailure other
```

Match `FileSystemError` to recover from a failure:

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

The same family — `readAllTextWithEncoding`, `readAllTextAsync`, `readAllBytes`, `writeAllBytes`, the `Async`
members on `IFileSystem`, file metadata getters/setters, and symbolic-link operations — is catalogued in the
[FileSystem API reference](/filesystem/api/Axial.FileSystem.FileSystemModule.html). The workflow-facing module functions wrap
the `Async` interface members and pass the runtime's cancellation token to the underlying I/O, so interrupting a
workflow stops a large read in progress.

## Streams and scopes

`openRead`, `openText`, `openWrite`, `createFile`, `createText`, `appendText`, and the `openFile` family return open
handles. Acquire open handles inside a scope to ensure disposal:

```fsharp
let readAndTransform (stream: Stream) (destination: string) : Flow<AppEnv, FileSystemError, unit> =
    use reader = new StreamReader(stream)
    FileSystem.writeAllText destination (reader.ReadToEnd().ToUpperInvariant())

let copyThrough (source: string) (destination: string) : Flow<AppEnv, FileSystemError, unit> =
    Flow.scopeAcquireRelease
        (FileSystem.openRead source)
        (fun stream ->
            Flow.delay (fun () ->
                stream.Dispose()
                Flow.ok ()))
    |> Flow.bind (fun stream -> readAndTransform stream destination)
    |> Flow.scoped
```


Cleanup then runs whether the workflow succeeds, fails, defects, or is interrupted. See
[scopes and resources](/scopes/index.html).

## Directories and paths

`createDirectory` creates missing parents, and `deleteDirectory path recursive` takes the recursion flag
explicitly so a recursive delete is visible at the call site. Listing comes in eager and lazy forms; the lazy
`enumerateFiles` used below avoids materializing every entry up front:

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

The other listing forms, `enumerateFileSystemInfos`, and the path helpers (`combine`, `getFullPath`, `getFileName`,
`getExtension`, `getRelativePath`, `getTempPath`, `getRandomFileName`, and the rest) are catalogued in the
[FileSystem API reference](/filesystem/api/Axial.FileSystem.FileSystemModule.html).

## Testing

To test a few `IFileSystem` calls without implementing the full interface, use one of these approaches:

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
