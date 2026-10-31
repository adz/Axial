namespace Axial

open System

/// <summary>Culture values and explicit machine-default reads.</summary>
[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
[<RequireQualifiedAccess>]
module Culture =
    /// <summary>The invariant culture, whose name is empty.</summary>
    let invariant : Culture = Culture ""

    /// <summary>Resolves a BCP 47 name to a canonical culture value.</summary>
    /// <remarks>On .NET the name is resolved through <c>CultureInfo.GetCultureInfo</c>; on Fable through <c>Intl.getCanonicalLocales</c>.</remarks>
    /// <example>
    /// <code>
    /// match Culture.ofName "de-DE" with
    /// | Ok german -> Format.number german 1234.5
    /// | Error message -> message
    /// </code>
    /// </example>
    let ofName (name: string) : Result<Culture, string> =
        match Platform.Bridge.canonicalCultureName name with
        | Some canonical -> Ok(Culture canonical)
        | None -> Error(String.Concat("Unknown culture '", name, "'."))

    /// <summary>The culture's canonical BCP 47 name; the invariant culture has an empty name.</summary>
    let name (culture: Culture) : string = culture.Name

    /// <summary>Reads the machine culture as an explicit effect.</summary>
    /// <remarks>Under <c>Flow.runTest</c> this is the invariant culture, not the culture of the test process.</remarks>
    /// <example>
    /// <code>
    /// let! culture = Culture.system
    /// return Format.date culture instant
    /// </code>
    /// </example>
    let system<'env, 'error> : Flow<'env, 'error, Culture> =
        Flow(fun _ _ -> Execution.ofValue (RuntimePlatform.current().Platform.Culture()))

    /// <summary>Sets the process-wide .NET culture defaults and the current thread culture.</summary>
    /// <remarks>
    /// On .NET this sets <c>DefaultThreadCurrentCulture</c>, <c>DefaultThreadCurrentUICulture</c>,
    /// <c>CurrentCulture</c> and <c>CurrentUICulture</c>. On Fable it is a no-op.
    /// </remarks>
    let setProcessDefault (culture: Culture) : Flow<'env, 'error, unit> =
        Flow(fun _ _ ->
            Platform.Bridge.setProcessCulture culture.Name
            Execution.ofValue ())

/// <summary>Culture-aware formatting that takes the culture as a value.</summary>
[<RequireQualifiedAccess>]
module Format =
    /// <summary>Formats a number with group separators and two fractional digits.</summary>
    /// <example>
    /// <code>
    /// Format.number Culture.invariant 1234.5 // "1,234.50"
    /// </code>
    /// </example>
    let number (culture: Culture) (value: float) : string =
        Platform.Bridge.formatNumber culture.Name value

    /// <summary>Formats an amount in the culture's currency.</summary>
    /// <remarks>
    /// .NET uses the culture's currency symbol. Fable uses <c>Intl</c> with a region-derived currency, falling back
    /// to USD for regions it does not map.
    /// </remarks>
    let currency (culture: Culture) (value: decimal) : string =
        Platform.Bridge.formatCurrency culture.Name value

    /// <summary>Formats the culture's short date for the instant's wall clock.</summary>
    let date (culture: Culture) (instant: DateTimeOffset) : string =
        Platform.Bridge.formatDate culture.Name instant

    /// <summary>Formats the culture's short time for the instant's wall clock.</summary>
    let time (culture: Culture) (instant: DateTimeOffset) : string =
        Platform.Bridge.formatTime culture.Name instant

/// <summary>Time zone values and UTC-to-local conversion.</summary>
[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
[<RequireQualifiedAccess>]
module TimeZone =
    /// <summary>UTC, available on every target.</summary>
    let utc : Axial.TimeZone = Axial.TimeZone "UTC"

    /// <summary>Resolves an IANA id (or a Windows id on .NET) to a time zone value.</summary>
    /// <example>
    /// <code>
    /// match TimeZone.ofId "Europe/Berlin" with
    /// | Ok zone -> TimeZone.toLocal zone instant
    /// | Error message -> message
    /// </code>
    /// </example>
    let ofId (id: string) : Result<Axial.TimeZone, string> =
        match Platform.Bridge.canonicalTimeZoneId id with
        | Some canonical -> Ok(Axial.TimeZone canonical)
        | None -> Error(String.Concat("Unknown time zone '", id, "'."))

    /// <summary>The time zone's id.</summary>
    let id (zone: Axial.TimeZone) : string = zone.Id

    /// <summary>Reads the machine time zone as an explicit effect.</summary>
    /// <remarks>Under <c>Flow.runTest</c> this is UTC, not the time zone of the test process.</remarks>
    let local<'env, 'error> : Flow<'env, 'error, Axial.TimeZone> =
        Flow(fun _ _ -> Execution.ofValue (RuntimePlatform.current().Platform.TimeZone()))

    /// <summary>Converts a UTC instant to the zone's wall clock.</summary>
    /// <remarks>
    /// The result keeps the same instant with the zone's offset. Local-to-UTC conversion is not provided because
    /// skipped and repeated hours need a typed result.
    /// </remarks>
    /// <example>
    /// <code>
    /// let! now = Clock.now
    /// let! zone = TimeZone.local
    /// return TimeZone.toLocal zone now
    /// </code>
    /// </example>
    let toLocal (zone: Axial.TimeZone) (instant: DateTimeOffset) : DateTimeOffset =
        Platform.Bridge.toLocal zone.Id instant
