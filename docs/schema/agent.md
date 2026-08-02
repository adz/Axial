---
title: For AI agents
description: High-signal Schema and Data guidance for coding agents.
weight: 100
---

# For AI agents

Use this section for `Axial.Schema` and `Axial.Data`. These packages do not require Flow.

For source-neutral fixtures and produced output, prefer `Axial.Data.Syntax`: construct with `data`, derive values with
`patch`, `variants`, or `matrix`, and prove output with `matching`. Use `Data.compare` when the complete tree is the
contract and partial patterns when only selected evidence matters.

Use `Data.render` for human-readable diagnostics and `Data.Json.render` for JSON text. Parse JSON portably with
`Axial.Schema.Json.Json.parseData`; use `Data.ofJsonElement`/`ofJsonDocument` for existing .NET 8+ JSON DOM values or
`Data.ofJsonValue` for a native Fable `JSON.parse` result.

- Start domain models with `Schema<'model>` and constructor-last declarations.
- Use plain F# `Result` with an application error type for smaller fail-fast operations.
- Declare records with `schema<Model> { field ...; construct ... }`.
- Use an optional field block for `withSchema`, `constrain`, `refine`, and `validate`.
- Prefer `refine Type.refinement` when selection should be explicit. Use bare `refine` for the destination type's single canonical static refinement contribution.
- Treat `Data`, wire records, and editable drafts as untrusted values.
- Use `Schema.parse` at structured input boundaries and `Schema.check` for already assembled typed drafts.
- Use private refined fields or private aggregates when later code must rely on an invariant.
- Use `SchemaErrors.toList` for complete path-bearing issues and `SchemaErrors.toString` for display text.
- Compile `Axial.Schema.Json` codecs once for trusted payloads; use `Data` plus `Schema.parse` for untrusted payloads.
- Keep generated `[<DeriveSchema>]` records at the wire tier and map them through a domain constructor.

Platform support is listed in [Packages and platforms]({{< relref "/schema/packages-and-platforms.md" >}}). For compact prompt context, load
[`/schema/llms.txt`](/schema/llms.txt).
