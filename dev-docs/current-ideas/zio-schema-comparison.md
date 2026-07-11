# ZIO Schema Deep Dive (design input, 2026-07-11)

Source-level review of zio/zio-schema (cloned at depth 1, main branch) as a calibration point for the Axial.Schema
generation/contracts direction. Scala has compile-time metaprogramming F# lacks, and Axial rejects runtime
reflection; the point here is what ZIO Schema *achieved*, what its user surface costs, and where our plan differs
on purpose versus differs by gap.

## What ZIO Schema Is

`Schema[A]` is a first-class sealed ADT reifying the structure of an existing Scala type: `CaseClass1..22`,
`Enum1..22` (+ `CaseSet` for beyond 22), `GenericRecord`, collections (`Sequence`, `Set`, `Map`, `NonEmpty*`),
`Tuple2`, `Either`, `Fallback`, `Optional`, `Transform` (invariant map — their `Value.refined` analogue),
`Primitive` (very wide: full java.time, currency), `Lazy` (recursion), `Dynamic`. One 10,330-line `Schema.scala`.

Everything else is an interpreter over that ADT:

- **Codecs**: JSON, Protobuf, Avro, Thrift, BSON, MessagePack, XML — each a separate module over the same schema.
- **DynamicValue**: a typed, self-describing value tree (`toDynamic`/`fromDynamic`) — schema-aware generic data.
- **Diff/Patch**: structural diffing of any two values of a schema'd type, patches applicable and serializable.
- **MetaSchema**: the schema of schemas — schemas serialize as data, enabling wire-transmitted schemas and
  structural compatibility checks.
- **Migration**: `Schema.migrate(newSchema)` auto-derives a value migration from the MetaSchema diff (add node,
  delete node, relabel, optional/require, type change); manual migration = applying a `Chunk[Migration]` of those
  same structural steps to a `DynamicValue`.
- **Optics**: `makeAccessors(b: AccessorBuilder)` derives Lens/Prism/Traversal with *caller-supplied* optic types
  (the builder is abstract over `Lens[F,S,A]` etc., so any optics library plugs in). Field names are tracked at the
  type level (`Field1 <: Singleton with String`).
- **Validation**: `Validation[A]` = a boolean algebra (`Bool[Predicate[A]]`) over structural predicates (string
  length/regex, numeric comparisons, time formats), attached per-field, run by `Schema.validate(value):
  Chunk[ValidationError]`.
- **Derived misc**: ordering, equality, default values, zio-test `Gen` derivation (test data from schema).

User surface: `case class Person(name: String, age: Int)` plus one line —
`implicit val schema: Schema[Person] = DeriveSchema.gen[Person]` — and every interpreter above lights up through
implicits. The macro is ~725 lines (Scala 3) / ~807 lines (Scala 2), maintained twice. 17 annotations tune
derivation (`@fieldName`, aliases, `@discriminatorName`, `@rejectExtraFields`, `@transientField`, `@validate`, ...).

## Where They Hit Our Walls (and what they did)

- **The arity wall is real for them too.** No variadic generics in Scala either: `CaseClass1..22` and `Enum1..22`
  are hand-rolled arity families — roughly 8,000 of `Schema.scala`'s 10,330 lines. Beyond 22: fall back to
  `CaseSet`/`GenericRecord`. Axial's progressive typed chain (`FieldsAppend`/`IFieldChain`) has no cap and no
  parallel class family. On this axis our core is genuinely ahead.
- **Generic construction is boxed and marked unsafe.** `Record.construct(values: Chunk[Any])(implicit unsafe:
  Unsafe): Either[String, Z]` — the exact `obj array` shape we refused to ship as `Model.construct`, shipped behind
  an `Unsafe` marker. Their typed `construct` fields exist per arity class. Our draft-record → `validate` route is
  strictly more principled than both.
