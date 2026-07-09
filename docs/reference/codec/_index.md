---
title: "Codec"
weight: 500
---

This page shows the `Axial.Codec` surface: `Json.compile` turns a built `Schema<'model>` into a reusable `JsonCodec<'model>` with reflection-free, constructor-specialized encode and decode plans. The codec is the trusted hot path for serialization; parse untrusted boundary input with [schema input parsing](../schema/interpreters/) when path-aware diagnostics matter.

## Core types

- [`Codec.JsonCodec`](./t-codec-jsoncodec.md): A compiled JSON codec for one schema-described model.
- [`Codec.JsonCodecException`](./t-codec-jsoncodecexception.md): The exception raised when JSON text cannot be decoded through a compiled schema codec.

## Module functions

- [`Codec.Json.compile`](./m-codec-json-compile.md): Compiles a built model schema into a reusable JSON codec.
- [`Codec.Json.serialize`](./m-codec-json-serialize.md): Serializes a trusted model to a JSON string through a compiled codec.
- [`Codec.Json.serializeBytes`](./m-codec-json-serializebytes.md): Serializes a trusted model to UTF-8 JSON bytes through a compiled codec.
- [`Codec.Json.serializeToStream`](./m-codec-json-serializetostream.md): Serializes a trusted model as UTF-8 JSON directly to a stream through a compiled codec, flushing once when complete.
- [`Codec.Json.deserialize`](./m-codec-json-deserialize.md): Deserializes a JSON string to a trusted model through a compiled codec.
- [`Codec.Json.deserializeBytes`](./m-codec-json-deserializebytes.md): Deserializes UTF-8 JSON bytes to a trusted model through a compiled codec.
- [`Codec.Json.deserializeStreamAsync`](./m-codec-json-deserializestreamasync.md): Reads a stream to end into a pooled buffer, then deserializes it as UTF-8 JSON through a compiled codec.
- [`Codec.Json.tryDeserialize`](./m-codec-json-trydeserialize.md): Deserializes a JSON string, returning decode failures as a rendered message instead of raising.
