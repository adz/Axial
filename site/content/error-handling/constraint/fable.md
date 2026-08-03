---
weight: 50
title: Fable support
type: docs
description: What constraint rendering does, and deliberately does not do, under Fable.
---


`Axial.Constraint` compiles to JavaScript through Fable, including the rendering edge. Constraints, violations,
descriptors, the catalogue, and `Renderer` all work there. What differs is culture: the browser has no .NET
resource manager and no `CultureInfo` machinery, so the constructors that depend on them are absent rather than
present-and-useless.

## The portable renderer

`Renderer.ofLookup` is the constructor to use. It takes any key-to-template function:

```fsharp
open Axial.Constraint

let translations =
    Map [ "constraint.presence.present", "doit être renseigné"
          "attribute.signup.name", "Le nom" ]

let renderer =
    Renderer.ofLookup translations.TryFind
    |> Renderer.context "signup"

violation |> Violation.fullMessage (renderer |> Renderer.attribute "name")
// "Le nom doit être renseigné"
```

A `Map`, an object fetched as JSON, or a function reaching into an existing i18n library all satisfy
`MessageLookup`. Axial owns the candidate order, so a lookup only ever answers "do you have this exact key".

Everything the [Localization](../localization/) page describes about candidate order, contextual fallback,
`.one`/`.other` selection, named interpolation, attribute nouns, humanization, segment encoding, and group and list
joining behaves identically under Fable. So does `Renderer.english`, `Renderer.Advanced.ofResolver`, and the whole
inspection surface.

## What is absent

These three constructors do not exist in a Fable build:

```fsharp
Renderer.ofResourceManager
Renderer.ofResourceManagerWithCultures
Renderer.ofCurrentCulture
```

Calling one is a compile error under Fable, which is the point. A .NET resource constructor that compiled to a
silent no-op would produce untranslated English in production with nothing to catch it.

If a codebase is shared between a .NET host and a Fable client, put the constructor behind the one conditional you
already have:

```fsharp
let renderer =
#if FABLE_COMPILER
    Renderer.ofLookup translations.TryFind
#else
    Renderer.ofCurrentCulture resources
#endif
```

Nothing else in the localization path needs conditioning. `Violation.message`, `Violation.fullMessage`,
`Renderer.context`, `Renderer.attribute`, and `SchemaErrors.fullMessages` are the same calls on both targets.

## Operand formatting

The built-in formatter under Fable renders operands with the portable invariant rendering and **ignores placeholder
format suffixes**. `{divisor:N0}` renders as though it were `{divisor}`.

Placeholder *parsing* is identical on every target — the suffix is recognized and stripped, not left in the output —
but there is no culture to format through, and inventing one would give the browser and the server two different
numbers for the same constraint.

Supply your own formatter when the suffix matters. `Intl.NumberFormat` is usually what you want:

```fsharp
let renderer =
    Renderer.ofLookup translations.TryFind
    |> Renderer.Advanced.withValueFormatting (fun request ->
        match request.Value, request.Format with
        | ConstraintValue.Integer value, Some _ -> formatNumber (float value)
        | value, _ -> ConstraintValue.render value)
```

Use `Renderer.withValues` instead when one uniform rendering is enough and suffixes are irrelevant.

## Operand agreement across targets

A constraint must mean the same thing on both runtimes, and its message must too. Two behaviours matter here.

Fable erases a `Guid` to a string and a `TimeSpan` to a number, so a boxed type test labels them `Text` and
`Integer` there while .NET labels them correctly. Axial's constructors resolve the operand at the call site, where
the type is still concrete, so the same constraint describes itself identically — and therefore interpolates
identically — on both targets. This is checked by the shared Fable surface test, not assumed.

Blankness and text length are the other pair. Text sizes count Unicode code points rather than UTF-16 code units,
and whitespace is defined the same way on both runtimes, so `constraint.cardinality.between` reports the same
`{actual}` for the same string in the browser and on the server.

## Descriptors and comparison

`MessageDescriptor` keeps structural equality under Fable, so violations carrying one compare equal across
independently constructed values:

```fsharp
MessageDescriptor.Advanced.create "books.isbn.invalid" Map.empty
    = MessageDescriptor.Advanced.ofSegments [ "books"; "isbn"; "invalid" ] Map.empty
// true, on both targets
```

That is what lets a Fable client compare a violation it received against one it computed, and what lets a test
assert on a whole violation value rather than on its rendered text.

## Shipping the translations

The lookup is a plain function, so how the bundle arrives is entirely yours. Two shapes work well:

```fsharp
// Bundled at build time. One Map per language, selected once.
let renderer = Renderer.ofLookup (catalogue culture).TryFind

// Fetched at runtime. Build the renderer after the fetch resolves; it is an immutable value,
// so there is no registry to update.
let renderer = Renderer.ofLookup (fun key -> loaded |> Map.tryFind key)
```

Generate the bundle from `Catalogue.keys` and `SchemaMessages.keys` rather than transcribing it — see
[Adding a language](../adding-a-language/). Since the same catalogue drives both targets, one generated bundle
serves the browser and the server.

## AOT and trimming

The rendering path uses no runtime reflection. Message identities are generated data, plural selection is a value
comparison, and interpolation is string parsing, so a NativeAOT or trimmed .NET build has nothing to preserve
beyond the resources themselves. See [AOT, trimming, and Fable]({{< relref "/schema/aot-trimming-fable/" >}}) for
the wider picture across packages.
