---
weight: 55
title: JSON Codec
description: Compile a schema into a reflection-free JSON codec for trusted hot-path serialization.
type: docs
---


This page shows how `Axial.Codec` turns the schema you already declared into a compiled JSON codec, so trusted
serialization and boundary parsing come from one declaration.

Axial has two lanes for JSON, and they exist because they optimize for different things:

- **Boundary lane** — `RawInput` + `Input.parse`: for untrusted input. It runs constraint metadata, accumulates
  path-aware diagnostics, and keeps the raw input for redisplay.
- **Trusted lane** — `Json.compile` + `Json.serialize`/`Json.deserialize`: for payloads whose producer you trust, such
  as internal services, storage, caches, and queues. It enforces the wire shape and required fields, skips constraint
  checking, and runs about 6x faster with a fraction of the allocations (see the
  [benchmarks]({{< relref "/patterns/benchmarks.md#schema-json-codec" >}})).

## Compile Once, Reuse Everywhere

```fsharp
open Axial.Schema
open Axial.Codec

type Address = { Street: string; City: string }

type Customer =
    { Name: string
      Age: int
      Address: Address }

let addressSchema =
    Schema.recordFor<Address, _> (fun street city -> { Street = street; City = city })
    |> Schema.text "street" _.Street
    |> Schema.text "city" _.City
    |> Schema.build

let customerSchema =
    Schema.recordFor<Customer, _> (fun name age address -> { Name = name; Age = age; Address = address })
    |> Schema.text "name" _.Name
    |> Schema.int "age" _.Age
    |> Schema.nested "address" _.Address addressSchema
    |> Schema.build

let codec = Json.compile customerSchema   // compile once, typically at startup

let json = Json.serialize codec { Name = "Ada"; Age = 36; Address = { Street = "12 Analytical Way"; City = "London" } }
// {"name":"Ada","age":36,"address":{"street":"12 Analytical Way","city":"London"}}

let customer = Json.deserialize codec json
```

`Json.compile` walks the typed field chain that `Schema.build` retains and emits a direct record plan: ordered field
descriptors, cached UTF-8 wire-name bytes, typed field decoders, and the original curried constructor applied without
boxing. There is no reflection at compile time or per value, so the codec is AOT- and trimming-safe by construction.

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
input — a form, a public API — that is the boundary lane's job:

```fsharp
// Boundary lane: complete diagnostics for untrusted input.
let parsed = Input.parse customerSchema (RawInput.ofJsonDocument document)
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
- Constructors from `Schema.buildResult` still run, so intrinsic cross-field invariants hold even on the trusted lane;
  their errors surface as `JsonCodecException`.

## Next

- Serve the same declaration as a contract with [`JsonSchema.generate`]({{< relref "/reference/schema" >}}).
- See the two lanes together in the runnable
  [minimal API sample]({{< relref "/patterns/examples#minimal-api-boundary-example" >}}).
