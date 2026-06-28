# Error Handling, Refined, And Policy PRD

Status: current idea with settled direction.

This PRD defines the next Axial data-boundary architecture. It supersedes the older `Check` shape in which `Check`
returned `Result<_, unit>`, and it treats the package rename from `Axial.Result` to `Axial.ErrorHandling` as a settled
direction for the next pre-1.0 iteration.

## Problem Statement

Axial currently has a small `Axial.Result` package that owns `result { }` and a broad `Check` module. The package name is
accurate for the carrier, but it is weak as a public entry point because F# already has `Result` in FSharp.Core. The
current `Check` module also mixes several semantic jobs: predicates, unit-error checks, type-preserving guards,
extraction helpers, typed structural diagnostics, and parser-like lifters.

That mixed surface makes the library harder to explain, harder for code models to predict, and harder to position beside
libraries that already produce standard F# `Result` values. Axial needs a clearer set of independent but coherent lanes:
small predicates, fail-fast result helpers, type refinement, accumulating validation, and environment-aware workflow
policy.

## Solution

Rename `Axial.Result` to `Axial.ErrorHandling` and split its current responsibilities into focused modules. Keep the
package focused on fail-fast boundary decisions rather than making it a broad ecosystem umbrella.

Add `Axial.Refined` as the home for parsing, type narrowing, refined value constructors, and a dedicated `refine { }`
computation expression. Add `Policy` to `Axial.Flow` as the environment-aware adapter that maps local checks, parsing,
refinement, validation results, or contextual requirements into workflow errors.

The intended architecture is:

```text
Axial.ErrorHandling
  Check      pure predicates, 'input -> bool
  Result     fail-fast Result helpers, guards, structural result operations, result { }

Axial.Refined
  Parse      untrusted text -> primitive Result
  Refine     raw primitive/structure -> refined Result
  refine { } target-type-driven refinement CE

Axial.Validation
  Validation accumulating diagnostics with tree-shaped error context

Axial.Flow
  Flow       environment-aware workflow execution
  Policy     'env -> 'input -> Result<'output, 'workflowError>
  Flow.verify
```

The core separation is:

```text
Check decides locally.
Result turns local facts into fail-fast values.
Parse turns untrusted serialized values into primitives.
Refine turns primitives or structures into stronger values.
Validation accumulates diagnostics.
Policy assigns workflow meaning and environment.
Flow executes the workflow.
```

Axial should coexist with any library that produces standard F# `Result` values. Users can combine FsToolkit result
helpers with `Flow`, `Refined`, or `Validation` because the integration point is the standard `Result<'value, 'error>`
shape, not an Axial-specific carrier.

## User Stories

1. As an F# developer, I want `Axial.ErrorHandling` to describe fail-fast boundary work, so that the package name matches
   the engineering problem I am solving.
2. As an F# developer, I want `Check` functions to return `bool`, so that predicates can be used directly with native F#
   boolean operators and collection functions.
3. As an F# developer, I want `Result.guard` to lift a predicate into a `Result`, so that I can attach an application
   error only where the application context is known.
4. As an F# developer, I want `Result.require` to turn a boolean condition into `Result<unit, 'error>`, so that I can
   express checks that do not preserve a source value.
5. As an F# developer, I want type-preserving guards to live under `Result`, so that `Result.notBlank MissingName name`
   clearly returns the original value or an error.
6. As an F# developer, I want extraction helpers to live outside `Check`, so that `Check` remains a simple predicate
   module rather than a result and shape-transformation module.
7. As an F# developer, I want parser functions to live in `Axial.Refined`, so that text-to-primitive conversion is part
   of the same boundary story as type refinement.
8. As an F# developer, I want `Refine` functions to build stronger values, so that domain code can accept refined types
   instead of repeatedly checking raw primitives.
9. As an F# developer, I want a `refine { }` CE, so that untrusted input records can be compiled into trusted domain
   records before workflow execution.
