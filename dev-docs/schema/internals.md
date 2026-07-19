# Axial.Schema internals

How the schema library is put together, for someone working on it for the first time.
Read this before touching `src/Axial.Schema`. Everything here is checkable against the source; when the
source and this document disagree, fix this document.

## The one idea

A `Schema<'value>` is a *description*, not behavior. It records shape, constraints, construction, and
metadata. Everything that does work — parsing, checking, codecs, JSON Schema, UI metadata, docs — is an
*interpreter* that walks the description. When you add a feature, decide first: is this new description
metadata, or a new interpreter over existing metadata? Most features are the second.

## File map (compile order matters — F# reads top to bottom)

Each file opens with a short grounding comment saying what it holds and where it fits; the table is
the overview, the comments are the ground truth.

| File | What it holds |
|---|---|
| `Platform.fs` | Small shims so the same code compiles for .NET, Fable, and AOT. |
| `Derive.fs` | Inert attributes read by `schemagen` from source text. Never used at runtime. |
| `SchemaError.fs` | `SchemaError` — the portable error vocabulary interpreters report. |
| `Names.fs` | `ExternalFieldName`, `FieldOrder` — validated wrappers for a field's wire name and position. |
| `Constraints.fs` | `ConstraintMetadata`, `Constraint` — the portable constraint vocabulary (no check logic). |
| `Definitions.fs` | The type-erased description layer: `ConstructorApplication`, value-shape definitions, `FieldDescriptor`/`ModelSchemaDefinition`, typed `FieldDefinition`/`Field`. |
| `RecordPlan.fs` | The typed record plan: `IShapeFields` chain nodes, `ShapeClosure`, `IRecordPlanCompiler`/`CompiledRecordPlan`. |
| `SchemaType.fs` | `Schema<'model>` itself plus `UnionCase`/`EnumCase` companions. |
| `ValueCatalog.fs` | The internal `Value` module behind the `Schema.*` value catalog. |
| `SchemaCore.fs` | The internal core module `SchemaApi.fs` re-exports; `closeTotal`/`closeResult`. |
| `Shape.fs` | The constructor-last authoring surface: `DefineShape`, `ObjectShape`, `Syntax` (module and type), `Schema.admit`. |
| `SchemaValidation.fs` | Constraint interpretation: each portable constraint's runtime meaning. |
| `RetainedParseResult.fs` | `RetainedParseResult` — parse results plus redisplay data. |
| `FieldRef.fs` | `FieldRef` — typed get/set field references used by rules and generated code. |
| `ContextRules.fs` | Contextual (cross-field, per-context) rules over built schemas. |
| `Parsing.fs` | The parse/check interpreter (`SchemaParsing`). |
| `SchemaApi.fs` | The public `Schema` module. A facade: every function delegates to an internal implementation. |
| `Contract.fs` | Versioned contracts: version detection + stepwise migrations. |
| `RefinedSchemas.fs` | Stock refined schemas (ranges etc.). |
| `Inspection.fs` | The metadata interpreter: `Inspect.model`, `Inspect.schema`. |

`Data` lives in its own dependency-free package (`src/Axial.Data`).

The JSON Schema interpreter lives in its own package (`src/Axial.Schema.JsonSchema`), and the compiled
JSON codecs in `src/Axial.Schema.Codec`; both keep the `Axial.Schema` namespace family.

## Map of the core files (former Schema.fs), in compile order:

1. **`ExternalFieldName`, `FieldOrder`** — validated wrappers for a field's wire name and position.
2. **`ConstraintMetadata`, `Constraint`** — one constraint = a code (`"minLength"`), typed metadata for
   interpreters, and arguments. Constraints carry no check logic; `SchemaValidation.fs` interprets them.
3. **`ConstructorApplication<'model>`** — how a model gets built: an argument count plus
   `ApplyTrusted: obj array -> 'model` and `TryApplyTrusted: obj array -> Result<'model, string>`.
   This is the *only* place construction is type-erased, and it is built from typed closures — never
   reflection.
4. **Value definitions** (`ValueSchemaDefinition`, `ValueSchemaShape`) — the type-erased description of
   one value: primitive kind, option, collection, map, union, enum, refined, deferred, or a nested model.
5. **`FieldDescriptor<'model>` / `ModelSchemaDefinition<'model>`** — the type-erased description of a
   record: ordered fields (wire name, getter, value schema, constraints) plus its
   `ConstructorApplication`. Interpreters that don't need field value types work from this.
6. **`FieldDefinition<'model,'value>` / `Field<'model,'value>`** — the *typed* view of one field.
7. **The typed record plan** (`IRecordPlanCompiler`, `IRecordPlanState`, internal `IShapeFields`) — the
   interpreter-facing typed view retained when a constructor-last shape closes. `Axial.Codec` folds it
   into direct typed encoders and decoders. Checked constructors use the same compiled path as total constructors.
8. **`Schema<'model>`** — a sealed wrapper over `SchemaDefinition` (model or value) plus an optional compiled
   record plan.
9. **`Value` module** — value-schema constructors (`text`, `int`, `list`, `refine`, `union`, …).
10. **`SchemaCore`** — the internal module `SchemaApi.fs` re-exports.

## Data flow

Constructor-last authoring produces a `ModelSchemaDefinition`:

```
Schema.define |> field ... |> construct ctor        (Shape.fs)
```

Closing the shape produces ordered `FieldDescriptor`s, a `ConstructorApplication`, and a typed record
plan for compilers that need direct constructor application.

