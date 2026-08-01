# Axial.Data ergonomic structured values

## Status

Phase 1 implemented. This note retains the complete Phase 1 rationale and the proposed follow-on phases.

Phase 1 now supplies the coherent authoring language described below. Later phases record intended extension points, not commitments to ship every listed operation.

## Product role

`Axial.Data` should be an independent package for immutable structured values. It should make constructed, transformed, and produced data equally easy to work with.

Its initial uses include:

- source-neutral input passed to `Axial.Schema`
- concise boundary and integration-test fixtures
- malformed and partially supplied input
- nested maps and lists used for bulk operations
- named variations and bounded Cartesian test matrices
- exact and selective proofs over produced data
- adapters for JSON, query strings, forms, configuration, command-line arguments, and Fable JavaScript values

The package keeps one portable owned tree. It does not define separate .NET and Fable recursive representations.

```fsharp
[<RequireQualifiedAccess>]
type Data =
    | Null
    | Text of value: string
    | Number of token: string
    | Bool of value: bool
    | List of items: Data list
    | Object of fields: (string * Data) list
```

`Text "42"`, `Number "42"`, and `Bool true` remain distinct. Number tokens stay lexical so .NET and JavaScript do not narrow the same value differently.

Object fields remain ordered and may contain duplicate names. Every operation must state how it treats order, duplicate fields, lexical numbers, missing values, and list positions.

## Design standard

The interface succeeds only if construction, editing, case generation, lookup, and proof feel like one small language.

Normal callers should not spell union cases, conversion witnesses, lexical-number formatting, or parsed path values in ordinary examples.

After opening `Axial.Data.Syntax`, the Phase 1 vocabulary should remain close to:

```text
data  =>  ?=>  nil  num  fields
set  put  remove  append  prepend  insert  rename  update  patch
variant  variants  dimension  matrix
at  absent  matching  exactly  containing
containingItems  inOrder  allItems  someItem
any  anyText  anyNumber  oneOf  satisfying
```

The implementation may use SRTP conversion and hidden instruction types. It must not use `obj`, reflection, or runtime type discovery to implement authoring syntax.

Throwing functions are for authored fixtures and test declarations. Result-returning functions expose the same engine for dynamic input. Neither form should duplicate semantics.

## Package boundaries

`Axial.Data` owns the tree, literal conversion, strict paths, lookup and extraction, edits, deterministic traversal, comparison, mismatch data, and framework-neutral matching.

Source adapters own conversion to and from JSON, query strings, forms, configuration, command-line arguments, and JavaScript values.

Snapshot adapters own files, approval and update policies, source locations, and test-runner integration. FsCheck adapters own generators and shrinkers.

`Axial.Schema` owns typed parsing and contracts. HTTP packages own transport metadata. Neither concern should be absorbed into `Axial.Data` merely because it consumes `Data`.

# Phase 1: one expressive data language

Phase 1 establishes the complete everyday workflow: author a value, derive cases from it, consume produced JSON, inspect paths, and prove exact or partial structure with useful diagnostics.

## Literal construction

`=>` returns an opaque field instruction rather than a raw tuple. Recursive conversion distinguishes object fields from an ordinary list, so nested objects do not repeat `data`.

```fsharp
open Axial.Data.Syntax

let customer =
    data [
        "name" => "Ada"
        "age" => 36
        "active" => true

        "address" => [
            "city" => "Adelaide"
            "postcode" => 5000
        ]

        "roles" => [
            "admin"
            "author"
        ]

        "contacts" => [
            [
                "kind" => "email"
                "value" => "ada@example.com"
            ]
            [
                "kind" => "phone"
                "value" => "+61 400 000 000"
            ]
        ]
    ]
```

The intended public shape is approximately:

```fsharp
type DataField = private DataField

module Axial.Data.Syntax =
    val inline data : value: ^value -> Data
    val inline (=>) : name: string -> value: ^value -> DataField
    val inline (?=>) : name: string -> value: ^value option -> DataField
    val nil : Data
    val num : token: string -> Data
    val fields : value: Data -> DataField list
```

The private types in this note describe visibility, not implementation. Callers may construct instructions but cannot inspect or forge their payloads.

### Missing, null, and exact numbers

Optional omission differs from a present null:

