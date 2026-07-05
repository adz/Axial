namespace Axial.Validation.Schema

open Axial.Refined
open Axial.Schema

module private RefinedSchemaConstruction =
    let fromResult target result =
        match result with
        | Ok value -> value
        | Error error -> failwithf "%s schema construction failed after schema constraints passed: %A" target error

/// <summary>Ready-made schema values for the built-in <c>Axial.Refined</c> catalog.</summary>
/// <remarks>
/// <para>The catalog lives in <c>Axial.Validation.Schema</c> so <c>Axial.Refined</c> can remain independent of
/// <c>Axial.Schema</c>. Each schema carries the same constraint meaning as the matching standalone <c>Refine</c>
/// constructor before constructing the refined value.</para>
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

    /// <summary>Describes a trimmed string as a schema refined value over text with no leading or trailing whitespace.</summary>
    let trimmedString : ValueSchema<TrimmedString> =
        Value.text
        |> Value.withConstraint SchemaConstraint.trimmed
        |> Value.refined
            (Refine.trimmedString >> RefinedSchemaConstruction.fromResult "TrimmedString")
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
        Value.int
        |> Value.withConstraint (SchemaConstraint.greaterThan 0)
        |> Value.refined (PositiveInt.create >> RefinedSchemaConstruction.fromResult "PositiveInt") PositiveInt.value

    /// <summary>Describes a non-negative integer as a schema refined value over an integer greater than or equal to zero.</summary>
    let nonNegativeInt : ValueSchema<NonNegativeInt> =
        Value.int
        |> Value.withConstraint (SchemaConstraint.atLeast 0)
        |> Value.refined
            (Refine.nonNegativeInt >> RefinedSchemaConstruction.fromResult "NonNegativeInt")
            (fun value -> value.Value)

    /// <summary>Describes a non-zero integer as a schema refined value over an integer not equal to zero.</summary>
    let nonZeroInt : ValueSchema<NonZeroInt> =
        Value.int
        |> Value.withConstraint (SchemaConstraint.notEqualTo 0)
        |> Value.refined
            (Refine.nonZeroInt >> RefinedSchemaConstruction.fromResult "NonZeroInt")
            (fun value -> value.Value)

    /// <summary>Describes a negative integer as a schema refined value over an integer less than zero.</summary>
    let negativeInt : ValueSchema<NegativeInt> =
        Value.int
        |> Value.withConstraint (SchemaConstraint.lessThan 0)
        |> Value.refined
            (Refine.negativeInt >> RefinedSchemaConstruction.fromResult "NegativeInt")
            (fun value -> value.Value)

    /// <summary>Describes a non-positive integer as a schema refined value over an integer less than or equal to zero.</summary>
    let nonPositiveInt : ValueSchema<NonPositiveInt> =
        Value.int
        |> Value.withConstraint (SchemaConstraint.atMost 0)
        |> Value.refined
            (Refine.nonPositiveInt >> RefinedSchemaConstruction.fromResult "NonPositiveInt")
            (fun value -> value.Value)

    /// <summary>Describes a non-empty list as a schema refined value over a collection of item schemas.</summary>
    let nonEmptyList (itemSchema: ValueSchema<'value>) : ValueSchema<NonEmptyList<'value>> =
        Value.manyOf itemSchema
        |> Value.withConstraint (SchemaConstraint.minCount 1)
        |> Value.refined
            (Refine.nonEmptyList >> RefinedSchemaConstruction.fromResult "NonEmptyList")
            NonEmptyList.toList

    /// <summary>Describes a non-empty array as a schema refined value over a collection of item schemas.</summary>
    let nonEmptyArray (itemSchema: ValueSchema<'value>) : ValueSchema<NonEmptyArray<'value>> =
        Value.manyOf itemSchema
        |> Value.withConstraint (SchemaConstraint.minCount 1)
        |> Value.refined
            (Refine.nonEmptyArray >> RefinedSchemaConstruction.fromResult "NonEmptyArray")
            (fun value -> value.ToArray() |> Array.toList)

    /// <summary>Describes a distinct list as a schema refined value over a distinct collection of item schemas.</summary>
    let distinctList<'value when 'value: equality>
        (itemSchema: ValueSchema<'value>)
        : ValueSchema<DistinctList<'value>> =
        Value.manyOf itemSchema
        |> Value.withConstraint SchemaConstraint.distinct
        |> Value.refined
            (Refine.distinctList >> RefinedSchemaConstruction.fromResult "DistinctList")
            (fun value -> value.ToList())

    /// <summary>Describes a bounded list as a schema refined value over a collection with inclusive count bounds.</summary>
    let boundedList minCount maxCount (itemSchema: ValueSchema<'value>) : ValueSchema<BoundedList<'value>> =
        Value.manyOf itemSchema
        |> Value.withConstraint (SchemaConstraint.countBetween minCount maxCount)
        |> Value.refined
            (Refine.boundedList minCount maxCount >> RefinedSchemaConstruction.fromResult "BoundedList")
            (fun value -> value.ToList())

    /// <summary>Describes a bounded array as a schema refined value over a collection with inclusive count bounds.</summary>
    let boundedArray minCount maxCount (itemSchema: ValueSchema<'value>) : ValueSchema<BoundedArray<'value>> =
        Value.manyOf itemSchema
        |> Value.withConstraint (SchemaConstraint.countBetween minCount maxCount)
        |> Value.refined
            (Refine.boundedArray minCount maxCount >> RefinedSchemaConstruction.fromResult "BoundedArray")
            (fun value -> value.ToArray() |> Array.toList)

    /// <summary>Describes a date-time range as a record schema with <c>start</c> and <c>end</c> fields.</summary>
    let dateTimeOffsetRange : Schema<DateTimeOffsetRange> =
        Schema.recordFor<DateTimeOffsetRange, _> Refine.dateTimeOffsetRange
        |> Schema.dateTime "start" _.Start
        |> Schema.dateTime "end" _.End
        |> Schema.buildResultWith RefinementError.describe

#if NET8_0_OR_GREATER
    /// <summary>Describes a date-only range as a record schema with <c>start</c> and <c>end</c> fields.</summary>
    let dateOnlyRange : Schema<DateOnlyRange> =
        Schema.recordFor<DateOnlyRange, _> Refine.dateOnlyRange
        |> Schema.date "start" _.Start
        |> Schema.date "end" _.End
        |> Schema.buildResultWith RefinementError.describe
#endif
