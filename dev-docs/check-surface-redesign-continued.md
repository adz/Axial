This is the logical next step. Filling in those `n/a` gaps moves the matrix from a pragmatic list of common helpers to a mathematically complete, highly predictable domain-specific language. For LLMs and human muscle memory alike, **uniformity beats sparseness every single time.**

Let’s systematically expand those missing intersections by adhering strictly to your three architectural rules:

1. `unprefixed` $\rightarrow$ Predicate test returning `Result<unit, 'err>`
2. `take` $\rightarrow$ Structural extraction/narrowing returning `Result<'a, 'err>`
3. `when` $\rightarrow$ Value-preserving gate returning `Result<input, 'err>`

---

## The Complete, Expanded Semantics Matrix

Legend: 🔁 renamed · 📦 moved · ✨ new expansion · ✓ existing/unchanged
*Note: `Error` types follow the expanded diagnostic pattern (`StringLengthFailure`, `RangeFailure<'a>`, etc.) discussed previously.*

| Group | Concept | Predicate $\rightarrow$ `Result<unit, 'err>` | Take $\rightarrow$ `Result<'a, 'err>` | When $\rightarrow$ `Result<input, 'err>` |
| --- | --- | --- | --- | --- |
| **bool** | true | `isTrue` ✓ | — | `whenTrue` ✨ `-> Result<bool,_>` |
|  | false | `isFalse` ✓ | — | `whenFalse` ✨ `-> Result<bool,_>` |
| **option** | some | `isSome` 🔁 | `takeSome` 📦 `-> 'a` | `whenSome` 📦 `-> 'a option` |
|  | none | `isNone` 🔁 | — | `whenNone` ✨ `-> Result<'a option,_>` |
| **voption** | some | `isValueSome` 🔁 | `takeValueSome` 📦 `-> 'a` | `whenValueSome` 📦 `-> 'a voption` |
|  | none | `isValueNone` 🔁 | — | `whenValueNone` ✨ `-> Result<'a voption,_>` |
| **Nullable** | has value | `hasValue` ✓ | `takeHasValue` 📦 `-> 'a` | `whenHasValue` 📦 `-> Nullable<'a>` |
|  | no value | `hasNoValue` ✓ | — | `whenHasNoValue` ✨ `-> Nullable<'a>` |
| **ref null** | non-null | `notNull` ✓ | — | `whenNotNull` 📦 `-> 'a` |
|  | null | `isNull` ✓ | — | `whenNull` ✨ `-> 'a` |
| **Result** | ok | `isOk` ✨ | `takeOk` ✨ `-> 'a` | `whenOk` ✨ `-> Result<'a,'e>` |
|  | error | `isError` ✨ | `takeError` ✨ `-> 'e` | `whenError` ✨ `-> Result<'a,'e>` |
| **string** | not blank | `notBlank` ✓ | — | `whenNotBlank` 📦 `-> string` |
|  | blank | `blank` ✓ | — | `whenBlank` ✨ `-> string` |
|  | not null/empty | `notNullOrEmpty` ✓ | — | `whenNotNullOrEmpty` 📦 `-> string` |
|  | null/empty | `nullOrEmpty` ✓ | — | `whenNullOrEmpty` ✨ `-> string` |
|  | min length | `minLength n` ✨ | — | `whenMinLength n` ✨ `-> string` |
|  | max length | `maxLength n` ✨ | — | `whenMaxLength n` ✨ `-> string` |
|  | exact length | `exactLength n` ✨ | — | `whenExactLength n` ✨ `-> string` |
|  | regex | `matches pattern` ✨ | — | `whenMatches pattern` ✨ `-> string` |
| **seq** | not empty | `notEmpty` ✓ | — | `whenNotEmpty` 📦 `-> 'coll` |
|  | empty | `empty` ✓ | — | `whenEmpty` ✨ `-> 'coll` |
|  | contains | `contains x` ✓ | — | `whenContains x` ✨ `-> 'coll` |
|  | count | `hasCount n` ✓ | — | `whenCount n` ✨ `-> 'coll` |
|  | duplicates | `hasDuplicates` ✓ | — | `whenHasDuplicates` ✨ `-> 'coll` |
| **cardinality** | exactly one | `isSingle` 🔁 | `takeSingle` 📦 `-> 'a` (CF) | `whenSingle` 📦 `-> 'coll` (CF) |
|  | at most one | `atMostOne` ✓ | `takeAtMostOne` 📦 `-> 'a opt` (CF) | `whenAtMostOne` 📦 `-> 'coll` (CF) |
|  | at least one | `atLeastOne` ✓ | — | `whenAtLeastOne` ✨ `-> 'coll` (CF) |
|  | more than one | `moreThanOne` ✓ | — | `whenMoreThanOne` ✨ `-> 'coll` (CF) |
| **equality** | equal | `equalTo x` ✓ | — | `whenEqualTo x` ✨ `-> 'a` |
|  | not equal | `notEqualTo x` ✓ | — | `whenNotEqualTo x` ✨ `-> 'a` |
| **comparison** | $>$ | `greaterThan n` ✨ | — | `whenGreaterThan n` ✨ `-> 'a` |
|  | $<$ | `lessThan n` ✨ | — | `whenLessThan n` ✨ `-> 'a` |
|  | $>=$ | `atLeast n` ✨ | — | `whenAtLeast n` ✨ `-> 'a` |
|  | $<=$ | `atMost n` ✨ | — | `whenAtMost n` ✨ `-> 'a` |
|  | range | `between lo hi` ✨ | — | `whenBetween lo hi` ✨ `-> 'a` |
| **numeric** | $> 0$ | `positive` ✨ | — | `whenPositive` ✨ `-> 'a` |
|  | $>= 0$ | `nonNegative` ✨ | — | `whenNonNegative` ✨ `-> 'a` |
|  | $< 0$ | `negative` ✨ | — | `whenNegative` ✨ `-> 'a` |

