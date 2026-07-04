namespace Axial.ErrorHandling

open System
open System.Text.RegularExpressions

/// <summary>
/// Lightweight boolean predicates for local structural facts.
/// </summary>
/// <remarks>
/// These helpers return <c>bool</c> and intentionally live outside the <c>Check</c> module, where public helpers return
/// structured <see cref="T:Axial.ErrorHandling.Check`1" /> results. Use predicates for local branching and
/// <c>Check</c> programs when callers need typed failure details. Names inside each type-specific submodule are kept
/// short and unprefixed (<c>String.blank</c>, not <c>String.isBlank</c>) since the submodule already states the type.
/// </remarks>
[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Predicate =
    let private emailRegex =
        Regex(@"^[^@]+@[^@]+$", RegexOptions.Compiled ||| RegexOptions.IgnoreCase)

    let private numericRegex =
        Regex(@"^\d+$", RegexOptions.Compiled)

    /// <summary>Boolean predicates for option values.</summary>
    module Option =
        /// <summary>Returns true when the option contains a value.</summary>
        let isSome value =
            Microsoft.FSharp.Core.Option.isSome value

        /// <summary>Returns true when the option contains no value.</summary>
        let isNone value =
            Microsoft.FSharp.Core.Option.isNone value

        /// <summary>Returns true when the option contains a value.</summary>
        let present value =
            isSome value

        /// <summary>Returns true when the option contains no value.</summary>
        let empty value =
            isNone value

        /// <summary>Returns true when the option contains a value.</summary>
        let notEmpty value =
            isSome value

    /// <summary>Boolean predicates for value option values.</summary>
    module ValueOption =
        /// <summary>Returns true when the value option contains a value.</summary>
        let isSome value =
            match value with
            | ValueSome _ -> true
            | ValueNone -> false

        /// <summary>Returns true when the value option contains no value.</summary>
        let isNone value =
            match value with
            | ValueNone -> true
            | ValueSome _ -> false

        /// <summary>Returns true when the value option contains a value.</summary>
        let present value =
            isSome value

        /// <summary>Returns true when the value option contains no value.</summary>
        let empty value =
            isNone value

        /// <summary>Returns true when the value option contains a value.</summary>
        let notEmpty value =
            isSome value

    /// <summary>Boolean predicates for nullable values.</summary>
    module Nullable =
        /// <summary>Returns true when the nullable value contains a value.</summary>
        let hasValue (value: System.Nullable<'value>) =
            value.HasValue

        /// <summary>Returns true when the nullable value contains no value.</summary>
        let hasNoValue (value: System.Nullable<'value>) =
            not value.HasValue

        /// <summary>Returns true when the nullable value contains a value.</summary>
        let present value =
            hasValue value

        /// <summary>Returns true when the nullable value contains no value.</summary>
        let empty value =
            hasNoValue value

        /// <summary>Returns true when the nullable value contains a value.</summary>
        let notEmpty value =
            hasValue value

    /// <summary>Boolean predicates for result values.</summary>
    module Result =
        /// <summary>Returns true when the result is successful.</summary>
        let isOk result =
            match result with
            | Ok _ -> true
            | Error _ -> false

        /// <summary>Returns true when the result is failed.</summary>
        let isError result =
            match result with
            | Error _ -> true
            | Ok _ -> false

    /// <summary>Boolean predicates for reference values.</summary>
    module Reference =
        /// <summary>Returns true when the reference is null.</summary>
        let isNull (value: 'value when 'value: null) =
            Object.ReferenceEquals(value, null)

        /// <summary>Returns true when the reference is not null.</summary>
        let notNull (value: 'value when 'value: null) =
            not (isNull value)

    /// <summary>Boolean predicates for strings.</summary>
    module String =
        /// <summary>Returns true when the string is exactly empty and non-null.</summary>
        let empty (value: string) =
            not (isNull value) && value.Length = 0

        /// <summary>Returns true when the string has at least one character and is non-null.</summary>
        let notEmpty (value: string) =
            not (isNull value) && value.Length > 0

        /// <summary>Returns true when the string is null, empty, or whitespace.</summary>
        let blank (value: string) =
            global.System.String.IsNullOrWhiteSpace value

        /// <summary>Returns true when the string is non-null and contains at least one non-whitespace character.</summary>
        let notBlank value =
            not (blank value)

        /// <summary>Returns true when the string length is at least the supplied minimum.</summary>
        let minLength minimum (value: string) =
            not (isNull value) && value.Length >= minimum

        /// <summary>Returns true when the string length is at most the supplied maximum.</summary>
        let maxLength maximum (value: string) =
            not (isNull value) && value.Length <= maximum

        /// <summary>Returns true when the string length lies inside the supplied inclusive bounds.</summary>
        let lengthBetween minimum maximum (value: string) =
            minLength minimum value && maxLength maximum value

        /// <summary>Returns true when the string length equals the supplied expected length.</summary>
        let length expected (value: string) =
            not (isNull value) && value.Length = expected

        /// <summary>Returns true when the string matches the supplied regular expression pattern.</summary>
        let matches pattern (value: string) =
            not (isNull value) && Regex.IsMatch(value, pattern)

        /// <summary>Returns true when the string matches Axial's pragmatic email format.</summary>
        let email (value: string) =
            not (isNull value) && emailRegex.IsMatch value

        /// <summary>Returns true when the string contains only numeric characters.</summary>
        let numeric (value: string) =
            not (isNull value) && numericRegex.IsMatch value

        /// <summary>Returns true when the string contains only letter or digit characters.</summary>
        let alphaNumeric (value: string) =
            not (isNull value) && value.Length > 0 && value |> Seq.forall Char.IsLetterOrDigit

    /// <summary>Boolean predicates for sequence-shaped values.</summary>
    module Seq =
        let private tryCount (values: #seq<'value>) =
            if Object.ReferenceEquals(values, null) then None
            else Some(Microsoft.FSharp.Collections.Seq.length values)

        /// <summary>Returns true when the sequence is non-null and empty.</summary>
        let empty values =
            tryCount values = Some 0

        /// <summary>Returns true when the sequence is non-null and contains at least one item.</summary>
        let notEmpty values =
            match tryCount values with
            | Some count -> count > 0
            | None -> false

        /// <summary>Returns true when the sequence is non-null and contains the supplied value.</summary>
        let contains expected (values: #seq<'value>) =
            not (Object.ReferenceEquals(values, null))
            && values |> Microsoft.FSharp.Collections.Seq.contains expected

        /// <summary>Returns true when the sequence is non-null and contains exactly the supplied count.</summary>
        let count expected values =
            tryCount values = Some expected

        /// <summary>Returns true when the sequence is non-null and contains at least the supplied count.</summary>
        let minCount minimum values =
            match tryCount values with
            | Some count -> count >= minimum
            | None -> false

        /// <summary>Returns true when the sequence is non-null and contains at most the supplied count.</summary>
        let maxCount maximum values =
            match tryCount values with
            | Some count -> count <= maximum
            | None -> false

        /// <summary>Returns true when the sequence is non-null and its count lies inside the supplied inclusive bounds.</summary>
        let countBetween minimum maximum values =
            minCount minimum values && maxCount maximum values

        /// <summary>Returns true when the sequence is non-null and contains exactly one item.</summary>
        let single values =
            count 1 values

        /// <summary>Returns true when the sequence is non-null and contains zero or one item.</summary>
        let atMostOne values =
            maxCount 1 values

        /// <summary>Returns true when the sequence is non-null and contains at least one item.</summary>
        let atLeastOne values =
            notEmpty values

        /// <summary>Returns true when the sequence is non-null and contains more than one item.</summary>
        let moreThanOne values =
            minCount 2 values

        /// <summary>Returns true when the sequence is non-null and contains duplicate values.</summary>
        let duplicates (values: #seq<'value>) =
            if Object.ReferenceEquals(values, null) then
                false
            else
                let seen = System.Collections.Generic.HashSet<'value>()
                values |> Microsoft.FSharp.Collections.Seq.exists (seen.Add >> not)

        /// <summary>Returns true when the sequence is non-null and contains no duplicate values.</summary>
        let distinct values =
            not (Object.ReferenceEquals(values, null)) && not (duplicates values)

    /// <summary>Boolean predicates for comparable values.</summary>
    module Number =
        /// <summary>Returns true when the value is greater than the supplied exclusive lower bound.</summary>
        let inline greaterThan minimum value =
            value > minimum

        /// <summary>Returns true when the value is less than the supplied exclusive upper bound.</summary>
        let inline lessThan maximum value =
            value < maximum

        /// <summary>Returns true when the value is greater than or equal to the supplied lower bound.</summary>
        let inline atLeast minimum value =
            value >= minimum

        /// <summary>Returns true when the value is less than or equal to the supplied upper bound.</summary>
        let inline atMost maximum value =
            value <= maximum

        /// <summary>Returns true when the value lies inside the supplied inclusive bounds.</summary>
        let inline between minimum maximum value =
            value >= minimum && value <= maximum

        /// <summary>Returns true when the value is greater than zero.</summary>
        let inline positive value =
            value > LanguagePrimitives.GenericZero

        /// <summary>Returns true when the value is greater than or equal to zero.</summary>
        let inline nonNegative value =
            value >= LanguagePrimitives.GenericZero

        /// <summary>Returns true when the value is less than zero.</summary>
        let inline negative value =
            value < LanguagePrimitives.GenericZero

        /// <summary>Returns true when the value is less than or equal to zero.</summary>
        let inline nonPositive value =
            value <= LanguagePrimitives.GenericZero
