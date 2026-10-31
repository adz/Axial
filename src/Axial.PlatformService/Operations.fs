namespace Axial.PlatformService

open System
open System.Collections.Generic
open System.Globalization
open Axial

/// <summary>Helpers for the environment-variable service.</summary>
[<RequireQualifiedAccess>]
module EnvironmentVariables =
    /// Reads the service from the environment.
    let service<'env, 'error when 'env :> IHasEnvironmentVariables> : Flow<'env, 'error, IEnvironmentVariables> =
        Flow.envWith _.EnvironmentVariables

    /// <summary>Reads a raw environment-variable value from an explicit environment-variable service.</summary>
    let tryGet<'env, 'error when 'env :> IHasEnvironmentVariables>
        (name: string)
        : Flow<'env, 'error, string option> =
        service
        |> Flow.map (fun environmentVariables -> environmentVariables.TryGet name)

    /// <summary>Returns all visible environment variables from an explicit environment-variable service.</summary>
    let getAll<'env, 'error when 'env :> IHasEnvironmentVariables>
        : Flow<'env, 'error, IReadOnlyDictionary<string, string>> =
        service
        |> Flow.map (fun environmentVariables -> environmentVariables.GetAll())

    /// <summary>Sets or updates an environment variable through an explicit environment-variable service.</summary>
    let set<'env, 'error when 'env :> IHasEnvironmentVariables>
        (name: string)
        (value: string)
        : Flow<'env, 'error, unit> =
        service
        |> Flow.map (fun environmentVariables -> environmentVariables.Set(name, Some value))

    /// <summary>Clears an environment variable through an explicit environment-variable service.</summary>
    let clear<'env, 'error when 'env :> IHasEnvironmentVariables>
        (name: string)
        : Flow<'env, 'error, unit> =
        service
        |> Flow.map (fun environmentVariables -> environmentVariables.Set(name, None))

    /// <summary>Expands environment-variable references in text through an explicit environment-variable service.</summary>
    let expand<'env, 'error when 'env :> IHasEnvironmentVariables>
        (text: string)
        : Flow<'env, 'error, string> =
        service
        |> Flow.map (fun environmentVariables -> environmentVariables.Expand text)

    /// <summary>Creates a live provider backed by the current process environment.</summary>
    let live : IEnvironmentVariables =
        Platform.environmentVariables ()

    /// <summary>Creates a deterministic provider from a fixed set of name/value pairs.</summary>
    let fromPairs (values: seq<string * string>) : IEnvironmentVariables =
        Platform.environmentVariablesFromPairs values

    /// <summary>Builds the live environment-variable service as a layer.</summary>
    let layer : Layer<unit, Never, IEnvironmentVariables> =
        Layer.succeed live

/// <summary>Helpers for reading and parsing environment variables through an explicit service.</summary>
[<RequireQualifiedAccess>]
module EnvironmentVariable =
    let private readParsed
        (expected: string)
        (parser: string -> 'value option)
        (name: string)
        : Flow<'env, EnvironmentVariableError, 'value>
        when 'env :> IHasEnvironmentVariables =
        flow {
            let! value = EnvironmentVariables.tryGet name

            match value with
            | None -> return! Flow.fail (EnvironmentVariableError.MissingVariable name)
            | Some value ->
                match parser value with
                | Some parsed -> return parsed
                | None -> return! Flow.fail (EnvironmentVariableError.InvalidVariable(name, value, expected))
        }

    /// <summary>Reads a raw string environment variable through an explicit service.</summary>
    let get<'env when 'env :> IHasEnvironmentVariables>
        (name: string)
        : Flow<'env, EnvironmentVariableError, string> =
        flow {
            let! value = EnvironmentVariables.tryGet name

            match value with
            | Some value -> return value
            | None -> return! Flow.fail (EnvironmentVariableError.MissingVariable name)
        }

    /// <summary>Reads a raw string environment variable without wrapping it in a result.</summary>
    let tryGet<'env, 'error when 'env :> IHasEnvironmentVariables>
        (name: string)
        : Flow<'env, 'error, string option> =
        EnvironmentVariables.tryGet name

    /// <summary>Reads an integer environment variable through an explicit service.</summary>
    let getInt<'env when 'env :> IHasEnvironmentVariables>
        (name: string)
        : Flow<'env, EnvironmentVariableError, int> =
        readParsed "an integer" (fun value ->
#if FABLE_COMPILER
            match Int32.TryParse(value) with
#else
            match Int32.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture) with
#endif
            | true, parsed -> Some parsed
            | false, _ -> None) name

    /// <summary>Reads a 64-bit integer environment variable through an explicit service.</summary>
    let getInt64<'env when 'env :> IHasEnvironmentVariables>
        (name: string)
        : Flow<'env, EnvironmentVariableError, int64> =
        readParsed "a 64-bit integer" (fun value ->
#if FABLE_COMPILER
            match Int64.TryParse(value) with
#else
            match Int64.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture) with
