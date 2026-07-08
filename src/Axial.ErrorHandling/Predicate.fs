namespace Axial.ErrorHandling

open System
open System.Text.RegularExpressions

/// <summary>
/// Boolean predicates for local structural facts, exposed as extension members directly on the types they describe.
/// </summary>
/// <remarks>
/// These members return <c>bool</c> and intentionally live outside the <c>Check</c> module, where public helpers
/// return structured <see cref="T:Axial.ErrorHandling.Check`1" /> results. Use predicates for local branching and
/// <c>Check</c> programs when callers need typed failure details. This module is <c>AutoOpen</c> so the extension
/// members are available wherever <c>Axial.ErrorHandling</c> is opened.
/// </remarks>
[<AutoOpen>]
module PredicateExtensions =
    let private emailRegex =
        Regex(@"^[^@]+@[^@]+$", RegexOptions.Compiled ||| RegexOptions.IgnoreCase)

    let private numericRegex =
        Regex(@"^\d+$", RegexOptions.Compiled)

    /// <summary>Boolean predicates for strings.</summary>
    type System.String with

        /// <summary>True when the string is exactly empty and non-null.</summary>
        member this.IsEmpty = not (isNull this) && this.Length = 0

        /// <summary>True when the string has at least one character and is non-null.</summary>
        member this.IsNotEmpty = not (isNull this) && this.Length > 0

        /// <summary>True when the string is null, empty, or whitespace.</summary>
        member this.IsBlank = String.IsNullOrWhiteSpace this

        /// <summary>True when the string is non-null and contains at least one non-whitespace character.</summary>
        member this.IsNotBlank = not this.IsBlank

        /// <summary>Alias for <c>IsBlank</c>; true when the string is null, empty, or whitespace.</summary>
        member this.IsAbsent = this.IsBlank

        /// <summary>Alias for <c>IsNotBlank</c>; true when the string is non-null and contains at least one non-whitespace character.</summary>
        member this.IsPresent = this.IsNotBlank

        /// <summary>True when the string matches Axial's pragmatic email format.</summary>
        member this.IsEmail = not (isNull this) && emailRegex.IsMatch this

        /// <summary>True when the string contains only numeric characters.</summary>
        member this.IsNumeric = not (isNull this) && numericRegex.IsMatch this

        /// <summary>True when the string contains only letter or digit characters.</summary>
        member this.IsAlphaNumeric =
            not (isNull this) && this.Length > 0 && this |> Seq.forall Char.IsLetterOrDigit

        /// <summary>True when the string length is at least the supplied minimum.</summary>
        member this.HasMinLength(minimum: int) =
            not (isNull this) && this.Length >= minimum

        /// <summary>True when the string length is at most the supplied maximum.</summary>
        member this.HasMaxLength(maximum: int) =
            not (isNull this) && this.Length <= maximum

        /// <summary>True when the string length lies inside the supplied inclusive bounds.</summary>
        member this.HasLengthBetween(minimum: int, maximum: int) =
            this.HasMinLength minimum && this.HasMaxLength maximum

        /// <summary>True when the string length equals the supplied expected length.</summary>
        member this.HasLength(expected: int) =
            not (isNull this) && this.Length = expected

        /// <summary>True when the string matches the supplied regular expression pattern.</summary>
        member this.MatchesPattern(pattern: string) =
            not (isNull this) && Regex.IsMatch(this, pattern)

    /// <summary>Boolean predicates for option values.</summary>
    type Option<'value> with

        /// <summary>True when the option contains a value.</summary>
        member this.IsPresent = Microsoft.FSharp.Core.Option.isSome this

        /// <summary>True when the option contains no value.</summary>
        member this.IsAbsent = Microsoft.FSharp.Core.Option.isNone this

    /// <summary>Boolean predicates for value option values.</summary>
    type ValueOption<'value> with

        /// <summary>True when the value option contains a value.</summary>
        member this.IsPresent =
            match this with
            | ValueSome _ -> true
            | ValueNone -> false

        /// <summary>True when the value option contains no value.</summary>
        member this.IsAbsent = not this.IsPresent

    /// <summary>Boolean predicates for nullable values.</summary>
    type System.Nullable<'value when 'value: struct and 'value: (new: unit -> 'value) and 'value :> System.ValueType> with

        /// <summary>True when the nullable value contains a value.</summary>
        member this.IsPresent = this.HasValue

        /// <summary>True when the nullable value contains no value.</summary>
        member this.IsAbsent = not this.HasValue

    /// <summary>Boolean predicates for result values.</summary>
    type Result<'value, 'error> with

        /// <summary>True when the result is successful.</summary>
        member this.IsOk =
            match this with
            | Ok _ -> true
            | Error _ -> false

        /// <summary>True when the result is failed.</summary>
        member this.IsError = not this.IsOk

    /// <summary>Boolean predicates for sequence-shaped values (lists, arrays, and other <c>seq</c>-typed values).</summary>
    type System.Collections.Generic.IEnumerable<'value> with

        /// <summary>True when the sequence is non-null and empty.</summary>
        member this.HasNoItems =
            not (Object.ReferenceEquals(this, null)) && Microsoft.FSharp.Collections.Seq.isEmpty this

        /// <summary>True when the sequence is non-null and contains at least one item.</summary>
        member this.HasItems =
            not (Object.ReferenceEquals(this, null)) && not (Microsoft.FSharp.Collections.Seq.isEmpty this)

        /// <summary>Alias for <c>HasNoItems</c>; true when the sequence is non-null and empty.</summary>
        member this.IsAbsent = this.HasNoItems

        /// <summary>Alias for <c>HasItems</c>; true when the sequence is non-null and contains at least one item.</summary>
        member this.IsPresent = this.HasItems

        /// <summary>True when the sequence is non-null and contains exactly the supplied count.</summary>
        member this.HasCount(expected: int) =
            not (Object.ReferenceEquals(this, null)) && Microsoft.FSharp.Collections.Seq.length this = expected

        /// <summary>True when the sequence is non-null and contains at least the supplied count.</summary>
        member this.HasMinCount(minimum: int) =
            not (Object.ReferenceEquals(this, null)) && Microsoft.FSharp.Collections.Seq.length this >= minimum

        /// <summary>True when the sequence is non-null and contains at most the supplied count.</summary>
        member this.HasMaxCount(maximum: int) =
            not (Object.ReferenceEquals(this, null)) && Microsoft.FSharp.Collections.Seq.length this <= maximum

        /// <summary>True when the sequence is non-null and its count lies inside the supplied inclusive bounds.</summary>
        member this.HasCountBetween(minimum: int, maximum: int) =
            this.HasMinCount minimum && this.HasMaxCount maximum

        /// <summary>True when the sequence is non-null and contains exactly one item.</summary>
        member this.HasSingleItem = this.HasCount 1

        /// <summary>True when the sequence is non-null and contains zero or one item.</summary>
        member this.HasAtMostOneItem = this.HasMaxCount 1

        /// <summary>True when the sequence is non-null and contains more than one item.</summary>
        member this.HasMoreThanOneItem = this.HasMinCount 2

        /// <summary>True when the sequence is non-null and contains the supplied value.</summary>
        member this.HasItem(expected: 'value) : bool =
            if Object.ReferenceEquals(this, null) then
                false
            else
                let comparer = System.Collections.Generic.EqualityComparer<'value>.Default
                this |> Microsoft.FSharp.Collections.Seq.exists (fun item -> comparer.Equals(item, expected))

        /// <summary>True when the sequence is non-null and contains duplicate values.</summary>
        member this.HasDuplicates =
            if Object.ReferenceEquals(this, null) then
                false
            else
                let seen = System.Collections.Generic.HashSet<'value>()
                this |> Microsoft.FSharp.Collections.Seq.exists (seen.Add >> not)

        /// <summary>True when the sequence is non-null and contains no duplicate values.</summary>
        member this.IsDistinct =
            not (Object.ReferenceEquals(this, null)) && not this.HasDuplicates

/// <summary>
/// A small type-directed facade over the most common presence predicates, plus type-specific helpers that don't fit
/// as extension members on an unconstrained generic type.
/// </summary>
[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Predicate =
    /// <summary>Boolean predicates for reference values.</summary>
    module Reference =
        /// <summary>Returns true when the reference is null.</summary>
        let isNull (value: 'value when 'value: null) =
            Object.ReferenceEquals(value, null)

        /// <summary>Returns true when the reference is not null.</summary>
        let notNull (value: 'value when 'value: null) =
            not (isNull value)

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

    [<System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)>]
    type Present =
        static member Apply(value: string, _: 'result, _: Present) : 'result =
            value.IsNotBlank |> box :?> 'result

        static member Apply(value: 'value option, _: 'result, _: Present) : 'result =
            value.IsPresent |> box :?> 'result

        static member Apply(value: 'value voption, _: 'result, _: Present) : 'result =
            value.IsPresent |> box :?> 'result

        static member Apply(value: System.Nullable<'value>, _: 'result, _: Present) : 'result =
            value.IsPresent |> box :?> 'result

        static member inline Invoke(value: ^value) : 'result =
            ((^value or Present): (static member Apply: ^value * 'result * Present -> 'result)
                (value, Unchecked.defaultof<'result>, Unchecked.defaultof<Present>))

    [<System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)>]
    type Empty =
        static member Apply(value: string, _: 'result, _: Empty) : 'result =
            value.IsEmpty |> box :?> 'result

        static member Apply(value: 'value option, _: 'result, _: Empty) : 'result =
            value.IsAbsent |> box :?> 'result

        static member Apply(value: 'value voption, _: 'result, _: Empty) : 'result =
            value.IsAbsent |> box :?> 'result

        static member Apply(value: System.Nullable<'value>, _: 'result, _: Empty) : 'result =
            value.IsAbsent |> box :?> 'result

        static member Apply(value: 'value list, _: 'result, _: Empty) : 'result =
            value.HasNoItems |> box :?> 'result

        static member Apply(value: 'value array, _: 'result, _: Empty) : 'result =
            value.HasNoItems |> box :?> 'result

        static member inline Invoke(value: ^value) : 'result =
            ((^value or Empty): (static member Apply: ^value * 'result * Empty -> 'result)
                (value, Unchecked.defaultof<'result>, Unchecked.defaultof<Empty>))

    [<System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)>]
    type NotEmpty =
        static member Apply(value: string, _: 'result, _: NotEmpty) : 'result =
            value.IsNotEmpty |> box :?> 'result

        static member Apply(value: 'value option, _: 'result, _: NotEmpty) : 'result =
            value.IsPresent |> box :?> 'result

        static member Apply(value: 'value voption, _: 'result, _: NotEmpty) : 'result =
            value.IsPresent |> box :?> 'result

        static member Apply(value: System.Nullable<'value>, _: 'result, _: NotEmpty) : 'result =
            value.IsPresent |> box :?> 'result

        static member Apply(value: 'value list, _: 'result, _: NotEmpty) : 'result =
            value.HasItems |> box :?> 'result

        static member Apply(value: 'value array, _: 'result, _: NotEmpty) : 'result =
            value.HasItems |> box :?> 'result

        static member inline Invoke(value: ^value) : 'result =
            ((^value or NotEmpty): (static member Apply: ^value * 'result * NotEmpty -> 'result)
                (value, Unchecked.defaultof<'result>, Unchecked.defaultof<NotEmpty>))

    /// <summary>Runs the type-directed presence predicate for an already parsed optional, nullable, or text value.</summary>
    let inline present value : bool =
        Present.Invoke value

    /// <summary>
    /// Runs the type-directed empty predicate for an already parsed optional, nullable, text, or supported
    /// sequence-shaped value.
    /// </summary>
    let inline empty value : bool =
        Empty.Invoke value

    /// <summary>
    /// Runs the type-directed non-empty predicate for an already parsed optional, nullable, text, or supported
    /// sequence-shaped value.
    /// </summary>
    let inline notEmpty value : bool =
        NotEmpty.Invoke value
