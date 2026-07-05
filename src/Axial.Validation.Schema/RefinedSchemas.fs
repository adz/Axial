namespace Axial.Validation.Schema

open Axial.Refined
open Axial.Schema

module private RefinedSchemaConstruction =
    let fromResult target result =
        match result with
        | Ok value -> value
        | Error error -> failwithf "%s schema construction failed after schema constraints passed: %A" target error

/// <summary>Ready-made schema values for the built-in <c>Axial.Refined</c> scalar catalog.</summary>
/// <remarks>
/// <para>
/// The catalog lives in <c>Axial.Validation.Schema</c> so <c>Axial.Refined</c> can remain independent of
/// <c>Axial.Schema</c>. Each schema carries the same constraint meaning as the matching standalone
/// <c>Refine</c> constructor before constructing the refined value.
/// </para>
/// </remarks>
[<RequireQualifiedAccess>]
module RefinedSchema =
    /// <summary>Describes a non-blank string as a schema refined value over required text.</summary>
    let nonBlankString : ValueSchema<NonBlankString> =
        Value.text
        |> Value.withConstraint SchemaConstraint.required
        |> Value.refined
            (NonBlankString.create >> RefinedSchemaConstruction.fromResult "NonBlankString")
            NonBlankString.value

    /// <summary>Describes a bounded string as a schema refined value over required text with inclusive length bounds.</summary>
    let boundedString minLength maxLength : ValueSchema<BoundedString> =
        Value.text
        |> Value.withConstraints [ SchemaConstraint.required; SchemaConstraint.lengthBetween minLength maxLength ]
        |> Value.refined
            (Refine.boundedString minLength maxLength >> RefinedSchemaConstruction.fromResult "BoundedString")
            (fun value -> value.Value)

    /// <summary>Describes an ASCII slug as a schema refined value over required text with the built-in slug pattern.</summary>
    let slug : ValueSchema<Slug> =
        Value.text
        |> Value.withConstraints
            [ SchemaConstraint.required
              SchemaConstraint.pattern "^[a-z0-9]+(-[a-z0-9]+)*$" ]
        |> Value.refined (Refine.slug >> RefinedSchemaConstruction.fromResult "Slug") (fun value -> value.Value)

    /// <summary>Describes a positive integer as a schema refined value over an integer greater than zero.</summary>
    let positiveInt : ValueSchema<PositiveInt> =
        Value.``int``
        |> Value.withConstraint (SchemaConstraint.greaterThan 0)
        |> Value.refined (PositiveInt.create >> RefinedSchemaConstruction.fromResult "PositiveInt") PositiveInt.value

    /// <summary>Describes a non-negative integer as a schema refined value over an integer greater than or equal to zero.</summary>
    let nonNegativeInt : ValueSchema<NonNegativeInt> =
        Value.``int``
        |> Value.withConstraint (SchemaConstraint.atLeast 0)
        |> Value.refined
            (Refine.nonNegativeInt >> RefinedSchemaConstruction.fromResult "NonNegativeInt")
            (fun value -> value.Value)

    /// <summary>Describes a negative integer as a schema refined value over an integer less than zero.</summary>
    let negativeInt : ValueSchema<NegativeInt> =
        Value.``int``
        |> Value.withConstraint (SchemaConstraint.lessThan 0)
        |> Value.refined
            (Refine.negativeInt >> RefinedSchemaConstruction.fromResult "NegativeInt")
            (fun value -> value.Value)

    /// <summary>Describes a non-positive integer as a schema refined value over an integer less than or equal to zero.</summary>
    let nonPositiveInt : ValueSchema<NonPositiveInt> =
        Value.``int``
        |> Value.withConstraint (SchemaConstraint.atMost 0)
        |> Value.refined
            (Refine.nonPositiveInt >> RefinedSchemaConstruction.fromResult "NonPositiveInt")
            (fun value -> value.Value)
