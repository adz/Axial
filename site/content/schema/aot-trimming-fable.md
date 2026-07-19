---
weight: 82
title: Zero Reflection, AOT, and Fable
description: Why Axial works under NativeAOT, aggressive trimming, and Fable by construction.
type: docs
---


This page explains Axial's runtime-portability guarantees and what makes them hold.

Axial contains no runtime reflection in any core path. That is not an optimization applied afterwards — it is an
architectural rule: schemas, constructors, getters, checks, codecs, and service access are all explicit declarations
that the compiler can see, so there is nothing for the trimmer to remove by mistake and nothing NativeAOT cannot
compile ahead of time.

## Why It Holds By Construction

- **Schemas declare construction explicitly.** `Schema.define<Customer> |> field ... |> construct ctor`
  captures the real constructor and typed getters as values. There is no property discovery, no attribute scanning,
  and no `Activator.CreateInstance`.
- **Codecs compile from the typed shape.** `Json.compile` turns the schema's retained typed constructor and fields
  into encode/decode plans — cached wire-name bytes and typed field decoders — where `System.Text.Json`'s default path
  builds converters through reflection and asks you to switch to source generators for AOT. Axial has nothing to
  switch: the explicit path is the only path.
- **Refined values are functions, not conventions.** `Schema.convert construct inspect` carries the conversion in both
  directions as ordinary closures.
- **Services are explicit.** Dependencies live in `'env` records or nominal `IHas<'service>` contracts; there is no
  runtime service map or proxy generation.

The one deliberate exception: `Service<'service>.resolve()` can look up registrations from an
`IServiceProvider` at .NET host edges. That is host integration you opt into at the boundary, not a core mechanism.

## Verified In CI

Every push publishes and runs a NativeAOT probe (`bash scripts/run-aot-probe.sh`), which compiles an application
exercising flows, schemas, parsing, and services with `PublishAot=true` and executes the native binary. If a change
introduced reflection the trimmer could not prove safe, CI fails.

## Fable

The same explicitness is what makes Fable compilation work: `Axial.Flow`, `Axial.ErrorHandling` (including
`Validation`), and `Axial.Schema` (including `Refined`) all compile to JavaScript, so a browser front end can parse
and redisplay through the same schema declaration the server uses. CI compiles the Fable JavaScript surface
(`bash scripts/check-fable-js-surface.sh`), and the [benchmarks]({{< relref "/schema/benchmarks.md" >}}) include
Node and Erlang runs of the same workloads. .NET-only conveniences — such as `RawInput.ofJsonDocument` and the
`DateOnly` field type — are compile-time gated so the Fable surface never references them.

## What This Buys You

- `PublishAot=true` and `PublishTrimmed=true` work without `DynamicDependency` annotations, trimmer XML, or source
  generators.
- Startup does not pay for converter caches or expression-tree compilation; codecs and parsers are compiled once from
  explicit declarations.
- The same domain model and boundary declaration can serve .NET services, native binaries, and Fable clients.
