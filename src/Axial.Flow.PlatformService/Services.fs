namespace Axial.Flow.PlatformService

open System
open System.Collections.Generic
open Axial.Flow

/// <summary>Provides synchronous access to the current UTC clock.</summary>
type IClock =
    /// <summary>Returns the current UTC timestamp.</summary>
    abstract UtcNow: unit -> DateTimeOffset

/// <summary>Provides synchronous access to workflow logging as an explicit service.</summary>
type ILog =
    /// <summary>Writes a log message at the requested level.</summary>
    abstract Log: level: LogLevel -> message: string -> unit
    /// <summary>Writes a log message carrying an exception, preserving its stack trace for the host logger.</summary>
    abstract LogException: level: LogLevel -> error: exn -> message: string -> unit

/// <summary>Provides synchronous random-number generation.</summary>
type IRandom =
    abstract Next: unit -> int
    abstract NextMax: maxExclusive: int -> int
    abstract NextInt: minInclusive: int -> maxExclusive: int -> int
    abstract NextDouble: unit -> float
    abstract NextBytes: buffer: byte array -> unit

/// <summary>Provides synchronous GUID generation.</summary>
type IGuid =
    abstract NewGuid: unit -> Guid

/// <summary>Provides environment-variable access supplied by the application host.</summary>
type IEnvironmentVariables =
    abstract TryGet: name: string -> string option
    abstract Set: name: string * value: string option -> unit
    abstract Expand: text: string -> string
    abstract GetAll: unit -> IReadOnlyDictionary<string, string>

[<RequireQualifiedAccess>]
type EnvironmentVariableError =
    | MissingVariable of name: string
    | InvalidVariable of name: string * value: string * expected: string

[<RequireQualifiedAccess>]
type BaseRuntimeError =
    | MissingService of serviceName: string
