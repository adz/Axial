---
title: "Codec"
weight: 20
type: docs
---

This page shows the `Axial.Schema.Codec` surface: `Json.compile` turns a built `Schema<'model>` into a reusable `JsonCodec<'model>` with compiler-directed, runtime-reflection-free, constructor-specialized encode and decode plans. The codec is the trusted hot path for serialization; parse untrusted boundary input with [schema input parsing](../schema/interpreters/) when path-aware diagnostics matter.

## Core types

- [`Schema.Codec.JsonCodec`](./t-schema-codec-jsoncodec.md): A compiled JSON codec for one schema-described model.
- [`Schema.Codec.JsonCodecException`](./t-schema-codec-jsoncodecexception.md): The exception raised when JSON text cannot be decoded through a compiled schema codec.

## Module functions

- [`Schema.Codec.Json.compile`](./m-schema-codec-json-compile.md): Compiles a completed schema into a reusable JSON codec.
- [`Schema.Codec.Json.serialize`](./m-schema-codec-json-serialize.md): Serializes a trusted model to a JSON string through a compiled codec.
- [`Schema.Codec.Json.serializeBytes`](./m-schema-codec-json-serializebytes.md): Serializes a trusted model to UTF-8 JSON bytes through a compiled codec.
- [`Schema.Codec.Json.serializeToStream`](./m-schema-codec-json-serializetostream.md): Serializes a trusted model as UTF-8 JSON directly to a stream through a compiled codec, flushing once when complete.
- [`Schema.Codec.Json.deserialize`](./m-schema-codec-json-deserialize.md): Deserializes a JSON string to a trusted model through a compiled codec.
- [`Schema.Codec.Json.deserializeBytes`](./m-schema-codec-json-deserializebytes.md): Deserializes UTF-8 JSON bytes to a trusted model through a compiled codec.
- [`Schema.Codec.Json.deserializeStreamAsync`](./m-schema-codec-json-deserializestreamasync.md): Reads a stream to end into a pooled buffer, then deserializes it as UTF-8 JSON through a compiled codec.
- [`Schema.Codec.Json.tryDeserialize`](./m-schema-codec-json-trydeserialize.md): Deserializes a JSON string, returning decode failures as a rendered message instead of raising.
