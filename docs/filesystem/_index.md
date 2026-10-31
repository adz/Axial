---
title: FileSystem
linkTitle: FileSystem
description: Files, directories, paths, streaming traversal, atomic writes, and the append log.
project: src/Axial.FileSystem/Axial.FileSystem.fsproj
---

# FileSystem

`Axial.FileSystem` makes file access an explicit dependency. Install it once and open `Axial.FileSystem`:

```bash
dotnet add package Axial.FileSystem
```

```fsharp
open Axial.FileSystem
```

The package contains the `IFileSystem` contract and live `FileSystem` service, the in-memory `TestFileSystem`, and
the higher-level `Files` and `AppendLog` operations. `Files` and `AppendLog` are .NET-only because their streaming
and framed-log implementations use `System.IO` types that Fable cannot compile.

## Guides

1. [Files and directories](/filesystem/filesystem.html): paths, reads, writes, directory operations, and typed `FileSystemError` values.
2. [Streaming, atomic writes, and the append log](/filesystem/files.html): `Files.walk`, `Files.lines`, `Files.writeAtomic`, and `AppendLog`.

## Reference

The generated [FileSystem API](xref:M:Axial.FileSystem.FileSystem.live) covers the service contract and the live and test
implementations. The guides show how to compose the operations in a workflow.
