---
weight: 25
title: Input Sources
type: docs
description: HTTP form-like, CLI, JSON-like, and configuration input through one schema.
---


[`Data`]({{< relref "/schema/data/" >}}) is the source-agnostic boundary shape: `Null`, `Text`, `Number`, `Bool`, `List`, and
`Object`. Adapters turn common sources into `Data`, and one schema parses them all. (`Data` is its own dependency-free
package, useful beyond schemas — see [its docs]({{< relref "/schema/data/" >}}).)

## The Schema

```fsharp
open type Axial.Schema.Syntax
type Contact = { Kind: string; Value: string }

type Customer =
    { Name: string
      Address: Address
      Contacts: Contact list }

let customerSchema =
    schema<Customer> {
        field "name" _.Name
        field "address" _.Address {
            withSchema addressSchema
        }
        field "contacts" _.Contacts {
            withSchema (Schema.listWith contactSchema)
            constrain (Constraint.minCount 1)
        }
        construct (fun name address contacts ->
            { Name = name; Address = address; Contacts = contacts })
    }
```

Here `addressSchema` and `contactSchema` are intentionally local value schemas. If `Address` and `Contact` declare
canonical intrinsic schemas, both lines can use ordinary `field` and the contact list resolves recursively.

Nested fields expect object-shaped input and prefix their diagnostics with the field name; collection fields expect
`Data.List`, parse every item, accumulate every item error, and prefix diagnostics with the item index.

## HTTP Form-Like Input

Form posts and query strings are name/value pairs; repeated names become `Data.List` values:

```fsharp
let raw =
    Data.ofNameValues
        [ "name", "Ada Lovelace"
          "tag", "vip"
          "tag", "beta" ]      // repeated names accumulate into Data.List
```

`Data.ofMap` handles single-valued maps, and `Data.ofNameValueCollection` adapts
`System.Collections.Specialized.NameValueCollection` directly from ASP.NET-style APIs. Name/value sources are flat; use
the configuration or JSON adapters below when the input carries nested models or indexed collections.

## CLI Arguments

```fsharp
let raw = Data.ofCliArgs [ "--name"; "Ada Lovelace"; "--verbose"; "--no-color" ]
```

`--name value`, `--name=value`, `-n value`, boolean flags, `--no-name`, and repeated options are supported; positional
arguments collect under the `_` field.

## JSON Bodies With System.Text.Json

On .NET 8+ targets, adapt a parsed `JsonDocument` or `JsonElement` directly — the natural fit for ASP.NET Core
request bodies:

```fsharp
use! document = JsonDocument.ParseAsync request.Body
let raw = Data.ofJsonDocument document
```

JSON null, numbers, Booleans, arrays, and objects retain their corresponding `Data` cases. Number tokens keep their
exact lexical representation. The adapter uses the in-box `System.Text.Json`, so the package stays dependency-free.

## Other JSON libraries

On other targets (including Fable), deserialize with any JSON library directly into `Data`:

```fsharp
let data = jsonValue // already a Data value
```

Nested `Data.List` and `Data.Object` values parse with no extra shaping.

## Configuration

Configuration keys use `:`-separated sections and numeric segments for collection indexes:

```fsharp
let raw =
    Data.ofConfiguration
        [ "name", "Ada Lovelace"
          "address:city", "London"
          "contacts:0:kind", "email"
          "contacts:0:value", "ada@example.com" ]
```

Later pairs override earlier ones at the same path, matching .NET configuration layering: a repeated key keeps its
last value, and a later scalar or section replaces the earlier shape at that key. Collections come from numeric
segments, never from repetition — repeated names as multi-value input is a wire convention that belongs to
`ofNameValues`. Section keys with null values, as `IConfiguration.AsEnumerable()` emits alongside a section's
children, never override those children, so real layered `IConfiguration` output round-trips directly.

## One Parse For All Of Them

```fsharp
let parsed = Schema.parseRetainingInput customerSchema raw

match parsed.Result with
| Ok customer -> customer
| Error diagnostics ->
    // same paths for every source:
    parsed.ErrorsFor "contacts[0].value" |> render
```

## From C#

Consume-don't-author: F# declares the schema, C# parses and reads diagnostics. Most adapters take plain .NET types
already — `ofNameValueCollection` takes `NameValueCollection`, `ofCliArgs` and `ofData` take ordinary
sequences and values — and call as plain static methods. `ofMap` and `ofConfiguration` take F#-only types (`Map` and
a sequence of F# tuples), so use their C#-friendly equivalents instead:

```csharp
using Axial.Schema;

// ofMap's C# equivalent — takes IDictionary<string, string> instead of an F# Map:
Data raw = DataModule.ofDictionary(new Dictionary<string, string> { ["name"] = "Ada Lovelace" });

// ofConfiguration's C# equivalent — takes the pairs IConfiguration.AsEnumerable() already returns:
Data fromConfig = DataModule.ofConfigurationPairs(configuration.AsEnumerable());

RetainedParseResult<Customer, SchemaError> parsed = Schema.parseRetainingInput(customerSchema, raw);

if (parsed.IsValid)
{
    Customer customer = parsed.Value;
}
else
{
    var errors = parsed.Errors; // FSharpList<Diagnostic<SchemaError>>
}
```

`Schema.parseWith` takes an F# function for its `configure` parameter, which C# cannot pass a lambda to directly. Use
`Schema.parseWithOptions`, which takes a `Func<Options, Options>` instead:

```csharp
var parsed = Schema.parseWithOptions(o => Schema.constructorErrorAt("end", o), dateRangeSchema, raw);
```
