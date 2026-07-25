---
title: For AI agents
description: High-signal Schema and Data guidance for coding agents.
weight: 100
type: docs
---


Use this section for `Axial.Schema` and `Axial.Data`. These packages do not require Flow.

- Start domain models with [`Schema<'model>`]({{< relref "/schema/reference/schema/t-schema-schema/" >}}) and constructor-last declarations.
- Use plain F# `Result` with an application error type for smaller fail-fast operations.
- Declare records with `schema<Model> { field ...; construct ... }`.
- Use an optional field block for `withSchema`, [`constrain`]({{< relref "/schema/reference/schema/m-schema-schema-constrain/" >}}), type-directed [`refine`]({{< relref "/schema/reference/schema/m-schema-schema-refine/" >}}), and [`validate`]({{< relref "/schema/reference/schema/m-schema-schema-validate/" >}}).
- Treat [`Data`]({{< relref "/schema/reference/data/t-data/" >}}), wire records, and editable drafts as untrusted values.
- Use [`Schema.parse`]({{< relref "/schema/reference/schema/interpreters/m-schema-schema-parse/" >}}) at structured input boundaries and [`Schema.check`]({{< relref "/schema/reference/schema/interpreters/m-schema-schema-check/" >}}) for already assembled typed drafts.
- Use private refined fields or private aggregates when later code must rely on an invariant.
- Use [`SchemaErrors.toList`]({{< relref "/schema/reference/schema/interpreters/m-schema-schemaerrors-tolist/" >}}) for complete path-bearing issues and [`SchemaErrors.toString`]({{< relref "/schema/reference/schema/interpreters/m-schema-schemaerrors-tostring/" >}}) for display text.
- Compile `Axial.Schema.Json` codecs once for trusted payloads; use `Data` plus `Schema.parse` for untrusted payloads.
- Keep generated [`[<DeriveSchema>]`]({{< relref "/schema/reference/schema/t-schema-derive-deriveschemaattribute/" >}}) records at the wire tier and map them through a domain constructor.

Platform support is listed in [Packages and platforms]({{< relref "/schema/packages-and-platforms.md" >}}). For compact prompt context, load
[`/schema/llms.txt`](/schema/llms.txt).
