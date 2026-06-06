---
title: "Implementations"
type: docs
---

This page shows the live `FileSystem.FileSystem` implementations used to provide the explicit file-system service.

- [`FileSystem.FileSystem.live`](./p-filesystem-filesystem-live.md): Creates a live file-system service backed by <a href="https://learn.microsoft.com/dotnet/api/system.io.file">File</a>, <a href="https://learn.microsoft.com/dotnet/api/system.io.directory">Directory</a>, and <a href="https://learn.microsoft.com/dotnet/api/system.io.path">Path</a>.
- [`FileSystem.FileSystem.layer`](./p-filesystem-filesystem-layer.md): Builds the live file-system service as a layer.