Parsing (`SchemaValidation.fs`): for each field, look up the wire name in `Data`, parse the value
shape, run constraints, collect diagnostics by path. Only if every field succeeded, call
`TryApplyTrusted` with the boxed arguments; a constructor `Error` becomes
`SchemaError.ConstructorFailed` placed by `Schema.constructorErrorAt`.

Checking (`Schema.check`): same pipeline, but values come from the model's getters instead of structured data.

## The constructor-last surface (Shape.fs)

`Schema.define<'model>` returns a `DefineShape<'model>`; the first `field` turns it into an
`ObjectShape<'model,'constructor,'remaining,'last>`. The phantom parameters record the curried
constructor type the declared fields demand: `'constructor` is the full constructor, `'remaining` is
what is left of it after the declared fields consume their arguments, and `'last` is the current
field's value type. Runtime state is a boxed committed `IShapeFields` chain plus the current field as
a boxed `FieldDefinition<'model,'last>` — the cursor.

- Each `field`/`fieldWith` commits the cursor onto the chain (`ShapeFieldsAppend`) and installs the
  new field, peeling one curried argument at the type level: a shape at `'f -> 'n` becomes a shape at
  `'n`. This is ordinary unification, so **field count is unbounded** — there is no per-arity code.
- `constrain (c: Constraint<'v>)` only accepts a shape whose `'last` is `'v` — a misplaced constraint
  is a type mismatch at the `constrain` line. It rewrites the cursor in place.
- `construct (f: 'constructor)` requires `'remaining = 'model` (`constructResult` requires
  `Result<'model, string>`); both are plain functions that commit the cursor and close via
  `ShapeClosure` into erased metadata plus the typed record plan. A constructor mismatch surfaces at
  the closing call with concrete types.
- `field`/`fieldWith` are inline and dispatch on the shape via an SRTP static member (`Field`) present
  on both `DefineShape` and `ObjectShape` — that is how `define` stays single-type-parameter (F# has
  no partial explicit type application) while the first field fixes `'constructor`.
- `open type Axial.Schema.Syntax` adds the overloaded `field` member: the same named form, plus a
  bare-getter form (`field _.Name`) that reads the property name from the getter quotation
  (`ReflectedDefinition(includeValue = true)`) once at schema build, camelCases it, and keeps the
  compiled getter for the hot path. Explicit names are never transformed; the camelCase policy applies
  only to derived names. Verified under NativeAOT by the AOT probe.
- `field` (no explicit schema) resolves the value schema from the getter's result type via
  `SchemaDefaults.Resolve()` — an overload set, extendable by giving a type a
  `static member Schema: T -> Schema<T>`. Its generic option/list/map overloads resolve member schemas recursively. Optional
  type extensions do not satisfy SRTP constraints; the member must be intrinsic to the type. No reflection;
  unresolvable types are a compile error whose fix is `fieldWith`.
- `Schema.list<'item>()` and `Schema.map<'item>()` use the same resolver. `listWith`/`mapWith` accept an explicit member
  schema for recursion or local configuration. `constrainItems`/`constrainValues` rewrite the nested value definition;
  collection constraints remain on the outer definition.
- `Axial.Schema.Contracts.Emitter` mirrors handwritten authoring: it emits `field` for canonically resolvable fields,
  `fieldWith` when a field carries an explicit value schema (documentation, defaults, unions, recursion, or generated
  references), and adjacent `constrain` lines for non-optional field constraints. Optional constraints stay inside the
  explicit inner schema because their type is `Constraint<'item>`, not `Constraint<'item option>`.

`Schema.admit create project draft` composes over the draft's `ModelSchemaDefinition`: field getters
become `project >> getter`, the constructor becomes `draftCtor >> Result.bind create`. Nothing else
changes, so wire names, constraints, docs, parsing, and checking all survive into the domain schema.

## Rules that keep this codebase what it is

- **No runtime reflection — always compiler-directed.** Fable and NativeAOT are first-class; the goal
  is maximal deterministic verification. Type erasure is done with typed closures and `unbox` at points
  where the static type is known. One-time, build-phase metadata reading (the bare `field _.Name`
  quotation) is fine; reflection on the parse/encode paths is not.
- **Descriptions don't execute.** If you're adding behavior to `Schema.fs`, you're probably building an
  interpreter and it belongs in its own file.
- **One erased view, one typed view.** `FieldDescriptor` serves metadata interpreters; the typed record
  plan serves compilers that need direct construction. Don't invent a third.
- **The facade is thin.** `SchemaApi.fs` delegates; logic lives in the implementation modules.
- **What remains of arity code is boring.** The authoring surface has none (the chain peels one
  curried argument per field); the small `ConstructorApplication.createN` helpers that remain extend by
  copying the previous arity.

## Where to add things

| You want to… | Touch |
|---|---|
| Add a constraint | `Constraint` module (`Schema.fs`), its check in `SchemaValidation.fs`, JSON Schema lowering in `Axial.Schema.JsonSchema`, typed wrapper in `Syntax` (`Shape.fs`) |
| Add a primitive | `PrimitiveValueKind`, `Value` module, parsing in `SchemaValidation.fs`, `SchemaDefaults` overloads |
| Add an interpreter | New file after `SchemaApi.fs` (or a new package, like `Axial.Schema.JsonSchema`); walk `Inspect.model` output or compile the typed record plan |
| Change generated code | `src/Axial.Schema.Contracts` (Emitter) — not this project |
