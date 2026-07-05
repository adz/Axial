# Open Questions From Phases 23–24

This file expands the Phase 25 queue in `dev-docs/TASKS.md` with the context, motivation, and concrete examples needed
to decide each question in depth. Each section ends with the realistic options. When a question is decided, fold the
durable rule into `dev-docs/decisions/README.md` (or a task into `TASKS.md`) and delete the section here.

Baseline numbers referenced throughout come from the recorded BenchmarkDotNet short-job run on the current tree
(`benchmarks/Axial.Benchmarks/CodecSuites.fs`, results in `docs/patterns/benchmarks.md`):

| Measurement | Mean | Allocated |
| --- | --- | --- |
| STJ Serialize | 1.44 µs | 1.11 KB |
| Axial `Json.serialize` | 1.55 µs | 1.44 KB |
| Axial `Json.deserializeBytes` | 2.85 µs | 2.46 KB |
| Axial `Json.deserialize` | 3.10 µs | 2.84 KB |
| STJ Deserialize | 3.11 µs | 2.01 KB |
| Boundary lane (`JsonDocument` → `RawInput` → `Input.parse`) | 19.78 µs | 27.71 KB |

---

## 1. Codec decode allocations: match CodecMapper, not just STJ

**Context.** Decode speed is at parity with `System.Text.Json` (and `deserializeBytes` beats it), but allocations are
~1.4–2x higher (2.46–2.84 KB vs 2.01 KB per aggregate). CodecMapper — the stated performance reference in
`dev-docs/PLAN.md` — comfortably beats STJ on both axes with fixed-arity record decoders that hold field values in
typed mutable locals.

**Why Axial allocates more today.** `Json.compile` builds an `objectDecoder` that, per decoded object:

1. allocates one `Slot<'field>` object per field plus an `ISlot[]` (N+1 allocations per object — this is the price of
   arity-independence; CodecMapper's `RecordDecoder1..8` classes use locals instead);
2. builds F# lists by consing then `List.rev` (one extra pass and the cons cells are the list, so this part is
   inherent to `'item list` targets);
3. `deserialize` (string entry point) additionally pays `Encoding.UTF8.GetBytes` for the whole payload — that is the
   0.38 KB / 0.25 µs gap to `deserializeBytes` and is unavoidable without a `ReadOnlySpan<char>` reader.

**Motivation.** "Compiled codec on par with STJ" is already a good story; "beats STJ" is a *pitch* (and the PLAN's
explicit bar: CodecMapper is the reference *for this shape*). The counterweight: slots are what keep the decoder
arity-independent and reflection-free, which fixed-arity classes in CodecMapper are not (it falls back to
reflection-built `Func<...>` ctors above arity 8).

**Options.**

