namespace Axial.Telemetry

/// <summary>An OpenTelemetry-compatible attribute value supported on both .NET and JavaScript.</summary>
[<RequireQualifiedAccess>]
type AttributeValue =
    | StringValue of string
    | BooleanValue of bool
    | IntegerValue of int64
    | FloatValue of float
    | StringValues of string list
    | BooleanValues of bool list
    | IntegerValues of int64 list
    | FloatValues of float list

/// <summary>A typed name for one telemetry attribute.</summary>
/// <typeparam name="value">The value type required by the attribute.</typeparam>
type AttributeKey<'value> =
    internal
        { Name: string
          Encode: 'value -> AttributeValue
          Decode: AttributeValue -> 'value option }

/// <summary>One telemetry attribute whose key has already validated its value type.</summary>
type Attribute =
    internal
        { Name: string
          Value: AttributeValue }

    /// <summary>The OpenTelemetry attribute name.</summary>
    member this.Key = this.Name

    /// <summary>The encoded cross-platform value.</summary>
    member this.AttributeValue = this.Value

/// <summary>An immutable set of telemetry attributes propagated with a running workflow.</summary>
type TelemetryContext =
    internal
    | TelemetryContext of Map<string, AttributeValue>

/// <summary>Creates typed keys for application-defined telemetry attributes.</summary>
[<RequireQualifiedAccess>]
module AttributeKey =
    let private create name encode decode =
        if System.String.IsNullOrWhiteSpace name then
            invalidArg "name" "A telemetry attribute name must not be empty."

        { Name = name
          Encode = encode
          Decode = decode }

    /// <summary>Creates a string-valued attribute key.</summary>
    let string name =
        create name AttributeValue.StringValue (function AttributeValue.StringValue value -> Some value | _ -> None)

    /// <summary>Creates a Boolean-valued attribute key.</summary>
    let boolean name =
        create name AttributeValue.BooleanValue (function AttributeValue.BooleanValue value -> Some value | _ -> None)

    /// <summary>Creates a 64-bit integer-valued attribute key.</summary>
    let int64 name =
        create name AttributeValue.IntegerValue (function AttributeValue.IntegerValue value -> Some value | _ -> None)

    /// <summary>Creates a floating-point-valued attribute key.</summary>
    let float name =
        create name AttributeValue.FloatValue (function AttributeValue.FloatValue value -> Some value | _ -> None)

    /// <summary>Creates a string-array-valued attribute key.</summary>
    let strings name =
        create name AttributeValue.StringValues (function AttributeValue.StringValues value -> Some value | _ -> None)

    /// <summary>Creates a Boolean-array-valued attribute key.</summary>
    let booleans name =
        create name AttributeValue.BooleanValues (function AttributeValue.BooleanValues value -> Some value | _ -> None)

    /// <summary>Creates a 64-bit integer-array-valued attribute key.</summary>
    let integers name =
        create name AttributeValue.IntegerValues (function AttributeValue.IntegerValues value -> Some value | _ -> None)

    /// <summary>Creates a floating-point-array-valued attribute key.</summary>
    let floats name =
        create name AttributeValue.FloatValues (function AttributeValue.FloatValues value -> Some value | _ -> None)
