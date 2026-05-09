namespace FsFlow.Caps.Core

open System
open System.Collections.Generic
open System.Globalization

/// <summary>Provides synchronous access to the current UTC clock.</summary>
type IClock =
    /// <summary>Returns the current UTC timestamp.</summary>
    abstract UtcNow : unit -> DateTimeOffset

/// <summary>Provides synchronous random-number generation.</summary>
type IRandom =
    /// <summary>Returns a random integer in the requested range.</summary>
    abstract NextInt : minInclusive: int -> maxExclusive: int -> int

/// <summary>Provides synchronous GUID generation.</summary>
type IGuid =
    /// <summary>Returns a new GUID value.</summary>
    abstract NewGuid : unit -> Guid

/// <summary>Provides synchronous environment-variable lookup.</summary>
type IEnvironmentVariables =
    /// <summary>Returns the environment-variable value if it is present.</summary>
    abstract TryGet : name: string -> string option

/// <summary>Describes a meaningful environment-variable failure.</summary>
[<RequireQualifiedAccess>]
type EnvironmentVariableError =
    /// <summary>The requested variable was not present.</summary>
    | MissingVariable of name: string

    /// <summary>The requested variable existed but could not be parsed.</summary>
    | InvalidVariable of name: string * value: string * expected: string

/// <summary>Helpers for clock capabilities.</summary>
[<RequireQualifiedAccess>]
module Clock =
    /// <summary>Reads the current UTC timestamp from a clock capability.</summary>
    let now (clock: IClock) : DateTimeOffset = clock.UtcNow()

    /// <summary>Creates a live clock backed by <see cref="P:System.DateTimeOffset.UtcNow" />.</summary>
    let live : IClock =
        { new IClock with
            member _.UtcNow() = DateTimeOffset.UtcNow }

    /// <summary>Creates a deterministic clock that always returns the supplied instant.</summary>
    let fromValue (utcNow: DateTimeOffset) : IClock =
        { new IClock with
            member _.UtcNow() = utcNow }

/// <summary>Helpers for random-number capabilities.</summary>
[<RequireQualifiedAccess>]
module Random =
    /// <summary>Reads a random integer from a random capability.</summary>
    let nextInt (random: IRandom) (minInclusive: int) (maxExclusive: int) : int =
        random.NextInt minInclusive maxExclusive

    /// <summary>Creates a live random-number generator backed by <see cref="T:System.Random" />.</summary>
    let live : IRandom =
        let rng = System.Random()
        let gate = obj()

        { new IRandom with
            member _.NextInt minInclusive maxExclusive =
                lock gate (fun () -> rng.Next(minInclusive, maxExclusive)) }

    /// <summary>Creates a deterministic random generator that always returns the supplied value.</summary>
    let fromValue (value: int) : IRandom =
        { new IRandom with
            member _.NextInt _ _ = value }

/// <summary>Helpers for GUID capabilities.</summary>
[<RequireQualifiedAccess>]
module Guid =
    /// <summary>Reads a GUID from a GUID capability.</summary>
    let newGuid (generator: IGuid) : global.System.Guid = generator.NewGuid()

    /// <summary>Creates a live GUID generator backed by <see cref="M:System.Guid.NewGuid" />.</summary>
    let live : IGuid =
        { new IGuid with
            member _.NewGuid() = global.System.Guid.NewGuid() }

    /// <summary>Creates a deterministic GUID generator that always returns the supplied value.</summary>
    let fromValue (value: global.System.Guid) : IGuid =
        { new IGuid with
            member _.NewGuid() = value }

/// <summary>Helpers for environment-variable providers.</summary>
[<RequireQualifiedAccess>]
module EnvironmentVariables =
    /// <summary>Reads a raw environment-variable value from a provider.</summary>
    let tryGet (environment: IEnvironmentVariables) (name: string) : string option =
        environment.TryGet name

    /// <summary>Creates a live provider backed by the current process environment.</summary>
    let live : IEnvironmentVariables =
        { new IEnvironmentVariables with
            member _.TryGet name =
                match Environment.GetEnvironmentVariable name with
                | null -> None
                | value -> Some value }

    /// <summary>Creates a deterministic provider from a fixed set of name/value pairs.</summary>
    let fromPairs (values: seq<string * string>) : IEnvironmentVariables =
        let lookup = Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)

        for (name, value) in values do
            lookup[name] <- value

        { new IEnvironmentVariables with
            member _.TryGet name =
                match lookup.TryGetValue name with
                | true, value -> Some value
                | false, _ -> None }

/// <summary>Helpers for reading and parsing environment variables.</summary>
[<RequireQualifiedAccess>]
module EnvironmentVariable =
    let private readParsed
        (expected: string)
        (parser: string -> 'value option)
        (environment: IEnvironmentVariables)
        (name: string)
        : Result<'value, EnvironmentVariableError> =
        match EnvironmentVariables.tryGet environment name with
        | None -> Error(EnvironmentVariableError.MissingVariable name)
        | Some value ->
            match parser value with
            | Some parsed -> Ok parsed
            | None -> Error(EnvironmentVariableError.InvalidVariable(name, value, expected))

    /// <summary>Reads a raw string environment variable.</summary>
    let get (environment: IEnvironmentVariables) (name: string) : Result<string, EnvironmentVariableError> =
        match EnvironmentVariables.tryGet environment name with
        | Some value -> Ok value
        | None -> Error(EnvironmentVariableError.MissingVariable name)

    /// <summary>Reads a raw string environment variable without wrapping it in a result.</summary>
    let tryGet (environment: IEnvironmentVariables) (name: string) : string option =
        EnvironmentVariables.tryGet environment name

    /// <summary>Reads an integer environment variable.</summary>
    let getInt (environment: IEnvironmentVariables) (name: string) : Result<int, EnvironmentVariableError> =
        readParsed "an integer" (fun value ->
            match Int32.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture) with
            | true, parsed -> Some parsed
            | false, _ -> None) environment name

    /// <summary>Reads a GUID environment variable.</summary>
    let getGuid (environment: IEnvironmentVariables) (name: string) : Result<global.System.Guid, EnvironmentVariableError> =
        readParsed "a GUID" (fun value ->
            match global.System.Guid.TryParse value with
            | true, parsed -> Some parsed
            | false, _ -> None) environment name

    /// <summary>Reads a boolean environment variable.</summary>
    let getBool (environment: IEnvironmentVariables) (name: string) : Result<bool, EnvironmentVariableError> =
        readParsed "a boolean" (fun value ->
            match Boolean.TryParse value with
            | true, parsed -> Some parsed
            | false, _ -> None) environment name

/// <summary>Helpers for formatting environment-variable errors.</summary>
[<RequireQualifiedAccess>]
module EnvironmentVariableErrors =
    /// <summary>Formats a human-readable description for an error.</summary>
    let describe =
        function
        | EnvironmentVariableError.MissingVariable name ->
            $"Environment variable '{name}' was not set."
        | EnvironmentVariableError.InvalidVariable(name, value, expected) ->
            $"Environment variable '{name}' had value '{value}' but expected {expected}."
