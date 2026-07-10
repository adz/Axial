# Model.construct Sketch

Status: parked (2026-07-11). Not accepted architecture. This records why a typed, checked "construct a model from
already-typed field values" function is harder than it looks, every shape tried, and why each was rejected — so the
next attempt doesn't re-derive the same wall from scratch.

## The Motivating Case

`Model.parse` and `Model.reconstruct` both start from *something the schema already knows how to walk* — untyped
`RawInput` for parse, an existing `'model` value (whose fields the schema's own captured getters can read) for
reconstruct. There's a third case neither covers: you already have the field values, correctly typed, just not yet
assembled — `let myStart : DateOnly = ...; let myEnd : DateOnly = ...` — and you want the same guarantee `Model.parse`
gives (field constraints *and* the constructor's cross-field invariant, e.g. `DateRange.Create`'s "start ≤ end")
without hand-calling the raw constructor and silently skipping the schema's own field-level constraints.

## The Wall

`Schema<'model>` carries exactly one type parameter: `'model`. It does not and cannot carry per-field type
information — that's what makes it a uniform type usable by `Model.parse`, `Codec.compile`, `Inspect.model`,
`JsonSchema.generate` alike. But a typed, checked `Model.construct : Schema<'model> -> 'a -> 'b -> Result<'model, _>`
needs the compiler to verify `'a`/`'b` against *this specific schema's* actual fields — information `Schema<'model>`'s
type doesn't have. This is structural, not a missing-effort gap: no factory, no clever generic constraint, and no
amount of `IFieldChainFactory` machinery bridges it, because the field-type information only exists in the type
system *before* `Schema.build`/`buildResult` erase it (in the `SchemaBuilder<'model,'constructor,'remaining,'chain>`
chain), never after.

`Model.parse` doesn't hit this wall because its input (`RawInput`) is a single already-erased type — the schema
interprets pieces of it entirely at runtime, so nothing needs to line up at compile time. `Model.reconstruct` doesn't
hit it either, because its input is exactly `'model`, and the schema already has explicit (non-reflective) getter
closures captured for that exact type at declaration time. `Model.construct`'s desired input — loose scalars, or an
independently-declared record — is neither.

## Shapes Tried, and Why Each Was Rejected

1. **`Model.construct schema arg1 arg2` as an ordinary function.** Impossible for the structural reason above.
2. **`SchemaBuilder.ConstructChecked`, alongside `.Build()`, on the pre-erasure builder.** Genuinely type-safe, zero
   reflection, same `IFieldChainFactory` pattern `Axial.Codec` already uses successfully. Rejected for the ceremony:
   requires keeping the intermediate `builder` value around and calling two separate finishers.
3. **`Schema.buildWithConstruct` returning `Schema<'model> * (typed constructor)` in one call.** Removes the
   two-finisher ceremony (one call, one destructuring `let`), same type safety. Still rejected — it's a new function
   to learn, different from what's written today, and felt like it was adding process to something that's supposed
   to be a simple smart constructor.
4. **Numbered arity-capped overloads (`construct1`, `construct2`, `construct3`, ...) taking `obj`, checked at
   runtime.** No `Schema<'model>` type pollution, no builder ceremony — but an arbitrary cap and a family of
   near-identical function names, "much worse than reflection" in the user's words. Also: `ConstructorApplication`'s
   `create0..create3` were initially (incorrectly) cited as an existing precedent for this cap — they are in fact
   dead code outside `ApiShapeTests.fs`; the real construction path (`TryApplyTrusted: obj array -> Result<...>`) is
   already arity-agnostic, so this shape would have been introducing a limitation, not respecting an existing one.
5. **`Model.construct schema (values: (string * obj) list)`**, mirroring `RawInput.ofNameValues`. Zero reflection,
   zero arity cap, `Schema<'model>` untouched, AOT/Fable-safe, consistent with how `Model.parse` already treats a
   missing field name (a `SchemaError.Required` diagnostic, not a throw) and an extra field name (silently ignored).
   The most defensible of the runtime-checked shapes — still rejected as "much much worse" than the dream syntax.
6. **`Signup.construct { Email = "..."; Age = 12 }` against an independently-declared record**, using
   `FSharpValue.GetRecordFields` to read the literal's fields by reflection. This is the shape that was actually
   wanted. Ruled out definitively, not just as risky: `docs/schema/aot-trimming-fable.md` states "no runtime
   reflection in any core path" as an architectural rule enforced by CI (the NativeAOT probe and the Fable
   JS-surface check), and this shape would fail both.

## Where This Leaves Things

Shape 2 (`SchemaBuilder.ConstructChecked`) is the only one that is simultaneously type-safe, reflection-free, and
AOT/Fable-safe. It was the answer before source generation entered the conversation — see
`schema-source-generation.md`'s "Additional Generation Targets" section for why generation gets closer to the
originally-wanted ergonomics than any hand-written library function can: a generator has full static knowledge of a
*specific* type's fields, the exact knowledge a library function operating on `Schema<'model>` structurally cannot
recover.

**Do not re-attempt shapes 1, 4, or 6** without new information — 1 and 6 are ruled out for structural/architectural
reasons that don't change with more implementation effort, and 4 was rejected on taste grounds that are unlikely to
have changed. Shapes 2, 3, and 5 remain open if `Model.construct` is picked back up before generation exists.