10. As an F# developer, I want target-type-driven binding inside `refine { }`, so that `let! (email: Email) = rawEmail`
    can select the appropriate refinement rule by the annotated target type.
11. As an F# developer, I want target-type-driven refinement to be isolated to `refine { }`, so that `flow { }` remains a
    straightforward workflow orchestration CE.
12. As an F# developer, I want refined errors to be mapped at a workflow boundary, so that refined value types do not know
    about application-specific error unions.
13. As an F# developer, I want `Policy` to live in `Axial.Flow`, so that environment-aware requirements are part of the
    workflow package rather than the error-handling package.
14. As an F# developer, I want `Policy` to adapt pure checks and contextual checks to the same shape, so that workflow code
    does not care whether a requirement reads from `'env`.
15. As an F# developer, I want `Flow.verify` to run a `Policy`, so that workflow code can pipe data into requirements with
    a consistent data-on-the-left style.
16. As an F# developer, I want `Policy.withError` for fixed workflow errors, so that low-level check errors can be ignored
    when they are not meaningful to the workflow.
17. As an F# developer, I want `Policy.pure` or `Policy.mapError` for preserving and mapping low-level errors, so that
    meaningful parser, refinement, or validation errors can be wrapped intentionally.
18. As an F# developer, I want `Policy.context` for environment-dependent requirements, so that feature flags, tenant
    limits, permissions, and configuration-backed rules can be expressed without manual environment plumbing in each
    workflow.
19. As an F# developer, I want `Policy.optional` for feature-flagged requirements, so that a policy can become a no-op
    based on the runtime environment.
20. As an F# developer, I want Axial packages to remain independent, so that I can use `Axial.Refined` or `Axial.Flow`
    alongside any existing result helpers.
21. As an F# developer, I want `Axial.Validation` to remain distinct from `Axial.ErrorHandling`, so that accumulating
    diagnostics are not forced into fail-fast APIs.
22. As an F# developer, I want validation error shape to remain a first-class Axial design point, so that tree-shaped
    diagnostics can be chosen when list-shaped errors are not enough.
23. As an AI coding agent, I want each module name to imply the return shape, so that generated code does not mix
    predicates, result guards, parsers, and refinement constructors.
24. As an AI coding agent, I want deprecated prefixes to be removed rather than kept as aliases, so that the generated API
    style has one canonical form before 1.0.
25. As a package maintainer, I want the pre-1.0 migration to remove stale names immediately, so that user-facing docs do
    not teach old and new idioms at the same time.

## Implementation Decisions

- `Axial.Result` will be renamed to `Axial.ErrorHandling` before 1.0.
- `Axial.ErrorHandling` is a leaf package, not a meta package over `Axial.Validation` and `Axial.Refined`.
- `Axial` remains the broad umbrella package over the Axial family.
- `Check` returns pure boolean predicates. It does not return `Result`, does not preserve values, and does not extract
  inner values.
- The `Check<'value> = Result<'value, unit>` alias is removed.
- `Check` owns names such as `notBlank`, `isEmpty`, `hasDuplicates`, `lessThan`, `atLeast`, `between`, `matchesRegex`, and
  `isNull` as predicates.
- Boolean composition helpers such as old `Check.both`, `Check.either`, `Check.all`, and `Check.any` are removed. Users
  compose predicates with `&&`, `||`, `not`, `Seq.forall`, and `Seq.exists`.
- `Result` owns generic result helpers, the fail-fast `result { }` CE, predicate lifting, error assignment, structural
  guards, and extraction helpers.
- `Result.guard` uses predicate-first ordering: `predicate -> error -> value -> Result<'value, 'error>`.
- `Result.require` uses condition-first ordering: `bool -> error -> Result<unit, 'error>`.
- `Result.fromPredicate`, `Result.fromTry`, and `Result.fromChoice` live in `Result`, not `Check`.
- Typed structural failures such as cardinality, length, and range errors live with the result-producing operations that
  return them.