```fsharp
data [
    "name" => "Ada"
    "nickname" ?=> nickname
    "deletedAt" => nil
]
```

- `nickname = None` omits the field.
- `deletedAt` is present as `Data.Null`.
- An ordinary `option` must not silently map `None` to null.

Exact numeric tokens remain available beside ordinary numeric conversion:

```fsharp
data [
    "ordinary" => 42
    "price" => 19.95m
    "huge" => num "999999999999999999999999999999"
    "exponent" => num "1.234567890123456789e+400"
]
```

`num` validates a portable number token when constructed. It does not parse, normalize, or equate alternate lexical forms.

### Dynamic fields and interpolation

The list form retains normal F# list-expression capabilities instead of introducing an object computation expression:

```fsharp
data [
    "kind" => "example"
    "customerId" => customerId
    yield! fields common

    if includeDebug then
        "debug" => true

    for name in names do
        $"user-{name}" => name
]
```

`fields` accepts only `Data.Object`. Another shape is an authoring defect and raises a specific exception. Spreading preserves field order and duplicates.

Domain-value interpolation requires an explicit conversion witness or adapter. The literal syntax must not discover serializers through reflection.

## Paths, lookup, and extraction

One path model should serve lookup, edits, proofs, diffs, and later redaction. String paths are the primary authored form because they keep fixtures compact.

```fsharp
Data.tryAt "customer.address.postcode" response
Data.tryAt "items[0].sku" response
Data.tryAt "metadata['build.version']" response
```

Phase 1 must specify root selection, quoted field names, escaping, indexes, malformed syntax, blocked traversal, and duplicate-name resolution.

`DataPath` overloads may support reusable or performance-sensitive code. String forms should parse through the same implementation.

Shape extraction remains explicit and small:

```fsharp
Data.tryText
Data.tryBool
Data.tryNumberToken
Data.tryList
Data.tryObject
```

These functions inspect the owned tree. Typed domain decoding remains the responsibility of `Axial.Schema` or an explicit codec.

## Immutable edits

The fixture-authoring surface uses paths for visual economy:

```fsharp
let promoted =
    customer
    |> patch [
        set "address.postcode" 5001
        append "roles" "billing"
        remove "address.city"
    ]
```

Operations have strict meanings:

- `set` replaces an existing value and preserves its position.
- `put` replaces an existing final field or appends a missing final field. Its parent object must exist.
- `remove` requires its target and removes the selected object field or list item.
- `append` and `prepend` require a list target.
- `insert` requires a list target and a valid insertion index.
- `rename` requires an object field and preserves its position and value.
- `update` requires its target and applies a `Data -> Data` function.
- No operation creates intermediate containers or fills list holes with `Null`.
- Edits apply in declaration order, and each edit observes the preceding edits.
- Application is atomic: failure never exposes a partially edited tree.

The proposed surface is:

```fsharp
type DataEdit = private DataEdit

module Axial.Data.Syntax =
    val inline set : path: string -> value: ^value -> DataEdit
    val inline put : path: string -> value: ^value -> DataEdit
    val remove : path: string -> DataEdit
    val inline append : path: string -> value: ^value -> DataEdit
    val inline prepend : path: string -> value: ^value -> DataEdit
    val inline insert : path: string -> index: int -> value: ^value -> DataEdit
    val rename : path: string -> name: string -> DataEdit
    val update : path: string -> change: (Data -> Data) -> DataEdit
    val patch : edits: DataEdit list -> input: Data -> Data

module Data =
    val tryPatch :
        edits: DataEdit list ->
        input: Data ->
        Result<Data, DataPatchFailure list>
```

`patch` raises `DataPatchException` for invalid authored edits. Diagnostics include the edit index, path, expected shape, actual shape, and nearby value.

`Data.tryPatch` returns the same structured failures. Both forms use one implementation.

## Named variations

Independent cases reuse edits:

```fsharp
let emailCases =
    customer
    |> variants [
        variant "valid" []

        variant "missing email" [
            remove "contacts[0].value"
        ]

        variant "blank email" [
            set "contacts[0].value" ""
        ]

        variant "wrong shape" [
            set "contacts[0].value" [ "one"; "two" ]
        ]
    ]
```

