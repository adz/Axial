---
title: "Codec"
weight: 20
type: docs
---

This page shows the `Axial.Schema.Json` surface: `Json.compile` turns a built `Schema<'model>` into a reusable `JsonCodec<'model>` with compiler-directed, runtime-reflection-free, constructor-specialized encode and decode plans. The codec is the trusted hot path for serialization; parse untrusted boundary input with [schema input parsing](../schema/interpreters/) when path-aware diagnostics matter.

## Core types

- [`Schema.Json.JsonCodec`](./t-schema-json-jsoncodec.md): A compiled JSON codec for one schema-described model.
- [`Schema.Json.JsonCodecException`](./t-schema-json-jsoncodecexception.md): The exception raised when JSON text cannot be decoded through a compiled schema codec.

## Module functions

- [`Schema.Json.compile`](./m-schema-json-json-compile.md): Compiles a completed schema into a reusable JSON codec.
- [`Schema.Json.serialize`](./m-schema-json-json-serialize.md): Serializes a trusted model to a JSON string through a compiled codec.
- [`Schema.Json.serializeBytes`](./m-schema-json-json-serializebytes.md): Serializes a trusted model to UTF-8 JSON bytes through a compiled codec.
- [`Schema.Json.serializeToStream`](./m-schema-json-json-serializetostream.md): Serializes a trusted model as UTF-8 JSON directly to a stream through a compiled codec, flushing once when complete.
- [`Schema.Json.deserialize`](./m-schema-json-json-deserialize.md): Deserializes a JSON string to a trusted model through a compiled codec.
- [`Schema.Json.deserializeBytes`](./m-schema-json-json-deserializebytes.md): Deserializes UTF-8 JSON bytes to a trusted model through a compiled codec.
- [`Schema.Json.deserializeStreamAsync`](./m-schema-json-json-deserializestreamasync.md): Reads a stream to end into a pooled buffer, then deserializes it as UTF-8 JSON through a compiled codec.
- [`Schema.Json.tryDeserialize`](./m-schema-json-json-trydeserialize.md): Deserializes a JSON string, returning decode failures as a rendered message instead of raising.