- Prefixes `when*`, `take*`, and `as*` are removed from the future API. Every existing operation using those prefixes must
  find a prefix-free home in `Check`, `Parse`, `Result`, or `Refine`.
- The naming rule is: if an operation preserves the type, use the property or requirement descriptor; if it transforms the
  type, use the target type or target concept name.
- `Parse` lives in `Axial.Refined`, not `Axial.ErrorHandling`.
- `Parse` owns untrusted serialized input to primitive conversion. Initial targets include `int`, `int64`, `decimal`,
  `float`, `bool`, `Guid`, `DateTime`, `DateTimeOffset`, `DateOnly`, `TimeOnly`, and enums.
- `Parse` returns `Result<'primitive, ParseError>` so callers can inspect the target primitive type and failed input.
- `Refine` lives in `Axial.Refined` and owns construction of stronger values from raw primitives or structures.
- `Axial.Refined` owns the dedicated `refine { }` CE.
- `refine { }` supports explicit result binding from parser/refinement functions.
- `refine { }` also supports target-type-driven binding from raw primitives. Left-hand type annotations are recommended
  when inference is ambiguous or when the target refined type is not obvious at the call site, such as
  `let! (email: Email) = rawEmail`.
- Target-type-driven binding is not added to `flow { }`.
- Refined types do not know about application workflow error unions. Application errors are assigned by `Policy` or normal
  `Result.mapError` at the boundary.
- `Policy` lives in `Axial.Flow`.
- `Policy<'env, 'error, 'input, 'output>` has the core shape `'env -> 'input -> Result<'output, 'error>`.
- `Policy` is an environment-aware requirement, not a validation-only abstraction.
- `Policy` constructors include pure lifting, fixed-error lifting, context-aware lifting, composition, pass-through, and
  optional policy execution.
- `Flow.verify` runs a `Policy` by pulling the workflow environment and applying the input.
- `Policy` may adapt outputs from `Check`, `Result`, `Parse`, `Refine`, `Validation.toResult`, FsToolkit helpers, or any
  other function that produces standard F# `Result` values.
- Axial should include pragmatic wrappers around common BCL and FSharp.Core utility gaps when those wrappers strengthen
  the same data-boundary story and do not create a broad miscellaneous utilities package.
- The public coexistence story is neutral: Axial packages are coherent but independent, and they interoperate through
  standard F# `Result` values.
- Documentation should not claim or imply that Axial is explicitly competing with FsToolkit. It should explain that Axial
  can be used with any library that produces `Result`, including FsToolkit.

The core type shapes are:

```fsharp
module Check =
    val notBlank : string -> bool
    val between : 'value -> 'value -> 'value -> bool when 'value : comparison

module Result =
    val guard : ('input -> bool) -> 'error -> 'input -> Result<'input, 'error>
    val require : bool -> 'error -> Result<unit, 'error>
    val fromPredicate : ('input -> bool) -> 'input -> Result<'input, unit>

module Parse =
    val int : string -> Result<int, ParseError>
    val guid : string -> Result<System.Guid, ParseError>

module Refine =
    val email : string -> Result<Email, RefinementError>

type Policy<'env, 'error, 'input, 'output> =
    'env -> 'input -> Result<'output, 'error>
```

## Detailed API Catalog

This catalog is intentionally concrete. It is not a complete final implementation, but it defines the expected homes,
names, and return shapes for the first implementation pass.

### Axial.ErrorHandling.Check

`Check` is a predicate module. It contains no error concepts, no `Result` envelope, no value-preserving helpers, and no
extraction helpers.

