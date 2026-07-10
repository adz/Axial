---
weight: 92
title: vs zod
description: The same parse-don't-validate philosophy, in F#, with no reflection and Fable support.
type: docs
---


This page maps zod's model onto Axial for readers who know "parse, don't validate" from TypeScript.

Axial's schema group is the same idea zod made mainstream: declare the shape once, parse unknown input through it, and
get either a typed value or structured, path-aware issues. If you like zod, Axial should feel familiar — the
differences come from F# and .NET, not from a different philosophy.

## The Same Moves

| zod | Axial |
| --- | --- |
| `z.object({ name: z.string().max(80) })` | `Schema.recordFor<...> ctor \|> Schema.fieldWith [ SchemaConstraint.maxLength 80 ] "name" _.Name Value.text \|> Schema.build` |
| `schema.safeParse(input)` | `Model.parse schema raw` → `ParsedInput` |
| `result.error.issues` with paths | path-aware `Diagnostics` (`parsed.ErrorsFor "contacts[1].value"`) |
| `z.string().email().brand<'Email'>()` | refined value schemas (`Value.refined` over a private constructor) |
| `z.discriminatedUnion("type", ...)` | `Value.union "type" "value" [ UnionCase.create ... ]` |
| `z.infer<typeof schema>` | not needed — the schema is declared against your record type directly |
| `zod-to-json-schema` | `JsonSchema.generate` (built in, from the same metadata) |

One inversion worth noticing: zod derives the static type from the schema; Axial declares the schema against a type
you own. Your domain type stays an ordinary F# record with real members, and the compiler checks constructor/getter
alignment field by field.

## What Axial Adds Beyond Parsing

- **Trusted-path serialization**: `Json.compile` turns the same declaration into a compiled JSON codec on par with
  `System.Text.Json` — zod validates on the way in but does not own the way out.
- **Redisplay**: failed parses keep the raw input, so form fields re-render with the user's values next to their
  errors without extra state.
- **Contextual rules and workflow policies**: requirements over already-trusted models, applied per workflow.

## Runtime Differences That Matter

- **No reflection**: Axial schemas are explicit declarations compiled into plans; nothing depends on runtime type
  inspection, so NativeAOT and aggressive trimming work by construction.
- **Fable**: the schema core, including `Axial.Codec`, compiles to JavaScript through Fable, so the browser and the
  server can share one declaration — encode and decode included — the role zod plays across the TypeScript stack.
- **Errors are values with rendering**: `SchemaError` is a typed union with a default English renderer
  (`ParsedInput.renderErrors`) and a one-function mapping into your own error union (`ParsedInput.mapErrors`), rather
  than a bag of issue objects.

## Where zod Fits Better

zod is the right tool in TypeScript-first codebases — Axial is not trying to run there. The comparison matters when an
F#/.NET team asks "what is our zod?": the answer is the schema group of Axial, with plain `Result` for code that never
needed a schema at all.