```fsharp
type DataVariation =
    {
        Name: string
        Edits: DataEdit list
    }

type DataCase =
    {
        Name: string
        Value: Data
    }

module Axial.Data.Syntax =
    val variant : name: string -> edits: DataEdit list -> DataVariation
    val variants : variations: DataVariation list -> baseline: Data -> DataCase list
```

Variation application preserves declaration order. Construction rejects duplicate names unless a demonstrated consumer establishes a stronger rule.

## Bounded Cartesian matrices

Matrices express independent axes over one baseline:

```fsharp
let cases =
    customer
    |> matrix [
        dimension "plan" [
            variant "free" [ put "plan" "free" ]
            variant "pro" [ put "plan" "pro" ]
        ]

        dimension "region" [
            variant "AU" [ put "region" "au" ]
            variant "US" [ put "region" "us" ]
        ]

        dimension "roles" [
            variant "none" [ set "roles" [] ]
            variant "admin" [ set "roles" [ "admin" ] ]
        ]
    ]
```

Names follow dimension order, such as `plan: pro / region: AU / roles: admin`.

Matrix construction uses a conservative maximum combination count and fails before materializing a larger product. `matrix` must never hide an unbounded Cartesian product.

```fsharp
type DataDimension =
    {
        Name: string
        Variations: DataVariation list
    }

module Axial.Data.Syntax =
    val dimension : name: string -> variations: DataVariation list -> DataDimension
    val matrix : dimensions: DataDimension list -> baseline: Data -> DataCase list
```

The exact limit configuration remains open for the prototype.

## Exact comparison and structural differences

Exact equality is distinct from partial matching. It compares the complete owned tree and preserves the model's deliberate distinctions.

Phase 1 exact comparison treats number tokens, object field order, duplicate occurrences, list length, and list order as significant.

```fsharp
module Data =
    val compare : expected: Data -> actual: Data -> Result<unit, DataDifference list>
    val diff : expected: Data -> actual: Data -> DataDifference list
```

`DataDifference` identifies the path, expected observation, actual observation, and cause. Rendering should show the smallest useful surrounding subtree.

Do not begin with configurable strict and lenient comparison modes. Add a named policy only when a consumer demonstrates semantics that cannot be expressed as a pattern.

## Selective path proofs

Sparse proofs establish chosen facts about a large produced value without copying its complete shape:

```fsharp
response
|> matching [
    at "customer.name" "Ada"
    at "customer.address.postcode" 5000
    at "roles[0]" "admin"
    absent "error"
]
```

`at` requires the path and exact value or pattern. `absent` requires path resolution to find no value. Each proof evaluates independently against the same root, and matching accumulates failures.

The authored and dynamic forms share one engine:

```fsharp
type DataExpectation = private DataExpectation

module Axial.Data.Syntax =
    val inline at : path: string -> expected: ^value -> DataExpectation
    val absent : path: string -> DataExpectation
    val matching : expectations: DataExpectation list -> actual: Data -> unit

module Data =
    val tryMatch :
        expectations: DataExpectation list ->
        actual: Data ->
        Result<unit, DataMismatch list>
```

`matching` raises a framework-neutral `DataMatchException` whose message is suitable for ordinary test output. Test adapters may provide native assertion integration later.

## Recursive structural patterns

Sparse paths become repetitive when the evidence is naturally a nested fragment. Phase 1 therefore includes opaque recursive patterns while preserving exact `Data` equality.

```fsharp
response
|> matching [
    at "customer" (
        containing [
            "name" => "Ada"
            "plan" => "pro"

            "address" => containing [
                "city" => "Adelaide"
                "postcode" => 5000
            ]
        ])
]
```

Literal values lift to exact patterns. `containing` requires the named object evidence and permits unrelated fields.

Collections use separately named semantics:

```fsharp
response
|> matching [
    at "items" (
        containingItems [
            containing [
                "sku" => "ABC"
                "quantity" => 2
            ]
        ])

    at "events" (
        allItems (
            containing [
                "id" => anyText
                "createdAt" => anyText
            ]))
]
```

Phase 1 should prototype these pattern families:

- `exactly value` for explicit exact matching
- `containing fields` for a partial object
- `containingItems patterns` for unordered containment
- `inOrder patterns` for an ordered subsequence
- `allItems pattern` and `someItem pattern` for quantified list matching
- `any`, `anyText`, and `anyNumber` for volatile values with required shapes
- `oneOf patterns` for alternatives
- `satisfying description predicate` for an ordinary F# predicate with useful diagnostics

