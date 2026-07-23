namespace Axial

open System
open System.Globalization

/// <summary>A portable tree representing the meaning and shape of unowned structured data.</summary>
/// <remarks>
/// <para>
/// Use <c>Data</c> between a source adapter and the code that assigns an application-owned type. It preserves null,
/// text, number, Boolean, list, and object distinctions without depending on a serializer, schema system, or boundary
/// source.
/// </para>
/// <para>
/// <c>Data</c> is a structured-value model, not a source syntax tree. It does not model whitespace, comments, source
/// locations, or other format-specific syntax. Number values currently retain a lexical token so adapters do not
/// narrow arbitrary-size integers, decimal precision, or exponent notation to one runtime numeric type.
/// </para>
/// </remarks>
[<RequireQualifiedAccess>]
type Data =
    /// <summary>A null value.</summary>
    | Null
    /// <summary>A text value.</summary>
    | Text of value: string
    /// <summary>A number whose portable lexical token avoids narrowing it to one runtime numeric type.</summary>
    | Number of token: string
    /// <summary>A Boolean value.</summary>
    | Bool of value: bool
    /// <summary>An ordered collection of structured values.</summary>
    | List of items: Data list
    /// <summary>An ordered collection of named structured values.</summary>
    | Object of fields: (string * Data) list
    /// <summary>Returns an existing structured value unchanged.</summary>
    static member From(value: Data) : Data = value
    /// <summary>Converts text, or converts a null string to <c>Null</c>.</summary>
    static member From(value: string) : Data = if isNull value then Data.Null else Data.Text value
    /// <summary>Converts a Boolean value.</summary>
    static member From(value: bool) : Data = Data.Bool value
    /// <summary>Converts a 32-bit integer using invariant formatting.</summary>
    static member From(value: int) : Data = Data.Number(value.ToString(CultureInfo.InvariantCulture))
    /// <summary>Converts a 64-bit integer using invariant formatting.</summary>
    static member From(value: int64) : Data = Data.Number(value.ToString(CultureInfo.InvariantCulture))
    /// <summary>Converts a decimal using invariant formatting.</summary>
    static member From(value: decimal) : Data = Data.Number(value.ToString(CultureInfo.InvariantCulture))
    /// <summary>Converts a double using invariant round-trip formatting.</summary>
    static member From(value: float) : Data =
#if FABLE_COMPILER
        Data.Number(string value)
#else
        Data.Number(value.ToString("R", CultureInfo.InvariantCulture))
#endif
    /// <summary>Converts a GUID to canonical text.</summary>
    static member From(value: Guid) : Data = Data.Text(value.ToString("D"))
    /// <summary>Converts a timestamp to round-trip ISO text.</summary>
    static member From(value: DateTimeOffset) : Data = Data.Text(value.ToString("O", CultureInfo.InvariantCulture))
#if NET8_0_OR_GREATER
    /// <summary>Converts a calendar date to ISO text.</summary>
    static member From(value: DateOnly) : Data = Data.Text(value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture))
#endif

    /// <summary>Recursively converts a supported list, or converts a null list to <c>Null</c>.</summary>
    static member inline From(values: ^value list) : Data =
        if isNull (box values) then
            Data.Null
        else
            let inline convert (witness: ^w) (value: ^v) =
                ((^w or ^v): (static member From: ^v -> Data) value)

            values
            |> List.map (convert Unchecked.defaultof<Data>)
            |> Data.List