- **Macros are their generator.** `DeriveSchema.gen` reads the case class at compile time — zero extra artifact,
  invisible output, refactors stay in-language. That is the ergonomic bar for low-friction adoption. But macro
  derivation can only *describe existing types* — it cannot mint new ones. Our generator owns the emitted file, so
  it can create things their approach structurally cannot: the draft record, the parts record, the
  private-constructor trusted wrapper. Trust-by-construction is not expressible as derivation.

## What They Have That We Lack (honest gap list)

1. **Recursion** (`Schema.Lazy`) — our builder cannot express recursive models at all (known, now written down).
2. **DynamicValue** — a typed generic intermediate; our `RawInput` is boundary-only and stringly.
3. **Diff/Patch** derived from schema.
4. **MetaSchema / serializable schemas** — directly relevant to the remote-config scenario (ship the schema to the
   editor as data rather than as compiled Fable code); worth remembering when contracts get built.
5. **Automatic structural migration** — cheap evolution for unambiguous changes. Deliberate contrast: our
   contract sketch says migrations are *manual, typed business logic*; theirs can only express structural moves
   (their docs' example literally deletes a field). Keep our position; steal the MetaSchema-diff idea as
   *diagnostics* (version-gap warnings) not as execution.
6. **Codec breadth** — six wire formats vs our one (JSON).
7. **Optics as an open interface** — `AccessorBuilder` abstracts the optic representation; also Prisms (union
   cases) and Traversals (collections). Our `FieldRef` is a fixed record covering the Lens case only.
8. **Tuples/Either/Set/NonEmpty collections; huge primitive set.**
9. **Test-data generation** derived from schema — a genuinely attractive future interpreter for us.

## What We Have That They Lack

1. **Trust as the organizing principle.** ZIO Schema case classes stay freely constructible; there is no
   draft/validated distinction anywhere. Validation is advisory and detached: `Schema.validate` returns a flat
   `Chunk[ValidationError]` (values, no paths), is *not* run by any codec on decode, and nothing in the type system
   records that it ran. "Parse, don't validate" is not their concern; it is our center.
2. **Path-aware accumulated boundary diagnostics + raw retention + redisplay** (`ParsedInput`). Their JSON decode
   is fail-fast with zio-json error strings.
3. **Constraints as portable metadata** lowering simultaneously to executable checks, JSON Schema documents, and
   docs/UI metadata. ZIO Schema has *no JSON Schema generation module at all*; its validation predicates are
   executable but feed no document generator.
4. **Constructor invariants in the schema** (`buildResult`) enforced by parse and reconstruct.
5. **Refined-type integration** — proof-carrying field types bridged into schemas; their `Transform` node is the
   mechanical analogue but carries no proof discipline.
6. **The wire/domain split with frozen versioned contracts** and typed manual migrations as a first-class design.
7. **Uncapped compile-checked construction alignment** (typed chain) without `Unsafe` or 22-arity families.

## What This Changes About Our Plan

- **Confirms the differentiator.** The two pillars we've been building — trusted construction and path-aware
  boundary diagnostics — are precisely what the most mature schema library in the adjacent ecosystem does *not*
  do. We are not reinventing ZIO Schema badly; we are building the part they skipped.
- **Confirms the typed chain and the draft/validate route.** Their alternatives to both are the two shapes we
  rejected (arity families, unsafe boxed construction).
- **Sets the ergonomic bar honestly.** Their derived one-liner beats anything we can offer for "I have a type,
  give me a codec." Our *manual* pipeline crushes their *manual* story (their docs' `CaseClass2` example with
  `set0` lambdas is far worse than our builder), but our generated route costs an external file and a build step
  where theirs costs one line. The `.model`/domain-tier generation must therefore justify itself by what
  derivation can't do (drafts, trusted wrappers, `create`) — which it does — not by convenience, where it loses.
- **Names the known structural holes**: no recursion in the builder; optics limited to field lenses (no
  prism/traversal analogue); one wire format. None block the current work; all are now recorded.
- **Two ideas worth stealing later**: schema-as-data (MetaSchema) for the remote-config editor, and test-data
  generation as an interpreter.
