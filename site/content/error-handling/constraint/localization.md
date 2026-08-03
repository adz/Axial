---
weight: 40
title: Localization
type: docs
description: Translating constraint failures, with the complete key catalogue.
---


A `Violation` carries no language. Built-in failures carry Axial-owned identities and operands; opaque failures
carry the prose their author wrote. Turning either into a sentence happens at the rendering edge, through a
`Renderer`.

Build one renderer at the composition root and reuse it:

```fsharp
open Axial.Constraint

let renderer = Renderer.ofResourceManager resources culture
let signup = renderer |> Renderer.context "signup"

violation
|> Violation.fullMessage (signup |> Renderer.attribute "name")
// "Le nom doit être renseigné"

errors |> SchemaErrors.fullMessages signup
```

Nothing in an ordinary application walks a violation tree, reproduces Axial's key catalogue, or implements
contextual fallback.

The renderer holds the language and the context; the violation holds the facts. That split is why a `Violation`
stays comparable data you can retain, test against, and pass across a boundary without dragging a culture or a
resource manager along with it.

## The two messages

`Violation.message` renders a bare predicate. `Violation.fullMessage` composes the attribute noun around it once.

```fsharp
let field = renderer |> Renderer.context "signup" |> Renderer.attribute "name"

violation |> Violation.message field      // "must be present"
violation |> Violation.fullMessage field  // "Name must be present"
```

Use `message` where a label already names the field — a form row, or a Schema result whose returned path identifies
it. Use `fullMessage` for API payloads, logs, and anywhere the message stands alone.

The noun is composed once, never per leaf, so a group of three failures still names the field once:

```fsharp
let name : Constraint<string> =
    Constraint.all [ Constraint.present; Constraint.lengthBetween 2 40 ]

"" |> Constraint.check name |> Result.mapError (Violation.fullMessage field)
// Error "Name must be present and must have a size between 2 and 40, but was 0"
```

## Building a renderer

Four constructors cover the ordinary cases.

```fsharp
// Any key-to-template lookup. The portable constructor, and the one Fable uses.
Renderer.ofLookup translations.TryFind

// A .NET resource manager and one culture for lookup, plurals, and operand formatting.
Renderer.ofResourceManager resources culture

// Interface language and operand conventions separately: English text, German decimal separators.
Renderer.ofResourceManagerWithCultures resources uiCulture valueCulture

// CurrentUICulture and CurrentCulture, read on every render rather than captured.
Renderer.ofCurrentCulture resources
```

`Renderer.english` needs no resources at all: every entry falls back to Axial's neutral English. It is the default
for tests, tools, and applications that never translate.

`ofCurrentCulture` is the one place ambient culture enters Axial. It reads the thread's cultures per render, so one
renderer registered as a singleton follows a per-request culture. Constraint execution itself stays free of ambient
effects.

Register a renderer as an ordinary immutable value. Axial introduces no renderer interface, no ambient registry,
and no global configuration:

```fsharp
services.AddSingleton(Renderer.ofCurrentCulture resources) |> ignore
```

## Context and attribute

A renderer holds two scoping roles, and they behave differently on purpose.

```fsharp
let signup = renderer |> Renderer.context "signup"     // appends a segment
let name = signup |> Renderer.attribute "name"         // replaces the whole attribute
let email = name |> Renderer.attribute "email"         // "name" is gone, not nested
let bare = email |> Renderer.unscoped                  // both roles cleared
```

`context` appends because a document, model, form, and component nest. `attribute` replaces because a form-scoped
renderer gets reused across sibling fields, and an appending `attribute` would quietly produce
`signup.name.email` on the second field.

The context is never used as a noun. With no attribute, `fullMessage` uses the contextual
`constraint.attribute.default` — "value" in English — so a whole-model failure reads "value must be present"
rather than "Signup must be present".