`containingItems` must define multiset consumption. One actual item cannot satisfy two expected items unless it occurs twice.

If correct unordered matching requires backtracking, the implementation must use deterministic limits. It must not silently switch to a greedy algorithm that rejects valid matches.

Partial object matching must not silently become last-wins for duplicate fields. The prototype should either consume occurrences in order and render `name#2`, or reject ambiguous patterns.

Predicates require a description. A thrown predicate exception becomes a structured mismatch or follows one clearly documented propagation rule.

Captures, wildcard paths, regex helpers, numeric tolerance, and negation are not Phase 1 requirements. The opaque pattern representation must leave room for them.

## JSON ownership and deterministic rendering

The JSON adapter materializes owned `Data`; it must not copy `JsonDocument`'s borrowed lifetime into the canonical model.

```fsharp
Data.Json.parse
Data.Json.ofJsonElement
Data.Json.render
Data.Json.renderIndented
```

Parsing and rendering preserve valid number tokens, field order, and duplicate fields. Rendering must be deterministic so diffs and later snapshot adapters remain stable.

If a zero-allocation reader is later justified, it belongs to a parser adapter and materializes an owned value on demand.

Other source adapters produce the same representation:

```fsharp
Data.ofQuery
Data.ofForm
Data.ofConfiguration
Data.ofCliArgs
Data.ofJsValue
```

An adapter preserves every distinction its source provides and reports representations it cannot carry.

JavaScript conversion must define `undefined`, `NaN`, infinities, `BigInt`, symbols, functions, accessors, class instances, and cycles. It must not stringify unsupported values.

## Phase 1 end-to-end proof

Before promotion into active architecture, one prototype must demonstrate the intended public workflow in one readable file:

1. author a deeply nested request without union cases or annotations
2. interpolate values, optional fields, exact numbers, spreads, `if`, `for`, and `yield!`
3. derive malformed variants without rebuilding the baseline
4. generate a bounded matrix with deterministic names and order
5. parse a real JSON response into the same owned representation
6. inspect a strict path and extract its shape
7. prove sparse facts and a nested partial object
8. prove exact, ordered, and unordered list semantics
9. compare complete values and render a focused structural diff
10. deliberately fail every operation and judge the diagnostics

If this file needs conversion witnesses, union cases, type annotations, or explanatory comments in normal usage, the interface has not met its ergonomic goal.

## Phase 1 technical proof requirements

The prototype and tests must also cover:

1. recursive object and list inference, including empty objects and empty lists
2. optional omission versus explicit null
3. primitive conversion on .NET and Fable
4. exact number-token validation and preservation
5. duplicate-field ordering through construction, spread, lookup, edits, comparison, and matching
6. every edit against objects, lists, roots, malformed paths, missing paths, and blocked traversal
7. atomic multi-edit failure and diagnostic rendering
8. deterministic variation and matrix ordering
9. matrix limits checked before allocation
10. accumulated proof and comparison failures with focused paths
11. partial-object and list-pattern ambiguity rules
12. deterministic JSON round trips
13. AOT and trimming compatibility
14. Fable compilation and JavaScript round trips

Public-interface tests must use expected end-user syntax. Do not retain transitional aliases after choosing the final surface.

# Follow-on phases

Later phases begin only after Phase 1 proves that the base language is coherent. Each phase should be justified by a demonstrated consumer and may be delivered as an adapter rather than core API.

## Phase 2: richer patterns and reusable selectors

Extend the pattern language where real tests require more than Phase 1:

- optional and explicitly absent object fields inside recursive patterns
- regex text patterns
- numeric tolerance with explicit lexical-to-numeric conversion rules
- negation with positive, understandable diagnostics
- exact list, prefix, suffix, unordered multiset, and index-specific patterns
- reusable compiled `DataPath` values
- wildcard selectors such as `items[*].id`

Strict paths and multi-result selectors remain distinct. A selector is not accepted by an operation that requires exactly one target.

Do not grow selectors into JSONPath or JSONata by accident. Filters, recursive descent, projections, grouping, and aggregation require a separate language proposal.

## Phase 3: snapshots, golden files, and redaction

Add deterministic approval testing over the Phase 1 renderer and diff engine:

