// A named catalog of stock schemas for Axial.Refined types (non-blank strings, positive ints,
// bounded collections, ranges). Members are ordinary Schema<'value> values — this module exists so
// common refinements need no hand-written schema.
namespace Axial.Schema

open Axial.Refined
open Axial.Schema
open Axial.Schema.Syntax

/// <summary>Ready-made schema values for the built-in <c>Axial.Refined</c> catalog.</summary>
/// <remarks>
/// <para>The catalog lives in <c>Axial.Schema</c> so <c>Axial.Refined</c> can remain independent of
/// <c>Axial.Schema</c>. Each schema carries the same constraint meaning as the matching standalone <c>Refine</c>
/// constructor before constructing the refined value.</para>
/// </remarks>
[<RequireQualifiedAccess>]
module RefinedSchemas =
    /// <summary>Describes a non-blank string as a schema refined value over required text.</summary>
    let nonBlankString : Schema<NonBlankString> =
        Schema.text
        |> Schema.constrain Constraint.required
        |> Schema.refine (Refinement.define NonBlankString.create NonBlankString.value)

    /// <summary>Describes a bounded string as a schema refined value over required text with inclusive length bounds.</summary>
    let boundedString minLength maxLength : Schema<BoundedString> =
        Schema.text
        |> Schema.constrainAll [ Constraint.required; Constraint.lengthBetween minLength maxLength ]
        |> Schema.refine
            (Refinement.define (Refine.boundedString minLength maxLength) _.Value)

    /// <summary>Describes a trimmed string as a schema refined value over text with no leading or trailing whitespace.</summary>
    let trimmedString : Schema<TrimmedString> =
        Schema.text
        |> Schema.constrain Constraint.trimmed
        |> Schema.refine (Refinement.define Refine.trimmedString _.Value)

    /// <summary>Describes an ASCII slug as a schema refined value over required text with the built-in slug pattern.</summary>
    let slug : Schema<Slug> =
        Schema.text
        |> Schema.constrainAll
            [ Constraint.required
              Constraint.pattern "^[a-z0-9]+(-[a-z0-9]+)*$" ]
        |> Schema.refine (Refinement.define Refine.slug _.Value)

    /// <summary>Describes a positive integer as a schema refined value over an integer greater than zero.</summary>
    let positiveInt : Schema<PositiveInt> =
        Schema.int
        |> Schema.constrain (Constraint.greaterThan 0)
        |> Schema.refine (Refinement.define PositiveInt.create PositiveInt.value)

    /// <summary>Describes a non-negative integer as a schema refined value over an integer greater than or equal to zero.</summary>
    let nonNegativeInt : Schema<NonNegativeInt> =
        Schema.int
        |> Schema.constrain (Constraint.atLeast 0)
        |> Schema.refine (Refinement.define Refine.nonNegativeInt _.Value)

    /// <summary>Describes a non-zero integer as a schema refined value over an integer not equal to zero.</summary>
    let nonZeroInt : Schema<NonZeroInt> =
        Schema.int
        |> Schema.constrain (Constraint.notEqualTo 0)
        |> Schema.refine (Refinement.define Refine.nonZeroInt _.Value)

    /// <summary>Describes a negative integer as a schema refined value over an integer less than zero.</summary>
    let negativeInt : Schema<NegativeInt> =
        Schema.int
        |> Schema.constrain (Constraint.lessThan 0)
        |> Schema.refine (Refinement.define Refine.negativeInt _.Value)

    /// <summary>Describes a non-positive integer as a schema refined value over an integer less than or equal to zero.</summary>
    let nonPositiveInt : Schema<NonPositiveInt> =
        Schema.int
        |> Schema.constrain (Constraint.atMost 0)
        |> Schema.refine (Refinement.define Refine.nonPositiveInt _.Value)

    /// <summary>Describes a non-empty list as a schema refined value over a collection of item schemas.</summary>
    let nonEmptyList (itemSchema: Schema<'value>) : Schema<NonEmptyList<'value>> =
        Schema.listWith itemSchema
        |> Schema.constrain (Constraint.minCount 1)
        |> Schema.refine (Refinement.define Refine.nonEmptyList NonEmptyList.toList)

    /// <summary>Describes a non-empty array as a schema refined value over a collection of item schemas.</summary>
    let nonEmptyArray (itemSchema: Schema<'value>) : Schema<NonEmptyArray<'value>> =
        Schema.listWith itemSchema
        |> Schema.constrain (Constraint.minCount 1)
        |> Schema.refine
            (Refinement.define Refine.nonEmptyArray (fun value -> value.ToArray() |> Array.toList))

    /// <summary>Describes a distinct list as a schema refined value over a distinct collection of item schemas.</summary>
    let distinctList<'value when 'value: equality>
        (itemSchema: Schema<'value>)
        : Schema<DistinctList<'value>> =
        Schema.listWith itemSchema
        |> Schema.constrain Constraint.distinct
        |> Schema.refine (Refinement.define Refine.distinctList _.ToList())

    /// <summary>Describes a bounded list as a schema refined value over a collection with inclusive count bounds.</summary>
    let boundedList minCount maxCount (itemSchema: Schema<'value>) : Schema<BoundedList<'value>> =
        Schema.listWith itemSchema
        |> Schema.constrain (Constraint.countBetween minCount maxCount)
        |> Schema.refine
            (Refinement.define (Refine.boundedList minCount maxCount) _.ToList())

    /// <summary>Describes a bounded array as a schema refined value over a collection with inclusive count bounds.</summary>
    let boundedArray minCount maxCount (itemSchema: Schema<'value>) : Schema<BoundedArray<'value>> =
        Schema.listWith itemSchema
        |> Schema.constrain (Constraint.countBetween minCount maxCount)
        |> Schema.refine
            (Refinement.define
                (Refine.boundedArray minCount maxCount)
                (fun value -> value.ToArray() |> Array.toList))

    /// <summary>Describes a date-time range as a record schema with <c>start</c> and <c>end</c> fields.</summary>
    let dateTimeOffsetRange : Schema<DateTimeOffsetRange> =
        Schema.define<DateTimeOffsetRange>
        |> fieldWith Schema.dateTime "start" _.Start
        |> fieldWith Schema.dateTime "end" _.End
        |> constructResult (fun start finish ->
            Refine.dateTimeOffsetRange start finish |> Result.mapError RefinementError.describe)

#if NET8_0_OR_GREATER
    /// <summary>Describes a date-only range as a record schema with <c>start</c> and <c>end</c> fields.</summary>
    /// <remarks>netstandard2.1: not available.</remarks>
    let dateOnlyRange : Schema<DateOnlyRange> =
        Schema.define<DateOnlyRange>
        |> fieldWith Schema.date "start" _.Start
        |> fieldWith Schema.date "end" _.End
        |> constructResult (fun start finish ->
            Refine.dateOnlyRange start finish |> Result.mapError RefinementError.describe)
#endif
