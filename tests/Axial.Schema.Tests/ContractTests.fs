namespace Axial.Schema.Tests

open Axial.ErrorHandling
open Axial.Schema
open Axial.Validation
open Swensen.Unquote
open Xunit

module ContractTests =
    type ConfigV1 = { Version: int; Name: string }
    type ConfigV2 = { Version: int; Name: string; Port: int }
    type Config = { Version: int; Label: string; Port: int }

    let private v1Schema () =
        Schema.recordFor<ConfigV1, _> (fun version name -> { Version = version; Name = name })
        |> Schema.field "version" _.Version Schema.int
        |> Schema.field "name" _.Name Schema.text
        |> Schema.build

    let private v2Schema () =
        Schema.recordFor<ConfigV2, _> (fun version name port -> { Version = version; Name = name; Port = port })
        |> Schema.field "version" _.Version Schema.int
        |> Schema.field "name" _.Name Schema.text
        |> Schema.field "port" _.Port Schema.int
        |> Schema.build

    let private currentSchema () =
        Schema.recordFor<Config, _> (fun version label port -> { Version = version; Label = label; Port = port })
        |> Schema.field "version" _.Version Schema.int
        |> Schema.field "label" _.Label (Schema.text |> Schema.constrainAll [ Constraint.minLength 3 ])
        |> Schema.field "port" _.Port (Schema.int |> Schema.constrainAll [ Constraint.between 1 65535 ])
        |> Schema.build

    let private migrateV1 (value: ConfigV1) : Result<ConfigV2, MigrationError> =
        Ok { Version = 2; Name = value.Name; Port = 8080 }

    let private migrateV2 (value: ConfigV2) : Result<Config, MigrationError> =
        Ok { Version = 3; Label = value.Name; Port = value.Port }

    let private builder () =
        Contract.create "device-config" 3 (currentSchema ())
        |> Contract.supersedes 2 (v2Schema ()) migrateV2
        |> Contract.supersedes 1 (v1Schema ()) migrateV1

    let private raw fields = RawInput.Object(Map.ofList fields)
    let private scalar value = RawInput.Scalar(string value)

    [<Fact>]
    let ``head version parses directly and returns a trusted model`` () =
        let contract = builder () |> Contract.build (VersionSource.Field "version")
        let input = raw [ "version", scalar 3; "label", scalar "edge"; "port", scalar 443 ]

        match Contract.parse contract input with
        | Ok model -> test <@ model = { Version = 3; Label = "edge"; Port = 443 } @>
        | Error error -> failwithf "Unexpected contract error: %A" error

    [<Fact>]
    let ``field source selects an older version and migrates one hop`` () =
        let contract =
            Contract.create "device-config" 3 (currentSchema ())
            |> Contract.supersedes 2 (v2Schema ()) migrateV2
            |> Contract.build (VersionSource.Field "version")

        let input = raw [ "version", scalar 2; "name", scalar "edge"; "port", scalar 8080 ]

        match Contract.parse contract input with
        | Ok model -> test <@ model = { Version = 3; Label = "edge"; Port = 8080 } @>
        | Error error -> failwithf "Unexpected contract error: %A" error

    [<Fact>]
    let ``registered migrations compose from oldest to head`` () =
        let contract = builder () |> Contract.build (VersionSource.Field "version")
        let input = raw [ "version", scalar 1; "name", scalar "edge" ]

        match Contract.parse contract input with
        | Ok model -> test <@ model = { Version = 3; Label = "edge"; Port = 8080 } @>
        | Error error -> failwithf "Unexpected contract error: %A" error

    [<Fact>]
    let ``unversioned fallback parses the configured registered version`` () =
        let contract = builder () |> Contract.build (VersionSource.UnversionedMeans 1)
        let input = raw [ "version", scalar 1; "name", scalar "edge" ]

        match Contract.parse contract input with
        | Ok model -> test <@ model.Label = "edge" @>
        | Error error -> failwithf "Unexpected contract error: %A" error

    [<Fact>]
    let ``newer versions report the highest supported version`` () =
        let contract = builder () |> Contract.build (VersionSource.Field "version")
        let result = Contract.parse contract (raw [ "version", scalar 4 ])
        test <@ result = Error(ContractError.VersionTooNew(4, 3)) @>

    [<Fact>]
    let ``gaps outside the registered chain are unrecognized`` () =
        let contract =
            Contract.create "device-config" 3 (currentSchema ())
            |> Contract.build (VersionSource.Field "version")

        let result = Contract.parse contract (raw [ "version", scalar 2 ])
        test <@ result = Error(ContractError.VersionUnrecognized 2) @>

    [<Fact>]
    let ``migration output is revalidated against the head schema`` () =
        let badMigration (_: ConfigV2) = Ok { Version = 3; Label = "x"; Port = 8080 }
        let contract =
            Contract.create "device-config" 3 (currentSchema ())
            |> Contract.supersedes 2 (v2Schema ()) badMigration
            |> Contract.build (VersionSource.Field "version")

        let input = raw [ "version", scalar 2; "name", scalar "edge"; "port", scalar 8080 ]
        match Contract.parse contract input with
        | Error(ContractError.Migration(MigrationError.RevalidationFailed diagnostics)) ->
            test <@ diagnostics |> Diagnostics.flatten |> List.map _.Path = [ [ PathSegment.Name "label" ] ] @>
        | result -> failwithf "Expected revalidation diagnostics, got %A" result

    [<Fact>]
    let ``migration failures remain distinct from schema failures`` () =
        let failing (_: ConfigV2) = Error(MigrationError.MigrationFailed "port has no replacement")
        let contract =
            Contract.create "device-config" 3 (currentSchema ())
            |> Contract.supersedes 2 (v2Schema ()) failing
            |> Contract.build (VersionSource.Field "version")

        let input = raw [ "version", scalar 2; "name", scalar "edge"; "port", scalar 8080 ]
        test <@ Contract.parse contract input = Error(ContractError.Migration(MigrationError.MigrationFailed "port has no replacement")) @>

    [<Fact>]
    let ``external version parsing uses the supplied version`` () =
        let contract = builder () |> Contract.build VersionSource.External
        let input = raw [ "version", scalar 1; "name", scalar "edge" ]

        match Contract.parseVersion contract 1 input with
        | Ok model -> test <@ model.Port = 8080 @>
        | Error error -> failwithf "Unexpected contract error: %A" error

    [<Fact>]
    let ``parse failures retain all paths and diagnostics`` () =
        let contract = builder () |> Contract.build (VersionSource.Field "version")
        let input = raw [ "version", scalar 3; "label", scalar "x"; "port", scalar 70000 ]

        match Contract.parse contract input with
        | Error(ContractError.ParseFailed(3, diagnostics)) ->
            test <@ diagnostics |> Diagnostics.flatten |> List.map _.Path = [ [ PathSegment.Name "label" ]; [ PathSegment.Name "port" ] ] @>
        | result -> failwithf "Expected parse diagnostics, got %A" result

    [<Fact>]
    let ``missing or malformed field versions cannot select a schema`` () =
        let contract = builder () |> Contract.build (VersionSource.Field "version")
        test <@ Contract.parse contract (raw [ "name", scalar "edge" ]) = Error ContractError.VersionMissing @>
        test <@ Contract.parse contract (raw [ "version", scalar "latest" ]) = Error ContractError.VersionMissing @>

    [<Fact>]
    let ``contract metadata exposes its stable identity and head`` () =
        let schema = currentSchema ()
        let contract = Contract.create "device-config" 3 schema |> Contract.build VersionSource.External
        test <@ Contract.name contract = "device-config" @>
        test <@ Contract.currentVersion contract = 3 @>
        test <@ obj.ReferenceEquals(Contract.currentSchema contract, schema) @>

    [<Fact>]
    let ``versions must be registered as a contiguous descending chain`` () =
        let skipV2 (value: ConfigV1) : Result<Config, MigrationError> =
            Ok { Version = 3; Label = value.Name; Port = 8080 }

        raises<System.ArgumentException> <@
            Contract.create "device-config" 3 (currentSchema ())
            |> Contract.supersedes 1 (v1Schema ()) skipV2
            |> ignore
        @>
