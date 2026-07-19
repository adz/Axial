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

| File | What it holds |
|---|---|
| `Platform.fs` | Small shims so the same code compiles for .NET, Fable, and AOT. |
| `Derive.fs` | Inert attributes read by `schemagen` from source text. Never used at runtime. |
| `SchemaError.fs` | `SchemaError` — the portable error vocabulary interpreters report. |
| `Schema.fs` | The core: constraints, value/model definitions, `Schema<'value>`, and compiled record plans. Large; map below. |
| `Shape.fs` | The constructor-last authoring surface: `ObjectShape`, `Syntax`, `Schema.admit`. |
| `RawInput.fs` | Source-neutral boundary input (`RawInput`, `JsonLikeValue`) and its constructors. |
| `SchemaValidation.fs` | The parse/check interpreter (`SchemaParsing`). |
| `ParsedInput.fs` | `ParsedInput` — parse results plus redisplay data. |
| `FieldRef.fs` | `FieldRef` — typed get/set field references used by rules and generated code. |
| `ContextRules.fs` | Contextual (cross-field, per-context) rules over built schemas. |
| `Model.fs` | Higher-level model helpers. |
| `SchemaApi.fs` | The public `Schema` module. A facade: every function delegates to an internal implementation. |
| `Contract.fs` | Versioned contracts: version detection + stepwise migrations. |
| `RefinedSchemas.fs` | Stock refined schemas (ranges etc.). |
| `Inspection.fs` | The metadata interpreter: `Inspect.model`, `Inspect.schema`. |
| `JsonSchema.fs` | The JSON Schema interpreter. |

## Map of Schema.fs

Top to bottom:

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

Parsing (`SchemaValidation.fs`): for each field, look up the wire name in `RawInput`, parse the value
shape, run constraints, collect diagnostics by path. Only if every field succeeded, call
`TryApplyTrusted` with the boxed arguments; a constructor `Error` becomes
`SchemaError.ConstructorFailed` placed by `Schema.constructorErrorAt`.

Checking (`Schema.check`): same pipeline, but values come from the model's getters instead of raw input.

## The constructor-last surface (Shape.fs)

`ObjectShape<'model,'fields>` is a structural shape with no constructor: a list of boxed
`FieldDefinition<'model,_>` values plus a phantom type parameter `'fields` that records each field's
value type left to right (`(NoFields * string) * int`). The phantom is what makes the surface safe:

- `constrain (c: Constraint<'v>)` only accepts a shape whose *last* phantom entry is `'v` — a
  misplaced constraint is a type mismatch at the `constrain` line.
- `construct`/`constructResult` dispatch through an SRTP witness (`Constructors.ApplyTotal/ApplyResult`)
  to one overload per arity. The overload unboxes each stored `FieldDefinition` at its known type,
  closes erased metadata and the typed record plan. Constructor mismatches surface at the closing call
  with concrete types.
- `field` (no explicit schema) resolves the value schema from the getter's result type via
  `SchemaDefaults.Resolve()` — an overload set, extendable by giving a type a
  `static member Schema: T -> Schema<T>`. Its generic option/list/map overloads resolve member schemas recursively. Optional
  type extensions do not satisfy SRTP constraints; the member must be intrinsic to the type. No reflection;
  unresolvable types are a compile error whose fix is `fieldWith`.
- `Schema.list<'item>()` and `Schema.map<'item>()` use the same resolver. `listWith`/`mapWith` accept an explicit member
  schema for recursion or local configuration. `constrainItems`/`constrainValues` rewrite the nested value definition;
  collection constraints remain on the outer definition.

`Schema.admit create project draft` composes over the draft's `ModelSchemaDefinition`: field getters
become `project >> getter`, the constructor becomes `draftCtor >> Result.bind create`. Nothing else
changes, so wire names, constraints, docs, parsing, and checking all survive into the domain schema.

## Rules that keep this codebase what it is

- **No reflection, ever.** Fable and NativeAOT are first-class. Type erasure is done with typed closures
  and `unbox` at points where the static type is known.
- **Descriptions don't execute.** If you're adding behavior to `Schema.fs`, you're probably building an
  interpreter and it belongs in its own file.
- **One erased view, one typed view.** `FieldDescriptor` serves metadata interpreters; the typed record
  plan serves compilers that need direct construction. Don't invent a third.
- **The facade is thin.** `SchemaApi.fs` delegates; logic lives in the implementation modules.
- **Arity code is generated-by-hand and boring.** `ConstructorApplication.createN`, the
  `Constructors` overloads: extend by copying the previous arity. Do not replace them with cleverness.

## Where to add things

| You want to… | Touch |
|---|---|
| Add a constraint | `Constraint` module (`Schema.fs`), its check in `SchemaValidation.fs`, JSON Schema lowering in `JsonSchema.fs`, typed wrapper in `Syntax` (`Shape.fs`) |
| Add a primitive | `PrimitiveValueKind`, `Value` module, parsing in `SchemaValidation.fs`, `SchemaDefaults` overloads |
| Add an interpreter | New file after `SchemaApi.fs`; walk `Inspect.model` output or compile the typed record plan |
| Extend construct arity | Next overload pair in `Constructors` (`Shape.fs`), next `createN` if needed |
| Change generated code | `src/Axial.Schema.Contracts` (Emitter) — not this project |