```fsharp
namespace Axial.ErrorHandling

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Check =
    // Option predicates
    val isSome : 'value option -> bool
    val isNone : 'value option -> bool
    val isValueSome : 'value voption -> bool
    val isValueNone : 'value voption -> bool

    // Nullable and reference predicates
    val hasValue : System.Nullable<'value> -> bool when 'value: struct
    val hasNoValue : System.Nullable<'value> -> bool when 'value: struct
    val isNull : 'value -> bool when 'value: null
    val notNull : 'value -> bool when 'value: null

    // Result predicates
    val isOk : Result<'value, 'error> -> bool
    val isError : Result<'value, 'error> -> bool

    // String predicates
    val notNullOrEmpty : string -> bool
    val nullOrEmpty : string -> bool
    val emptyString : string -> bool
    val notEmptyString : string -> bool
    val notBlank : string -> bool
    val blank : string -> bool
    val hasMinLength : int -> string -> bool
    val hasMaxLength : int -> string -> bool
    val hasExactLength : int -> string -> bool
    val matchesRegex : string -> string -> bool
    val isEmail : string -> bool
    val isNumeric : string -> bool
    val isAlphaNumeric : string -> bool

    // Equality and comparison predicates
    val equalTo : 'value -> 'value -> bool when 'value: equality
    val notEqualTo : 'value -> 'value -> bool when 'value: equality
    val greaterThan : 'value -> 'value -> bool when 'value: comparison
    val lessThan : 'value -> 'value -> bool when 'value: comparison
    val atLeast : 'value -> 'value -> bool when 'value: comparison
    val atMost : 'value -> 'value -> bool when 'value: comparison
    val between : 'value -> 'value -> 'value -> bool when 'value: comparison
    val positive : 'value -> bool
    val nonNegative : 'value -> bool
    val negative : 'value -> bool
    val nonPositive : 'value -> bool

    // Sequence predicates
    val isEmpty : seq<'value> -> bool
    val notEmpty : seq<'value> -> bool
    val contains : 'value -> seq<'value> -> bool when 'value: equality
    val hasCount : int -> seq<'value> -> bool
    val hasDuplicates : seq<'value> -> bool when 'value: equality
    val hasNoDuplicates : seq<'value> -> bool when 'value: equality
    val isSingle : seq<'value> -> bool
    val atMostOne : seq<'value> -> bool
    val atLeastOne : seq<'value> -> bool
    val moreThanOne : seq<'value> -> bool

    // Predicate combinators only. These return predicates, not Result values.
    val negate : ('input -> bool) -> 'input -> bool
```

Null semantics should be explicit and tested. For the first pass, use the current behavior where `notBlank null` is
false, `blank null` is true, `hasMinLength _ null` is false, `hasMaxLength _ null` is true, and `hasExactLength _ null` is
false.

Common BCL predicates should be included when they remove recurring verbose checks without introducing application-domain
policy. Initial examples are `isEmail`, `isNumeric`, and `isAlphaNumeric`. These should be null-safe. Regex-backed
predicates should use cached compiled regex values where that is appropriate for the target frameworks and AOT/trimming
constraints. Do not overstate these as full standards compliance; for example, `isEmail` is a pragmatic structural check,
not an RFC-complete mailbox validator.

### Axial.ErrorHandling.Result

`Result` owns all fail-fast value preservation, extraction, error assignment, and structural diagnostics. These functions
return standard F# `Result` values.

