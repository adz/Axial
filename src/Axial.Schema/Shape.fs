// Shared schema authoring vocabulary and canonical field-schema resolution.

// FS0064: the SRTP witness pattern (`(^w or ^s) : ...` with ^w fixed to a concrete witness class)
// intentionally constrains the witness type variable; the warning is noise here.
namespace Axial.Schema

open Axial.Constraint

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
    /// <summary>Builds an omittable schema from an explicitly resolved item schema.</summary>
    static member OptionWith(item: Schema<'item>) : Schema<'item option> = SchemaCore.option item
    /// <summary>Builds a list schema from an explicitly resolved item schema.</summary>
    static member ListWith(item: Schema<'item>) : Schema<'item list> = SchemaCore.listWith item
    /// <summary>Builds a string-keyed map schema from an explicitly resolved value schema.</summary>
    static member MapWith(item: Schema<'item>) : Schema<Map<string, 'item>> = SchemaCore.mapWith item
    static member Schema(_: string) : Schema<string> = SchemaCore.text
    static member Schema(_: int) : Schema<int> = SchemaCore.``int``
    static member Schema(_: int64) : Schema<int64> = SchemaCore.``int64``
    static member Schema(_: decimal) : Schema<decimal> = SchemaCore.``decimal``
    static member Schema(_: float) : Schema<float> = SchemaCore.``float``
    static member Schema(_: bool) : Schema<bool> = SchemaCore.``bool``
    static member Schema(_: System.DateTimeOffset) : Schema<System.DateTimeOffset> = SchemaCore.dateTime
    static member Schema(_: System.Guid) : Schema<System.Guid> = SchemaCore.guid
    // Every built-in refined type has exactly one canonical schema, so a bare field resolves it.
    // Numeric ranges are constraints rather than types: F# cannot propagate `> 0` through
    // arithmetic, so a refined number costs more at every use site than it saves. Express
    // them with `Schema.constrain (Constraint.greaterThan 0)` on the primitive.
    static member Schema(_: Axial.Refined.NonBlankString) : Schema<Axial.Refined.NonBlankString> =
        SchemaCore.refine Axial.Refined.NonBlankString.refinement SchemaCore.text
    static member Schema(_: Axial.Refined.FiniteFloat) : Schema<Axial.Refined.FiniteFloat> =
        SchemaCore.refine Axial.Refined.FiniteFloat.refinement SchemaCore.``float``
    static member Schema(_: Axial.Refined.UnitInterval) : Schema<Axial.Refined.UnitInterval> =
        SchemaCore.refine Axial.Refined.UnitInterval.refinement SchemaCore.``float``
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
        // The wire shape is a list; the refinement bridges list -> NonEmptyArray while retaining minLength 1.
        SchemaCore.refine (Axial.Refined.NonEmptyArray.listRefinement<'item> ()) (SchemaCore.listWith item)

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
                      Rules = field.Rules })

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
/// Collection-schema operations used inside schema definitions.
/// </summary>
/// <remarks>
/// There is no constraint catalogue here. One <c>Constraint</c> vocabulary serves direct checking, refinement,
/// and Schema, so a field block reaches for <c>Constraint.email</c> or an opened <c>ConstraintDSL</c> exactly as
/// standalone code does. Boundary supply is Schema-owned and stays here as <c>mustSupply</c> and <c>mayOmit</c>.
/// </remarks>
module Syntax =
    /// <summary>Adds a constraint to every item described by a list schema.</summary>
    /// <example><code>Schema.list () |> Syntax.constrainItems Constraint.present</code></example>
    let constrainItems (constraint': Constraint<'item>) (schema: Schema<'item list>) : Schema<'item list> =
        if isNull constraint' then nullArg (nameof constraint')
        SchemaCore.constrainItems constraint' schema

    /// <summary>Adds a constraint to every value described by a string-keyed map schema.</summary>
    /// <example><code>Schema.map () |> Syntax.constrainValues Constraint.present</code></example>
    let constrainValues (constraint': Constraint<'item>) (schema: Schema<Map<string, 'item>>) : Schema<Map<string, 'item>> =
        if isNull constraint' then nullArg (nameof constraint')
        SchemaCore.constrainValues constraint' schema

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
