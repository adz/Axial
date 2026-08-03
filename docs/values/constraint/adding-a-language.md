---
weight: 45
title: Adding a language
type: docs
description: Generating, filling, and proving a new translation of the constraint catalogue.
---

# Adding a language

Adding a language means supplying entries for keys Axial already publishes. The catalogue is available at runtime,
so the resource file, the coverage test, and this page never have to be kept in sync by hand.

The order below is the one that pays off: get the base catalogue rendering in the new language first, then
override the handful of fields whose wording actually differs.

## 1. Generate the skeleton

`Catalogue.keys` is every key Axial can produce, including the composition and joining entries.
`Catalogue.english` and `Catalogue.pluralArgument` give the starting text and which entries take `.one`/`.other`.

A short script writes a starting file for a translator:

```fsharp
open Axial.Constraint

let skeleton () =
    Catalogue.keys
    |> List.collect (fun key ->
        let english = Catalogue.english[key]

        match Catalogue.pluralArgument[key] with
        // An entry that declares an operand gets both forms; the translator deletes one if the language
        // needs only a single form.
        | Some _ -> [ $"{key}.one = {english}"; $"{key}.other = {english}" ]
        | None -> [ $"{key} = {english}" ])
    |> String.concat "\n"
```

Do the same with `SchemaMessages.keys` if the application parses boundary input:

```fsharp
open Axial.Schema

SchemaMessages.keys |> List.map (fun key -> $"{key} = {SchemaMessages.english[key]}")
```

Nothing in this step is Axial-specific beyond the three maps. Emit `.resx`, JSON, `.po`, or whatever your
localization pipeline already reads.

## 2. Translate the predicates

Entries are bare predicates. Write them as fragments that a noun can precede, not as complete sentences:

```text
constraint.presence.present    = doit être renseigné
constraint.relation.atLeast    = doit être au moins {expected}
constraint.cardinality.between = doit avoir une taille comprise entre {minimum} et {maximum}
```

Placeholder names are fixed by the catalogue; the table on the [Localization](../localization/) page lists them per
key. An unknown name renders literally rather than throwing, so a typo shows up in the message.

## 3. Translate the composition entries

These four decide sentence shape, and they are where most of a language's character lives:

```text
constraint.attribute.default = valeur
constraint.actual            = {message}, mais était {actual}
constraint.fullMessage       = {attribute} {message}
```

Reorder them freely. A language that puts the actual value first, or the noun last, changes only these entries —
not the twenty-five predicates:

```text
constraint.actual      = reçu {actual} au lieu de « {message} »
constraint.fullMessage = {message} — {attribute}
```

`{message}` and `{attribute}` hold text Axial has already rendered. They are substituted as-is and never
re-interpolated, so braces inside them stay literal.

## 4. Translate the joining patterns

Groups and lists join through `pair`, `start`, `middle`, and `end`:

```text
constraint.group.all.pair   = {first} et {second}
constraint.group.all.start  = {first}, {rest}
constraint.group.all.middle = {first}, {rest}
constraint.group.all.end    = {first} et {second}

constraint.group.any.pair   = {first} ou {second}
constraint.group.any.end    = {first} ou {second}
```

Three or more items combine the last two with `end`, fold the preceding items right-to-left with `middle`, and
apply `start` to the first. Two items use `pair`. One item is rendered alone with no lookup at all, so there is no
pattern to write for the singular case.

If your language's joining cannot be expressed this way — the conjunction changes the words of its members, or the
group has to be reordered as a whole — patterns are the wrong tool. Project `Violation.toMessageTree` and own the
traversal:

```fsharp
let rec render tree =
    match tree with
    | MessageTree.Leaf (MessageLeaf.Localized descriptor) -> lookup descriptor
    | MessageTree.Leaf (MessageLeaf.Verbatim prose) -> prose
    | MessageTree.All (first, rest) -> yourConjunction (render first) (List.map render rest)
    | MessageTree.Any (first, rest) -> yourDisjunction (render first) (List.map render rest)
```

That is a required path for those languages, not a fallback for a pattern you have not found yet.

## 5. Name the fields

