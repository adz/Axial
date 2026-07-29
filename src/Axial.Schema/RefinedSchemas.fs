namespace Axial.Schema

open Axial.Check
open Axial.Refined
open Axial.Schema.Syntax

/// Ready-made schemas for the built-in refined values.
[<RequireQualifiedAccess>]
module RefinedSchemas =
    let nonBlankString : Schema<NonBlankString> = SchemaDefaults.Resolve<NonBlankString>()

    let boundedString minLength maxLength : Schema<BoundedString> =
        let refinement =
            Refinement.defineAll
                [ Axial.Check.Constraint.required; Axial.Check.Constraint.lengthBetween minLength maxLength ]
                (fun value ->
                    match Refine.boundedString minLength maxLength value with
                    | Ok refined -> refined
                    | Error _ -> failwith "unreachable")
                _.Value
        Schema.text |> Schema.refine refinement

    let trimmedString : Schema<TrimmedString> = SchemaDefaults.Resolve<TrimmedString>()
    let slug : Schema<Slug> = SchemaDefaults.Resolve<Slug>()
    let positiveInt : Schema<PositiveInt> = SchemaDefaults.Resolve<PositiveInt>()
    let nonNegativeInt : Schema<NonNegativeInt> = SchemaDefaults.Resolve<NonNegativeInt>()
    let nonZeroInt : Schema<NonZeroInt> = SchemaDefaults.Resolve<NonZeroInt>()
    let negativeInt : Schema<NegativeInt> = SchemaDefaults.Resolve<NegativeInt>()
    let nonPositiveInt : Schema<NonPositiveInt> = SchemaDefaults.Resolve<NonPositiveInt>()

    let nonEmptyList (itemSchema: Schema<'value>) : Schema<NonEmptyList<'value>> =
        SchemaDefaults.NonEmptyListWith itemSchema

    let nonEmptyArray (itemSchema: Schema<'value>) : Schema<NonEmptyArray<'value>> =
        SchemaDefaults.NonEmptyArrayWith itemSchema

    let distinctList<'value when 'value: equality> (itemSchema: Schema<'value>) : Schema<DistinctList<'value>> =
        SchemaDefaults.DistinctListWith itemSchema

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
