---
title: Reference
type: docs
notoc: true
weight: 100
---

API reference for the Schema area, grouped by package and module.

## Axial.Schema

- [`Schema`](./schema/) — the portable model declaration: builders, fields, constraints, and metadata inspection.

Everything above and below shares the single `Axial.Schema` namespace — the module name, not the namespace, is what
separates declaration from interpreter.

## Interpreters

Schemas are consumed by interpreters that stay independent of workflow execution and diagnostics:

- [`Schema Interpreters`](./schema/interpreters/) — the boundary-parsing and rule-evaluation surface:
  - [`Data`](./schema/interpreters/#raw-input) — source-agnostic structured data captured at a data boundary.
  - [`Schema.parse` / `Schema.parseRetainingInput`](./schema/interpreters/#input-parsing) — parsing boundary input through
    a schema, with `RetainedParseResult` for redisplay, plus `Schema.check` for giving an already-existing value (a draft
    or an import) the same trust guarantee.
  - [`SchemaError`](./schema/interpreters/#errors) — schema input, checking, and rule failures.
  - [`RefinedSchemas`](./schema/interpreters/#refined-catalog-schemas) — bridges `Axial.Refined` types (see the
    [Validation reference]({{< relref "/error-handling/reference/" >}})) into schema field declarations.

## Axial.Schema.JsonSchema

- [`JsonSchema`](./schema/#json-schema-generation) — generates a JSON Schema document from a built schema's
  metadata. The module remains in the `Axial.Schema` namespace, but installation requires the separate
  `Axial.Schema.JsonSchema` package.

## Axial.Schema.Http

- [`Schema HTTP Boundary`](./schema/http/) — host-neutral server boundary support (namespace `Axial.Schema.Http`):
  `BoundaryInput` for query and form structured data, `ProblemDetails` for RFC 9457 error bodies with JSON-pointer paths,
  and `EndpointSpec`/`OpenApi` for assembling OpenAPI 3.1 documents.
- [`ASP.NET Core adapter`](./schema/http/aspnetcore/) — schema-trusted request operations, application Flow embedding,
  successful responses, native handler lowering, and the lower-level `SchemaRequest`/`SchemaResult` surface.
- [`GenHTTP adapter`](./schema/http/genhttp/) — the equivalent Flow endpoint boundary and lower-level host surface for
  GenHTTP's request-relative response model.

See the [HTTP servers guide]({{< relref "/schema/http-servers/" >}}) for the complete authoring model and outcome rules.

## Axial.Schema.Json

- [`Codec`](./codec/) — compiled JSON codecs from the same declaration (namespace `Axial.Schema.Json`). Optional:
  install it alongside `Axial.Schema` only when you need the compiled hot path.

`Axial.Refined`, `Validation`, and `Diagnostics` arrive through the focused `Axial.Refined` and
`Axial.Diagnostics` dependencies. See the
[Validation reference]({{< relref "/error-handling/reference/" >}}).
