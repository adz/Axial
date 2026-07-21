---
title: Packages and platforms
linkTitle: Packages and platforms
description: Schema product packages and their supported .NET and Fable JavaScript runtimes.
weight: 95
---

# Packages and platforms

Schema packages are independently installable. “Node and browser” means the package is compiled and exercised as Fable
JavaScript without depending on one of those hosts. Host-neutral packages can also run in other JavaScript environments
that provide the JavaScript primitives they use.

| Package | .NET | Fable JavaScript | JavaScript host | Purpose |
| --- | --- | --- | --- | --- |
| `Axial.Data` | Yes | Yes | Node and browser | Source-neutral structured boundary data. |
| `Axial.ErrorHandling` | Yes | Yes | Node and browser | `Result`, `Check`, `Validation`, diagnostics, parsing, and refined values. |
| `Axial.Schema` | Yes | Yes | Node and browser | Schema declaration, parsing, checking, and inspection. |
| `Axial.Schema.Json` | Yes | Yes | Node and browser | Compiled JSON codecs with platform-specific runtimes behind one API. |
| `Axial.Schema.JsonSchema` | Yes | No | — | JSON Schema document generation. |
| `Axial.Schema.Http` | Yes, .NET 8+ | No | — | Host-neutral .NET HTTP boundary contracts and OpenAPI assembly. |
| `Axial.Schema.Http.AspNetCore` | Yes, .NET 8+ | No | — | ASP.NET Core boundary adapter. |
| `Axial.Schema.Http.GenHttp` | Yes, .NET 8+ | No | — | GenHTTP boundary adapter. |
| `Axial.Schema.Contracts` | Yes, .NET 8+ | No | — | Repository tool-tier contract and record source generation; not packable. |
| `Axial.Schema.Contracts.Build` | Yes, .NET 8+ | No | — | MSBuild package that runs contract generation before compilation. |
| `Axial.Schema.Testing` | Yes, .NET 8+ | No | — | Repository-only FsCheck adapter; not packable. |

The Fable JavaScript build is a separate compilation of the same F# sources. A `netstandard2.1` target by itself does
not imply JavaScript support; the table records packages with an intentional Fable surface and repository coverage.
