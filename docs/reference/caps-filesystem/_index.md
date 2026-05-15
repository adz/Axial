---
title: "Capabilities FileSystem"
---

This page shows the file-system capability package. `IFileSystem` names the small set of file operations currently supported by FsFlow examples and app workflows: reading text, writing text, and existence checks. Use it when file access is part of a workflow boundary but you still want tests to provide an in-memory or fake implementation. The live provider belongs at the composition root; reusable workflow code should depend on the interface.

## Capability

- [`Capabilities.FileSystem.IFileSystem`](./t-capabilities-filesystem-ifilesystem.md): Provides synchronous access to file system operations.

## Helpers

- [`Capabilities.FileSystem.FileSystem.readAllText`](./m-capabilities-filesystem-filesystem-readalltext.md): Reads all text from a file using the file system environment.
- [`Capabilities.FileSystem.FileSystem.writeAllText`](./m-capabilities-filesystem-filesystem-writealltext.md): Writes all text to a file using the file system environment.
- [`Capabilities.FileSystem.FileSystem.exists`](./m-capabilities-filesystem-filesystem-exists.md): Checks if a file exists using the file system environment.
- [`Capabilities.FileSystem.FileSystem.live`](./m-capabilities-filesystem-filesystem-live.md): Creates a live file system backed by <a href="https://learn.microsoft.com/dotnet/api/system.io.file">File</a>.

