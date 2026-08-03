namespace Axial.Constraint

open System
open System.Text.RegularExpressions

/// <summary>
/// The Boolean facts the built-in constraint catalogue is built from.
/// </summary>
/// <remarks>
/// Internal by design. A constraint is the public reusable value; a bare predicate catalogue beside it would be a
/// second nearly identical vocabulary for users to choose between, which is what this design removes. Reach for
/// <c>Constraint.test</c> when only a Boolean answer is needed.
/// </remarks>
module internal Predicates =
    /// Axial's pragmatic email shape. Its compiled IgnoreCase is inert because the pattern contains no letters,
    /// which is what lets JSON Schema lower it as an exact pattern rather than an approximation.
    let emailPattern = @"^[^@]+@[^@]+$"

    /// ASCII digits. Deliberately not `\d`: .NET matches any Unicode decimal digit while ECMA-262 matches [0-9],
    /// so a `\d` runtime rule could not be lowered to JSON Schema without the export becoming stricter than
    /// execution. Fixing the runtime rule instead keeps the two agreeing by construction.
    let numericPattern = @"^[0-9]+$"

    let private emailRegex = Regex(emailPattern, RegexOptions.Compiled ||| RegexOptions.IgnoreCase)
    let private numericRegex = Regex(numericPattern, RegexOptions.Compiled)

    let private isSurrogate (value: char) =
        let code = int value
        code >= 0xD800 && code <= 0xDFFF

    let private isHighSurrogate (value: char) =
        let code = int value
        code >= 0xD800 && code <= 0xDBFF

    let private isLowSurrogate (value: char) =
        let code = int value
        code >= 0xDC00 && code <= 0xDFFF

    /// <summary>
    /// Counts Unicode code points, so one astral character such as an emoji counts once.
    /// </summary>
    /// <remarks>
    /// This departs from <c>String.Length</c>, which counts UTF-16 code units on both .NET and JavaScript. JSON
    /// Schema's <c>minLength</c>/<c>maxLength</c> count characters, so keeping the UTF-16 count would make
    /// <c>maxLength</c> under-enforce and <c>minLength</c> over-enforce whenever a value contains a supplementary
    /// character. The surrogate scan is skipped entirely for the common all-BMP string.
    /// </remarks>
    let textLength (value: string) =
        if isNull value then
            0
        elif not (value |> Seq.exists isSurrogate) then
            value.Length
        else
            let mutable count = 0
            let mutable index = 0

            while index < value.Length do
                if
                    isHighSurrogate value.[index]
                    && index + 1 < value.Length
                    && isLowSurrogate value.[index + 1]
                then
                    index <- index + 2
                else
                    index <- index + 1

                count <- count + 1

            count

    let isBlankText (value: string) = String.IsNullOrWhiteSpace value

    let isEmail (value: string) = not (isNull value) && emailRegex.IsMatch value

    let isNumeric (value: string) = not (isNull value) && numericRegex.IsMatch value

    let isAlphanumeric (value: string) =
        not (isNull value) && value.Length > 0 && value |> Seq.forall Char.IsLetterOrDigit

    let isTrimmed (value: string) = not (isNull value) && value.Trim() = value

    let matchesPattern (pattern: string) (value: string) =
        not (isNull value) && Regex.IsMatch(value, pattern)

    let isNullSeq (values: #seq<'value>) = Object.ReferenceEquals(values, null)

    let seqCount (values: #seq<'value>) = Seq.length values

    let seqContains (expected: 'value) (values: #seq<'value>) =
        let comparer = Collections.Generic.EqualityComparer<'value>.Default
        values |> Seq.exists (fun item -> comparer.Equals(item, expected))

    /// Returns the first repeated item, which becomes the violation's actual value.
    let tryFirstDuplicate (values: #seq<'value>) =
        let seen = Collections.Generic.HashSet<'value>()
        values |> Seq.tryFind (seen.Add >> not)
