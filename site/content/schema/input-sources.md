---
weight: 35
title: Input Sources
type: docs
description: HTTP form-like, CLI, JSON-like, and configuration input through one schema.
---


`RawInput` is the source-agnostic boundary shape: `Missing`, `Scalar`, `Many`, and `Object`. Adapters turn common
sources into `RawInput`, and one schema parses them all.

## The Schema

```fsharp
type Contact = { Kind: string; Value: string }

type Customer =
    { Name: string
      Address: Address
      Contacts: Contact list }

let customerSchema =
    Schema.recordFor<Customer, _> (fun name address contacts ->
        { Name = name; Address = address; Contacts = contacts })
    |> Schema.fieldWith [ SchemaConstraint.required ] "name" _.Name Value.text
    |> Schema.fieldWith [ SchemaConstraint.required ] "address" _.Address (Value.nested addressSchema)
    |> Schema.fieldWith [ SchemaConstraint.minCount 1 ] "contacts" _.Contacts (Value.many contactSchema)
    |> Schema.build
```

Nested fields expect object-shaped input and prefix their diagnostics with the field name; collection fields expect
`RawInput.Many`, parse every item, accumulate every item error, and prefix diagnostics with the item index.

## HTTP Form-Like Input

Form posts and query strings are name/value pairs; repeated names become `Many`:

```fsharp
let raw =
    RawInput.ofNameValues
        [ "name", "Ada Lovelace"
          "tag", "vip"
          "tag", "beta" ]      // repeated names accumulate into Many
```

`RawInput.ofMap` handles single-valued maps, and `RawInput.ofNameValueCollection` adapts
`System.Collections.Specialized.NameValueCollection` directly from ASP.NET-style APIs. Name/value sources are flat; use
the configuration or JSON adapters below when the input carries nested models or indexed collections.

## CLI Arguments

```fsharp
let raw = RawInput.ofCliArgs [ "--name"; "Ada Lovelace"; "--verbose"; "--no-color" ]
```

`--name value`, `--name=value`, `-n value`, boolean flags, `--no-name`, and repeated options are supported; positional
arguments collect under the `_` field.

## JSON Bodies With System.Text.Json

On .NET 8+ targets, adapt a parsed `JsonDocument` or `JsonElement` directly — the natural fit for ASP.NET Core
request bodies:

```fsharp
use! document = JsonDocument.ParseAsync request.Body
let raw = RawInput.ofJsonDocument document
```

JSON null becomes `Missing`, numbers keep their exact boundary text, and arrays and objects map to `Many` and
`Object`. The adapter uses the in-box `System.Text.Json`, so the package stays dependency-free.

## JSON-Like Input

On other targets (including Fable), deserialize with any JSON library into `JsonLikeValue`, then adapt:

```fsharp
let raw = RawInput.ofJsonLikeValue jsonValue   // objects, arrays, scalars, null
```

Arrays map to `Many` and objects to `Object`, so nested models and collections parse with no extra shaping.

## Configuration

Configuration keys use `:`-separated sections and numeric segments for collection indexes:

```fsharp
let raw =
    RawInput.ofConfiguration
        [ "name", "Ada Lovelace"
          "address:city", "London"
          "contacts:0:kind", "email"
          "contacts:0:value", "ada@example.com" ]
```

## One Parse For All Of Them

```fsharp
let parsed = Input.parse customerSchema raw

match parsed.Result with
| Ok customer -> customer
| Error diagnostics ->
    // same paths for every source:
    parsed.ErrorsFor "contacts[0].value" |> render
```

For running the parse inside a workflow — including workflow-specific acceptance rules — see
[Rules And Policies](../rules-and-policies/).

## From C#

Consume-don't-author: F# declares the schema, C# parses and reads diagnostics. Most adapters take plain .NET types
already — `ofNameValueCollection` takes `NameValueCollection`, `ofCliArgs` and `ofJsonLikeValue` take ordinary
sequences and values — and call as plain static methods. `ofMap` and `ofConfiguration` take F#-only types (`Map` and
a sequence of F# tuples), so use their C#-friendly equivalents instead:

```csharp
using Axial.Validation.Schema;

// ofMap's C# equivalent — takes IDictionary<string, string> instead of an F# Map:
RawInput raw = RawInputModule.ofDictionary(new Dictionary<string, string> { ["name"] = "Ada Lovelace" });

// ofConfiguration's C# equivalent — takes the pairs IConfiguration.AsEnumerable() already returns:
RawInput fromConfig = RawInputModule.ofConfigurationPairs(configuration.AsEnumerable());

ParsedInput<Customer, SchemaError> parsed = Input.parse(customerSchema, raw);

if (parsed.IsValid)
{
    Customer customer = parsed.Model;
}
else
{
    var errors = parsed.Errors; // FSharpList<Diagnostic<SchemaError>>
}
```

`Input.parseWith` takes an F# function for its `configure` parameter, which C# cannot pass a lambda to directly. Use
`Input.parseWithOptions`, which takes a `Func<Options, Options>` instead:

```csharp
var parsed = Input.parseWithOptions(o => Input.constructorErrorAt("end", o), dateRangeSchema, raw);
```
