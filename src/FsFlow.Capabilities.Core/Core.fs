namespace FsFlow.Capabilities.Core

open System
open System.Collections.Generic
open System.Globalization
open FsFlow

/// <summary>Provides synchronous access to the current UTC clock.</summary>
type IClock =
    /// <summary>Returns the current UTC timestamp.</summary>
    abstract UtcNow : unit -> DateTimeOffset

/// <summary>Provides synchronous access to structured runtime logging.</summary>
type ILog =
    /// <summary>Writes an informational log message.</summary>
    abstract Info : string -> unit

/// <summary>Bundles the standard runtime capabilities for host-level workflows.</summary>
type IRuntimeCaps =
    /// <summary>Provides clock access.</summary>
    abstract Clock : IClock
    /// <summary>Provides logging access.</summary>
    abstract Log : ILog

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

/// <summary>Bundles application-facing capabilities for workflow boundaries.</summary>
type IAppCaps =
    /// <summary>Provides environment-variable access for app-level configuration.</summary>
    abstract EnvironmentVariables : IEnvironmentVariables

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
    /// <summary>Reads the current UTC timestamp from the environment.</summary>
    let now<'env when 'env :> IClock> : Flow<'env, 'e, DateTimeOffset> =
        Flow.read (fun (env: 'env) -> env.UtcNow())

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
    /// <summary>Reads a random integer from the environment.</summary>
    let nextInt (minInclusive: int) (maxExclusive: int) : Flow<'env, 'e, int> when 'env :> IRandom =
        Flow.read (fun (env: 'env) -> env.NextInt minInclusive maxExclusive)

    /// <summary>Creates a live random-number generator backed by <see cref="T:System.Random" />.</summary>
    let live : IRandom =
        let rng = System.Random()
        let gate = obj()

        { new IRandom with
            member _.NextInt minInclusive maxExclusive =
                #if FABLE_COMPILER
                rng.Next(minInclusive, maxExclusive)
                #else
                lock gate (fun () -> rng.Next(minInclusive, maxExclusive)) 
                #endif
        }

    /// <summary>Creates a deterministic random generator that always returns the supplied value.</summary>
    let fromValue (value: int) : IRandom =
        { new IRandom with
            member _.NextInt _ _ = value }

/// <summary>Helpers for GUID capabilities.</summary>
[<RequireQualifiedAccess>]
module Guid =
    /// <summary>Reads a GUID from the environment.</summary>
    let newGuid<'env when 'env :> IGuid> : Flow<'env, 'e, Guid> =
        Flow.read (fun (env: 'env) -> env.NewGuid())

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
    /// <summary>Reads a raw environment-variable value from the environment.</summary>
    let tryGet (name: string) : Flow<'env, 'e, string option> when 'env :> IEnvironmentVariables =
        Flow.read (fun (env: 'env) -> env.TryGet name)

    /// <summary>Creates a live provider backed by the current process environment.</summary>
    let live : IEnvironmentVariables =
        { new IEnvironmentVariables with
            member _.TryGet name =
                #if FABLE_COMPILER
                // Fable/JS environment variable access depends on the host (Node/Browser).
                // For now, we return None or could try process.env in Node.
                None 
                #else
                match Environment.GetEnvironmentVariable name with
                | null -> None
                | value -> Some value 
                #endif
        }

    /// <summary>Creates a deterministic provider from a fixed set of name/value pairs.</summary>
    let fromPairs (values: seq<string * string>) : IEnvironmentVariables =
        #if FABLE_COMPILER
        let lookup = Dictionary<string, string>()

        for (name, value) in values do
            lookup[name.ToLowerInvariant()] <- value

        { new IEnvironmentVariables with
            member _.TryGet name =
                match lookup.TryGetValue(name.ToLowerInvariant()) with
                | true, value -> Some value
                | false, _ -> None }
        #else
        let lookup = Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)

        for (name, value) in values do
            lookup[name] <- value

        { new IEnvironmentVariables with
            member _.TryGet name =
                match lookup.TryGetValue name with
                | true, value -> Some value
                | false, _ -> None }
        #endif

/// <summary>Helpers for reading and parsing environment variables from the environment.</summary>
[<RequireQualifiedAccess>]
module EnvironmentVariable =
    let private readParsed
        (expected: string)
        (parser: string -> 'value option)
        (name: string)
        : Flow<'env, EnvironmentVariableError, 'value> when 'env :> IEnvironmentVariables =
        flow {
            let! (env: 'env) = Flow.env
            match env.TryGet name with
            | None -> return! Flow.fail (EnvironmentVariableError.MissingVariable name)
            | Some value ->
                match parser value with
                | Some parsed -> return parsed
                | None -> return! Flow.fail (EnvironmentVariableError.InvalidVariable(name, value, expected))
        }

    /// <summary>Reads a raw string environment variable from the environment.</summary>
    let get (name: string) : Flow<'env, EnvironmentVariableError, string> when 'env :> IEnvironmentVariables =
        flow {
            let! (env: 'env) = Flow.env
            match env.TryGet name with
            | Some value -> return value
            | None -> return! Flow.fail (EnvironmentVariableError.MissingVariable name)
        }

    /// <summary>Reads a raw string environment variable without wrapping it in a result.</summary>
    let tryGet (name: string) : Flow<'env, 'e, string option> when 'env :> IEnvironmentVariables =
        EnvironmentVariables.tryGet name

    /// <summary>Reads an integer environment variable from the environment.</summary>
    let getInt (name: string) : Flow<'env, EnvironmentVariableError, int> when 'env :> IEnvironmentVariables =
        readParsed "an integer" (fun value ->
            match Int32.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture) with
            | true, parsed -> Some parsed
            | false, _ -> None) name

    /// <summary>Reads a GUID environment variable from the environment.</summary>
    let getGuid (name: string) : Flow<'env, EnvironmentVariableError, Guid> when 'env :> IEnvironmentVariables =
        readParsed "a GUID" (fun value ->
            match Guid.TryParse value with
            | true, parsed -> Some parsed
            | false, _ -> None) name

    /// <summary>Reads a boolean environment variable from the environment.</summary>
    let getBool (name: string) : Flow<'env, EnvironmentVariableError, bool> when 'env :> IEnvironmentVariables =
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
            $"Environment variable '{name}' was not set."
        | EnvironmentVariableError.InvalidVariable(name, value, expected) ->
            $"Environment variable '{name}' had value '{value}' but expected {expected}."

/// <summary>Helpers that operate directly on a host context.</summary>
[<RequireQualifiedAccess>]
module Logger =
    /// <summary>Writes an informational log message through the host half of the context.</summary>
    let log
        (message: string)
        : Flow<HostContext<'host, 'appEnv>, 'error, unit>
        when 'host :> IRuntimeCaps =
        flow {
            let! (context: HostContext<'host, 'appEnv>) = Flow.env
            context.Host.Log.Info message
        }

    /// <summary>Writes an informational log message computed from the current host context.</summary>
    let logWith
        (messageFactory: HostContext<'host, 'appEnv> -> string)
        : Flow<HostContext<'host, 'appEnv>, 'error, unit>
        when 'host :> IRuntimeCaps =
        flow {
            let! (context: HostContext<'host, 'appEnv>) = Flow.env
            context.Host.Log.Info (messageFactory context)
        }

/// <summary>Helpers that operate directly on the host carrier.</summary>
[<RequireQualifiedAccess>]
module Host =
    /// <summary>Reads the current UTC timestamp from the host half of the context.</summary>
    let clockNow<'host, 'appEnv, 'error when 'host :> IRuntimeCaps>
        : Flow<HostContext<'host, 'appEnv>, 'error, DateTimeOffset> =
        Flow.read (fun (context: HostContext<'host, 'appEnv>) -> context.Host.Clock.UtcNow())

    /// <summary>Writes an informational log message through the host half of the context.</summary>
    let log
        (message: string)
        : Flow<HostContext<'host, 'appEnv>, 'error, unit>
        when 'host :> IRuntimeCaps =
        Logger.log message

    /// <summary>Writes an informational log message computed from the current host context.</summary>
    let logWith
        (messageFactory: HostContext<'host, 'appEnv> -> string)
        : Flow<HostContext<'host, 'appEnv>, 'error, unit>
        when 'host :> IRuntimeCaps =
        Logger.logWith messageFactory

/// <summary>Helpers that operate directly on the app environment half of a host context.</summary>
[<RequireQualifiedAccess>]
module AppEnv =
    let private readParsed
        (expected: string)
        (parser: string -> 'value option)
        (name: string)
        : Flow<HostContext<'host, 'appEnv>, EnvironmentVariableError, 'value>
        when 'appEnv :> IAppCaps =
        flow {
            let! (context: HostContext<'host, 'appEnv>) = Flow.env
            match context.AppEnv.EnvironmentVariables.TryGet name with
            | None -> return! Flow.fail (EnvironmentVariableError.MissingVariable name)
            | Some value ->
                match parser value with
                | Some parsed -> return parsed
                | None -> return! Flow.fail (EnvironmentVariableError.InvalidVariable(name, value, expected))
        }

    /// <summary>Reads a raw string environment variable from the app environment.</summary>
    let tryGet (name: string) : Flow<HostContext<'host, 'appEnv>, 'error, string option> when 'appEnv :> IAppCaps =
        Flow.read (fun (context: HostContext<'host, 'appEnv>) -> context.AppEnv.EnvironmentVariables.TryGet name)

    /// <summary>Reads a raw string environment variable from the app environment.</summary>
    let get
        (name: string)
        : Flow<HostContext<'host, 'appEnv>, EnvironmentVariableError, string>
        when 'appEnv :> IAppCaps =
        flow {
            let! (context: HostContext<'host, 'appEnv>) = Flow.env
            match context.AppEnv.EnvironmentVariables.TryGet name with
            | Some value -> return value
            | None -> return! Flow.fail (EnvironmentVariableError.MissingVariable name)
        }

    /// <summary>Reads an integer environment variable from the app environment.</summary>
    let getInt
        (name: string)
        : Flow<HostContext<'host, 'appEnv>, EnvironmentVariableError, int>
        when 'appEnv :> IAppCaps =
        readParsed "an integer" (fun value ->
            match Int32.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture) with
            | true, parsed -> Some parsed
            | false, _ -> None) name

    /// <summary>Reads a GUID environment variable from the app environment.</summary>
    let getGuid
        (name: string)
        : Flow<HostContext<'host, 'appEnv>, EnvironmentVariableError, Guid>
        when 'appEnv :> IAppCaps =
        readParsed "a GUID" (fun value ->
            match Guid.TryParse value with
            | true, parsed -> Some parsed
            | false, _ -> None) name

    /// <summary>Reads a boolean environment variable from the app environment.</summary>
    let getBool
        (name: string)
        : Flow<HostContext<'host, 'appEnv>, EnvironmentVariableError, bool>
        when 'appEnv :> IAppCaps =
        readParsed "a boolean" (fun value ->
            match Boolean.TryParse value with
            | true, parsed -> Some parsed
            | false, _ -> None) name
