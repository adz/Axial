namespace Axial.Refined

open System
open System.Globalization

/// <summary>Primitive parsers for untrusted serialized input.</summary>
[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Parse =
    let private parseFailure target (text: string) : ParseError =
        if String.IsNullOrWhiteSpace text then
            ParseError.MissingValue target
        else
            ParseError.InvalidFormat(target, text)

    let private parseNumeric target (parse: string -> 'value) (text: string) : Result<'value, ParseError> =
        if String.IsNullOrWhiteSpace text then
            Error(ParseError.MissingValue target)
        else
            try
                Ok(parse text)
            with
            | :? OverflowException -> Error(ParseError.OutOfRange(target, text))
            | :? FormatException -> Error(ParseError.InvalidFormat(target, text))

    /// <summary>Parses a 32-bit integer.</summary>
    let int (text: string) : Result<int, ParseError> =
        parseNumeric "int" (fun value -> Int32.Parse(value, NumberStyles.Integer, CultureInfo.InvariantCulture)) text

    /// <summary>Parses a 64-bit integer.</summary>
    let long (text: string) : Result<int64, ParseError> =
        parseNumeric "int64" (fun value -> Int64.Parse(value, NumberStyles.Integer, CultureInfo.InvariantCulture)) text

    /// <summary>Parses a decimal number.</summary>
    let decimal (text: string) : Result<decimal, ParseError> =
        parseNumeric "decimal" (fun value -> Decimal.Parse(value, NumberStyles.Number, CultureInfo.InvariantCulture)) text

    /// <summary>Parses a double-precision floating point number.</summary>
    let float (text: string) : Result<float, ParseError> =
        parseNumeric "float" (fun value -> Double.Parse(value, NumberStyles.Float ||| NumberStyles.AllowThousands, CultureInfo.InvariantCulture)) text

    /// <summary>Parses a boolean.</summary>
    let bool (text: string) : Result<bool, ParseError> =
        match Boolean.TryParse text with
        | true, value -> Ok value
        | false, _ -> Error(parseFailure "bool" text)

    /// <summary>Parses a GUID.</summary>
    let guid (text: string) : Result<Guid, ParseError> =
        match Guid.TryParse text with
        | true, value -> Ok value
        | false, _ -> Error(parseFailure "Guid" text)

    /// <summary>Parses a date and time value.</summary>
    let dateTime (text: string) : Result<DateTime, ParseError> =
        match DateTime.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.None) with
        | true, value -> Ok value
        | false, _ -> Error(parseFailure "DateTime" text)

    /// <summary>Parses a date and time value with offset.</summary>
    let dateTimeOffset (text: string) : Result<DateTimeOffset, ParseError> =
        match DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.None) with
        | true, value -> Ok value
        | false, _ -> Error(parseFailure "DateTimeOffset" text)

#if NET8_0_OR_GREATER
    /// <summary>Parses a date-only value.</summary>
    /// <remarks>netstandard2.1: not available.</remarks>
    let dateOnly (text: string) : Result<DateOnly, ParseError> =
        match DateOnly.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.None) with
        | true, value -> Ok value
        | false, _ -> Error(parseFailure "DateOnly" text)

    /// <summary>Parses a time-only value.</summary>
    /// <remarks>netstandard2.1: not available.</remarks>
    let timeOnly (text: string) : Result<TimeOnly, ParseError> =
        match TimeOnly.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.None) with
        | true, value -> Ok value
        | false, _ -> Error(parseFailure "TimeOnly" text)
#endif

    /// <summary>Parses an enum value by name or numeric text.</summary>
    let inline enum<'enum when 'enum: struct and 'enum : (new: unit -> 'enum) and 'enum :> ValueType>
        (text: string)
        : Result<'enum, ParseError> =
        match Enum.TryParse<'enum>(text, true) with
        | true, value -> Ok value
        | false, _ ->
            let target = typeof<'enum>.Name

            if String.IsNullOrWhiteSpace text then
                Error(ParseError.MissingValue target)
            else
                Error(ParseError.InvalidFormat(target, text))

    /// <summary>Parses an optional input, preserving a present input's parsing failure.</summary>
    /// <example>
    /// <code>
    /// Parse.optional Parse.int None = Ok None
    /// Parse.optional Parse.int (Some "42") = Ok (Some 42)
    /// Parse.optional Parse.int (Some "bad") = Error (ParseError.InvalidFormat ("int", "bad"))
    /// </code>
    /// </example>
    let optional
        (parser: 'raw -> Result<'value, 'error>)
        (input: 'raw option)
        : Result<'value option, 'error> =
        match input with
        | None -> Ok None
        | Some raw -> parser raw |> Result.map Some

    /// <summary>Parses an optional input, using the supplied fallback only when the input is absent.</summary>
    /// <example>
    /// <code>
    /// Parse.optionalOr 80 Parse.int None = Ok 80
    /// Parse.optionalOr 80 Parse.int (Some "443") = Ok 443
    /// Parse.optionalOr 80 Parse.int (Some "bad") = Error (ParseError.InvalidFormat ("int", "bad"))
    /// </code>
    /// </example>
    let optionalOr
        (fallback: 'value)
        (parser: 'raw -> Result<'value, 'error>)
        (input: 'raw option)
        : Result<'value, 'error> =
        input
        |> optional parser
        |> Result.map (Option.defaultValue fallback)

    /// <summary>Parses an optional integer. Absence returns <c>Ok None</c>; malformed present text returns its parsing error.</summary>
    /// <example><code>Parse.intOption (Some "42") = Ok (Some 42)</code></example>
    let intOption (text: string option) : Result<int option, ParseError> =
        text |> optional int

    /// <summary>Parses an optional Boolean. Absence returns <c>Ok None</c>; malformed present text returns its parsing error.</summary>
    /// <example><code>Parse.boolOption (Some "true") = Ok (Some true)</code></example>
    let boolOption (text: string option) : Result<bool option, ParseError> =
        text |> optional bool

    /// <summary>Parses an optional decimal. Absence returns <c>Ok None</c>; malformed present text returns its parsing error.</summary>
    /// <example><code>Parse.decimalOption (Some "12.5") = Ok (Some 12.5M)</code></example>
    let decimalOption (text: string option) : Result<decimal option, ParseError> =
        text |> optional decimal

    /// <summary>Parses an optional GUID. Absence returns <c>Ok None</c>; malformed present text returns its parsing error.</summary>
    /// <example><code>Parse.guidOption None = Ok None</code></example>
    let guidOption (text: string option) : Result<Guid option, ParseError> =
        text |> optional guid

    /// <summary>Parses an optional integer, using the supplied fallback only when the input is absent.</summary>
    /// <example><code>Parse.intOrDefault 80 None = Ok 80</code></example>
    let intOrDefault fallback text : Result<int, ParseError> =
        text |> optionalOr fallback int

    /// <summary>Parses an optional Boolean, using the supplied fallback only when the input is absent.</summary>
    /// <example><code>Parse.boolOrDefault false None = Ok false</code></example>
    let boolOrDefault fallback text : Result<bool, ParseError> =
        text |> optionalOr fallback bool

    /// <summary>Parses an optional decimal, using the supplied fallback only when the input is absent.</summary>
    /// <example><code>Parse.decimalOrDefault 5.5M None = Ok 5.5M</code></example>
    let decimalOrDefault fallback text : Result<decimal, ParseError> =
        text |> optionalOr fallback decimal