```fsharp
response
|> redact [
    replace "id" "<id>"
    replace "createdAt" "<timestamp>"
]
|> Snapshot.matchFile "approved/customer-response.json"
```

Core may own pure redaction or replacement transforms. A testing package owns snapshot paths, update modes, approval policy, source locations, and assertion exceptions.

Runner adapters may integrate with xUnit, Expecto, or other frameworks. Environment-variable and filesystem effects must remain explicit at the snapshot boundary.

## Phase 4: captures and relational proofs

Support tests that must reuse a produced value or prove relationships between locations:

```fsharp
let proof =
    proof {
        let! customerId = captureText "customer.id"
        do! equalAt "audit.customerId" customerId
    }
```

Captures require deterministic branch selection, conflict rules for repeated names, and clear behavior inside alternatives and unordered collections.

Ordinary F# bindings remain preferable when they are equally clear. Do not turn `Axial.Data` into a general test computation framework.

## Phase 5: property generation and shrinking

An FsCheck adapter may generate arbitrary owned trees, malformed boundary values, and stable shrunk counterexamples:

```fsharp
DataGen.any
DataGen.withMaximumDepth 5
DataGen.jsonCompatible
```

Generic `Data` generation belongs in an `Axial.Data` testing adapter. Schema-conforming generation remains in `Axial.Schema.Testing`.

The core package must not acquire an FsCheck dependency.

## Phase 6: contracts, examples, recording, and replay

Use `Data` as the common body representation for:

- HTTP request and response examples
- message-bus events and webhooks
- Pact-style consumer expectations
- database document fixtures
- recorded external responses
- schema and OpenAPI examples

Media types, headers, status codes, compatibility policy, and replay I/O remain in HTTP, Schema, or dedicated testing packages.

This phase should reuse structural patterns and differences. It must not introduce a second recursive value or matcher representation.

## Phase 7: standard patch interchange and overlays

Add adapters for RFC 6902 JSON Patch only when interoperability requires them:

```fsharp
DataEdit.ofJsonPatch
DataEdit.toJsonPatch
```

Standard `add`, `remove`, `replace`, `move`, `copy`, and `test` operations do not automatically replace the ergonomic edit vocabulary. Each adapter must account for path and duplicate-field differences.

If object overlay is demonstrated, name its duplicate policy explicitly, such as `overlayLast` or `overlayAll`. Do not add an ambiguous `merge` operation.

JSON Merge Patch is a separate interchange format with different null and omission semantics. It requires its own adapter and must not be inferred from `patch`.

## Phase 8: query and transformation language

Some consumers may eventually need filtering, projection, grouping, aggregation, or construction of new result shapes.

That capability is comparable to JSONPath, JSONata, or `jq`; it is not a small extension to strict paths.

Require a separate design note before adding it. It must define evaluation cardinality, ordering, failure behavior, resource limits, portability, and whether expressions are serializable.

# Continuing exclusions

- No `Data<'scalar>` in the initial public interface.
- No separate .NET and JavaScript recursive trees.
- No `obj`-valued scalar case.
- No reflection-based primitive conversion.
- No implicit `None` to `Null` conversion.
- No automatic creation of missing patch containers.
- No filling list holes with `Null`.
- No implicit normalization of `Number "1"`, `Number "1.0"`, and `Number "1e0"`.
- No vague `contains` or `matches` operation with configurable hidden semantics.
- No general-purpose lenses in the primary authoring surface.
- No object computation expression alongside list literal syntax.
- No snapshot filesystem policy in the representation package.
- No schema validation, transport contracts, or property-test dependency in the core package.

If native typed dynamic scalars become necessary, revisit a generic internal tree plus explicit scalar vocabularies. Do not add parallel recursive unions.

# Documentation consequences

This design gives `Axial.Data` a standalone role and justifies its independent NuGet package.

It remains a dependency of Schema, HTTP input adapters, and contract tooling, while becoming directly useful for fixtures, produced-data proofs, diffs, and bulk variation.

`Axial.Data` does not require a top-level product navigation item. Introduce it under structured Schema input and test authoring, with a focused package and reference page.

When Phase 1 becomes active architecture, teach the end-to-end workflow before listing individual operations. The documentation should show one value moving through construction, variation, transport, and proof.
