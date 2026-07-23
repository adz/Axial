---
title: For AI agents
description: High-signal Schema and Data guidance for coding agents.
weight: 100
---

# For AI agents

Use this section for `Axial.Schema` and `Axial.Data`. These packages do not require Flow.

- Start domain models with `Schema<'model>` and constructor-last declarations.
- Use plain F# `Result` with an application error type for smaller fail-fast operations.
- Declare records with `schema<Model> { field ...; construct ... }`.
- Use an optional field block for `withSchema`, `constrain`, type-directed `refine`, and `validate`.
- Treat `Data`, wire records, and editable drafts as untrusted values.
- Use `Schema.parse` at structured input boundaries and `Schema.check` for already assembled typed drafts.
- Use private refined fields or private aggregates when later code must rely on an invariant.
- Use `SchemaErrors.toList` for complete path-bearing issues and `SchemaErrors.toString` for display text.
- Compile `Axial.Schema.Json` codecs once for trusted payloads; use `Data` plus `Schema.parse` for untrusted payloads.
- Keep generated `[<DeriveSchema>]` records at the wire tier and map them through a domain constructor.

Platform support is listed in [Packages and platforms]({{< relref "/schema/packages-and-platforms.md" >}}). For compact prompt context, load
[`/schema/llms.txt`](/schema/llms.txt).
