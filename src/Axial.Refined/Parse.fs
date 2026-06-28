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
    let dateOnly (text: string) : Result<DateOnly, ParseError> =
        match DateOnly.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.None) with
        | true, value -> Ok value
        | false, _ -> Error(parseFailure "DateOnly" text)

    /// <summary>Parses a time-only value.</summary>
    let timeOnly (text: string) : Result<TimeOnly, ParseError> =
        match TimeOnly.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.None) with
        | true, value -> Ok value
        | false, _ -> Error(parseFailure "TimeOnly" text)
#endif

    /// <summary>Parses an enum value by name or numeric text.</summary>
    let enum<'enum when 'enum: struct and 'enum : (new: unit -> 'enum) and 'enum :> ValueType>
        (text: string)
        : Result<'enum, ParseError> =
        match Enum.TryParse<'enum>(text, true) with
        | true, value -> Ok value
        | false, _ -> Error(parseFailure typeof<'enum>.Name text)

    /// <summary>Parses an optional integer, returning <c>None</c> for missing or invalid text.</summary>
    let intOption (text: string option) : int option =
        text |> Option.bind (int >> Result.toOption)

    /// <summary>Parses an optional boolean, returning <c>None</c> for missing or invalid text.</summary>
    let boolOption (text: string option) : bool option =
        text |> Option.bind (bool >> Result.toOption)

    /// <summary>Parses an optional decimal, returning <c>None</c> for missing or invalid text.</summary>
    let decimalOption (text: string option) : decimal option =
        text |> Option.bind (decimal >> Result.toOption)

    /// <summary>Parses an optional GUID, returning <c>None</c> for missing or invalid text.</summary>
    let guidOption (text: string option) : Guid option =
        text |> Option.bind (guid >> Result.toOption)

    /// <summary>Parses an integer or returns the supplied fallback.</summary>
    let intOrDefault fallback text =
        int text |> Result.defaultValue fallback

    /// <summary>Parses a boolean or returns the supplied fallback.</summary>
    let boolOrDefault fallback text =
        bool text |> Result.defaultValue fallback

    /// <summary>Parses a decimal or returns the supplied fallback.</summary>
    let decimalOrDefault fallback text =
        decimal text |> Result.defaultValue fallback