#endif
            | true, parsed -> Some parsed
            | false, _ -> None) name

    /// <summary>Reads a floating-point environment variable through an explicit service.</summary>
    let getDouble<'env when 'env :> IHasEnvironmentVariables>
        (name: string)
        : Flow<'env, EnvironmentVariableError, float> =
        readParsed "a floating-point number" (fun value ->
#if FABLE_COMPILER
            match Double.TryParse(value) with
#else
            match Double.TryParse(value, NumberStyles.Float ||| NumberStyles.AllowThousands, CultureInfo.InvariantCulture) with
#endif
            | true, parsed -> Some parsed
            | false, _ -> None) name

    /// <summary>Reads a decimal environment variable through an explicit service.</summary>
    let getDecimal<'env when 'env :> IHasEnvironmentVariables>
        (name: string)
        : Flow<'env, EnvironmentVariableError, decimal> =
        readParsed "a decimal number" (fun value ->
#if FABLE_COMPILER
            match Decimal.TryParse(value) with
#else
            match Decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture) with
#endif
            | true, parsed -> Some parsed
            | false, _ -> None) name

    /// <summary>Reads a GUID environment variable through an explicit service.</summary>
    let getGuid<'env when 'env :> IHasEnvironmentVariables>
        (name: string)
        : Flow<'env, EnvironmentVariableError, global.System.Guid> =
        readParsed "a GUID" (fun value ->
            match Guid.TryParse value with
            | true, parsed -> Some parsed
            | false, _ -> None) name

    /// <summary>Reads a URI environment variable through an explicit service.</summary>
    let getUri<'env when 'env :> IHasEnvironmentVariables>
        (name: string)
        : Flow<'env, EnvironmentVariableError, Uri> =
        readParsed "an absolute URI" (fun value ->
            match Uri.TryCreate(value, UriKind.Absolute) with
            | true, parsed -> Some parsed
            | false, _ -> None) name

    /// <summary>Reads a time span environment variable through an explicit service.</summary>
    let getTimeSpan<'env when 'env :> IHasEnvironmentVariables>
        (name: string)
        : Flow<'env, EnvironmentVariableError, TimeSpan> =
        readParsed "a time span" (fun value ->
#if FABLE_COMPILER
            match TimeSpan.TryParse(value) with
#else
            match TimeSpan.TryParse(value, CultureInfo.InvariantCulture) with
#endif
            | true, parsed -> Some parsed
            | false, _ -> None) name

    /// <summary>Reads a boolean environment variable through an explicit service.</summary>
    let getBool<'env when 'env :> IHasEnvironmentVariables>
        (name: string)
        : Flow<'env, EnvironmentVariableError, bool> =
        readParsed "a boolean" (fun value ->
            match Boolean.TryParse value with
            | true, parsed -> Some parsed
            | false, _ -> None) name

/// <summary>Helpers for formatting environment-variable errors.</summary>
[<RequireQualifiedAccess>]
module EnvironmentVariableErrors =
    /// <summary>Formats a human-readable description for an error.</summary>
    let describe =
        function
        | EnvironmentVariableError.MissingVariable name ->
            (System.String.Format(Globalization.CultureInfo.InvariantCulture, "Missing required environment variable '{0}'.", name))
        | EnvironmentVariableError.InvalidVariable(name, value, expected) ->
            (System.String.Format(Globalization.CultureInfo.InvariantCulture, "Environment variable '{0}' had value '{1}' but expected {2}.", name, value, expected))


