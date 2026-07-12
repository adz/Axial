# Fallible Schema refinement review

The reference application exposed a mismatch between the old total refined-schema conversion and normal F# smart
constructors. Domain constructors returned `Result<'value, RefinementError>`, while schema construction required a
total `'raw -> 'value`. The app compensated with internal functions that threw if supposedly matching raw constraints
had already passed.

That adapter was unsafe because constraint metadata and the smart constructor could drift, structural refinement
failures could not always be expressed as raw constraints, and the schema called a separate unchecked path rather
than the authoritative constructor.

The implemented split is:

```fsharp
Schema.convert :
    ('raw -> 'value) ->
    ('value -> 'raw) ->
    Schema<'raw> ->
    Schema<'value>

Schema.refine :
    ('raw -> Result<'value, 'error>) ->
    ('error -> SchemaError list) ->
    ('value -> 'raw) ->
    Schema<'raw> ->
    Schema<'value>
```

`convert` describes a total reversible representation change. `refine` invokes the real smart constructor and lowers
its known error type into path-aware schema diagnostics. Parsing runs raw shape conversion and constraints before the
refinement; failure remains possible and is handled rather than asserted away.

`Axial.Refined` remains the independent owner of parsing, refinement, errors, and refined value types. `Axial.Schema`
supplies the integration because it owns interpreter errors and paths. The sibling `RefinedSchemas` catalog uses
`Schema.refine` with `SchemaError.ofRefinementError` and returns ordinary `Schema<_>` values.

The reference app now declares important text fields this way:

```fsharp
Schema.text
|> Schema.constrainAll [ Constraint.required; Constraint.maxLength 80 ]
|> Schema.refine WorkspaceName.create SchemaError.ofRefinementError WorkspaceName.value
```

The raw constraints supply standard codes, JSON Schema output, form attributes, and generator guidance.
`WorkspaceName.create` remains independently authoritative, so direct construction has the same intrinsic guarantee
as schema construction.

Tests cover successful refined schemas at the root, mapped refinement failures at the root path, refined fields,
layered refinements, and the built-in catalog. `RefinedSchemas` is a named set of schema-producing functions, not
another schema type or grammar.
