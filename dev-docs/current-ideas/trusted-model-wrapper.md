# Trusted&lt;'model&gt; Wrapper Sketch

Status: parked (2026-07-11). Not accepted architecture. This records an alternative, stronger enforcement mechanism
for "an invalid model is never constructed," found while working out how to actually close the gap `private` alone
leaves open, and deliberately not pursued yet because it's a bigger structural change than a documentation fix.

## The Gap

`docs/schema/trusted-construction.md` claims "an invalid model is never constructed." That's only true if the
model's record type is `private`, and even then only partially: F# privacy is scoped to the whole file/module a type
is declared in, not to a single function. A `private` record still lets *any other code in the same file* write a
second, competing `{ Field = ... }` literal that bypasses the intended smart constructor / schema entirely — nothing
enforces that the constructor passed to `Schema.recordFor` is the *only* code with construction access.

## Two Ways To Actually Close It

1. **`.fsi` signature files** (currently recommended — see `docs/schema/trusted-construction.md`). A signature file
   can expose only `val Schema : Schema<Signup>` and hide the record's field layout entirely from every other file,
   including files in the same assembly. This is a real, existing F# feature, costs nothing on the library side, and
   is airtight for the realistic threat model (library consumers, not the type's own author being careless in the
   same file). Recommended default.
2. **An opaque, library-owned wrapper type** — `type Trusted<'model> = private Trusted of 'model`, with the `Trusted`
   case constructor living inside `Axial.Schema`'s own module. No code outside `Axial.Schema` — not even code in the
   same file as the user's model declaration — can fabricate a `Trusted<'model>` except through `Model.parse`/
   `Model.reconstruct`/(eventually) `Model.construct`. This doesn't depend on the model author doing anything right;
   the enforcement is structural, not conventional.

Option 2 is strictly stronger than option 1, and doesn't require the model's own file to be split out with a
signature file — but it costs much more: `ParsedInput<'model,_>` becomes `ParsedInput<Trusted<'model>,_>`, and every
interpreter that currently deals in bare `'model` (`Codec`, `Rules`, `Inspect`, `JsonSchema`) would need to either
change to work in terms of `Trusted<'model>` or provide an unwrap step. That's a real, cross-cutting API change, not
a small addition — treat it as its own design pass with its own migration plan, not something to fold into an
unrelated batch of work.

## Why This Matters For `Model.construct` And Source Generation

`dev-docs/current-ideas/schema-source-generation.md`'s "Private Constructors" section notes that generated schemas
cannot target `private` representations, because a generator always emits a separate file, and same-file emission
(Myriad-style partial types) doesn't exist in F#. That's the same file-scope problem `.fsi` files solve for
hand-written types — but a generator can't emit a matching `.fsi` for a type it doesn't own. `Trusted<'model>` sidesteps
this cleanly: since its constructor lives in `Axial.Schema`'s own module rather than in either the user's file or a
generated file, generated `construct`/optic code could target genuinely trusted types without needing the generator
to also manage signature-file emission. If source generation and `Model.construct` are ever picked up together, this
connection is worth revisiting — `Trusted<'model>` may turn out to be the thing that makes *generated* checked
construction viable for private/trusted types, even though it's not needed for the `.fsi` + hand-written case.

## Promotion Criteria

Do not start on this until either (a) a concrete case shows up where `.fsi`-file discipline is genuinely
insufficient (not just theoretically weaker), or (b) source generation is picked up and generated code needs to
target genuinely trusted (not just public/`internal`) model types.
