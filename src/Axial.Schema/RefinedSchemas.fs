namespace Axial.Schema

open Axial.Check
open Axial.Refined
open Axial.Schema.Syntax

/// <summary>Ready-made schemas for the built-in refined values.</summary>
/// <remarks>
/// Concepts that carry no invariant past the boundary — trimmed text, slugs, length
/// bounds, numeric ranges — are expressed as constraints on a primitive schema rather
/// than as refined types. Compose them with <c>Schema.constrain</c> and <c>Schema.constrainAll</c>; the
/// metadata reaching an interpreter is identical to what the removed types produced.
/// </remarks>
[<RequireQualifiedAccess>]
module RefinedSchemas =
    let nonBlankString : Schema<NonBlankString> = SchemaDefaults.Resolve<NonBlankString>()
    let finiteFloat : Schema<FiniteFloat> = SchemaDefaults.Resolve<FiniteFloat>()
    let unitInterval : Schema<UnitInterval> = SchemaDefaults.Resolve<UnitInterval>()

    let nonEmptyList (itemSchema: Schema<'value>) : Schema<NonEmptyList<'value>> =
        SchemaDefaults.NonEmptyListWith itemSchema

    let nonEmptyArray (itemSchema: Schema<'value>) : Schema<NonEmptyArray<'value>> =
        SchemaDefaults.NonEmptyArrayWith itemSchema

    let distinctList<'value when 'value: equality> (itemSchema: Schema<'value>) : Schema<DistinctList<'value>> =
        SchemaDefaults.DistinctListWith itemSchema

    let private describe failures = CheckFailure.describeAll failures

    /// <summary>
    /// Builds a schema for an inclusive range, replacing the former per-type range
    /// schemas. Generic over any ordered value, so one definition covers what
    /// <c>dateTimeOffsetRange</c> and <c>dateOnlyRange</c> each needed separately.
    /// </summary>
    let interval (itemSchema: Schema<'value>) : Schema<Interval<'value>> =
        schema<Interval<'value>> {
            field "lower" (fun (value: Interval<'value>) -> value.Lower) { withSchema itemSchema }
            field "upper" (fun (value: Interval<'value>) -> value.Upper) { withSchema itemSchema }
            constructResult (fun lower upper -> Interval.create lower upper |> Result.mapError describe)
        }

    /// <summary>
    /// Builds a schema for a value confined to the supplied bounds. The bounds belong to
    /// the schema rather than to each value, so they are supplied once here.
    /// </summary>
    let bounded (bounds: Interval<'value>) (itemSchema: Schema<'value>) : Schema<Bounded<'value>> =
        itemSchema |> Schema.refine (Bounded.refinement bounds)

    /// <summary>
    /// Builds a schema for a range of instants using <c>start</c> and <c>end</c> field
    /// names. The same <c>Interval</c> type as <c>interval</c> above — only the wire
    /// vocabulary differs, which is why no second type is needed. An inverted pair is
    /// reported rather than silently reordered, since at a boundary that is a caller error.
    /// </summary>
    let dateRange : Schema<DateRange> =
        schema<DateRange> {
            field "start" (fun (value: DateRange) -> value.Lower) { withSchema Schema.dateTime }
            field "end" (fun (value: DateRange) -> value.Upper) { withSchema Schema.dateTime }
            constructResult (fun start finish -> Interval.create start finish |> Result.mapError describe)
        }
