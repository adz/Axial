namespace Axial.ErrorHandling

/// <summary>
/// A curated, unqualified check-authoring vocabulary designed to be opened inside a validation module.
/// </summary>
/// <remarks>
/// <para>
/// Opens the deduplicated <c>Check</c> root names bare, so a call reads <c>minLength 3 name</c> instead of
/// <c>Check.minLength 3 name</c>. Every name here is unique within <c>Check</c> itself — the type-directed
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
/// <code>
/// module SignupChecks =
///     open Axial.ErrorHandling.CheckDSL
///
///     let validateAge : Check&lt;int&gt; = atLeast 13
///     let validateEmail : Check&lt;string&gt; = Check.all [ present; email ]
/// </code>
/// </remarks>
module CheckDSL =
    /// <summary>Alias for <see cref="M:Axial.ErrorHandling.Check.present" />.</summary>
    let inline present value = Check.present value

    /// <summary>Alias for <see cref="M:Axial.ErrorHandling.Check.empty" />.</summary>
    let inline empty value = Check.empty value

    /// <summary>Alias for <see cref="M:Axial.ErrorHandling.Check.notEmpty" />.</summary>
    let inline notEmpty value = Check.notEmpty value

    /// <summary>Alias for <see cref="M:Axial.ErrorHandling.Check.minLength" />.</summary>
    let minLength = Check.minLength

    /// <summary>Alias for <see cref="M:Axial.ErrorHandling.Check.maxLength" />.</summary>
    let maxLength = Check.maxLength

    /// <summary>Alias for <see cref="M:Axial.ErrorHandling.Check.lengthBetween" />.</summary>
    let lengthBetween = Check.lengthBetween

    /// <summary>Alias for <see cref="M:Axial.ErrorHandling.Check.exactLength" />.</summary>
    let exactLength = Check.String.exactLength

    /// <summary>Alias for <see cref="M:Axial.ErrorHandling.Check.email" />.</summary>
    let email = Check.email

    /// <summary>Alias for <see cref="M:Axial.ErrorHandling.Check.matches" />.</summary>
    let matches = Check.matches

    /// <summary>Alias for <see cref="M:Axial.ErrorHandling.Check.oneOf" />.</summary>
    let oneOf = Check.oneOf

    /// <summary>Alias for <see cref="M:Axial.ErrorHandling.Check.greaterThan" />.</summary>
    let inline greaterThan minimum = Check.greaterThan minimum

    /// <summary>Alias for <see cref="M:Axial.ErrorHandling.Check.lessThan" />.</summary>
    let inline lessThan maximum = Check.lessThan maximum

    /// <summary>Alias for <see cref="M:Axial.ErrorHandling.Check.atLeast" />.</summary>
    let inline atLeast minimum = Check.atLeast minimum

    /// <summary>Alias for <see cref="M:Axial.ErrorHandling.Check.atMost" />.</summary>
    let inline atMost maximum = Check.atMost maximum

    /// <summary>Alias for <see cref="M:Axial.ErrorHandling.Check.positive" />.</summary>
    let inline positive value = Check.positive value

    /// <summary>Alias for <see cref="M:Axial.ErrorHandling.Check.nonNegative" />.</summary>
    let inline nonNegative value = Check.nonNegative value

    /// <summary>Alias for <see cref="M:Axial.ErrorHandling.Check.negative" />.</summary>
    let inline negative value = Check.negative value

    /// <summary>Alias for <see cref="M:Axial.ErrorHandling.Check.nonPositive" />.</summary>
    let inline nonPositive value = Check.nonPositive value

    /// <summary>Alias for <see cref="M:Axial.ErrorHandling.Check.minCount" />.</summary>
    let minCount = Check.minCount

    /// <summary>Alias for <see cref="M:Axial.ErrorHandling.Check.maxCount" />.</summary>
    let maxCount = Check.maxCount

    /// <summary>Alias for <see cref="M:Axial.ErrorHandling.Check.countBetween" />.</summary>
    let countBetween = Check.countBetween

    /// <summary>Alias for <see cref="M:Axial.ErrorHandling.Check.equalTo" />.</summary>
    let equalTo = Check.equalTo

    /// <summary>Alias for <see cref="M:Axial.ErrorHandling.Check.notEqualTo" />.</summary>
    let notEqualTo = Check.notEqualTo

    /// <summary>Alias for <see cref="M:Axial.ErrorHandling.Check.mapFailure" />.</summary>
    let mapFailure = Check.mapFailure
