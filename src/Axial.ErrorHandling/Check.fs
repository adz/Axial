namespace Axial.ErrorHandling

open System
open System.Text.RegularExpressions

/// <summary>Describes the length requirement that a value check expected a string-like value to satisfy.</summary>
type CheckLengthExpectation =
    /// <summary>The value was expected to have at least the supplied length.</summary>
    | MinimumLength of minimum: int
    /// <summary>The value was expected to have at most the supplied length.</summary>
    | MaximumLength of maximum: int
    /// <summary>The value was expected to have exactly the supplied length.</summary>
    | ExactLength of expected: int
    /// <summary>The value was expected to have a length inside the inclusive bounds.</summary>
    | LengthBetween of minimum: int * maximum: int

/// <summary>Describes the ordering requirement that a value check expected a comparable value to satisfy.</summary>
type CheckRangeExpectation =
    /// <summary>The value was expected to be greater than the supplied exclusive lower bound.</summary>
    | GreaterThan of minimumExclusive: string
    /// <summary>The value was expected to be less than the supplied exclusive upper bound.</summary>
    | LessThan of maximumExclusive: string
    /// <summary>The value was expected to be greater than or equal to the supplied lower bound.</summary>
    | AtLeast of minimumInclusive: string
    /// <summary>The value was expected to be less than or equal to the supplied upper bound.</summary>
    | AtMost of maximumInclusive: string
    /// <summary>The value was expected to be between the supplied inclusive bounds.</summary>
    | Between of minimumInclusive: string * maximumInclusive: string

/// <summary>Describes the count requirement that a value check expected a sequence-shaped value to satisfy.</summary>
type CheckCountExpectation =
    /// <summary>The sequence was expected to contain at least the supplied count.</summary>
    | MinimumCount of minimum: int
    /// <summary>The sequence was expected to contain at most the supplied count.</summary>
    | MaximumCount of maximum: int
    /// <summary>The sequence was expected to contain exactly the supplied count.</summary>
    | ExactCount of expected: int
    /// <summary>The sequence was expected to contain a count inside the inclusive bounds.</summary>
    | CountBetween of minimum: int * maximum: int

/// <summary>Describes the equality requirement that a value check expected a value to satisfy.</summary>
type CheckEqualityExpectation =
    /// <summary>The value was expected to equal the supplied value description.</summary>
    | EqualTo of expected: string
    /// <summary>The value was expected not to equal the supplied value description.</summary>
    | NotEqualTo of unexpected: string

/// <summary>Describes why an executable value check failed, without attaching source paths or raw input.</summary>
type CheckFailure =
    /// <summary>A required value was missing.</summary>
    | Missing
    /// <summary>A required text value was blank.</summary>
    | Blank
    /// <summary>The value did not match the expected format.</summary>
    | InvalidFormat of expected: string
    /// <summary>The value length did not match the expected length constraint.</summary>
    | Length of expectation: CheckLengthExpectation * actualLength: int option
    /// <summary>The value did not match the expected ordered range constraint.</summary>
    | Range of expectation: CheckRangeExpectation * actual: string option
    /// <summary>The sequence count did not match the expected count constraint.</summary>
    | Count of expectation: CheckCountExpectation * actualCount: int option
    /// <summary>The value did not match the expected equality constraint.</summary>
    | Equality of expectation: CheckEqualityExpectation * actual: string option
    /// <summary>A custom value check identified by an application-defined code failed.</summary>
    | CustomCode of code: string

/// <summary>
/// An executable, path-free value constraint that succeeds with <c>unit</c> or returns structured check failures.
/// </summary>
type Check<'value> = 'value -> Result<unit, CheckFailure list>

