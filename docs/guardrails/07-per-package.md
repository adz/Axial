---
title: Guardrails per package
linkTitle: Guardrails per package
description: Each Axial package turns on the rules for the effect it replaces; MSBuild items turn them on or off.
---

# Guardrails per package

A finding that says "use `IHttp`" is no help to a project that cannot reference `Axial.HttpClient`. So rules come in
groups, called guardrails, named after the package whose service replaces the effect. Referencing the package turns
its guardrail on.

| Guardrail | Reports |
| --- | --- |
| `Axial` | Clock reads, `Stopwatch`, `Task.Delay`, `Thread.Sleep`, `Environment.ProcessorCount`, randomness, GUIDs, `TimeZoneInfo.Local`, blocking waits, and the rules from `stale-allow` to `task-surface` |
| `Axial.Configuration` | Environment variables and other ambient environment state |
| `Axial.Console` | `System.Console` |
| `Axial.FileSystem` | `System.IO.File` and `System.IO.Directory` |
| `Axial.Process` | `Process.Start` |
| `Axial.HttpClient` | `new HttpClient` |

Change the set with MSBuild items, in a project file or `Directory.Build.props`:

```xml
<ItemGroup>
  <!-- Turn a guardrail off -->
  <AxialGuardrail Remove="Axial.Console" />
  <!-- Turn one on without referencing its package -->
  <AxialGuardrail Include="Axial.FileSystem" />
</ItemGroup>
```

Removing a guardrail stops its rules. It does not lower the severity of the rules that still run.

A package of your own can add a guardrail: see [Capabilities](capabilities.html) for a service that owns an API, and
[Package rules](package-rules.html) for simple rules.
