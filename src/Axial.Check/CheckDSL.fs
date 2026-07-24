namespace Axial.Check

/// <summary>
/// Check functions that can be used without the <c>Check.</c> prefix inside a module that checks values.
/// </summary>
/// <remarks>
/// <para>
/// Opens the common <c>Check</c> names, so a call reads <c>minLength 3 name</c> instead of
/// <c>Check.minLength 3 name</c>. Every check name here is unique within <c>Check</c> itself. The type-directed
/// <c>present</c>/<c>empty</c>/<c>notEmpty</c> facade already resolves across string, option, voption, nullable,
/// and sequence-shaped values, so there is nothing to disambiguate.
/// </para>
/// <para>
/// A handful of <c>Check</c> names are deliberately left off this vocabulary because they shadow core F# names
/// that the same validation code is likely to need in the same scope: <c>not</c>, <c>contains</c>, <c>distinct</c>,
/// <c>all</c>, <c>any</c>, <c>length</c>, and <c>between</c>. Reach for those as <c>Check.not</c>,
/// <c>Check.contains</c>, <c>Check.distinct</c>, <c>Check.all</c>, <c>Check.any</c>, <c>Check.length</c>, and
/// <c>Check.between</c> even inside a module that has opened this DSL.
/// </para>
/// <para>
/// <c>orError</c> and <c>mapError</c> are short forms of the matching <c>Result</c> functions. They let a check
/// pipeline finish with the application's error type without leaving the opened DSL.
/// </para>
/// <code>
/// module SignupChecks =
///     open Axial.Check.CheckDSL
///
///     let validateAge : Check&lt;int&gt; = atLeast 13
///     let validateEmail : Check&lt;string&gt; = Check.all [ present; email ]
///     let requireEmail value = value |> validateEmail |> orError EmailRequired
/// </code>
/// </remarks>
module CheckDSL =
    /// <summary>Alias for <see cref="M:Axial.Check.Check.present" />.</summary>
    let inline present value = Check.present value

    /// <summary>Alias for <see cref="M:Axial.Check.Check.empty" />.</summary>
    let inline empty value = Check.empty value

    /// <summary>Alias for <see cref="M:Axial.Check.Check.notEmpty" />.</summary>
    let inline notEmpty value = Check.notEmpty value

    /// <summary>Alias for <see cref="M:Axial.Check.Check.minLength" />.</summary>
    let minLength = Check.minLength

    /// <summary>Alias for <see cref="M:Axial.Check.Check.maxLength" />.</summary>
    let maxLength = Check.maxLength

    /// <summary>Alias for <see cref="M:Axial.Check.Check.lengthBetween" />.</summary>
    let lengthBetween = Check.lengthBetween

    /// <summary>Alias for <see cref="M:Axial.Check.Check.exactLength" />.</summary>
    let exactLength = Check.String.exactLength

    /// <summary>Alias for <see cref="M:Axial.Check.Check.email" />.</summary>
    let email = Check.email

    /// <summary>Alias for <see cref="M:Axial.Check.Check.matches" />.</summary>
    let matches = Check.matches

    /// <summary>Alias for <see cref="M:Axial.Check.Check.oneOf" />.</summary>
    let oneOf = Check.oneOf

    /// <summary>Alias for <see cref="M:Axial.Check.Check.greaterThan" />.</summary>
    let inline greaterThan minimum = Check.greaterThan minimum

    /// <summary>Alias for <see cref="M:Axial.Check.Check.lessThan" />.</summary>
    let inline lessThan maximum = Check.lessThan maximum

    /// <summary>Alias for <see cref="M:Axial.Check.Check.atLeast" />.</summary>
    let inline atLeast minimum = Check.atLeast minimum

    /// <summary>Alias for <see cref="M:Axial.Check.Check.atMost" />.</summary>
    let inline atMost maximum = Check.atMost maximum

    /// <summary>Alias for <see cref="M:Axial.Check.Check.positive" />.</summary>
    let inline positive value = Check.positive value

    /// <summary>Alias for <see cref="M:Axial.Check.Check.nonNegative" />.</summary>
    let inline nonNegative value = Check.nonNegative value

    /// <summary>Alias for <see cref="M:Axial.Check.Check.negative" />.</summary>
    let inline negative value = Check.negative value

    /// <summary>Alias for <see cref="M:Axial.Check.Check.nonPositive" />.</summary>
    let inline nonPositive value = Check.nonPositive value

    /// <summary>Alias for <see cref="M:Axial.Check.Check.minCount" />.</summary>
    let minCount = Check.minCount

    /// <summary>Alias for <see cref="M:Axial.Check.Check.maxCount" />.</summary>
    let maxCount = Check.maxCount

    /// <summary>Alias for <see cref="M:Axial.Check.Check.countBetween" />.</summary>
    let countBetween = Check.countBetween

    /// <summary>Alias for <see cref="M:Axial.Check.Check.equalTo" />.</summary>
    let equalTo = Check.equalTo

    /// <summary>Alias for <see cref="M:Axial.Check.Check.notEqualTo" />.</summary>
    let notEqualTo = Check.notEqualTo

    /// <summary>Alias for <see cref="M:Axial.Check.Check.mapFailure" />.</summary>
    let mapFailure = Check.mapFailure

    /// <summary>Replaces a failed check's errors with the supplied error.</summary>
    /// <example><code>value |> present |> orError NameRequired</code></example>
    let inline orError failure result =
        match result with
        | Ok value -> Ok value
        | Error _ -> Error failure

    /// <summary>Changes a failed check's errors with the supplied function.</summary>
    /// <example><code>value |> positive |> mapError InvalidQuantity</code></example>
    let inline mapError mapper result =
        match result with
        | Ok value -> Ok value
        | Error failure -> Error(mapper failure)
