---
weight: 55
title: JSON Codec
description: Compile a schema into a runtime-reflection-free JSON codec for trusted hot-path serialization.
---

# JSON Codec

This page shows how `Axial.Schema.Codec` turns the schema you already declared into a compiled JSON codec, so trusted
serialization and boundary parsing come from one declaration.

Axial has two paths for JSON, and they exist because they optimize for different things:

- **Boundary parsing** — `Data` + `Schema.parse`: for untrusted input. It runs constraint metadata, accumulates
  path-aware diagnostics, and keeps the structured data for redisplay.
- **Trusted path** — `Json.compile` + `Json.serialize`/`Json.deserialize`: for payloads whose producer you trust, such
  as internal services, storage, caches, and queues. It enforces the wire shape and required fields, skips constraint
  checking, and runs about 6x faster with a fraction of the allocations (see the
  [benchmarks]({{< relref "/schema/benchmarks.md#schema-json-codec" >}})).

## Compile Once, Reuse Everywhere

```fsharp
open Axial.Schema
open Axial.Schema.Codec
open Axial.Schema.Syntax

type Address =
    { Street: string; City: string }

    static member Schema(_: Address) : Schema<Address> =
        Schema.define<Address>
        |> field "street" _.Street
        |> field "city" _.City
        |> construct (fun street city -> { Street = street; City = city })

type Customer =
    { Name: string
      Age: int
      Address: Address }

let customerSchema =
    Schema.define<Customer>
    |> field "name" _.Name
    |> field "age" _.Age
    |> field "address" _.Address
    |> construct (fun name age address -> { Name = name; Age = age; Address = address })

let codec = Json.compile customerSchema   // compile once, typically at startup

let json = Json.serialize codec { Name = "Ada"; Age = 36; Address = { Street = "12 Analytical Way"; City = "London" } }
// {"name":"Ada","age":36,"address":{"street":"12 Analytical Way","city":"London"}}

let customer = Json.deserialize codec json
```

`Json.compile` walks the typed record plan retained when the object shape closes and emits a direct plan: ordered field
descriptors, cached UTF-8 wire-name bytes, typed field decoders, and the original curried constructor applied without
boxing. Everything is compiler-directed: there is no runtime reflection at codec-compile time or per value, so the codec is AOT- and trimming-safe by construction.

## Every Schema Shape Is Supported

Refined values encode as their raw representation and are reconstructed on decode; nested models, collections, and
tagged unions follow the same wire shapes the input parser reads:

```fsharp
// A union field {"type":"card","value":{...}} round-trips through the same discriminator convention.
let orderCodec = Json.compile orderSchema
```

## Decode Failures Carry Paths

Decoding trusted input can still meet malformed payloads. Failures raise `JsonCodecException` with a schema-relative
path, or use `tryDeserialize` for a `Result`:

```fsharp
match Json.tryDeserialize codec """{"name":"Ada","age":"not-a-number"}""" with
| Ok customer -> customer
| Error message -> failwith message   // JSON decode failed at $.age: expected digit
```

The codec reports the first structural failure and stops. When you need every problem reported with redisplayable
input — a form, a public API — that is boundary parsing's job:

```fsharp
// Boundary parsing: complete diagnostics for untrusted input.
let parsed = Schema.parse customerSchema (Data.ofJsonDocument document)
```

## Bytes In, Bytes Out

`Json.serializeBytes` and `Json.deserializeBytes` avoid the string conversion when the payload already lives as UTF-8
bytes, which is the faster path for network and storage boundaries:

```fsharp
let bytes = Json.serializeBytes codec customer
let roundTripped = Json.deserializeBytes codec bytes
```

## What The Codec Does Not Do

- It does not run constraint metadata such as `maxLength` or `between` — those belong to boundary parsing and
  validation. A value that only ever passes through trusted systems does not pay for checks it already passed.
- Checked constructors from `constructResult` still run, so intrinsic cross-field invariants hold on the trusted path;
  their errors surface as `JsonCodecException`.

## From C#

Consume-don't-author: F# declares the schema, C# compiles the codec, parses, and reads diagnostics. Every `Json.*`
function takes plain positional arguments, so it calls as an ordinary static method with no `FSharpFunc` conversion:

```csharp
using Axial.Schema;
using Axial.Schema.Codec;

JsonCodec<Customer> codec = Json.compile(customerSchema);

string json = Json.serialize(codec, customer);
Customer roundTripped = Json.deserialize(codec, json);

// Failures raise JsonCodecException instead of a Result, or use tryDeserialize:
var attempt = Json.tryDeserialize(codec, json); // FSharpResult<Customer, string>
```

`serializeToStream` and `deserializeStreamAsync` (both `async`/`Task`-based) are also plain static calls, so they work
directly against `HttpContext.Response.Body` / `Request.Body` in an ASP.NET Core handler.

## Next

- Serve the same declaration as a contract with [`JsonSchema.generate`]({{< relref "/reference/schema" >}}).
- See the two paths together in the runnable
  [minimal API sample]({{< relref "/schema/examples.md#minimal-api-boundary-example" >}}).
