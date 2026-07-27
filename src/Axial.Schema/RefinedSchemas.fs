namespace Axial.Schema

open Axial.Check
open Axial.Refined
open Axial.Schema.Syntax

/// Ready-made schemas for the built-in refined values.
[<RequireQualifiedAccess>]
module RefinedSchemas =
    let nonBlankString : Schema<NonBlankString> = Schema.text |> Schema.refine NonBlankString.refinement

    let boundedString minLength maxLength : Schema<BoundedString> =
        let refinement =
            Refinement.defineAll
                [ Axial.Check.Constraint.required; Axial.Check.Constraint.lengthBetween minLength maxLength ]
                (fun value ->
                    match Refine.boundedString minLength maxLength value with
                    | Ok refined -> refined
                    | Error _ -> failwith "unreachable")
                _.Value
        Schema.text
        |> Schema.constrainAll [ Axial.Schema.Constraint.required; Axial.Schema.Constraint.lengthBetween minLength maxLength ]
        |> Schema.refine refinement

    let trimmedString : Schema<TrimmedString> = Schema.text |> Schema.refine Text.trimmedStringRefinement
    let slug : Schema<Slug> = Schema.text |> Schema.refine Text.slugRefinement
    let positiveInt : Schema<PositiveInt> = Schema.int |> Schema.refine PositiveInt.refinement
    let nonNegativeInt : Schema<NonNegativeInt> = Schema.int |> Schema.refine Numeric.nonNegativeIntRefinement
    let nonZeroInt : Schema<NonZeroInt> = Schema.int |> Schema.refine Numeric.nonZeroIntRefinement
    let negativeInt : Schema<NegativeInt> = Schema.int |> Schema.refine Numeric.negativeIntRefinement
    let nonPositiveInt : Schema<NonPositiveInt> = Schema.int |> Schema.refine Numeric.nonPositiveIntRefinement

    let nonEmptyList (itemSchema: Schema<'value>) : Schema<NonEmptyList<'value>> =
        let refinement: Refinement<'value list, NonEmptyList<'value>> = Collection.nonEmptyListRefinement<'value> ()
        Schema.listWith itemSchema |> Schema.refine refinement

    let nonEmptyArray (itemSchema: Schema<'value>) : Schema<NonEmptyArray<'value>> =
        let refinement =
            Refinement.define (Axial.Check.Constraint.minCount 1)
                (List.toArray >> fun values -> match Refine.nonEmptyArray values with Ok value -> value | Error _ -> failwith "unreachable")
                (fun value -> value.ToArray() |> Array.toList)
        Schema.listWith itemSchema |> Schema.refine refinement

    let distinctList<'value when 'value: equality> (itemSchema: Schema<'value>) : Schema<DistinctList<'value>> =
        let refinement: Refinement<'value list, DistinctList<'value>> = Collection.distinctListRefinement<'value> ()
        Schema.listWith itemSchema |> Schema.refine refinement

    let boundedList minCount maxCount (itemSchema: Schema<'value>) : Schema<BoundedList<'value>> =
        let refinement = Refinement.define (Axial.Check.Constraint.countBetween minCount maxCount)
                            (fun values -> match Refine.boundedList minCount maxCount values with Ok value -> value | Error _ -> failwith "unreachable") _.ToList()
        Schema.listWith itemSchema |> Schema.refine refinement

    let boundedArray minCount maxCount (itemSchema: Schema<'value>) : Schema<BoundedArray<'value>> =
        let refinement = Refinement.define (Axial.Check.Constraint.countBetween minCount maxCount)
                            (fun values -> match Refine.boundedArray minCount maxCount values with Ok value -> value | Error _ -> failwith "unreachable")
                            (fun value -> value.ToArray() |> Array.toList)
        Schema.listWith itemSchema |> Schema.refine refinement

    let private describe failures = CheckFailure.describeAll failures

    let dateTimeOffsetRange : Schema<DateTimeOffsetRange> =
        schema<DateTimeOffsetRange> {
            field "start" (fun (value: DateTimeOffsetRange) -> value.Start)
            field "end" (fun (value: DateTimeOffsetRange) -> value.End)
            constructResult (fun start finish -> Refine.dateTimeOffsetRange start finish |> Result.mapError describe)
        }

#if NET8_0_OR_GREATER
    let dateOnlyRange : Schema<DateOnlyRange> =
        schema<DateOnlyRange> {
            field "start" (fun (value: DateOnlyRange) -> value.Start)
            field "end" (fun (value: DateOnlyRange) -> value.End)
            constructResult (fun start finish -> Refine.dateOnlyRange start finish |> Result.mapError describe)
        }
#endif