Attribute nouns live under their own prefix and resolve from most to least specific:

```text
attribute.signup.address.postcode = Le code postal de facturation
attribute.address.postcode        = Le code postal
attribute.postcode                = Le code postal
```

Name the ones that matter and let the rest humanize. Humanization only ever applies to a raw segment name — a
resolved resource value is returned byte-for-byte, including its casing and any leading or trailing whitespace the
translator wanted.

## 6. Override only what differs

Everything above is context-free. Add specificity only where the wording genuinely changes:

```text
signup.constraint.presence.present            = est obligatoire pour l'inscription
signup.name.constraint.presence.present       = veuillez indiquer votre nom
```

Lookup removes rightmost specificity one segment at a time, so an override costs one entry and nothing else has to
change. The message identity is never truncated: `books.isbn.invalid` never degrades to `books.isbn`.

## 7. Plurals

Entries that declare an operand accept `.one` and `.other`:

```text
constraint.cardinality.minimum.one   = doit contenir au moins {minimum} élément
constraint.cardinality.minimum.other = doit contenir au moins {minimum} éléments
```

`.one` is selected when the operand is exactly one; `.other` otherwise. The plural key is tried before the bare key
*at the same contextual level*, which means a bare field-specific entry still beats a pluralized model-level one.

Two forms are all ordinary lookup does. A language with more categories — or one where the category depends on more
than the value — takes an advanced resolver:

```fsharp
let renderer =
    Renderer.Advanced.ofResolver (fun request ->
        match request.PluralArgument, icu.TryGet request.BaseKey with
        | Some operand, Some entry ->
            Some (MessageResolution.Rendered (entry.Format(request.Arguments, cldrCategory operand request.Arguments)))
        | _, Some entry -> Some (MessageResolution.Rendered (entry.Format request.Arguments))
        | _, None -> None)
```

Axial keeps contextual fallback and violation composition; the resolver owns category selection and the entry's own
rendering.

## 8. Wire it up

For .NET resources, one renderer at the composition root:

```fsharp
let renderer = Renderer.ofCurrentCulture resources
services.AddSingleton renderer |> ignore
```

For a dictionary, a JSON bundle, or Fable:

```fsharp
let renderer = Renderer.ofLookup translations.TryFind
```

Both are immutable values. Scope them per document and field at the call site, not per request:

```fsharp
let signup = renderer |> Renderer.context "signup"

violation |> Violation.fullMessage (signup |> Renderer.attribute "name")
errors |> SchemaErrors.fullMessages signup
```

## 9. Prove coverage

Base-catalogue coverage is a one-line test:

```fsharp
[<Fact>]
let ``the French catalogue covers every Axial key`` () =
    let missing =
        Catalogue.keys @ SchemaMessages.keys
        |> List.filter (fun key -> not (french.ContainsKey key))

    test <@ missing = [] @>
```

Axial cannot enumerate your contexts and fields — it has never seen them. For contextual coverage, enumerate the
ones you care about and ask the renderer exactly what it will look up:

```fsharp
let candidates context field key =
    let spec =
        MessageDescriptor.Advanced.create key Map.empty
        |> MessageFormatSpec.Advanced.create Catalogue.english[key] None

    let renderer =
        Renderer.ofLookup french.TryFind
        |> Renderer.context context
        |> Renderer.attribute field

    Renderer.Advanced.lookupCandidates renderer spec,
    Renderer.Advanced.attributeCandidates renderer
```

`lookupCandidates` returns the exact encoded keys in order, including the selected `.one`/`.other` key at each
level; `attributeCandidates` returns the noun keys. Assert that at least one of each resolves.

For a pluralized entry, pass an argument map with a representative operand — the selected suffix depends on the
value, so `1` and `3` give you the `.one` and `.other` candidates respectively.

## What a translator never has to do

- reproduce the key list by hand — it is `Catalogue.keys`;
- write an optional-value template — `{actual}` is a separate composition entry;
- repeat the field name in every predicate — the noun composes once, around the finished message;
- handle a missing entry — every key falls back through the contextual chain to neutral English;
- worry about a field named with a dot or a bracket — segments are encoded before joining.