- **A. Pool the slot arrays.** Each `JsonCodec` keeps a `[ThreadStatic]`/`ObjectPool` of slot sets; slots get a
  `Reset()`. Cuts the N+1 allocations to zero on the steady state. Risk: thread-safety and re-entrancy (nested models
  decode inside the parent's call — needs a pool per plan-depth or rent-per-call).
- **B. Fixed-arity typed decoders for arities 1..N (e.g. 8), slot fallback above.** Mirrors CodecMapper but *without*
  reflection, because `Schema.specialize` can dispatch on field count while the typed chain is in hand. Most code, best
  result; the chain factory would need N specialized `OnComplete` shapes.
- **C. Accept parity, revisit post-1.0.** The two-lane story sells on the 6x lane gap, not on beating STJ.

Measurable target if pursued: `deserializeBytes` ≤ 2.0 µs / ≤ 1.5 KB on the benchmark aggregate.

---

## 2. Stream/`PipeWriter`/async entry points for the codec

**Context.** The public surface is `serialize`/`serializeBytes`/`deserialize`/`deserializeBytes` —
strings and `byte[]`. The minimal-API sample therefore writes:

```fsharp
return Results.Text(Json.serialize Boundary.codec signup, "application/json", statusCode = 201)
```

which materializes the whole payload as a string, which Kestrel then re-encodes to UTF-8.

**Motivation.** ASP.NET Core's native currency is `PipeWriter`/`Stream` and UTF-8 bytes. STJ's minimal-API integration
(`Results.Json`) writes straight to the response pipe. For the codec to be the obvious choice inside ASP.NET handlers
(not just for storage/queues), it needs at minimum:

```fsharp
Json.serializeToStream : JsonCodec<'model> -> Stream -> 'model -> unit        // sync, buffer flushed once
Json.deserializeStreamAsync : JsonCodec<'model> -> Stream -> Task<'model>    // read-to-end then decode
```

and ideally an `Axial.Codec.AspNetCore` (or a helper in the sample) exposing
`CodecResults.json codec model : IResult` handling content type, status code, and pooled buffers.

**Tension.** The decode side reads from a complete `byte[]` today (`ByteSource` is array+offset). True streaming decode
(incremental buffers) is a much bigger change than "read stream to pooled buffer, then decode" — the latter is cheap
and probably all that is needed pre-1.0. Keep `Axial.Codec` dependency-free; ASP.NET conveniences belong in a separate
package or stay in the sample.

**Options:** (a) add stream entry points that buffer internally (small, safe win); (b) also ship an ASP.NET helper
package; (c) leave as-is and let hosts adapt `serializeBytes`.

---

## 3. A "checked codec" mode

**Context.** The codec deliberately skips constraint metadata (`maxLength`, `between`, …): the trusted lane's contract
is "wire shape + required fields + constructor invariants". The boundary lane (`Input.parse`) runs everything but costs
~6x with ~10x allocations, largely because it materializes `RawInput` and accumulates diagnostics.

**Motivation.** There is a real middle case: internal services where the producer is *supposed* to be trusted but bugs
happen — an events queue fed by three teams, a cache written by an older deploy. Today the choice is binary: full
diagnostics lane (expensive, gives redisplay you don't need) or none. A checked codec would run the lowered constraint
checks on already-decoded values and throw/`Error` on the first violation:

```fsharp
let codec = Json.compileWith (fun o -> { o with EnforceConstraints = true }) customerSchema
// decode cost ≈ codec + one Check evaluation per constrained field; no RawInput, no diagnostics accumulation
```

The machinery exists: `Axial.Validation.Schema` already lowers `SchemaConstraint` metadata to executable checks for
`Validation.validate`. But wiring it into `Axial.Codec` would break the package rule that Codec references only
`Axial.Schema` — the check lowering lives in the interpreters package.

**Options:** (a) don't — tell users to `Json.deserialize` then `Validation.validate` (two lines, already possible,
keeps packages clean; cost is one extra model walk); (b) a `Json.deserializeValidated` helper in
`Axial.Validation.Schema` (right dependency direction: interpreters may reference Codec); (c) a codec compile option
with the constraint interpreter duplicated in Codec (rejected unless (b) proves too slow — duplication of lowering
logic is the known trap the one-catalog phase removed).

Option (a) or (b) preserves the invariant that `Axial.Codec` = wire shape only. Benchmark (b) before choosing (c).

---

## 4. Union wire shapes beyond the `{tag, payload}` wrapper

**Context.** `Value.union "type" "value" [...]` fixes the wire convention to an externally-wrapped payload:

```json
{ "type": "card", "value": { "number": "4111", "expiry": "12/28" } }
```

Parsing, the codec, and `JsonSchema.generate` (`oneOf` + `const` discriminator) all assume it.

**Motivation.** Two shapes are extremely common in real APIs and currently inexpressible:

1. **Internally tagged** (serde's `#[serde(tag = "type")]`, STJ's polymorphism): payload fields merged beside the tag —
   `{ "type": "card", "number": "4111", "expiry": "12/28" }`. Most REST APIs in the wild look like this.
2. **Bare enum strings** for payload-less cases: `"status": "shipped"` rather than
   `{ "type": "shipped", "value": {} }`. Today a no-payload DU case needs a dummy payload schema, which is ugly enough
   that users will fall back to hand-written parsing.

**Design sketch.** The union definition already carries per-case payload schemas; internal tagging is only valid when
every payload is an object (nested model) whose field names don't collide with the discriminator — a constraint that
can be checked at `Value.unionInline` construction time. Bare enums are a different, simpler artifact:

```fsharp
Value.unionInline "type" [ UnionCase.create "card" Card tryCard cardSchema; ... ]   // merged fields
Value.enumOf [ "draft", Draft; "published", Published ]                             // string enum
```

JSON Schema lowering: internally-tagged cases become `oneOf` members with the discriminator `const` alongside payload
properties; enums lower to `"enum": [...]`.

**Cost.** Three interpreters (Input.parse, Codec, JsonSchema) × two new shapes, plus Inspect descriptions. This is the
largest item in this file. Decide whether demand is real before building; the wrapper convention is defensible and
zod's `discriminatedUnion` is internally-tagged-only, so alignment with (1) matters more than (2).

---

## 5. Optional fields (`'field option`)

**Context.** Every schema field consumes one constructor argument and must be present: `Input.parse` reports `Missing`
→ `Required`, and the codec raises `missing required field`. `SchemaConstraint.optional` exists but is *metadata only*
— nothing interprets it, and there is no way to declare a `Nickname: string option` field:

```fsharp
type Profile = { Name: string; Nickname: string option }   // cannot be schema-described today
```

**Motivation.** Optional fields are not an edge case; they are most real models. Today's workarounds — defaulting via a
refined schema over sentinel text, or `buildResult` constructors that reinterpret empty strings — are exactly the
"per-consumer re-implementation" the schema group exists to remove. This gap also blocks honest JSON Schema output
(`required` currently lists only fields with the `required` constraint, while the parser actually requires everything —
a real contract/behavior mismatch worth fixing on its own).

**Design sketch.**

```fsharp
Value.optionOf : ValueSchema<'value> -> ValueSchema<'value option>
// Input.parse: Missing/null -> Ok None; present -> Some (parsed with constraints)
// Codec decode: absent or null -> None; encode: None -> omit field (or null, one policy to choose)
// JsonSchema: field drops out of "required"
```

Questions inside the question: does `None` encode as *omitted* or `null` (pick omitted, offer nothing else pre-1.0);
does `optionOf` nest (`option option` — forbid); how does it interact with field-level `required` constraint metadata
(forbid the combination at build time).

**Recommendation-shaped note:** this is the highest-leverage item in the file; every adopter hits it in week one.

---

## 6. `JsonSchema.generate` fidelity: `$schema`, titles, `$defs`

**Context.** The generator emits a compact, draft-agnostic document: no `$schema` marker, no titles/descriptions, and
nested models are inlined at every occurrence — a schema used by ten fields is emitted ten times.

**Motivation.** Consumers are tooling: OpenAPI validators, form generators, LLM structured-output APIs. Concretely:

- OpenAPI 3.1 *is* JSON Schema 2020-12; pinning `"$schema": "https://json-schema.org/draft/2020-12/schema"` (or
  offering `JsonSchema.generateFor OpenApi31`) removes guesswork for validators.
- `SchemaFormat` and constraint messages could carry human text into `title`/`description`, which form generators and
  LLM tool schemas surface directly. Schema currently has no description metadata at all — that would be a new
  `Value.withDescription`/`Schema.describe` authoring surface, not just generator work.
- Without `$defs`, recursive schemas would not terminate (recursion is not expressible in the builder today, so this
  is latent, not broken) and deep reuse bloats documents.

**Options:** (a) minimal: pin `$schema`, keep inlining (one-line change, honest); (b) add `$defs` hoisting keyed by
reference equality of nested definitions; (c) full: description metadata + `$defs` + draft selection. Option (a) now,
(b) when the OpenAPI sample grows a second nested reuse, (c) only with a real consumer.

---

## 7. Promote a UI-metadata interpreter?

**Context.** Two form renderers exist over `Inspect`: the `UiMetadata` prototype in
`tests/Axial.Schema.Tests/SchemaInterpreterPrototypeTests.fs` (controls, required flags, max lengths) and the
hand-rolled HTML in `examples/Axial.Api/Program.fs` (input types, `required`/`maxlength` attributes, error/redisplay
wiring). They encode the same lowering rules twice — the exact drift the "one catalog" phases eliminated elsewhere.

**Motivation.** "One declaration drives UI" is a headline claim; the shipped artifact for it is currently a test file.
A small `Axial.Schema.Ui`-style module (`UiField` records: label, control kind, required, max length, path) would give
Fable clients and server-rendered forms one blessed metadata shape without Axial becoming a UI framework.

**Counterweight.** UI metadata is where scope creep lives (layout, localization, widget options...). The JSON Schema
module was promoted only after the metadata slice was proven; the UI slice has exactly two consumers, both ours.

**Options:** (a) promote the prototype as-is (field list + control kinds, explicitly frozen small); (b) fold the
sample's needs in (per-path error lookup helpers) and rewrite the sample against it — the honest test of sufficiency;
(c) wait for an external consumer. If promoted, the API sample must consume it, or the duplication just moves.

---

## 8. Should `Axial.Codec` join the checked Fable surface?

**Context.** The codec sources carry `FABLE_COMPILER` gates (inherited from the CodecMapper port), but
`scripts/check-fable-js-surface.sh` does not compile the package, no Fable test exercises it, and the docs do not claim
codec-on-Fable. CodecMapper itself genuinely supports Fable — its byte runtime was designed for it.

**Motivation.** The zod comparison leans on "share one declaration between server and browser". Today that sharing
covers *parsing* (schema core + interpreters compile via Fable) but not *serialization*. A Fable client that must POST
JSON hand-writes the encode side or uses `JS.JSON` — acceptable, but the asymmetry is odd given the gates are already
written.

**Cost.** Adding the package to the Fable check is cheap; *keeping* it working is a real maintenance promise
(`ArrayPool`, `SearchValues`, `Utf8Parser` paths are all gated — each future optimization must maintain the JS branch).
Also unclear whether byte-level JSON en/decoding is even the right tool in a JS runtime where `JSON.parse` is native.

**Options:** (a) add to `check-fable-js-surface.sh` and claim support (with a Node round-trip test); (b) explicitly
document codec as .NET-only and strip the dead gates for clarity; (c) leave gates, claim nothing (current state —
worst of both). Pick (a) or (b); (b) is honest if no Fable user asks.

---

## 9. A fused fast boundary path?

**Context.** The boundary lane costs 19.8 µs / 27.7 KB vs the codec's 3.2 µs / 2.8 KB. The overhead is structural:
`JsonDocument` parse → full `RawInput` tree (Maps, lists, boxed scalars as strings) → recursive interpretation with
diagnostics accumulation. Every number is stringified twice (`GetRawText` then re-parse).

**Motivation.** For a high-traffic public API, 20 µs per request is fine — until it isn't. A fused interpreter could
walk `Utf8JsonReader` directly against the schema, run constraints on decoded values, and only *on failure* fall back
to materializing enough context for diagnostics. Happy-path cost would approach codec cost while keeping the same
`ParsedInput` result type.

**Counterweight — redisplay is load-bearing.** `ParsedInput.Input` keeps the raw input so forms can redisplay exactly
what the user typed (`RawInput.redisplayPath`). A fused path that discards raw input on success breaks that contract
silently for form scenarios; JSON APIs never redisplay, forms always might. That suggests the split is by *entry
point*, not by optimization flag: `Input.parse` (raw-retaining, forms) vs a hypothetical `Input.parseUtf8`
(diagnostics-on-failure, no redisplay, API bodies).

**Options:** (a) not now — 20 µs is not a reported problem, and question 1 (codec allocations) pays off first;
(b) prototype `Input.parseUtf8` in the benchmarks project only, promote if the ratio is compelling; (c) build it. Note
(b) is exactly how the codec itself earned promotion.

---

## 10. Where does the STJ adapter live if netstandard2.1 needs it?

**Context.** `RawInput.ofJsonElement`/`ofJsonDocument` are compiled only for `net8.0 && !FABLE_COMPILER`, because
`System.Text.Json` is in-box there and the netstandard2.1 target stays dependency-free. A netstandard2.1 consumer
(older Unity, legacy frameworks) sees a package whose documented functions don't exist on their target — the failure
mode is a confusing "not defined" at compile time.

**Motivation.** This is a placeholder question: no such consumer exists yet. The decision matters only because both
fixes are breaking-ish: adding a TFM-conditional `PackageReference` to `System.Text.Json` changes the dependency story
("dependency-free" becomes "dependency-free on net8.0+"); splitting an `Axial.Validation.Schema.SystemTextJson` package
fragments an API that TASKS deliberately named `RawInput.ofJsonElement` (F# cannot extend a module across assemblies,
so a split package means a different module name — the naming cost that motivated the gate in the first place).

**Options:** (a) wait for a real request (current decision, implicitly); (b) conditional package reference on
netstandard2.1 only — smallest surface change, mild story change; (c) split package with a distinct module name like
`JsonInput`. Default to (a); record (b) as the pre-chosen answer so the request doesn't reopen design.

---

## 11. "Calling from C#" snippets

**Context.** Phase 24's C#-friendliness item was reviewed but produced no C#-facing artifact. The public types are
already C#-usable in the mechanical sense — `JsonCodec<T>` is a sealed class, `ParsedInput<TModel,TError>` exposes
`IsValid`/`Model`/`Errors` properties, `JsonCodecException` is an ordinary exception — but every doc sample is F#, and
schema *authoring* is genuinely F#-shaped (curried constructors, `_.Name` getters, pipelines).

**Motivation.** The realistic C# story is *consume, don't author*: an F# domain project declares schemas; C# host code
compiles codecs, parses requests, and reads diagnostics. That story is one short snippet per page:

```csharp
var codec = Json.compile(Domain.customerSchema);           // module functions are static methods
var customer = Json.deserialize(codec, json);              // throws JsonCodecException with .Path
var parsed = Input.parse(Domain.customerSchema, raw);
if (!parsed.IsValid) foreach (var e in parsed.Errors) ...  // Diagnostic<SchemaError> list
```

Before writing docs, verify the ergonomics honestly: F# module functions surface as static methods but curried ones
become `FSharpFunc` chains in C# — check which of `Json.*`/`Input.*` compile to clean static signatures and which
need `[<CompiledName>]` or tupled overloads. That audit may produce small API work (e.g. C#-friendly overloads on a
`JsonCodec.Deserialize(string)` member) rather than just prose.

**Options:** (a) one "From C#" section on the codec page and the input-sources page after the ergonomics audit;
(b) full C# guide page; (c) skip until a C# consumer appears. The audit itself is cheap and worth doing regardless —
it either validates or falsifies a claim the comparison pages already make implicitly.

---

## 12. Docgen target-framework skew

**Context.** After phase 23, `scripts/docgen/Program.fs` reads `Axial.Validation.Schema` from its `net8.0` build (so
`ofJsonElement`/`ofJsonDocument` produce reference pages) but still reads `Axial.Schema` from `netstandard2.1`. Net
effect: the reference docs describe a surface that exists on *no single* target — STJ adapters documented (net8-only)
while `Value.date`/`Schema.date` (gated `NET6_0_OR_GREATER`) are silently absent from the reference even though most
users are on net8+ and will use them.

**Motivation.** Reference docs should describe one coherent surface, and the acceptance check "generated reference docs
match source comments" is currently only true per-assembly-per-TFM. The `date` gap is the visible symptom: a documented
builder shorthand (`Schema.date` appears in guides) with no reference page.

**Options:** (a) standardize all docgen inputs on `net8.0` builds and annotate the few net8-only members with a
"netstandard2.1: not available" line in their XML remarks (the STJ adapter already says this; `Value.date` should
too) — most accurate for the majority audience; (b) generate from netstandard2.1 and hide net8 extras (loses the
adapters again); (c) dual-generate with availability tables (overkill at this package count). Option (a) is the obvious
pick; the real work is auditing which members are TFM-gated and making their remarks say so.

---

## Suggested order

Cheap and high-leverage first: **5 (optional fields)** unblocks real models and fixes a contract mismatch; **6a**
(`$schema` pin) and **12** (docgen on net8.0) are small correctness wins; **2a** (stream entry points) makes the codec
the obvious ASP.NET choice; **1** (decode allocations) when performance becomes a pitch line; **11** (C# audit) before
any C#-facing marketing. **4 (union shapes)**, **7 (UI metadata)**, **9 (fused boundary path)** wait for demand
signals; **3**, **8**, **10** are decisions to record more than code to write.