```fsharp
namespace Axial.ErrorHandling

type CardinalityFailure =
    | ExpectedSingle of observedCount: int
    | ExpectedAtMostOne of observedCount: int
    | ExpectedAtLeastOne
    | ExpectedMoreThanOne of observedCount: int

type StringLengthFailure =
    | ExpectedMinLength of minLength: int * actualLength: int
    | ExpectedMaxLength of maxLength: int * actualLength: int
    | ExpectedExactLength of expectedLength: int * actualLength: int

type RangeFailure<'value> =
    | ExpectedGreaterThan of minimumExclusive: 'value * actual: 'value
    | ExpectedLessThan of maximumExclusive: 'value * actual: 'value
    | ExpectedAtLeast of minimumInclusive: 'value * actual: 'value
    | ExpectedAtMost of maximumInclusive: 'value * actual: 'value
    | ExpectedBetween of minimumInclusive: 'value * maximumInclusive: 'value * actual: 'value

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Result =
    // FSharp.Core-compatible helpers
    val ok : 'value -> Result<'value, 'error>
    val error : 'error -> Result<'value, 'error>
    val map : ('value -> 'next) -> Result<'value, 'error> -> Result<'next, 'error>
    val mapError : ('error -> 'nextError) -> Result<'value, 'error> -> Result<'value, 'nextError>
    val bind : ('value -> Result<'next, 'error>) -> Result<'value, 'error> -> Result<'next, 'error>

    // Lifts and conversions
    val guard : ('input -> bool) -> 'error -> 'input -> Result<'input, 'error>
    val require : bool -> 'error -> Result<unit, 'error>
    val fromPredicate : ('input -> bool) -> 'input -> Result<'input, unit>
    val fromTry : bool * 'value -> Result<'value, unit>
    val fromChoice : Choice<'value, 'error> -> Result<'value, 'error>

    // Trailing conversions
    val toOption : Result<'value, 'error> -> 'value option
    val toValueOption : Result<'value, 'error> -> 'value voption
    val defaultValue : 'value -> Result<'value, 'error> -> 'value

    // Option, value-option, nullable, and result extraction
    val some : 'value option -> Result<'value, unit>
    val none : 'value option -> Result<unit, unit>
    val valueSome : 'value voption -> Result<'value, unit>
    val valueNone : 'value voption -> Result<unit, unit>
    val nullable : System.Nullable<'value> -> Result<'value, unit> when 'value: struct
    val okValue : Result<'value, 'error> -> Result<'value, unit>
    val errorValue : Result<'value, 'error> -> Result<'error, unit>

    // Type-preserving structural guards with caller-supplied errors
    val notBlank : 'error -> string -> Result<string, 'error>
    val notNull : 'error -> 'value -> Result<'value, 'error> when 'value: null
    val notEmpty : 'error -> 'collection -> Result<'collection, 'error> when 'collection :> seq<'value>
    val contains : 'value -> 'error -> 'collection -> Result<'collection, 'error> when 'collection :> seq<'value> and 'value: equality
    val hasNoDuplicates : 'error -> 'collection -> Result<'collection, 'error> when 'collection :> seq<'value> and 'value: equality

    // Structural operations with built-in diagnostics
    val length : int -> int -> string -> Result<string, StringLengthFailure>
    val minLength : int -> string -> Result<string, StringLengthFailure>
    val maxLength : int -> string -> Result<string, StringLengthFailure>
    val exactLength : int -> string -> Result<string, StringLengthFailure>
    val range : 'value -> 'value -> 'value -> Result<'value, RangeFailure<'value>> when 'value: comparison
    val greaterThan : 'value -> 'value -> Result<'value, RangeFailure<'value>> when 'value: comparison
    val lessThan : 'value -> 'value -> Result<'value, RangeFailure<'value>> when 'value: comparison
    val atLeast : 'value -> 'value -> Result<'value, RangeFailure<'value>> when 'value: comparison
    val atMost : 'value -> 'value -> Result<'value, RangeFailure<'value>> when 'value: comparison
    val single : seq<'value> -> Result<'value, CardinalityFailure>
    val atMostOne : seq<'value> -> Result<'value option, CardinalityFailure>
    val atLeastOne : seq<'value> -> Result<seq<'value>, CardinalityFailure>
```

The preferred argument order for caller-supplied errors is `error -> value` for named type-preserving guards such as
`Result.notBlank MissingName rawName`. The preferred argument order for the generic lifter remains
`predicate -> error -> value`, as in `Result.guard Check.notBlank MissingName rawName`.

Trailing converters exist to make the end of a pipeline explicit without importing a larger abstraction layer. They should
be small, obvious, and limited to standard F# shapes. They are not a mandate to recreate a type-class or optics library.