/// <summary>
/// Lightweight boolean predicates for local structural facts.
/// </summary>
/// <remarks>
/// These helpers return <c>bool</c> and intentionally live outside <c>Check</c>, where public helpers return
/// structured <see cref="T:Axial.ErrorHandling.Check`1" /> results.
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
        let isEmpty (value: string) =
            not (isNull value) && value.Length = 0

        /// <summary>Returns true when the string has at least one character and is non-null.</summary>
        let isNotEmpty (value: string) =
            not (isNull value) && value.Length > 0

        /// <summary>Returns true when the string is null, empty, or whitespace.</summary>
        let isBlank (value: string) =
            global.System.String.IsNullOrWhiteSpace value

        /// <summary>Returns true when the string is non-null and contains at least one non-whitespace character.</summary>
        let isNotBlank value =
            not (isBlank value)

        /// <summary>Returns true when the string length is at least the supplied minimum.</summary>
        let hasMinLength minimum (value: string) =
            not (isNull value) && value.Length >= minimum

        /// <summary>Returns true when the string length is at most the supplied maximum.</summary>
        let hasMaxLength maximum (value: string) =
            not (isNull value) && value.Length <= maximum

        /// <summary>Returns true when the string length equals the supplied expected length.</summary>
        let hasLength expected (value: string) =
            not (isNull value) && value.Length = expected

        /// <summary>Returns true when the string matches the supplied regular expression pattern.</summary>
        let matches pattern (value: string) =
            not (isNull value) && Regex.IsMatch(value, pattern)

        /// <summary>Returns true when the string matches Axial's pragmatic email format.</summary>
        let isEmail (value: string) =
            not (isNull value) && emailRegex.IsMatch value

        /// <summary>Returns true when the string contains only numeric characters.</summary>
        let isNumeric (value: string) =
            not (isNull value) && numericRegex.IsMatch value

        /// <summary>Returns true when the string contains only letter or digit characters.</summary>
        let isAlphaNumeric (value: string) =
            not (isNull value) && value.Length > 0 && value |> Seq.forall Char.IsLetterOrDigit

    /// <summary>Boolean predicates for sequence-shaped values.</summary>
    module Seq =
        let private tryCount (values: #seq<'value>) =
            if Object.ReferenceEquals(values, null) then None
            else Some(Microsoft.FSharp.Collections.Seq.length values)

        /// <summary>Returns true when the sequence is non-null and empty.</summary>
        let isEmpty values =
            tryCount values = Some 0

        /// <summary>Returns true when the sequence is non-null and contains at least one item.</summary>
        let isNotEmpty values =
            match tryCount values with
            | Some count -> count > 0
            | None -> false

        /// <summary>Returns true when the sequence is non-null and contains the supplied value.</summary>
        let contains expected (values: #seq<'value>) =
            not (Object.ReferenceEquals(values, null))
            && values |> Microsoft.FSharp.Collections.Seq.contains expected

        /// <summary>Returns true when the sequence is non-null and contains exactly the supplied count.</summary>
        let hasCount expected values =
            tryCount values = Some expected

        /// <summary>Returns true when the sequence is non-null and contains exactly one item.</summary>
        let isSingle values =
            hasCount 1 values

        /// <summary>Returns true when the sequence is non-null and contains zero or one item.</summary>
        let atMostOne values =
            match tryCount values with
            | Some count -> count <= 1
            | None -> false

        /// <summary>Returns true when the sequence is non-null and contains at least one item.</summary>
        let atLeastOne values =
            isNotEmpty values

        /// <summary>Returns true when the sequence is non-null and contains more than one item.</summary>
        let moreThanOne values =
            match tryCount values with
            | Some count -> count > 1
            | None -> false

        /// <summary>Returns true when the sequence is non-null and contains duplicate values.</summary>
        let hasDuplicates (values: #seq<'value>) =
            if Object.ReferenceEquals(values, null) then
                false
            else
                let seen = Collections.Generic.HashSet<'value>()
                values |> Microsoft.FSharp.Collections.Seq.exists (seen.Add >> not)

        /// <summary>Returns true when the sequence is non-null and contains no duplicate values.</summary>
        let isDistinct values =
            not (Object.ReferenceEquals(values, null)) && not (hasDuplicates values)

    /// <summary>Boolean predicates for comparable values.</summary>
    module Compare =
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

/// <summary>
/// Typed value-check programs for local structural facts.
/// </summary>
/// <remarks>
/// The nested modules such as <c>Check.String</c>, <c>Check.Number</c>, and <c>Check.Seq</c> return
/// <see cref="T:Axial.ErrorHandling.Check`1" /> programs. Common top-level helpers such as
/// <c>lengthBetween</c>, <c>between</c>, and <c>countBetween</c> are structured checks for single-target values.
/// </remarks>
[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Check =
    let private emailRegex =
        Regex(@"^[^@]+@[^@]+$", RegexOptions.Compiled ||| RegexOptions.IgnoreCase)

    let private numericRegex =
        Regex(@"^\d+$", RegexOptions.Compiled)

    /// <summary>Executable, path-free value checks for already parsed strings.</summary>
    module String =
        let private pass : Result<unit, CheckFailure list> = Ok ()

        let private fail failure : Result<unit, CheckFailure list> =
            Error [ failure ]

        let private actualLength (value: string) =
            if isNull value then None else Some value.Length

        let private actualString (value: string) =
            if isNull value then None else Some value

        /// <summary>Requires an already parsed string value to be non-null and contain at least one non-whitespace character.</summary>
        let present : Check<string> =
            fun value ->
                if isNull value then fail Missing
                elif global.System.String.IsNullOrWhiteSpace value then fail Blank
                else pass

        /// <summary>Requires an already parsed string value to be exactly empty. Null fails as a missing value.</summary>
        let empty : Check<string> =
            fun value ->
                if isNull value then fail Missing
                elif value.Length = 0 then pass
                else fail (Length(ExactLength 0, Some value.Length))

        /// <summary>Requires an already parsed string value to contain at least one character. Whitespace counts as present text.</summary>
        let notEmpty : Check<string> =
            fun value ->
                if isNull value then fail Missing
                elif value.Length > 0 then pass
                else fail (Length(MinimumLength 1, Some 0))

        /// <summary>Requires an already parsed string value to have at least the supplied length. Null fails with an unknown actual length.</summary>
        let minLength (minimum: int) : Check<string> =
            fun value ->
                match actualLength value with
                | Some length when length >= minimum -> pass
                | actual -> fail (Length(MinimumLength minimum, actual))

        /// <summary>Requires an already parsed string value to have at most the supplied length. Null fails with an unknown actual length.</summary>
        let maxLength (maximum: int) : Check<string> =
            fun value ->
                match actualLength value with
                | Some length when length <= maximum -> pass
                | actual -> fail (Length(MaximumLength maximum, actual))

        /// <summary>Requires an already parsed string value length to lie inside the supplied inclusive bounds. Null fails with an unknown actual length.</summary>
        let lengthBetween (minimum: int) (maximum: int) : Check<string> =
            fun value ->
                match actualLength value with
                | Some length when length >= minimum && length <= maximum -> pass
                | actual -> fail (Length(LengthBetween(minimum, maximum), actual))

        /// <summary>Requires an already parsed string value to have exactly the supplied length. Null fails with an unknown actual length.</summary>
        let length (expected: int) : Check<string> =
            fun value ->
                match actualLength value with
                | Some length when length = expected -> pass
                | actual -> fail (Length(ExactLength expected, actual))

        /// <summary>Requires an already parsed string value to have exactly the supplied length. Null fails with an unknown actual length.</summary>
        let exactLength (expected: int) : Check<string> =
            length expected

        /// <summary>Requires an already parsed string value to match Axial's pragmatic email format.</summary>
        let email : Check<string> =
            fun value ->
                if not (isNull value) && emailRegex.IsMatch value then pass
                else fail (InvalidFormat "email")

        /// <summary>Requires an already parsed string value to match the supplied regular expression pattern.</summary>
        let matches (pattern: string) : Check<string> =
            fun value ->
                if not (isNull value) && Regex.IsMatch(value, pattern) then pass
                else fail (InvalidFormat pattern)

        /// <summary>Requires an already parsed string value to contain only numeric characters.</summary>
        let numeric : Check<string> =
            fun value ->
                if not (isNull value) && numericRegex.IsMatch value then pass
                else fail (InvalidFormat "numeric")

        /// <summary>Requires an already parsed string value to contain only letter or digit characters.</summary>
        let alphaNumeric : Check<string> =
            fun value ->
                if not (isNull value) && value.Length > 0 && value |> Seq.forall Char.IsLetterOrDigit then pass
                else fail (InvalidFormat "alphaNumeric")

        /// <summary>Requires an already parsed string value to equal one of the supplied choices. Null fails with an unknown actual value.</summary>
        let oneOf (choices: string seq) : Check<string> =
            let choices = choices |> Seq.toList
            let expected = global.System.String.Join("|", choices)

            fun value ->
                if not (isNull value) && List.contains value choices then pass
                else fail (Equality(EqualTo expected, actualString value))

    /// <summary>Executable, path-free value checks for already parsed ordered values.</summary>
    module Number =
        /// <summary>Requires a value to lie inside the supplied inclusive bounds.</summary>
        let inline between minimum maximum : Check<'value> =
            fun value ->
                if value >= minimum && value <= maximum then Ok ()
                else Error [ Range(Between(string minimum, string maximum), Some(string value)) ]

        /// <summary>Requires a value to be greater than the supplied exclusive lower bound.</summary>
        let inline greaterThan minimum : Check<'value> =
            fun value ->
                if value > minimum then Ok ()
                else Error [ Range(GreaterThan(string minimum), Some(string value)) ]

        /// <summary>Requires a value to be less than the supplied exclusive upper bound.</summary>
        let inline lessThan maximum : Check<'value> =
            fun value ->
                if value < maximum then Ok ()
                else Error [ Range(LessThan(string maximum), Some(string value)) ]

        /// <summary>Requires a value to be greater than or equal to the supplied lower bound.</summary>
        let inline atLeast minimum : Check<'value> =
            fun value ->
                if value >= minimum then Ok ()
                else Error [ Range(AtLeast(string minimum), Some(string value)) ]

        /// <summary>Requires a value to be less than or equal to the supplied upper bound.</summary>
        let inline atMost maximum : Check<'value> =
            fun value ->
                if value <= maximum then Ok ()
                else Error [ Range(AtMost(string maximum), Some(string value)) ]

        /// <summary>Requires a value to be greater than zero.</summary>
        let inline positive value =
            greaterThan LanguagePrimitives.GenericZero value

        /// <summary>Requires a value to be greater than or equal to zero.</summary>
        let inline nonNegative value =
            atLeast LanguagePrimitives.GenericZero value

        /// <summary>Requires a value to be less than zero.</summary>
        let inline negative value =
            lessThan LanguagePrimitives.GenericZero value

        /// <summary>Requires a value to be less than or equal to zero.</summary>
        let inline nonPositive value =
            atMost LanguagePrimitives.GenericZero value

    /// <summary>Executable, path-free value checks for already parsed sequence-shaped values.</summary>
    module Seq =
        let private pass : Result<unit, CheckFailure list> = Ok ()

        let private fail failure : Result<unit, CheckFailure list> =
            Error [ failure ]

        let private actualCount (values: #seq<'value>) =
            if Object.ReferenceEquals(values, null) then None
            else Some(Microsoft.FSharp.Collections.Seq.length values)

        /// <summary>Requires an already parsed sequence-shaped value to contain at least one item. Null fails with an unknown actual count.</summary>
        let notEmpty : Check<#seq<'value>> =
            fun values ->
                match actualCount values with
                | Some count when count > 0 -> pass
                | actual -> fail (Count(MinimumCount 1, actual))

        /// <summary>Requires an already parsed sequence-shaped value to contain no items. Null fails with an unknown actual count.</summary>
        let empty : Check<#seq<'value>> =
            fun values ->
                match actualCount values with
                | Some 0 -> pass
                | actual -> fail (Count(ExactCount 0, actual))

        /// <summary>Requires an already parsed sequence-shaped value to contain exactly the supplied count. Null fails with an unknown actual count.</summary>
        let count (expected: int) : Check<#seq<'value>> =
            fun values ->
                match actualCount values with
                | Some count when count = expected -> pass
                | actual -> fail (Count(ExactCount expected, actual))

        /// <summary>Requires an already parsed sequence-shaped value to contain at least the supplied count. Null fails with an unknown actual count.</summary>
        let minCount (minimum: int) : Check<#seq<'value>> =
            fun values ->
                match actualCount values with
                | Some count when count >= minimum -> pass
                | actual -> fail (Count(MinimumCount minimum, actual))

        /// <summary>Requires an already parsed sequence-shaped value to contain at most the supplied count. Null fails with an unknown actual count.</summary>
        let maxCount (maximum: int) : Check<#seq<'value>> =
            fun values ->
                match actualCount values with
                | Some count when count <= maximum -> pass
                | actual -> fail (Count(MaximumCount maximum, actual))

        /// <summary>Requires an already parsed sequence-shaped value count to lie inside the supplied inclusive bounds. Null fails with an unknown actual count.</summary>
        let countBetween (minimum: int) (maximum: int) : Check<#seq<'value>> =
            fun values ->
                match actualCount values with
                | Some count when count >= minimum && count <= maximum -> pass
                | actual -> fail (Count(CountBetween(minimum, maximum), actual))

        /// <summary>Requires an already parsed sequence-shaped value to contain no duplicate values.</summary>
        let noDuplicates : Check<#seq<'value>> =
            fun values ->
                if Object.ReferenceEquals(values, null) then
                    fail Missing
                else
                    let seen = Collections.Generic.HashSet<'value>()

                    if values |> Microsoft.FSharp.Collections.Seq.forall seen.Add then
                        pass
                    else
                        fail (CustomCode "seq.distinct")

        /// <summary>Requires an already parsed sequence-shaped value to contain the supplied value.</summary>
        let contains (expected: 'value) : Check<#seq<'value>> =
            fun values ->
                if Object.ReferenceEquals(values, null) then
                    fail Missing
                elif values |> Microsoft.FSharp.Collections.Seq.contains expected then
                    pass
                else
                    fail (Equality(EqualTo(string expected), None))

        /// <summary>Requires an already parsed sequence-shaped value to contain exactly one item.</summary>
        let single (values: #seq<'value>) : Result<unit, CheckFailure list> =
            count 1 values

        /// <summary>Requires an already parsed sequence-shaped value to contain zero or one item.</summary>
        let atMostOne (values: #seq<'value>) : Result<unit, CheckFailure list> =
            maxCount 1 values

        /// <summary>Requires an already parsed sequence-shaped value to contain at least one item.</summary>
        let atLeastOne (values: #seq<'value>) : Result<unit, CheckFailure list> =
            minCount 1 values

        /// <summary>Requires an already parsed sequence-shaped value to contain more than one item.</summary>
        let moreThanOne (values: #seq<'value>) : Result<unit, CheckFailure list> =
            minCount 2 values

    /// <summary>Executable, path-free value checks for already parsed optional values.</summary>
    module Option =
        let private pass : Result<unit, CheckFailure list> = Ok ()

        let private fail failure : Result<unit, CheckFailure list> =
            Error [ failure ]

        /// <summary>Requires an option to contain a value.</summary>
        let some : Check<'value option> =
            fun value ->
                match value with
                | Some _ -> pass
                | None -> fail Missing

        /// <summary>Alias for <c>some</c>; requires an option to contain a value.</summary>
        let present : Check<'value option> =
            some

        /// <summary>Requires an option to contain no value.</summary>
        let none : Check<'value option> =
            fun value ->
                match value with
                | None -> pass
                | Some _ -> fail (Equality(EqualTo "None", Some "Some"))

        /// <summary>Alias for <c>none</c>; requires an option to contain no value.</summary>
        let empty : Check<'value option> =
            none

        /// <summary>Alias for <c>some</c>; requires an option to contain a value.</summary>
        let notEmpty : Check<'value option> =
            some

    /// <summary>Executable, path-free value checks for already parsed value option values.</summary>
    module ValueOption =
        let private pass : Result<unit, CheckFailure list> = Ok ()

        let private fail failure : Result<unit, CheckFailure list> =
            Error [ failure ]

        /// <summary>Requires a value option to contain a value.</summary>
        let some : Check<'value voption> =
            fun value ->
                match value with
                | ValueSome _ -> pass
                | ValueNone -> fail Missing

        /// <summary>Alias for <c>some</c>; requires a value option to contain a value.</summary>
        let present : Check<'value voption> =
            some

        /// <summary>Requires a value option to contain no value.</summary>
        let none : Check<'value voption> =
            fun value ->
                match value with
                | ValueNone -> pass
                | ValueSome _ -> fail (Equality(EqualTo "ValueNone", Some "ValueSome"))

        /// <summary>Alias for <c>none</c>; requires a value option to contain no value.</summary>
        let empty : Check<'value voption> =
            none

        /// <summary>Alias for <c>some</c>; requires a value option to contain a value.</summary>
        let notEmpty : Check<'value voption> =
            some

    /// <summary>Executable, path-free value checks for already parsed nullable values.</summary>
    module Nullable =
        let private pass : Result<unit, CheckFailure list> = Ok ()

        let private fail failure : Result<unit, CheckFailure list> =
            Error [ failure ]

        /// <summary>Requires a nullable value to contain a value.</summary>
        let hasValue : Check<System.Nullable<'value>> =
            fun value -> if value.HasValue then pass else fail Missing

        /// <summary>Alias for <c>hasValue</c>; requires a nullable value to contain a value.</summary>
        let present : Check<System.Nullable<'value>> =
            hasValue

        /// <summary>Requires a nullable value to contain no value.</summary>
        let hasNoValue : Check<System.Nullable<'value>> =
            fun value -> if value.HasValue then fail (Equality(EqualTo "null", Some "value")) else pass

        /// <summary>Alias for <c>hasNoValue</c>; requires a nullable value to contain no value.</summary>
        let empty : Check<System.Nullable<'value>> =
            hasNoValue

        /// <summary>Alias for <c>hasValue</c>; requires a nullable value to contain a value.</summary>
        let notEmpty : Check<System.Nullable<'value>> =
            hasValue

    /// <summary>Executable, path-free value checks for result values.</summary>
    module Result =
        let private pass : Result<unit, CheckFailure list> = Ok ()

        let private fail failure : Result<unit, CheckFailure list> =
            Error [ failure ]

        /// <summary>Requires a result to contain a successful value.</summary>
        let ok : Check<Result<'value, 'error>> =
            fun value ->
                match value with
                | Ok _ -> pass
                | Error _ -> fail (Equality(EqualTo "Ok", Some "Error"))

        /// <summary>Requires a result to contain an error value.</summary>
        let error : Check<Result<'value, 'error>> =
            fun value ->
                match value with
                | Error _ -> pass
                | Ok _ -> fail (Equality(EqualTo "Error", Some "Ok"))

    let private pass : Result<unit, CheckFailure list> = Ok ()

    let private fail failure : Result<unit, CheckFailure list> =
        Error [ failure ]

    let private actualValue value =
        if Object.ReferenceEquals(box value, null) then None else Some(string value)

    /// <summary>Requires an already parsed string value to have exactly the supplied length.</summary>
    let length (expected: int) : Check<string> =
        String.length expected

    /// <summary>Requires an already parsed string value to have at least the supplied length.</summary>
    let minLength (minimum: int) : Check<string> =
        String.minLength minimum

    /// <summary>Requires an already parsed string value to have at most the supplied length.</summary>
    let maxLength (maximum: int) : Check<string> =
        String.maxLength maximum

    /// <summary>Requires an already parsed string value length to lie inside the supplied inclusive bounds.</summary>
    let lengthBetween (minimum: int) (maximum: int) : Check<string> =
        String.lengthBetween minimum maximum

    /// <summary>Requires an already parsed string value to match Axial's pragmatic email format.</summary>
    let email (value: string) : Result<unit, CheckFailure list> =
        String.email value

    /// <summary>Requires an already parsed string value to match the supplied regular expression pattern.</summary>
    let matches (pattern: string) : Check<string> =
        String.matches pattern

    /// <summary>Requires an already parsed string value to equal one of the supplied choices.</summary>
    let oneOf (choices: string seq) : Check<string> =
        String.oneOf choices

    /// <summary>Requires a value to lie inside the supplied inclusive bounds.</summary>
    let inline between minimum maximum : Check<'value> =
        Number.between minimum maximum

    /// <summary>Requires a value to be greater than the supplied exclusive lower bound.</summary>
    let inline greaterThan minimum : Check<'value> =
        Number.greaterThan minimum

    /// <summary>Requires a value to be less than the supplied exclusive upper bound.</summary>
    let inline lessThan maximum : Check<'value> =
        Number.lessThan maximum

    /// <summary>Requires a value to be greater than or equal to the supplied lower bound.</summary>
    let inline atLeast minimum : Check<'value> =
        Number.atLeast minimum

    /// <summary>Requires a value to be less than or equal to the supplied upper bound.</summary>
    let inline atMost maximum : Check<'value> =
        Number.atMost maximum

    /// <summary>Requires a value to be greater than zero.</summary>
    let inline positive value =
        Number.positive value

    /// <summary>Requires a value to be greater than or equal to zero.</summary>
    let inline nonNegative value =
        Number.nonNegative value

    /// <summary>Requires a value to be less than zero.</summary>
    let inline negative value =
        Number.negative value

    /// <summary>Requires a value to be less than or equal to zero.</summary>
    let inline nonPositive value =
        Number.nonPositive value

    /// <summary>Requires an already parsed sequence-shaped value to contain exactly the supplied count.</summary>
    let count (expected: int) : Check<#seq<'value>> =
        Seq.count expected

    /// <summary>Requires an already parsed sequence-shaped value to contain at least the supplied count.</summary>
    let minCount (minimum: int) : Check<#seq<'value>> =
        Seq.minCount minimum

    /// <summary>Requires an already parsed sequence-shaped value to contain at most the supplied count.</summary>
    let maxCount (maximum: int) : Check<#seq<'value>> =
        Seq.maxCount maximum

    /// <summary>Requires an already parsed sequence-shaped value count to lie inside the supplied inclusive bounds.</summary>
    let countBetween (minimum: int) (maximum: int) : Check<#seq<'value>> =
        Seq.countBetween minimum maximum

    /// <summary>Requires an already parsed sequence-shaped value to contain no duplicate values.</summary>
    let distinct (values: #seq<'value>) : Result<unit, CheckFailure list> =
        Seq.noDuplicates values

    /// <summary>Requires an already parsed sequence-shaped value to contain the supplied value.</summary>
    let contains (expected: 'value) : Check<#seq<'value>> =
        fun values -> Seq.contains expected values

    /// <summary>Requires an already parsed sequence-shaped value to contain exactly one item.</summary>
    let single (values: #seq<'value>) : Result<unit, CheckFailure list> =
        Seq.single values

    /// <summary>Requires an already parsed sequence-shaped value to contain zero or one item.</summary>
    let atMostOne (values: #seq<'value>) : Result<unit, CheckFailure list> =
        Seq.atMostOne values

    /// <summary>Requires an already parsed sequence-shaped value to contain at least one item.</summary>
    let atLeastOne (values: #seq<'value>) : Result<unit, CheckFailure list> =
        Seq.atLeastOne values

    /// <summary>Requires an already parsed sequence-shaped value to contain more than one item.</summary>
    let moreThanOne (values: #seq<'value>) : Result<unit, CheckFailure list> =
        Seq.moreThanOne values

    [<System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)>]
    type Present =
        static member Apply(value: string, _: 'result, _: Present) : 'result =
            String.present value |> box :?> 'result

        static member Apply(value: 'value option, _: 'result, _: Present) : 'result =
            Option.present value |> box :?> 'result

        static member Apply(value: 'value voption, _: 'result, _: Present) : 'result =
            ValueOption.present value |> box :?> 'result

        static member Apply(value: System.Nullable<'value>, _: 'result, _: Present) : 'result =
            Nullable.present value |> box :?> 'result

        static member inline Invoke(value: ^value) : 'result =
            ((^value or Present): (static member Apply: ^value * 'result * Present -> 'result)
                (value, Unchecked.defaultof<'result>, Unchecked.defaultof<Present>))

    [<System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)>]
    type Empty =
        static member Apply(value: string, _: 'result, _: Empty) : 'result =
            String.empty value |> box :?> 'result

        static member Apply(value: 'value option, _: 'result, _: Empty) : 'result =
            Option.empty value |> box :?> 'result

        static member Apply(value: 'value voption, _: 'result, _: Empty) : 'result =
            ValueOption.empty value |> box :?> 'result

        static member Apply(value: System.Nullable<'value>, _: 'result, _: Empty) : 'result =
            Nullable.empty value |> box :?> 'result

        static member inline Invoke(value: ^value) : 'result =
            ((^value or Empty): (static member Apply: ^value * 'result * Empty -> 'result)
                (value, Unchecked.defaultof<'result>, Unchecked.defaultof<Empty>))

    [<System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)>]
    type NotEmpty =
        static member Apply(value: string, _: 'result, _: NotEmpty) : 'result =
            String.notEmpty value |> box :?> 'result

        static member Apply(value: 'value option, _: 'result, _: NotEmpty) : 'result =
            Option.notEmpty value |> box :?> 'result

        static member Apply(value: 'value voption, _: 'result, _: NotEmpty) : 'result =
            ValueOption.notEmpty value |> box :?> 'result

        static member Apply(value: System.Nullable<'value>, _: 'result, _: NotEmpty) : 'result =
            Nullable.notEmpty value |> box :?> 'result

        static member inline Invoke(value: ^value) : 'result =
            ((^value or NotEmpty): (static member Apply: ^value * 'result * NotEmpty -> 'result)
                (value, Unchecked.defaultof<'result>, Unchecked.defaultof<NotEmpty>))

    /// <summary>Requires an already parsed optional, nullable, or text value to be present.</summary>
    let inline present value : Result<unit, CheckFailure list> =
        Present.Invoke value

    /// <summary>Requires an already parsed optional, nullable, or text value to be empty.</summary>
    let inline empty value : Result<unit, CheckFailure list> =
        Empty.Invoke value

    /// <summary>Requires an already parsed optional, nullable, or text value to be non-empty.</summary>
    let inline notEmpty value : Result<unit, CheckFailure list> =
        NotEmpty.Invoke value

    /// <summary>Requires the actual value to equal the supplied expected value.</summary>
    let equalTo (expected: 'value) : Check<'value> =
        fun actual ->
            if actual = expected then pass
            else fail (Equality(EqualTo(string expected), actualValue actual))

    /// <summary>Requires the actual value not to equal the supplied unexpected value.</summary>
    let notEqualTo (unexpected: 'value) : Check<'value> =
        fun actual ->
            if actual <> unexpected then pass
            else fail (Equality(NotEqualTo(string unexpected), actualValue actual))

    /// <summary>Combines checks conjunctively by running every check against the value and accumulating all failures. An empty list succeeds.</summary>
    let all (checks: Check<'value> list) : Check<'value> =
        fun value ->
            let failures =
                checks
                |> List.collect (fun check ->
                    match check value with
                    | Ok () -> []
                    | Error failures -> failures)

            if List.isEmpty failures then Ok () else Error failures

    /// <summary>Combines checks disjunctively by running checks until one succeeds, or returns accumulated failures when every check fails. An empty list fails with no failures.</summary>
    let any (checks: Check<'value> list) : Check<'value> =
        fun value ->
            let rec loop failures remaining =
                match remaining with
                | [] -> Error(List.rev failures)
                | check :: rest ->
                    match check value with
                    | Ok () -> Ok ()
                    | Error nextFailures -> loop (List.rev nextFailures @ failures) rest

            loop [] checks

    /// <summary>Inverts a check. A successful inner check becomes a custom-code failure, while any failed inner check succeeds.</summary>
    let ``not`` (check: Check<'value>) : Check<'value> =
        fun value ->
            match check value with
            | Ok () -> Error [ CustomCode "check.not" ]
            | Error _ -> Ok ()

    /// <summary>Maps every failure produced by a check.</summary>
    let mapFailure (mapper: CheckFailure -> CheckFailure) (check: Check<'value>) : Check<'value> =
        fun value ->
            match check value with
            | Ok () -> Ok ()
            | Error failures -> Error(List.map mapper failures)
