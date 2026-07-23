// Versioned contracts: detect a payload's schema version and migrate it stepwise to the current one
// before parsing. This is the at-scale tier over plain schemas — start with Schema.parse; reach for
// contracts when multiple wire versions must stay readable.
namespace Axial.Schema

open Axial

open System
/// A failure produced while migrating an older wire representation.
[<RequireQualifiedAccess>]
type MigrationError =
    | MigrationFailed of message: string
    | RevalidationFailed of SchemaErrors

/// The explicit source of a contract version.
[<RequireQualifiedAccess>]
type VersionSource =
    | Field of wireName: string
    | External
    | UnversionedMeans of version: int

/// A failure to select, parse, or migrate a versioned contract.
[<RequireQualifiedAccess>]
type ContractError =
    | VersionMissing
    | VersionUnrecognized of version: int
    | VersionTooNew of detected: int * highestSupported: int
    | ParseFailed of version: int * SchemaErrors
    | Migration of MigrationError

type private ContractVersion =
    {
        Version: int
        Parse: Data -> Result<obj, SchemaErrors>
        MigrateToNext: (obj -> Result<obj, MigrationError>) option
    }

/// A progressively typed version chain. The second type parameter is the oldest representation currently registered.
type ContractBuilder<'model, 'current> =
    private
        {
            Name: string
            CurrentVersion: int
            CurrentSchema: Schema<'model>
            Versions: ContractVersion list
        }

/// A versioned wire contract whose successful result is the current domain model.
type Contract<'model> =
    private
        {
            Name: string
            CurrentVersion: int
            CurrentSchema: Schema<'model>
            Source: VersionSource
            Versions: ContractVersion list
        }

/// Functions for declaring and parsing explicitly versioned wire contracts.
[<RequireQualifiedAccess>]
module Contract =
    let private validateName (name: string) =
        if isNull name then nullArg (nameof name)
        if String.IsNullOrWhiteSpace name then invalidArg (nameof name) "Contract names cannot be empty."

    let private validateVersion parameterName version =
        if version < 1 then invalidArg parameterName "Contract versions must be positive integers."

    /// Starts a contract at its current version and schema.
    let create<'model> (name: string) (currentVersion: int) (currentSchema: Schema<'model>) : ContractBuilder<'model, 'model> =
        validateName name
        validateVersion (nameof currentVersion) currentVersion
        if isNull (box currentSchema) then nullArg (nameof currentSchema)

        let head =
            {
                Version = currentVersion
                Parse = fun raw -> Schema.parse currentSchema raw |> Result.map box
                MigrateToNext = None
            }

        { Name = name; CurrentVersion = currentVersion; CurrentSchema = currentSchema; Versions = [ head ] }

    /// Adds the immediately preceding wire version and its typed migration to the next registered version.
    let supersedes
        (version: int)
        (schema: Schema<'previous>)
        (migrate: 'previous -> Result<'current, MigrationError>)
        (builder: ContractBuilder<'model, 'current>)
        : ContractBuilder<'model, 'previous> =
        validateVersion (nameof version) version
        if isNull (box schema) then nullArg (nameof schema)
        if isNull (box migrate) then nullArg (nameof migrate)

        let oldest = builder.Versions.Head.Version
        if version <> oldest - 1 then
            invalidArg (nameof version) $"Expected the immediately preceding version {oldest - 1}, but received {version}."

        let entry =
            {
                Version = version
                Parse = fun raw -> Schema.parse schema raw |> Result.map box
                MigrateToNext = Some(fun value -> migrate (unbox<'previous> value) |> Result.map box)
            }

        { Name = builder.Name; CurrentVersion = builder.CurrentVersion; CurrentSchema = builder.CurrentSchema; Versions = entry :: builder.Versions }

    /// Finishes a contract with an explicit version-detection strategy.
    let build (source: VersionSource) (builder: ContractBuilder<'model, 'oldest>) : Contract<'model> =
        match source with
        | VersionSource.Field wireName ->
            if isNull wireName then nullArg (nameof wireName)
            if String.IsNullOrWhiteSpace wireName then invalidArg (nameof wireName) "Version field names cannot be empty."
        | VersionSource.UnversionedMeans version ->
            if not (builder.Versions |> List.exists (fun item -> item.Version = version)) then
                invalidArg (nameof source) $"The unversioned fallback {version} is not registered in this contract."
        | VersionSource.External -> ()

        { Name = builder.Name; CurrentVersion = builder.CurrentVersion; CurrentSchema = builder.CurrentSchema; Source = source; Versions = builder.Versions }

    let private parseSelected (contract: Contract<'model>) version raw =
        if version > contract.CurrentVersion then
            Error(ContractError.VersionTooNew(version, contract.CurrentVersion))
        else
            match contract.Versions |> List.tryFindIndex (fun item -> item.Version = version) with
            | None -> Error(ContractError.VersionUnrecognized version)
            | Some index ->
                let selected = contract.Versions[index]
                match selected.Parse raw with
                | Error diagnostics -> Error(ContractError.ParseFailed(version, diagnostics))
                | Ok parsed ->
                    let migrations = contract.Versions |> List.skip index |> List.choose _.MigrateToNext
                    let migrated = migrations |> List.fold (fun state migrate -> state |> Result.bind migrate) (Ok parsed)
                    match migrated with
                    | Error error -> Error(ContractError.Migration error)
                    | Ok value when version = contract.CurrentVersion -> Ok(unbox<'model> value)
                    | Ok value ->
                        match Schema.check contract.CurrentSchema (unbox<'model> value) with
                        | Ok current -> Ok current
                        | Error diagnostics -> Error(ContractError.Migration(MigrationError.RevalidationFailed diagnostics))

    /// Parses input using an out-of-band version value.
    let parseVersion (contract: Contract<'model>) (version: int) (raw: Data) : Result<'model, ContractError> =
        validateVersion (nameof version) version
        parseSelected contract version raw

    /// Detects the input version according to the contract and parses it into the current trusted model.
    let parse (contract: Contract<'model>) (raw: Data) : Result<'model, ContractError> =
        match contract.Source with
        | VersionSource.External -> Error ContractError.VersionMissing
        | VersionSource.UnversionedMeans version -> parseSelected contract version raw
        | VersionSource.Field wireName ->
            match Data.tryRedisplayPath wireName raw with
            | None
            | Some "" -> Error ContractError.VersionMissing
            | Some text ->
                match Int32.TryParse text with
                | true, version when version > 0 -> parseSelected contract version raw
                | _ -> Error ContractError.VersionMissing

    /// Returns the contract's stable name.
    let name (contract: Contract<'model>) = contract.Name

    /// Returns the highest supported version.
    let currentVersion (contract: Contract<'model>) = contract.CurrentVersion

    /// Returns the schema for the current domain model.
    let currentSchema (contract: Contract<'model>) = contract.CurrentSchema
