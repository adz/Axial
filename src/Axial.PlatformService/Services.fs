namespace Axial.PlatformService

open System
open System.Collections.Generic
open Axial

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

/// <summary>Provides cryptographically secure random values for secrets, tokens, and nonces.</summary>
/// <remarks><see cref="T:Axial.PlatformService.IRandom" /> is not suitable for security-sensitive values.</remarks>
type ISecureRandom =
    /// <summary>Fills a buffer with cryptographically secure random bytes.</summary>
    abstract Fill: buffer: byte array -> unit
    /// <summary>Returns a cryptographically secure integer in the requested half-open range.</summary>
    abstract GetInt32: minInclusive: int -> maxExclusive: int -> int

/// <summary>Provides synchronous GUID generation.</summary>
type IGuid =
    abstract NewGuid: unit -> Guid

/// <summary>Provides environment-variable access supplied by the application host.</summary>
type IEnvironmentVariables =
    abstract TryGet: name: string -> string option
    abstract Set: name: string * value: string option -> unit
    abstract Expand: text: string -> string
    abstract GetAll: unit -> IReadOnlyDictionary<string, string>

/// <summary>Declares that an environment supplies the logging service.</summary>
#if FABLE_COMPILER
[<Fable.Core.Mangle>]
#endif
type IHasLog =
    /// The logging service supplied by this environment.
    abstract Log : ILog

/// <summary>Declares that an environment supplies the random-number service.</summary>
#if FABLE_COMPILER
[<Fable.Core.Mangle>]
#endif
type IHasRandom =
    /// The random-number service supplied by this environment. This service is not suitable for secrets, tokens, or nonces.
    abstract Random : IRandom

/// <summary>Declares that an environment supplies cryptographically secure randomness for secrets, tokens, and nonces.</summary>
/// <remarks><see cref="T:Axial.PlatformService.IRandom" /> is not suitable for security-sensitive values.</remarks>
#if FABLE_COMPILER
[<Fable.Core.Mangle>]
#endif
type IHasSecureRandom =
    /// The cryptographically secure random service supplied by this environment.
    abstract SecureRandom : ISecureRandom

/// <summary>Declares that an environment supplies the GUID service.</summary>
#if FABLE_COMPILER
[<Fable.Core.Mangle>]
#endif
type IHasGuid =
    /// The GUID service supplied by this environment.
    abstract Guid : IGuid

/// <summary>Declares that an environment supplies the environment-variable service.</summary>
#if FABLE_COMPILER
[<Fable.Core.Mangle>]
#endif
type IHasEnvironmentVariables =
    /// The environment-variable service supplied by this environment.
    abstract EnvironmentVariables : IEnvironmentVariables

[<RequireQualifiedAccess>]
type EnvironmentVariableError =
    | MissingVariable of name: string
    | InvalidVariable of name: string * value: string * expected: string

    /// Describes the error without reflection (safe under NativeAOT); matches EnvironmentVariableErrors.describe.
    override this.ToString() =
        match this with
        | MissingVariable name -> (System.String.Format(Globalization.CultureInfo.InvariantCulture, "Missing required environment variable '{0}'.", name))
        | InvalidVariable(name, value, expected) -> (System.String.Format(Globalization.CultureInfo.InvariantCulture, "Environment variable '{0}' had value '{1}' but expected {2}.", name, value, expected))

[<RequireQualifiedAccess>]
type BaseRuntimeError =
    | MissingService of serviceName: string

    /// Describes the error without reflection so it stays safe under NativeAOT.
    override this.ToString() =
        match this with
        | MissingService serviceName -> (System.String.Format(Globalization.CultureInfo.InvariantCulture, "Missing runtime service '{0}'.", serviceName))
