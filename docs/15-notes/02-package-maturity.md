---
title: Package maturity
---

# Package maturity

A package's rating reflects adoption outside this repository, not its age or test coverage.

- **Experimental:** no real application uses the package. It may change or be removed in any release.
- **Incubating:** at least one real application uses it directly or through a package it references, and it is pre-1.0 or used by fewer than two real applications. Its API may change in minor releases.
- **Stable:** version 1.0 or later **and** used by at least two real applications. Semantic versioning and deprecation cycles apply.

A real application is in active use outside this repository. Currently gitkay and Logs Digger qualify; examples, samples, tests, and reference applications do not. Promote a package only when its usage and version meet the next level's criteria. The package project sets `PackageMaturity`; NuGet descriptions and `maturity-*` tags derive from it.

| Package | Maturity | Evidence |
| --- | --- | --- |
| `Axial` | Incubating | gitkay, Logs Digger |
| `Axial.Layers` | Incubating | gitkay, Logs Digger |
| `Axial.PlatformService` | Incubating | gitkay, Logs Digger |
| `Axial.FileSystem` | Incubating | gitkay, Logs Digger |
| `Axial.Guardrails` | Incubating | gitkay, Logs Digger |
| `Axial.ElmishFlows` | Incubating | gitkay, Logs Digger |
| `Axial.Console` | Incubating | gitkay |
| `Axial.HttpClient` | Incubating | gitkay |
| `Axial.Process` | Incubating | gitkay |
| `Axial.Files` | Incubating | Logs Digger |
| `Axial.Hosting` | Experimental | No qualifying application |
| `Axial.Hosting.Browser` | Experimental | No qualifying application |
| `Axial.Hosting.Node` | Experimental | No qualifying application |
| `Axial.Hosting.AspNetCore` | Experimental | No qualifying application |
| `Axial.Hosting.GenHttp` | Experimental | No qualifying application |
| `Axial.Telemetry` | Experimental | No qualifying application |
| `Axial.Telemetry.JavaScript` | Experimental | No qualifying application |
| `Axial.WebSocket` | Experimental | No qualifying application |
