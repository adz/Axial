namespace Axial

open System
open System.Globalization

/// <summary>A portable tree of null, primitive, list, and named object values.</summary>
/// <remarks>
/// <para>
/// <c>Data</c> preserves the distinction between text, numbers, and Boolean values while remaining independent of a
/// particular serializer, schema system, or boundary source. Number tokens are retained as text so arbitrary-size
/// integers, decimal precision, and exponent notation are not narrowed to one runtime numeric type.
/// </para>
/// </remarks>
[<RequireQualifiedAccess>]
type Data =
    /// <summary>A null value.</summary>
    | Null
    /// <summary>A text value.</summary>
    | Text of value: string
    /// <summary>A number represented by its portable lexical token.</summary>
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
    static member From(value: float) : Data = Data.Number(value.ToString("R", CultureInfo.InvariantCulture))
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

/// <summary>Construction and inspection helpers for <see cref="T:Axial.Data" />.</summary>
[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Data =
    /// <summary>Concise, opt-in syntax for constructing structured objects.</summary>
    module Syntax =
        /// <summary>Associates a field name with a supported primitive, structured value, or recursive list.</summary>
        /// <example>
        /// <code>
        /// "age" =&gt; 42
        /// "tags" =&gt; [ "fsharp"; "schema" ]
        /// </code>
        /// </example>
        let inline (=>) (name: string) (value: ^value) : string * Data =
            if isNull name then
                nullArg (nameof name)

            let inline convert (witness: ^w) (value: ^v) =
                ((^w or ^v): (static member From: ^v -> Data) value)

            let converted = convert Unchecked.defaultof<Data> value

            name, converted

        /// <summary>Builds an object from ordered name/value pairs produced with <c>=&gt;</c>.</summary>
        /// <example>
        /// <code>
        /// data [
        ///     "name" =&gt; "Ada"
        ///     "age" =&gt; 42
        /// ]
        /// </code>
        /// </example>
        let data (fields: (string * Data) list) : Data =
            if isNull (box fields) then
                nullArg (nameof fields)

            Data.Object fields