---

## Design Rationale for the New Expansions

### 1. The `Result` Complete Set (`takeError`, `whenOk`, `whenError`)

You noted previously that nesting results can get unpleasant, but providing the complete set makes the combinators highly regular when working directly with pipeline validation boundaries.

* **`takeError`**: Maps a `Result<'a, 'e>` to a `Result<'e, unit>`. Perfect when you explicitly want to intercept, inspect, or isolate a failure pathway.
* **`whenOk` / `whenError**`: Standard structural gates. They assert the branch without altering the internal shape, passing the original `Result<'a, 'e>` right through.

### 2. Universal Logic Gates for Valuables (`whenTrue`, `whenFalse`, `whenNull`, `whenBlank`)

Even though a value like `blank` or `false` represents an empty or invalid state in domain terms, developers frequently write inverted code paths (e.g., executing a fallback only *when* a string is blank).

```fsharp
// Regular syntax makes inverted routing trivial:
let fallbackDescription = 
    input.Description
    |> Check.whenBlank
    |> Result.map (fun _ -> "No description provided.")

```

### 3. Collection Sizing Gates (`whenCount n`, `whenHasDuplicates`)

Adding `whenCount` and `whenHasDuplicates` allows structural inspection of data collections without needing to count/filter manually or drop down to raw F# core functions midway through a validation chain.

### 4. Non-Primitive Targets (`whenNotEqualTo`, `whenLessThan`)

Completing the equality and comparison matrices ensures that any filterable rule can act as a pipeline checkpoint. An LLM trying to guess how to check if an ID is validly non-zero or not equal to a system sentinel value will hit `whenNotEqualTo` intuitively.

## Error-Type Symmetry

With this matrix fully uniform, your diagnostic error types map 1:1 with the concepts:

* `whenMinLength`, `whenMaxLength`, `whenExactLength` all leverage `StringLengthFailure`.
* `greaterThan`, `lessThan`, `atLeast`, `atMost`, `between` all leverage `RangeFailure<'a>`.
* `isSingle`, `atMostOne`, `atLeastOne`, `moreThanOne` all leverage `CardinalityFailure`.





....but we are missing hasNoDuplicates... so:

Yes, good catch. Adding **`hasNoDuplicates`** fixes the missing inverse for sequence uniqueness.

If we scrutinize the matrix for complete conceptual symmetry—ensuring that every concept has its logical inverse and every type has its expected equivalents—there are a few other subtle gaps.

Here is what is missing to make the API completely air-tight and intuitive for both humans and LLMs:

---

## 1. Missing Inverses & Completions

### Sequence Uniqueness

* **`hasNoDuplicates`** (Predicate) $\rightarrow$ `Result<unit, _>`
* **`whenHasNoDuplicates`** (When) $\rightarrow$ `Result<'coll, _>`

> *Complements `hasDuplicates` / `whenHasDuplicates`.*

### String Whitespace Handling

While `blank` and `notBlank` handle whitespace-only strings, developers often need to check for literal, raw emptiness (length 0) regardless of whitespace, especially when performance or exact data formats matter.

* **`emptyString`** (Predicate) $\rightarrow$ `Result<unit, _>`
* **`whenEmptyString`** (When) $\rightarrow$ `Result<string, _>`
* **`notEmptyString`** (Predicate) $\rightarrow$ `Result<unit, _>`
* **`whenNotEmptyString`** (When) $\rightarrow$ `Result<string, _>`

### Numeric Sign Complete Set

We have `positive` ($>0$), `negative` ($<0$), and `nonNegative` ($>=0$). We are missing the inverse inclusion:

* **`nonPositive`** (Predicate) $\rightarrow$ `Result<unit, _>` ($\le 0$, alias to `atMost 0`)
* **`whenNonPositive`** (When) $\rightarrow$ `Result<'a, _>`

---

## 2. Missing Structural "Take" Operations

If **`take`** means *"safe extraction where the success is a useful inner value or a deliberately different success shape,"* a reference null check does not qualify on its own: `whenNotNull` already returns the same reference type, and the library does not define a `NonNull<'T>` wrapper.

### Collection Extraction (Head / First)

For cardinality, `isSingle` maps to `takeSingle` (extracting the lone element). For a general sequence, checking `notEmpty` or `atLeastOne` usually means the developer's immediate next step is to grab the first element.

* **`takeHead`** (Take) $\rightarrow$ `Result<'a, _>`

> *Asserts the sequence is not empty and extracts `Seq.head`. This elegantly unifies sequence validation with item extraction.*

---

## Updated Sub-Sections of the Matrix

Here is how these additions slot into the existing architecture:

| Group | Concept | Predicate | Take | When |
| --- | --- | --- | --- | --- |
| **ref null** | non-null | `notNull` | — | `whenNotNull` |
| **string** | empty (length 0) | `emptyString` ✨ | — | `whenEmptyString` ✨ |
|  | not empty (length > 0) | `notEmptyString` ✨ | — | `whenNotEmptyString` ✨ |
| **seq** | head / first | — | `takeHead` ✨ `-> 'a` | — |
|  | no duplicates | `hasNoDuplicates` ✨ | — | `whenHasNoDuplicates` ✨ |
| **numeric** | $\le 0$ | `nonPositive` ✨ | — | `whenNonPositive` ✨ |

These additions ensure that no matter which direction an LLM or developer approaches a validation problem (positive vs. negative space, structural vs. gate), the expected function exists.


WE ALSO NEED TO USE FUNCTIONS THAT CAN STAND ALONE SO 'matches' IS LIKELY A PROBLEM.

ALSO lets do a much better starting doc in Validation & Results that talks about the base Result type in F#, its' basic map/bind functionality, and what we then add: Result ce, as well as checks, validation+diagnostics - and how Result ce + Check are all based on the core Result type, while Validation+Diagnositcs work with a Result like type, expanded to allow for accumulation. This is all distinct from flow, and could be a self-contained library (which we might go with, although splitting causes issues with compatibility in early days when we are under unstable apis). 

THEN, add a tutorial relating to these - totally independent of flow, first base Check, adding in Result. 

So, when we come into the current tutorilas, we are all about task|async + reader.
