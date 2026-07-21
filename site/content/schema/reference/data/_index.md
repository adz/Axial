---
title: "Data"
weight: 30
type: docs
---

This page shows `Axial.Data`: one source-neutral tree for nulls, primitives, lists, and objects, with constructors for maps, name/value pairs, CLI arguments, JSON, and configuration. Use it to shape data in tests and fixtures, to carry boundary input before a type is assigned, and to redisplay raw values by path. It has no dependencies on other Axial packages.

## The tree

- [`Data`](./t-data.md): A portable tree representing the meaning and shape of unowned structured data.
- [`DataPathSegment`](./t-datapathsegment.md): A segment in a structured data path.
- [`DataPath`](./t-datapath.md): Helpers for constructing, parsing, and rendering structured data paths.

## Constructors

- [`Data.ofMap`](./m-data-ofmap.md): Builds object-shaped structured data from a map of scalar field values.
- [`Data.ofNameValues`](./m-data-ofnamevalues.md): Builds object-shaped structured data from name/value pairs, grouping repeated names into <code>Many</code>.
- [`Data.ofCliArgs`](./m-data-ofcliargs.md):
 Builds structured data from command-line arguments.

- [`Data.ofJsonElement`](./m-data-ofjsonelement.md): Builds structured data from a <a href="https://learn.microsoft.com/dotnet/api/system.text.json.jsonelement">JsonElement</a>.
- [`Data.ofJsonDocument`](./m-data-ofjsondocument.md): Builds structured data from the root element of a <a href="https://learn.microsoft.com/dotnet/api/system.text.json.jsondocument">JsonDocument</a>.
- [`Data.ofConfiguration`](./m-data-ofconfiguration.md):
 Builds structured data from flattened configuration keys using <code>:</code> as the path separator.


## Redisplay

- [`Data.redisplay`](./m-data-redisplay.md):  Redisplays a scalar structured data value, returning blank text for missing, object-shaped, or collection-shaped input.
- [`Data.redisplayPath`](./m-data-redisplaypath.md): Parses an input path and redisplays the addressed scalar structured data value.