Schema supplies its typed path as the attribute for you; see [Schema errors](#schema-errors).

### Message fallback

For identity `constraint.presence.present`, context `signup`, and attribute path `address.postcode`, lookup removes
rightmost specificity one segment at a time:

```text
signup.address.postcode.constraint.presence.present
signup.address.constraint.presence.present
signup.constraint.presence.present
constraint.presence.present
```

The message identity itself is never truncated. `books.isbn.invalid` never degrades to `books.isbn` or `books`:
those are segments of one name, not a namespace to search.

That is what makes a per-field override cheap. Translate `constraint.presence.present` once, then override only the
handful of fields whose wording genuinely differs.

### Attribute nouns

Attribute nouns use their own prefix and their own chain. For context `signup` and attribute path
`address.postcode`:

```text
attribute.signup.address.postcode
attribute.address.postcode
attribute.postcode
```

If no resource resolves, Axial humanizes the final raw attribute segment. Humanization splits camelCase,
`snake_case`, and `kebab-case`, preserves acronym runs, keeps `_id`, and applies invariant sentence casing:

```text
postcode         -> Postcode
firstName        -> First name
postcodeID       -> Postcode ID
billing_address  -> Billing address
```

Humanization applies only to a raw segment Axial invented a noun for. A value that came out of a resource file is
returned exactly as authored — no recasing, trimming, or Unicode normalization, ever.

### Segments are opaque

Context names, attribute names, and Schema path keys are arbitrary text. Axial rejects an explicitly empty segment
and encodes the reserved characters before joining:

```text
% -> %25
. -> %2E
[ -> %5B
] -> %5D
```

`%` is encoded first, so a field literally named `%2E` cannot collide with a field named `.`. Consequently these
two renderers address different resources:

```fsharp
renderer |> Renderer.context "address.postcode"

renderer |> Renderer.context "address" |> Renderer.context "postcode"
```

Emitted hex digits are uppercase. Lookup never decodes and re-encodes your resource keys.

## Message composition

Localized entries are bare predicates:

```text
constraint.presence.present    = must be present
constraint.relation.atLeast    = must be at least {expected}
constraint.cardinality.between = must have a size between {minimum} and {maximum}
```

The noun and the actual-value clause are separate entries:

```text
constraint.attribute.default = value
constraint.actual            = {message}, but was {actual}
constraint.fullMessage       = {attribute} {message}
```

Rendering one interpreted leaf goes: resolve the predicate, wrap it in `constraint.actual` if the violation carries
an actual value, and — for `fullMessage` only — wrap that in `constraint.fullMessage` with the resolved noun.

Keeping them separate is what makes `{actual}` optional without an optional-placeholder rule, and it lets a locale
put the value or the noun somewhere English would not:

```text
constraint.actual      = reçu {actual} au lieu de « {message} »
constraint.fullMessage = {message} — {attribute}
```

## Named interpolation

Templates use named placeholders with an optional format suffix:

```text
constraint.number.multipleOf = must be a multiple of {divisor:N0}
```

`{{` and `}}` produce literal braces. Placeholder parsing is identical on every target.

Resource defects degrade rather than throw:

- an unknown placeholder name stays literal, so a translator's typo shows up in the message instead of taking down
  the request;
- unmatched or malformed braces make that template unusable, and lookup continues down the fallback chain to the
  neutral English;
- an unknown or unsupported format suffix falls back to ordinary formatting of the value.

Exceptions raised by *your* lookup, resolver, or formatter callbacks propagate untouched. Those are application
defects, not resource misses, and swallowing one would hide the bug in whichever locale nobody exercises.

### Operand formatting

Operands are `ConstraintValue`. The built-in formatter uses the renderer's value culture and honours format
suffixes. Two hooks replace it:

```fsharp
// One uniform callback. Format suffixes are ignored.
renderer |> Renderer.withValues ConstraintValue.render

// The suffix, when you need it.
renderer
|> Renderer.Advanced.withValueFormatting (fun request -> format request.Value request.Format)
```

A list operand — `choices` on `Constraint.oneOf`, for instance — is joined through the contextual
`constraint.list.*` patterns, and each item goes through the formatter.

## Lists and groups

Joining is pattern-driven, not hard-coded:

```text
constraint.group.all.pair    constraint.group.any.pair    constraint.list.pair
constraint.group.all.start   constraint.group.any.start   constraint.list.start
constraint.group.all.middle  constraint.group.any.middle  constraint.list.middle
constraint.group.all.end     constraint.group.any.end     constraint.list.end
```

`pair` and `end` take `{first}` and `{second}`; `start` and `middle` take `{first}` and `{rest}`. The algorithm is
deterministic:

| Items | Result |
| --- | --- |
| none | `""`, with no lookup |
| one | the item itself, with no lookup |
| two | `pair` |
| three or more | combine the last two with `end`, fold the preceding items right-to-left with `middle`, and apply `start` to the first |

```fsharp
Violation.message Renderer.english group
// "must be present, must have a size between 2 and 40 and must be an email address"
```

A language whose joining cannot be expressed as pair/start/middle/end — where the conjunction changes the words of
its members, or the whole group has to be reordered — is not served by these patterns. That case takes
`Violation.toMessageTree` and owns its own traversal. This is a genuine limit, not a gap to work around with a
cleverer pattern.

## Your own messages

`Constraint.custom` hands Axial prose and nothing else, so prose is all Axial can give back: it renders verbatim in
every language. Name a key and the rule becomes translatable:

```fsharp
let isbn =
    Constraint.customLocalized
        "books.isbn.invalid"
        "must be a valid ISBN"
        isValidIsbn

let isbnWithLength =
    Constraint.customLocalizedWith
        "books.isbn.invalid"
        "must be a valid ISBN"
        (Map.ofList [ "expectedLength", ConstraintValue.Integer 13L ])
        isValidIsbn
```

The prose stays required and becomes the fallback: an untranslated language still says something true. The key
takes the ordinary contextual chain, so for context `signup` and attribute `book`:

```text
signup.book.books.isbn.invalid
signup.books.isbn.invalid
books.isbn.invalid
```

A key is `segment ("." segment)*`. An empty key or empty segment is rejected at construction — a malformed key
written in source is a defect, and failing at the call site beats failing at a rendering edge in whichever language
nobody tested. `%`, brackets, whitespace, and non-ASCII characters are exact input; you never pre-encode a key.

Custom constraints declare no plural operand, and Axial does not infer one from an argument's name or value.
Guessing would silently change which key a translator has to supply. If you need `.one`/`.other` for a custom rule,
select it in an [advanced resolver](#advanced-resolvers).

Axial never invents a key for `Constraint.custom` prose. A key it made up would name a catalogue entry that does
not exist, and the lookup would fail in production, in the language you did not test.

## Plurals

An entry may declare at most one plural operand. Where one is declared, ordinary lookup tries `<key>.one` when the
operand is exactly one and `<key>.other` otherwise, then the bare key — at each contextual level, before moving
outwards:

```text
signup.tags.constraint.cardinality.minimum.other
signup.tags.constraint.cardinality.minimum
signup.constraint.cardinality.minimum.other
signup.constraint.cardinality.minimum
constraint.cardinality.minimum.other
constraint.cardinality.minimum
```

A bare field-specific entry therefore beats a pluralized model-level entry: the field override is the more
deliberate statement.

`.one`/`.other` covers the languages that need only those two. Full CLDR category selection belongs to an advanced
resolver.

## Schema errors

Schema owns the diagnostic path and supplies it as the attribute for you. Supply only the document context:

```fsharp
let signup = renderer |> Renderer.context "signup"

errors |> SchemaErrors.messages signup
// [ Path "name",                  "must be present"
//   Path "addresses[0].postcode", "must have a size between 2 and 40, but was 1" ]

errors |> SchemaErrors.fullMessages signup
// [ Path "name",                  "Name must be present"
//   Path "addresses[0].postcode", "Postcode must have a size between 2 and 40, but was 1" ]

errors |> SchemaErrors.toStringWith signup   // one full message per line
```

`messages` returns predicates because the returned `Path` already identifies the field. `fullMessages` composes the
noun for payloads and logs.

Index components are omitted from resource keys and kept in every returned path. `addresses[0].postcode` and
`addresses[7].postcode` are one field for a translator, and two distinct locations for a form.

Schema's own parse and structural failures have a `schema.*` catalogue that rides the same mechanics — see
[SchemaMessages](#the-schema-catalogue). At `Path.root`, full rendering uses `constraint.attribute.default`, never
the document context.

## Advanced resolvers

`Renderer.ofLookup` owns the entire candidate order and asks a plain lookup for exact keys. When your localization
system wants to select plural categories and render entries itself — ICU, for instance — take a resolver instead.
It receives one request per contextual level:

```fsharp
type MessageRequest =
    { BaseKey: string                              // encoded contextual key, no plural suffix
      Arguments: Map<string, ConstraintValue>
      PluralArgument: string option }

let renderer =
    Renderer.Advanced.ofResolver (fun request ->
        match icu.TryGet request.BaseKey with
        | Some entry -> Some (MessageResolution.Rendered (entry.Format request.Arguments))
        | None -> None)
```

The answer means:

- `None` — continue to the next, less specific level;
- `MessageResolution.Template template` — Axial interpolates and formats it;
- `MessageResolution.Rendered text` — you rendered it; Axial never interpolates that text again, so literal braces
  in it stay literal.

`Rendered` is final for the entry that was requested. Axial may still use its text as the `{message}` of
`constraint.actual`, as a child of a group pattern, or as the `{message}` of `constraint.fullMessage`, because
those are separate entries with their own resolution.

A resolver does not replace group traversal. A system that must reorder or reinterpret a whole group takes
`Violation.toMessageTree`.

### Inspecting what will be asked for

```fsharp
Renderer.Advanced.lookupCandidates renderer spec   // exact encoded keys, in order
Renderer.Advanced.messageRequests renderer spec    // one request per contextual level
Renderer.Advanced.attributeCandidates renderer     // encoded attribute-noun keys
Renderer.Advanced.format spec renderer             // render any catalogue's entry
```

These take a `MessageFormatSpec`: a message identity and arguments (`MessageDescriptor`) plus the owning
catalogue's neutral fallback and plural operand. That pairing is what lets Schema — or your own catalogue — reuse
every renderer mechanic without `Axial.Constraint` knowing a single one of its keys.

```fsharp
let spec =
    MessageDescriptor.Advanced.ofSegments [ "billing"; "cardExpired" ] Map.empty
    |> MessageFormatSpec.Advanced.create "card has expired" None

renderer |> Renderer.Advanced.format spec
```

`lookupCandidates` and the rest return *encoded* resource keys. Canonical unencoded identity comes only from
`MessageDescriptor.key`.

## `Violation.render` compatibility

`Violation.render` remains the resource-free, culture-free path, unchanged. It does not go through `Renderer`.

| Violation | `render` | `message Renderer.english` | `fullMessage Renderer.english` |
| --- | --- | --- | --- |
| presence, no actual | `value must be present` | `must be present` | `value must be present` |
| relation, actual `11` | `expected a value at least 13, but was 11` | `must be at least 13, but was 11` | `value must be at least 13, but was 11` |
| opaque prose | prose unchanged | prose unchanged | noun composed once around the prose |
| group | legacy `; ` and `, or ` join | localized group composed once | noun composed once around the group |

Localized English uses bare predicates and composition, which reads better in a sentence. Exact wording
compatibility is promised for `render` only.

## The key catalogue

Every key Axial can produce. `actual` is not listed as an argument on any predicate: it arrives through the
separate `constraint.actual` entry.

| Key | Arguments | Plural on | Default English |
| --- | --- | --- | --- |
| `constraint.presence.present` | — | — | must be present |
| `constraint.presence.blank` | — | — | must be blank |
| `constraint.cardinality.exact` | `expected` | `expected` | must have a size of exactly {expected} |
| `constraint.cardinality.minimum` | `minimum` | `minimum` | must have a size of at least {minimum} |
| `constraint.cardinality.maximum` | `maximum` | `maximum` | must have a size of at most {maximum} |
| `constraint.cardinality.between` | `minimum`, `maximum` | — | must have a size between {minimum} and {maximum} |
| `constraint.relation.equal` | `expected` | — | must be {expected} |
| `constraint.relation.notEqual` | `expected` | — | must not be {expected} |
| `constraint.relation.greaterThan` | `expected` | — | must be greater than {expected} |
| `constraint.relation.lessThan` | `expected` | — | must be less than {expected} |
| `constraint.relation.atLeast` | `expected` | — | must be at least {expected} |
| `constraint.relation.atMost` | `expected` | — | must be at most {expected} |
| `constraint.relation.within` | `minimum`, `maximum` | — | must be between {minimum} and {maximum} |
| `constraint.membership.oneOf` | `choices` | — | must be one of {choices} |
| `constraint.membership.noneOf` | `choices` | — | must not be one of {choices} |
| `constraint.membership.contains` | `item` | — | must contain {item} |
| `constraint.membership.notContains` | `item` | — | must not contain {item} |
| `constraint.uniqueness` | — | — | must not contain duplicate values |
| `constraint.format.email` | — | — | must be an email address |
| `constraint.format.trimmed` | — | — | must not have leading or trailing whitespace |
| `constraint.format.numeric` | — | — | must contain digits only |
| `constraint.format.alphanumeric` | — | — | must contain letters and digits only |
| `constraint.format.pattern` | `pattern` | — | must match {pattern} |
| `constraint.number.multipleOf` | `divisor` | — | must be a multiple of {divisor} |
| `constraint.number.finite` | — | — | must be a finite number |

A built-in whose operand has no portable representation reports the operation rather than approximating the
operand. These carry no arguments:

| Key | Arguments | Plural on | Default English |
| --- | --- | --- | --- |
| `constraint.unsupportedOperand.relation.equal` | — | — | failed an equality rule whose operand has no portable representation |
| `constraint.unsupportedOperand.relation.notEqual` | — | — | failed an inequality rule whose operand has no portable representation |
| `constraint.unsupportedOperand.relation.greaterThan` | — | — | failed a greater-than rule whose operand has no portable representation |
| `constraint.unsupportedOperand.relation.lessThan` | — | — | failed a less-than rule whose operand has no portable representation |
| `constraint.unsupportedOperand.relation.atLeast` | — | — | failed an at-least rule whose operand has no portable representation |
| `constraint.unsupportedOperand.relation.atMost` | — | — | failed an at-most rule whose operand has no portable representation |
| `constraint.unsupportedOperand.within` | — | — | failed a range rule whose operand has no portable representation |
| `constraint.unsupportedOperand.contains` | — | — | failed a containment rule whose operand has no portable representation |
| `constraint.unsupportedOperand.multipleOf` | — | — | failed a multiple-of rule whose operand has no portable representation |

The composition and joining entries:

| Key | Arguments | Plural on | Default English |
| --- | --- | --- | --- |
| `constraint.attribute.default` | — | — | value |
| `constraint.actual` | `message`, `actual` | — | {message}, but was {actual} |
| `constraint.fullMessage` | `attribute`, `message` | — | {attribute} {message} |
| `constraint.group.all.pair` | `first`, `second` | — | {first} and {second} |
| `constraint.group.all.start` | `first`, `rest` | — | {first}, {rest} |
| `constraint.group.all.middle` | `first`, `rest` | — | {first}, {rest} |
| `constraint.group.all.end` | `first`, `second` | — | {first} and {second} |
| `constraint.group.any.pair` | `first`, `second` | — | {first} or {second} |
| `constraint.group.any.start` | `first`, `rest` | — | {first}, {rest} |
| `constraint.group.any.middle` | `first`, `rest` | — | {first}, {rest} |
| `constraint.group.any.end` | `first`, `second` | — | {first} or {second} |
| `constraint.list.pair` | `first`, `second` | — | {first} and {second} |
| `constraint.list.start` | `first`, `rest` | — | {first}, {rest} |
| `constraint.list.middle` | `first`, `rest` | — | {first}, {rest} |
| `constraint.list.end` | `first`, `second` | — | {first} and {second} |

The same data is available at runtime, so a coverage test never has to copy this page:

```fsharp
Catalogue.keys            // string list
Catalogue.arguments       // Map<string, string list>
Catalogue.english         // Map<string, string>
Catalogue.pluralArgument  // Map<string, string option>
```

A test enumerates the atom union against both this page and `Catalogue`, so a new rule cannot ship with its key
undocumented or unimplemented.

### The Schema catalogue

`Axial.Schema` owns its own keys for parse, boundary-supply, and structural failures. They stay in that package —
Schema depends on Constraint and never the reverse.

| Key | Arguments | Default English |
| --- | --- | --- |
| `schema.omitted` | — | must be supplied |
| `schema.blank` | — | must be present |
| `schema.expectedScalar` | — | must be a single value |
| `schema.expectedObject` | — | must be an object |
| `schema.expectedMany` | — | must be a collection |
| `schema.invalidFormat` | `expected` | must be a valid {expected} |
| `schema.parseOutOfRange` | `target` | must be within the range of {target} |
| `schema.unknownTag` | `choices` | must be one of {choices} |

`SchemaMessages.keys`, `.arguments`, and `.english` expose the same data. Constructor failures and custom errors
carrying authored prose have no catalogue entry: Schema does not invent a key for text your application wrote.

## Next

- [Adding a language](./adding-a-language/) — the working order for a new translation, and how to prove coverage.
- [Fable support](./fable/) — what the rendering edge does and does not do in JavaScript.

## Argument values

Arguments are `ConstraintValue`, the portable value type: text, char, boolean, integer, decimal, big integer,
float, GUID, timespan, date-time, date-time-offset, null, and lists of those. `ConstraintValue.render` gives the
invariant rendering of one; a renderer formats through its value culture instead.
