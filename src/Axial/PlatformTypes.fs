namespace Axial

open System

/// Random values are controlled per runtime scope, not read from the application environment.
type IRandom =
    abstract Next: unit -> int
    abstract NextMax: maxExclusive: int -> int
    abstract NextInt: minInclusive: int -> maxExclusive: int -> int
    abstract NextDouble: unit -> float
    abstract NextBytes: buffer: byte array -> unit

/// GUID values are controlled per runtime scope.
type IGuid =
    abstract NewGuid: unit -> Guid

[<RequireQualifiedAccess>]
type LogLevel =
    | Trace | Debug | Information | Warning | Error | Critical
    override this.ToString() =
        match this with
        | Trace -> "Trace" | Debug -> "Debug" | Information -> "Information"
        | Warning -> "Warning" | Error -> "Error" | Critical -> "Critical"

/// Immutable snapshots of annotations and named fiber locals at the logging call.
type LogEntry =
    { Level: LogLevel
      Message: string
      Error: exn option
      Annotations: Map<string, string>
      Locals: (string * obj) list }

/// The level gate is checked before constructing a log entry. Sink exceptions are guarded.
type ILogSink =
    abstract IsEnabled: level: LogLevel -> bool
    abstract Write: entry: LogEntry -> unit

/// Write-only observation slots. Installing telemetry replaces both slots within its scope.
type ITelemetry =
    abstract Annotate: name: string * value: string -> unit
    abstract Attribute: attribute: Axial.Telemetry.Attribute -> unit


/// <summary>A culture selected explicitly as a value.</summary>
/// <remarks>
/// On .NET this wraps a read-only <c>CultureInfo</c> and implements <c>IFormatProvider</c>; on Fable it carries a
/// BCP 47 name for <c>Intl</c>. The empty name is the invariant culture.
/// </remarks>
[<Sealed>]
type Culture internal (name: string) =
    member _.Name = name

#if !FABLE_COMPILER
    member internal _.CultureInfo =
        if String.IsNullOrEmpty name then System.Globalization.CultureInfo.InvariantCulture
        else System.Globalization.CultureInfo.GetCultureInfo name

    interface IFormatProvider with
        member this.GetFormat(formatType: Type) = (this.CultureInfo :> IFormatProvider).GetFormat formatType
#endif

    override _.ToString() = if String.IsNullOrEmpty name then "invariant" else name

/// <summary>A time zone selected explicitly as a value.</summary>
/// <remarks>Ids are IANA ids on every target; .NET also accepts Windows ids. <c>UTC</c> is always available.</remarks>
[<Sealed>]
type TimeZone internal (id: string) =
    member _.Id = id
    override _.ToString() = id
