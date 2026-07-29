---
title: Build Generation
linkTitle: Build Generation
description: Configure MSBuild to generate schemas from marked F# records automatically.
weight: 10
---

# Build Generation

Add the runtime Schema package and the private build-time generator package:

```xml
<ItemGroup>
  <PackageReference Include="Axial.Schema" Version="..." />
  <PackageReference Include="Axial.Schema.Contracts.Build" Version="..." PrivateAssets="all" />
</ItemGroup>
```

Put `[<DeriveSchema>]` records in ordinary `.fs` files already listed as `<Compile>` items. No generator item, target,
or manual generated-file `<Compile>` entry is required. Before `CoreCompile`, the package:

1. examines the project's F# compile files in order;
2. generates a companion for every file containing marked records;
3. inserts each generated file immediately after its source file; and
4. lets the F# compiler catch stale field names and incompatible generated code.

By default output is written below `obj/`, so it should not be committed.

## Complete project example

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
  </PropertyGroup>

  <ItemGroup>
    <Compile Include="Wire.fs" />
    <Compile Include="Program.fs" />
  </ItemGroup>

  <ItemGroup>
    <PackageReference Include="Axial.Schema" Version="..." />
    <PackageReference Include="Axial.Schema.Contracts.Build" Version="..." PrivateAssets="all" />
  </ItemGroup>
</Project>
```

`Wire.fs` must use a namespace and place marked records at namespace level:

```fsharp
namespace MyApp.Wire

open Axial.Schema.Derive

[<DeriveSchema>]
type Order = { Sku: string; Quantity: int }
```

An ordinary `dotnet build` now generates and compiles the companion schema.

## MSBuild properties

| Property | Default | Purpose |
| --- | --- | --- |
| `AxialSchemaGenEnabled` | `true` | Set to `false` to skip generation. |
| `AxialSchemaNaming` | `camel` | Default wire naming: `camel`, `snake`, or `verbatim`. |
| `AxialSchemaGeneratedFiles` | `Intermediate` | `Intermediate` writes under `obj`; `CheckedIn` writes sibling `.g.fs` files. |
| `AxialSchemaGenToolPath` | packaged tool | Override the generator task assembly path, mainly for repository development. |

### Checked-in output

Use checked-in output when generated source must be reviewed or consumed outside the normal build:

```xml
<PropertyGroup>
  <AxialSchemaGeneratedFiles>CheckedIn</AxialSchemaGeneratedFiles>
</PropertyGroup>
```

The build writes `Wire.g.fs` beside `Wire.fs` and still manages F# compile ordering. Do **not** add the `.g.fs` file to
`<Compile>` yourself.

## Troubleshooting

- **No output:** confirm generation is enabled, the source is a `<Compile>` item, and the record has
  `[<DeriveSchema>]`.
- **Namespace diagnostic:** marked records must be namespace-level declarations; one generated companion has one
  namespace.
- **Duplicate compile item:** remove a manually listed `.g.fs`; the target inserts it.
- **A stale checked-in file fails compilation:** rebuild to regenerate it. This failure is intentional drift detection.