### Axial.Refined.Parse

`Parse` lives in `Axial.Refined` because parsing is part of structural data compilation. It converts untrusted serialized
input into primitives, usually before refinement.

```fsharp
namespace Axial.Refined

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Parse =
    val int : string -> Result<int, ParseError>
    val long : string -> Result<int64, ParseError>
    val decimal : string -> Result<decimal, ParseError>
    val float : string -> Result<float, ParseError>
    val bool : string -> Result<bool, ParseError>
    val guid : string -> Result<System.Guid, ParseError>
    val dateTime : string -> Result<System.DateTime, ParseError>
    val dateTimeOffset : string -> Result<System.DateTimeOffset, ParseError>
    val dateOnly : string -> Result<System.DateOnly, ParseError>
    val timeOnly : string -> Result<System.TimeOnly, ParseError>
    val enum<'enum when 'enum: struct and 'enum : (new: unit -> 'enum) and 'enum :> System.ValueType> : string -> Result<'enum, ParseError>

    // Optional configuration helpers
    val intOption : string option -> int option
    val boolOption : string option -> bool option
    val decimalOption : string option -> decimal option
    val guidOption : string option -> System.Guid option
    val intOrDefault : int -> string -> int
    val boolOrDefault : bool -> string -> bool
    val decimalOrDefault : decimal -> string -> decimal
```

Optional and defaulting parse helpers are included because application configuration and environment variables often
arrive as optional strings. These helpers deliberately return `option` or a raw defaulted value rather than `Result`: they
are convenience adapters for configuration mining, not replacements for the primary fail-fast `Parse.*` functions.

### Axial.Refined.Refine

`Refine` owns smart constructors for structural refined values. Domain-specific applications may add their own refine
modules, but the package should provide core structural examples and the machinery needed by `refine { }`.

```fsharp
namespace Axial.Refined

type ParseError =
    | MissingValue of target: string
    | InvalidFormat of target: string * input: string
    | OutOfRange of target: string * input: string

type RefinementError =
    | ParseFailed of ParseError
    | InvalidFormat of target: string * reason: string
    | OutOfRange of target: string * reason: string
    | MissingValue of target: string
    | InvalidStructure of target: string * reason: string

type NonBlankString
type PositiveInt
type NonEmptyList<'value>

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Refine =
    val nonBlankString : string -> Result<NonBlankString, RefinementError>
    val positiveInt : int -> Result<PositiveInt, RefinementError>
    val nonEmptyList : seq<'value> -> Result<NonEmptyList<'value>, RefinementError>
```

The package should not ship pretend application-specific refined values such as a universal `Email` unless that type is
explicitly accepted as a general-purpose structural type. Examples may use local `Email` and `Age` types in tests or docs.

### Axial.Refined refine computation expression

`refine { }` is the only computation expression that may use target-type-driven raw binding.

```fsharp
let trusted = refine {
    let! count = Parse.int input.RawCount
    let! quantity = Refine.positiveInt count
    return quantity
}

let trustedForm = refine {
    let! (name: NonBlankString) = input.RawName
    let! (quantity: PositiveInt) = input.RawQuantity
    return name, quantity
}
```

Target-type-driven binding may be inferred when the surrounding expression gives enough type information. Use explicit
left-hand type annotations when inference is ambiguous or when the refined target would otherwise be unclear to readers.

### Axial.Flow.Policy

`Policy` lives with `Flow` because it is an environment-aware requirement adapter. It does not require a dependency on
`Axial.ErrorHandling`, `Axial.Refined`, or `Axial.Validation`; it only depends on standard F# `Result` shapes.

