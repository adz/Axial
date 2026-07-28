// Shared schema authoring vocabulary and canonical field-schema resolution.

// FS0064: the SRTP witness pattern (`(^w or ^s) : ...` with ^w fixed to a concrete witness class)
// intentionally constrains the witness type variable; the warning is noise here.
namespace Axial.Schema

#nowarn "64"

#if !FABLE_COMPILER
open Microsoft.FSharp.Quotations
open Microsoft.FSharp.Quotations.Patterns
#endif

[<RequireQualifiedAccess>]
module internal ShapeInternals =
    let camelCase (name: string) =
        if System.String.IsNullOrEmpty name then
            name
        else
            string (System.Char.ToLowerInvariant name[0]) + name.Substring 1

/// <summary>
/// Canonical value-schema resolution for a schema field. A type participates by exposing
/// <c>static member Schema: T -&gt; Schema&lt;T&gt;</c>; Axial supplies that member for its supported built-in types.
/// When no member matches, define the field with a <c>withSchema</c> operation.
/// </summary>
[<Sealed; AbstractClass>]
type SchemaDefaults =
    /// <summary>Builds an optional schema from an explicitly resolved item schema.</summary>
    static member OptionWith(item: Schema<'item>) : Schema<'item option> = SchemaCore.option item
    /// <summary>Builds a list schema from an explicitly resolved item schema.</summary>
    static member ListWith(item: Schema<'item>) : Schema<'item list> = SchemaCore.listWith item
    /// <summary>Builds a string-keyed map schema from an explicitly resolved value schema.</summary>
    static member MapWith(item: Schema<'item>) : Schema<Map<string, 'item>> = SchemaCore.mapWith item
    static member Schema(_: string) : Schema<string> = SchemaCore.text
    static member Schema(_: int) : Schema<int> = SchemaCore.``int``
    static member Schema(_: decimal) : Schema<decimal> = SchemaCore.``decimal``
    static member Schema(_: bool) : Schema<bool> = SchemaCore.``bool``
    static member Schema(_: System.DateTimeOffset) : Schema<System.DateTimeOffset> = SchemaCore.dateTime
    static member Schema(_: System.Guid) : Schema<System.Guid> = SchemaCore.guid
    // Refined types whose refinement takes no parameters have exactly one canonical schema, so a bare
    // field can resolve it. Parameterised refinements (boundedString, boundedList) have no canonical
    // bounds and must still be supplied with `withSchema`.
    static member Schema(_: Axial.Refined.NonBlankString) : Schema<Axial.Refined.NonBlankString> =
        SchemaCore.refine Axial.Refined.NonBlankString.refinement SchemaCore.text
    static member Schema(_: Axial.Refined.TrimmedString) : Schema<Axial.Refined.TrimmedString> =
        SchemaCore.refine Axial.Refined.Text.trimmedStringRefinement SchemaCore.text
    static member Schema(_: Axial.Refined.Slug) : Schema<Axial.Refined.Slug> =
        SchemaCore.refine Axial.Refined.Text.slugRefinement SchemaCore.text
    static member Schema(_: Axial.Refined.PositiveInt) : Schema<Axial.Refined.PositiveInt> =
        SchemaCore.refine Axial.Refined.PositiveInt.refinement SchemaCore.``int``
    static member Schema(_: Axial.Refined.NonNegativeInt) : Schema<Axial.Refined.NonNegativeInt> =
        SchemaCore.refine Axial.Refined.Numeric.nonNegativeIntRefinement SchemaCore.``int``
    static member Schema(_: Axial.Refined.NonZeroInt) : Schema<Axial.Refined.NonZeroInt> =
        SchemaCore.refine Axial.Refined.Numeric.nonZeroIntRefinement SchemaCore.``int``
    static member Schema(_: Axial.Refined.NegativeInt) : Schema<Axial.Refined.NegativeInt> =
        SchemaCore.refine Axial.Refined.Numeric.negativeIntRefinement SchemaCore.``int``
    static member Schema(_: Axial.Refined.NonPositiveInt) : Schema<Axial.Refined.NonPositiveInt> =
        SchemaCore.refine Axial.Refined.Numeric.nonPositiveIntRefinement SchemaCore.``int``
#if NET8_0_OR_GREATER
    static member Schema(_: System.DateOnly) : Schema<System.DateOnly> = SchemaCore.date
#endif

    static member inline Schema(_: ^item list) : Schema< ^item list> =
        let inline resolve (witness: ^w, marker: ^value) : Schema< ^value> =
            ((^w or ^value): (static member Schema: ^value -> Schema< ^value>) marker)

        SchemaDefaults.ListWith(
            resolve (Unchecked.defaultof<SchemaDefaults>, Unchecked.defaultof< ^item>)
        )

    /// <summary>Builds a non-empty list schema from an explicitly resolved item schema.</summary>
    static member NonEmptyListWith(item: Schema<'item>) : Schema<Axial.Refined.NonEmptyList<'item>> =
        SchemaCore.refine (Axial.Refined.Collection.nonEmptyListRefinement<'item> ()) (SchemaCore.listWith item)

    static member inline Schema(_: Axial.Refined.NonEmptyList< ^item>) : Schema<Axial.Refined.NonEmptyList< ^item>> =
        let inline resolve (witness: ^w, marker: ^value) : Schema< ^value> =
            ((^w or ^value): (static member Schema: ^value -> Schema< ^value>) marker)

        SchemaDefaults.NonEmptyListWith(
            resolve (Unchecked.defaultof<SchemaDefaults>, Unchecked.defaultof< ^item>)
        )

    /// <summary>Builds a non-empty array schema from an explicitly resolved item schema.</summary>
    static member NonEmptyArrayWith(item: Schema<'item>) : Schema<Axial.Refined.NonEmptyArray<'item>> =
        // The wire shape is a list; the refinement bridges list -> NonEmptyArray while retaining minCount 1.
        let refinement =
            Axial.Refined.Refinement.define
                (Axial.Check.Constraint.minCount 1)
                (fun (values: 'item list) ->
                    match Axial.Refined.Refine.nonEmptyArray (List.toArray values) with
                    | Ok value -> value
                    | Error _ -> failwith "unreachable")
                (fun value -> value.ToArray() |> Array.toList)

        SchemaCore.refine refinement (SchemaCore.listWith item)

    static member inline Schema(_: Axial.Refined.NonEmptyArray< ^item>) : Schema<Axial.Refined.NonEmptyArray< ^item>> =
        let inline resolve (witness: ^w, marker: ^value) : Schema< ^value> =
            ((^w or ^value): (static member Schema: ^value -> Schema< ^value>) marker)

        SchemaDefaults.NonEmptyArrayWith(
            resolve (Unchecked.defaultof<SchemaDefaults>, Unchecked.defaultof< ^item>)
        )

    /// <summary>Builds a duplicate-free list schema from an explicitly resolved item schema.</summary>
    static member DistinctListWith(item: Schema<'item>) : Schema<Axial.Refined.DistinctList<'item>> =
        SchemaCore.refine (Axial.Refined.Collection.distinctListRefinement<'item> ()) (SchemaCore.listWith item)

    static member inline Schema(_: Axial.Refined.DistinctList< ^item>) : Schema<Axial.Refined.DistinctList< ^item>> =
        let inline resolve (witness: ^w, marker: ^value) : Schema< ^value> =
            ((^w or ^value): (static member Schema: ^value -> Schema< ^value>) marker)

        SchemaDefaults.DistinctListWith(
            resolve (Unchecked.defaultof<SchemaDefaults>, Unchecked.defaultof< ^item>)
        )

    static member inline Schema(_: ^item option) : Schema< ^item option> =
        let inline resolve (witness: ^w, marker: ^value) : Schema< ^value> =
            ((^w or ^value): (static member Schema: ^value -> Schema< ^value>) marker)

        SchemaDefaults.OptionWith(
            resolve (Unchecked.defaultof<SchemaDefaults>, Unchecked.defaultof< ^item>)
        )

    static member inline Schema(_: Map<string, ^item>) : Schema<Map<string, ^item>> =
        let inline resolve (witness: ^w, marker: ^value) : Schema< ^value> =
            ((^w or ^value): (static member Schema: ^value -> Schema< ^value>) marker)

        SchemaDefaults.MapWith(
            resolve (Unchecked.defaultof<SchemaDefaults>, Unchecked.defaultof< ^item>)
        )

    static member inline Resolve() : Schema< ^value> =
        let inline call (witness: ^w, marker: ^v) : Schema< ^v> =
            ((^w or ^v): (static member Schema: ^v -> Schema< ^v>) marker)

        call (Unchecked.defaultof<SchemaDefaults>, Unchecked.defaultof< ^value>)

[<RequireQualifiedAccess>]
module internal ShapeOps =
    /// Model-level trusted construction: maps a permissive draft schema to a domain schema through an
    /// admission function and a projection, preserving fields, wire names, constraints, and metadata.
    let admit (create: 'draft -> Result<'domain, string>) (project: 'domain -> 'draft) (draft: Schema<'draft>) : Schema<'domain> =
        if isNull (box create) then nullArg (nameof create)
        if isNull (box project) then nullArg (nameof project)
        if isNull (box draft) then nullArg (nameof draft)

        match draft.Definition with
        | ModelDefinition definition ->
            let fields =
                definition.Fields
                |> List.map (fun field ->
                    { FieldDescriptor.ExternalName = field.ExternalName
                      Order = field.Order
                      Getter = fun (domain: 'domain) -> field.Getter(project domain)
                      ValueSchema = field.ValueSchema
                      Constraints = field.Constraints })

            let tryApply arguments =
                definition.Constructor.TryApplyTrusted arguments |> Result.bind create

            let constructor =
                { ConstructorApplication.ArgumentCount = definition.Constructor.ArgumentCount
                  ApplyTrusted =
                    fun arguments ->
                        match tryApply arguments with
                        | Ok domain -> domain
                        | Error message -> invalidOp message
                  TryApplyTrusted = tryApply }

            Schema(
                ModelDefinition
                    { Constructor = constructor
                      Fields = fields
                      Description = definition.Description },
                None
            )
        | ValueDefinition _ ->
            invalidArg (nameof draft) "Schema.admit expects a model schema; refine value schemas with Schema.refine."
        | PendingDefinition -> invalidArg (nameof draft) "Expected a completed schema definition."

/// <summary>
/// Typed constraints and collection-schema operations used by schema definitions.
/// </summary>
module Syntax =
    /// <summary>Adds a typed constraint to every item described by a list schema.</summary>
    let constrainItems (constraint': SchemaConstraint<'item>) (schema: Schema<'item list>) : Schema<'item list> =
        if isNull (box constraint') then nullArg (nameof constraint')
        SchemaCore.constrainItems constraint'.Untyped schema

    /// <summary>Adds a typed constraint to every value described by a string-keyed map schema.</summary>
    let constrainValues (constraint': SchemaConstraint<'item>) (schema: Schema<Map<string, 'item>>) : Schema<Map<string, 'item>> =
        if isNull (box constraint') then nullArg (nameof constraint')
        SchemaCore.constrainValues constraint'.Untyped schema

    // ---- typed constraints ----

    /// <summary>Requires a field value to be supplied by boundary interpreters.</summary>
    let required<'value> : SchemaConstraint<'value> = Constraint.required<'value>

    /// <summary>Marks a field value as optional for boundary interpreters.</summary>
    let optional<'value> : SchemaConstraint<'value> = Constraint.optional<'value>

    /// <summary>Requires a text field to have at least the supplied length.</summary>
    let minLength minimum : SchemaConstraint<string> = Constraint.minLength minimum

    /// <summary>Requires a text field to have at most the supplied length.</summary>
    let maxLength maximum : SchemaConstraint<string> = Constraint.maxLength maximum

    /// <summary>Requires a text field's length to fall inside the supplied inclusive bounds.</summary>
    let lengthBetween minimum maximum : SchemaConstraint<string> =
        Constraint.lengthBetween minimum maximum

    /// <summary>Requires a text field to match Axial's pragmatic email format.</summary>
    let email: SchemaConstraint<string> = Constraint.email

    /// <summary>Requires a text field to have no leading or trailing whitespace.</summary>
    let trimmed: SchemaConstraint<string> = Constraint.trimmed

    /// <summary>Requires a text field to match the supplied regular expression pattern.</summary>
    let pattern expression : SchemaConstraint<string> = Constraint.pattern expression

    /// <summary>Requires a field to be at least the supplied value (inclusive).</summary>
    let atLeast (minimum: 'value) : SchemaConstraint<'value> = Constraint.atLeast minimum

    /// <summary>Requires a field to be at most the supplied value (inclusive).</summary>
    let atMost (maximum: 'value) : SchemaConstraint<'value> = Constraint.atMost maximum

    /// <summary>Requires a field to be greater than the supplied value (exclusive).</summary>
    let greaterThan (minimum: 'value) : SchemaConstraint<'value> = Constraint.greaterThan minimum

    /// <summary>Requires a field to be less than the supplied value (exclusive).</summary>
    let lessThan (maximum: 'value) : SchemaConstraint<'value> = Constraint.lessThan maximum

    /// <summary>Requires a numeric field to be an exact multiple of the supplied value.</summary>
    let inline multipleOf (factor: ^value) : SchemaConstraint<^value> = Constraint.multipleOf factor

    /// <summary>Requires a field to fall inside the supplied inclusive bounds.</summary>
    let between (minimum: 'value) (maximum: 'value) : SchemaConstraint<'value> =
        Constraint.between minimum maximum

    /// <summary>Requires a field to equal the supplied value.</summary>
    let equalTo (expected: 'value) : SchemaConstraint<'value> = Constraint.equalTo expected

    /// <summary>Requires a field to differ from the supplied value.</summary>
    let notEqualTo (unexpected: 'value) : SchemaConstraint<'value> = Constraint.notEqualTo unexpected

    /// <summary>Requires a text field to equal one of the supplied choices.</summary>
    let oneOf (choices: string list) : SchemaConstraint<string> = Constraint.oneOf choices

    /// <summary>Requires a list field to have exactly the supplied number of items.</summary>
    let count expected : SchemaConstraint<'item list> = Constraint.count expected

    /// <summary>Requires a list field to have at least the supplied number of items.</summary>
    let minCount minimum : SchemaConstraint<'item list> = Constraint.minCount minimum

    /// <summary>Requires a list field to have at most the supplied number of items.</summary>
    let maxCount maximum : SchemaConstraint<'item list> = Constraint.maxCount maximum

    /// <summary>Requires a list field's item count to fall inside the supplied inclusive bounds.</summary>
    let countBetween minimum maximum : SchemaConstraint<'item list> =
        Constraint.countBetween minimum maximum

    /// <summary>Requires a list field's items to be distinct.</summary>
    let distinct<'item when 'item: equality> : SchemaConstraint<'item list> = Constraint.distinct<'item>

    /// <summary>Requires a list field to contain the supplied item.</summary>
    let contains (item: 'item) : SchemaConstraint<'item list> = Constraint.contains item

    /// <summary>Requires a field to be greater than zero.</summary>
    let inline positive<'value when 'value: comparison and 'value: (static member Zero: 'value)> : SchemaConstraint<'value> =
        greaterThan LanguagePrimitives.GenericZero<'value>

    /// <summary>Requires a field to be greater than or equal to zero.</summary>
    let inline nonNegative<'value when 'value: comparison and 'value: (static member Zero: 'value)> : SchemaConstraint<'value> =
        atLeast LanguagePrimitives.GenericZero<'value>

    /// <summary>Requires a field to be less than zero.</summary>
    let inline negative<'value when 'value: comparison and 'value: (static member Zero: 'value)> : SchemaConstraint<'value> =
        lessThan LanguagePrimitives.GenericZero<'value>

    /// <summary>Requires a field to be less than or equal to zero.</summary>
    let inline nonPositive<'value when 'value: comparison and 'value: (static member Zero: 'value)> : SchemaConstraint<'value> =
        atMost LanguagePrimitives.GenericZero<'value>

    /// <summary>Adapts a complete Check constraint for use in a Schema field block.</summary>
    let fromCheck (constraint': Axial.Check.Constraint<'value>) : SchemaConstraint<'value> =
        Constraint.fromCheck constraint'

    /// <summary>Replaces a typed constraint's user-facing message.</summary>
    let withMessage (message: string) (constraint': SchemaConstraint<'value>) : SchemaConstraint<'value> =
        Constraint.withMessage message constraint'

#if !FABLE_COMPILER
/// <summary>
/// The bare-getter field form: <c>open type Axial.Schema.Syntax</c> brings an overloaded <c>field</c>
/// into scope that accepts either a name and getter (like the module form) or a bare property getter
/// such as <c>field _.Name</c>, deriving the wire name from the property (camelCased). Explicit names
/// are never transformed; the camelCase policy applies only to derived names.
/// </summary>
[<Sealed; AbstractClass>]
type Syntax =

    /// <summary>Splits a property-access getter quotation into a derived (camelCased) wire name and the
    /// compiled getter. Infrastructure for the bare <c>field</c> form; not intended for direct use.</summary>
    static member DerivedField(getter: Expr<'model -> 'value>) : string * ('model -> 'value) =
        match getter :> Expr with
        | WithValue(value, _, Lambda(_, PropertyGet(Some(Var _), property, []))) ->
            ShapeInternals.camelCase property.Name, (value :?> ('model -> 'value))
        | _ ->
            invalidArg
                (nameof getter)
                "The bare field form requires a property getter such as `_.Name`; use `field \"name\" getter` for anything else."

#endif
