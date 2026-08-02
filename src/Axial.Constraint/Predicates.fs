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

    /// At least one character ECMA-262 does not call whitespace. A sound export of `present` on text: every
    /// character a validator treats as whitespace is blank here too, so a string this rejects is one Axial
    /// rejects as well. See `isBlankChar` for why that subset relation holds.
    let nonBlankPattern = @"\S"

    /// No leading or trailing ECMA-262 whitespace, written without lookaround so it means the same thing in every
    /// dialect. Sound in the same direction and for the same reason as `nonBlankPattern`.
    let trimmedPattern = @"^(\S|\S[\s\S]*\S)?$"

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

    /// <summary>
    /// Whether one character counts as blank: .NET's whitespace set, plus U+FEFF.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The extra character is what makes the export sound. ECMA-262's <c>\s</c> is .NET's whitespace set plus
    /// U+FEFF and minus a few others, and that overlap-in-both-directions is why <c>\S</c> was previously
    /// unusable: U+FEFF is whitespace to a JSON Schema validator but not to <c>Char.IsWhiteSpace</c> on .NET
    /// Core, so an emitted <c>\S</c> would have rejected a string the library accepts.
    /// </para>
    /// <para>
    /// Adding U+FEFF makes ECMA-262 whitespace a strict subset of blankness, which removes that direction
    /// entirely. What remains — U+0085 and friends, blank here but not to a validator — only ever lets a value
    /// through the wire check for Axial to reject with a proper diagnostic.
    /// </para>
    /// </remarks>
    let isBlankChar (value: char) = Char.IsWhiteSpace value || value = '\uFEFF'

    let isBlankText (value: string) =
        isNull value || value |> Seq.forall isBlankChar

    let isEmail (value: string) = not (isNull value) && emailRegex.IsMatch value

    let isNumeric (value: string) = not (isNull value) && numericRegex.IsMatch value

    let isAlphanumeric (value: string) =
        not (isNull value) && value.Length > 0 && value |> Seq.forall Char.IsLetterOrDigit

    /// Trims the blank set above rather than calling String.Trim, which would leave U+FEFF in place and put the
    /// rule back out of step with its exported pattern.
    let isTrimmed (value: string) =
        not (isNull value)
        && (value.Length = 0 || (not (isBlankChar value.[0]) && not (isBlankChar value.[value.Length - 1])))

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
