---
title: Configuration
linkTitle: Configuration
description: Layered environment, command-line, JSON, browser, and Microsoft configuration decoded by a Reified schema.
project: src/Axial.Configuration/Axial.Configuration.fsproj
---

# Configuration

`Axial.Configuration` replaces ad-hoc environment reads with one layered configuration model. Install it and open
`Axial.Configuration`:

```bash
dotnet add package Axial.Configuration
```

```fsharp
open Axial.Configuration
```

Sources produce `Reified.Data`; later sources win, and one schema decodes the merged result. Failures are values in
`ConfigError`, not exceptions. `Config.processArg` passes a schema-declared secret to a process without exposing it in
plans or transcripts. Microsoft `IConfiguration` remains supported through `HostConfiguration.source` in
`Axial.Hosting`.

> **Fable:** Axial 0.12 requires Reified 0.12.1. Its Fable schema builder supports typed `schema<...>`
> construction and `Config.load` decoding, so source loading, merging, plain reads and watching use the same typed
> path on Fable.

## Guides

1. [Sources, loading, secrets, and watching](configuration.html): build sources, decode a model, read plain values,
   redact secrets, and watch file changes.

## Reference

The generated [Configuration API](xref:M:Axial.Configuration.Config.load) covers sources, errors, loading, plain reads,
watching, and the process-argument bridge.