```fsharp
namespace Axial.Flow

type Policy<'env, 'error, 'input, 'output> =
    'env -> 'input -> Result<'output, 'error>

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Policy =
    val pure : ('input -> Result<'output, 'innerError>) -> ('innerError -> 'error) -> Policy<'env, 'error, 'input, 'output>
    val withError : ('input -> Result<'output, 'innerError>) -> 'error -> Policy<'env, 'error, 'input, 'output>
    val context : ('env -> 'input -> Result<'output, 'innerError>) -> ('innerError -> 'error) -> Policy<'env, 'error, 'input, 'output>
    val pass : Policy<'env, 'error, 'input, 'input>
    val compose : Policy<'env, 'error, 'input, 'middle> -> Policy<'env, 'error, 'middle, 'output> -> Policy<'env, 'error, 'input, 'output>
    val optional : ('env -> bool) -> Policy<'env, 'error, 'input, 'input> -> Policy<'env, 'error, 'input, 'input>

module Flow =
    val verify : Policy<'env, 'error, 'input, 'output> -> 'input -> Flow<'env, 'error, 'output>
```

`Policy` is not a replacement for `Bind.error` or `Bind.mapError`. `Bind` remains a bind-site adapter for immediate
assignment or mapping. `Policy` is a named, reusable environment-aware requirement that can be defined once and reused
across workflows.

### Collection and traversal utilities

Axial may add a small collection/traversal module for recurring FSharp.Core boilerplate, but its package home should follow
the effect shape:

```fsharp
namespace Axial.ErrorHandling

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Collection =
    val traverseResult : ('input -> Result<'output, 'error>) -> seq<'input> -> Result<'output list, 'error>
    val sequenceResult : seq<Result<'value, 'error>> -> Result<'value list, 'error>

namespace Axial.Flow

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Collection =
    val mapAsyncParallel : ('input -> Async<'output>) -> seq<'input> -> Async<'output array>
```

`traverseResult` belongs with `Axial.ErrorHandling` because it is fail-fast `Result` traversal. Async parallel mapping is
effect orchestration, so its default home is `Axial.Flow`, not `Axial.ErrorHandling`. If `Axial.Flow` already exposes a
more domain-specific parallel traversal API, prefer that API over adding a generic duplicate.

## Prefix Migration Map

Every existing prefixed helper must be removed or renamed into one of the semantic homes below.

```text
Old shape                         New home
Check.whenNotBlank                Result.notBlank
Check.whenNotNull                 Result.notNull
Check.whenNotEmpty                Result.notEmpty
Check.whenContains                Result.contains
Check.whenHasNoDuplicates         Result.hasNoDuplicates
Check.takeSome                    Result.some
Check.takeValueSome               Result.valueSome
Check.takeHasValue                Result.nullable
Check.takeOk                      Result.okValue
Check.takeError                   Result.errorValue
Check.takeHead                    Result.head or Refine.nonEmptyList, depending on desired output
Check.takeSingle                  Result.single
Check.takeAtMostOne               Result.atMostOne
Check.fromPredicate               Result.fromPredicate
Check.fromTry                     Result.fromTry
Check.fromChoice                  Result.fromChoice
Check.orError                     Result.mapError, Result.guard, Policy.withError, or Bind.error depending on boundary
Option.bind (Parse.int >> ...)     Parse.intOption or explicit Parse.int + Result/Option conversion
Manual Result-to-option match      Result.toOption
Manual Result default match        Result.defaultValue
Manual Result traversal loop       Collection.traverseResult
```

If a removed helper only returned `Check<unit>`, it usually becomes a `Check` predicate. If it preserved the original
input, it becomes a `Result` guard. If it extracted a different output shape, it becomes a `Result`, `Parse`, or `Refine`
operation depending on whether the output is a plain value, parsed primitive, or refined type.

The intended workflow pattern is:

```fsharp
module Policies =
    let requireEmail =
        Policy.withError (Result.fromPredicate Check.notBlank) MissingEmail

    let count =
        Policy.withError Parse.int BadCountFormat

    let email =
        Policy.pure Refine.email InvalidEmail

let process input = flow {
    let! count = input.RawCount |> Flow.verify Policies.count
    let! email = input.RawEmail |> Flow.verify Policies.email
    return count, email
}
```

Exact constructor names may change during implementation if the final API reads better, but the package placement and
type responsibilities above are settled for this PRD.

## Testing Decisions

- Tests should assert observable behavior and public type shape, not private helper implementation.
- Add API shape tests that prove `Check` functions return `bool`, not `Result`.
- Add API shape tests that prove the old `Check<'value>` alias and prefixed `when*`, `take*`, and `as*` helpers are gone.
- Add behavior tests for representative `Check` predicates, including null-sensitive string behavior, range predicates,
  email/numeric/alphanumeric predicates, and duplicate detection.
- Add behavior tests for `Result.guard`, `Result.require`, `Result.fromPredicate`, `Result.fromTry`, and structural result
  helpers.
- Add behavior tests for `Result.toOption`, `Result.toValueOption`, and `Result.defaultValue`.
- Add parse tests for every primitive parser in `Axial.Refined.Parse`, including failure behavior.
- Add parse tests for optional and defaulting parse helpers, including `None`, invalid text, and valid text cases.
- Add refinement tests for initial refined values and structural refined types.
- Add `refine { }` tests for explicit result binding and target-type-driven raw binding, including annotated examples for
  clarity and inferred examples where practical.
- Add negative compile-time or API-shape coverage where practical to ensure raw refinement binding does not leak into
  `flow { }`.
- Add `Policy` tests proving pure, fixed-error, context-aware, composed, pass-through, and optional policies all run with
  the expected environment and error mapping behavior.
- Add `Flow.verify` tests proving it injects the current environment into the policy and short-circuits workflow execution
  on policy failure.
- Add interop tests with plain standard F# `Result`-returning functions. Do not require FsToolkit as a test dependency just
  to prove interop unless there is a specific compatibility bug.
- Add traversal tests for fail-fast ordering and first-error short-circuit behavior.
- Add async parallel mapping tests only if the helper is added to `Axial.Flow`, and avoid timing-sensitive assertions.
- Regenerate and validate docs after implementation with `bash scripts/validate-docs.sh`.

## Out Of Scope

- Designing the full set of refined domain value types beyond the initial structural examples.
- Adding target-type-driven refinement to `flow { }`.
- Making `Axial.ErrorHandling` a meta package over `Axial.Validation` and `Axial.Refined`.
- Adding direct package dependencies from `Axial.Flow` to `Axial.ErrorHandling`, `Axial.Refined`, or `Axial.Validation` just
  to support policy adapters.
- Replacing or wrapping FsToolkit APIs.
- Preserving backwards-compatible aliases for removed pre-1.0 `Check` helpers.
- Redesigning `Axial.Validation` in this PRD. Its relevant role is remaining independent and retaining tree-shaped
  diagnostics as a distinct value proposition.
- Designing asynchronous or effectful policies in detail. If policies need async/database work, decide whether to add a
  separate effectful policy shape or return `Flow` directly in a later PRD.
- Building a broad BCL/FSharp.Core replacement library. Utility additions must stay attached to Axial's boundary,
  fail-fast, refinement, validation, or flow story.

## Further Notes

The public story should emphasize composability through standard F# types. Axial does not need to claim ownership over all
error handling in an application. `Axial.ErrorHandling`, `Axial.Refined`, `Axial.Validation`, and `Axial.Flow` are designed
as coherent but independent packages that can be adopted separately.

The `refine { }` CE is intentionally the place for target-type-driven behavior. That keeps the main `flow { }` CE focused
on orchestration and keeps the more inference-sensitive refinement behavior inside a block whose name announces the
operation.

The migration should update source comments and generated/user-facing docs in the same pass as code changes. Current idea
documents that conflict with this PRD should be deleted or folded into `dev-docs/PLAN.md` after the design is accepted.
