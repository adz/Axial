namespace Axial.Tests

open Axial

open Axial.Schema
open Axial.Tests.Generated
open Swensen.Unquote
open Xunit

/// Behavior tests over the checked-in golden output of `axial schemagen` for the .contract corpus.
/// The generator test suite proves the emitter reproduces these files byte-for-byte; these tests
/// prove the emitted shape actually delivers named-field trusted construction and boundary parsing.
module GeneratedContractTests =

    [<Fact>]
    let ``generated recursive contract parses child trees`` () =
        let raw =
            Data.objectOfMap (Map.ofList
                    [ "name", Data.Text "root"
                      "children",
                      Data.List
                          [ Data.objectOfMap (Map.ofList [ "name", Data.Text "leaf"; "children", Data.List [] ]) ] ]
            )

        match Axial.Tests.Generated.Category.parse raw with
        | Ok parsed ->
            test <@ parsed.Children = [ { Axial.Tests.Generated.Category.Name = "leaf"; Children = [] } ] @>
        | Error diagnostics -> failwithf "Expected recursive generated input to parse, got %A" diagnostics

    [<Fact>]
    let ``validate promotes a draft record literal when every constraint passes`` () =
        let result =
            Signup.validate
                { Email = "ada@example.com"
                  DisplayName = Some "Ada"
                  Age = 42
                  Plan = SignupPlan.Pro
                  Tags = [ "fsharp"; "schema" ]
                  Limits = Map.ofList [ "daily", 10 ]
                  Location = None }

        match result with
        | Ok signup ->
            test <@ signup.Email = "ada@example.com" @>
            test <@ signup.Plan = SignupPlan.Pro @>
        | Error diagnostics -> failwithf "Expected trusted construction, got %A" diagnostics

    [<Fact>]
    let ``validate refuses invalid drafts with path-aware diagnostics`` () =
        let result =
            Signup.validate
                { Email = "not-an-email"
                  DisplayName = None
                  Age = 12
                  Plan = SignupPlan.Free
                  Tags = []
                  Limits = Map.empty
                  Location = None }

        match result with
        | Ok _ -> failwith "Expected constraint failures."
        | Error diagnostics ->
            let paths =
                diagnostics
                |> SchemaErrors.toList
                |> List.map _.Path

            test <@ paths |> List.contains (Path.key "email") @>
            test <@ paths |> List.contains (Path.key "age") @>

    [<Fact>]
    let ``validate checks nested optional payloads at nested paths`` () =
        let badGeo =
            match Geo.validate { Lat = 200m; Lon = 10m } with
            | Ok _ -> failwith "Expected latitude out of range."
            | Error diagnostics -> diagnostics

        let paths = badGeo |> SchemaErrors.toList |> List.map _.Path
        test <@ paths = [ Path.key "lat" ] @>

        let withLocation =
            Signup.validate
                { Email = "ada@example.com"
                  DisplayName = None
                  Age = 42
                  Plan = SignupPlan.Free
                  Tags = []
                  Limits = Map.empty
                  Location = Some { Lat = 45m; Lon = 90m } }

        test <@ Result.isOk withLocation @>

        let withBadLocation =
            Signup.validate
                { Email = "ada@example.com"
                  DisplayName = None
                  Age = 42
                  Plan = SignupPlan.Free
                  Tags = []
                  Limits = Map.empty
                  Location = Some { Lat = 200m; Lon = 90m } }

        match withBadLocation with
        | Ok _ -> failwith "Expected the nested latitude to fail."
        | Error diagnostics ->
            let paths = diagnostics |> SchemaErrors.toList |> List.map _.Path
            test <@ paths = [ TestPath.fromLegacy [ PathSegment.Name "location"; PathSegment.Name "lat" ] ] @>

    [<Fact>]
    let ``parse accepts wire names and enum tags`` () =
        let raw =
            Data.objectOfMap (Map.ofList
                    [ "email", Data.Text "ada@example.com"
                      "display_name", Data.Text "Ada"
                      "age", Data.Text "42"
                      "plan", Data.Text "pro"
                      "tags", Data.List [ Data.Text "one" ]
                      "limits", Data.objectOfMap (Map.ofList [ "daily", Data.Text "10" ]) ]
            )

        let parsed = Signup.parse raw

        match parsed with
        | Ok signup ->
            test <@ signup.DisplayName = Some "Ada" @>
            test <@ signup.Plan = SignupPlan.Pro @>
            test <@ signup.Location = None @>
        | Error diagnostics -> failwithf "Expected parse success, got %A" diagnostics

    [<Fact>]
    let ``tagged union contracts validate and parse through the generated inline union`` () =
        let payment =
            Payment.validate { Source = PaymentSource.Card { Number = "4242424242424242" } }

        match payment with
        | Ok trusted -> test <@ trusted.Source = PaymentSource.Card { Number = "4242424242424242" } @>
        | Error diagnostics -> failwithf "Expected trusted Payment, got %A" diagnostics

        match Payment.validate { Source = PaymentSource.Card { Number = "short" } } with
        | Ok _ -> failwith "Expected the card number length constraint to fail."
        | Error _ -> ()

        let raw =
            Data.objectOfMap (Map.ofList
                    [ "source",
                      Data.objectOfMap (Map.ofList [ "kind", Data.Text "invoice"; "reference", Data.Text "inv-42" ]
                      ) ]
            )

        match (Payment.parse raw) with
        | Ok parsed ->
            match parsed.Source with
            | PaymentSource.Invoice invoice -> test <@ invoice.Reference = "inv-42" @>
            | PaymentSource.Card _ -> failwith "Expected the invoice case."
        | Error diagnostics -> failwithf "Expected parse success, got %A" diagnostics

    [<Fact>]
    let ``schema metadata carries doc comments and defaults`` () =
        let description = Inspect.model Signup.schema

        let emailField =
            description.Fields
            |> List.find (fun field -> field.Name = "email")

        test <@ emailField.Schema.Description = Some "Primary contact address." @>

        let planField =
            description.Fields
            |> List.find (fun field -> field.Name = "plan")

        test <@ planField.Schema.Default = Some(box SignupPlan.Free) @>

    /// The hand-written migration the generated Profile.contract builder is wired with.
    let private profileContract () =
        Profile.contract
            (fun (v1: ProfileV1) -> Ok { Name = v1.Name; Email = v1.Email; MarketingOptIn = false })
            (VersionSource.Field "schemaVersion")

    [<Fact>]
    let ``the generated contract builder migrates a superseded version to the current model`` () =
        let rawV1 =
            Data.objectOfMap (Map.ofList
                    [ "schemaVersion", Data.Text "1"
                      "name", Data.Text "Ada"
                      "email", Data.Text "ada@example.com" ]
            )

        match Contract.parse (profileContract ()) rawV1 with
        | Ok profile -> test <@ profile = { Name = "Ada"; Email = "ada@example.com"; MarketingOptIn = false } @>
        | Error error -> failwithf "Expected a migrated v1 payload, got %A" error

    [<Fact>]
    let ``the generated contract builder parses the current version directly`` () =
        let rawV2 =
            Data.objectOfMap (Map.ofList
                    [ "schemaVersion", Data.Text "2"
                      "name", Data.Text "Ada"
                      "email", Data.Text "ada@example.com"
                      "marketing_opt_in", Data.Text "true" ]
            )

        match Contract.parse (profileContract ()) rawV2 with
        | Ok profile -> test <@ profile.MarketingOptIn @>
        | Error error -> failwithf "Expected a v2 parse, got %A" error

    [<Fact>]
    let ``the generated contract builder rejects versions newer than the chain`` () =
        let rawV3 =
            Data.objectOfMap (Map.ofList
                    [ "schemaVersion", Data.Text "3"
                      "name", Data.Text "Ada"
                      "email", Data.Text "ada@example.com" ]
            )

        test <@ Contract.parse (profileContract ()) rawV3 = Error(ContractError.VersionTooNew(3, 2)) @>

    [<Fact>]
    let ``record-derived schemas parse wire payloads with enums unions and defaults`` () =
        let raw =
            Data.objectOfMap (Map.ofList
                    [ "reference", Data.Text "SH-42"
                      "notify_email", Data.Text "ada@example.com"
                      "items", Data.objectOfMap (Map.ofList [ "widget", Data.Text "3" ])
                      "tags", Data.List [ Data.Text "fragile" ]
                      "weightKg", Data.Text "2.5"
                      "priority", Data.Text "same-day"
                      "delivery",
                      Data.objectOfMap (Map.ofList [ "kind", Data.Text "pickup"; "code", Data.Text "LOCKER-9" ]
                      )
                      "boxes", Data.Text "2" ]
            )

        match (Shipment.parse raw) with
        | Ok shipment ->
            test <@ shipment.Reference = "SH-42" @>
            test <@ shipment.Priority = ShipmentPriority.SameDay @>
            test <@ shipment.Delivery = DeliveryMethod.Pickup { Code = "LOCKER-9" } @>
            test <@ shipment.Origin = None @>
            test <@ shipment.Boxes = 2 @>
        | Error diagnostics -> failwithf "Expected a wire parse, got %A" diagnostics

        // Defaults are schema metadata (for JSON Schema output and editors), same as the .contract path.
        let boxesField =
            (Inspect.model Shipment.schema).Fields |> List.find (fun field -> field.Name = "boxes")

        test <@ boxesField.Schema.Default = Some(box 1) @>

    [<Fact>]
    let ``record-derived schemas enforce the attribute constraints`` () =
        let draft: Shipment =
            { Reference = "not-a-reference"
              NotifyEmail = "not-an-email"
              Items = Map.empty
              Tags = []
              WeightKg = 0.1m
              Priority = ShipmentPriority.Standard
              Delivery = DeliveryMethod.Courier { TrackingUrl = "https://example.com/t/1" }
              Origin = None
              Boxes = 0 }

        match Shipment.validate draft with
        | Ok _ -> failwith "Expected constraint failures."
        | Error diagnostics ->
            let paths = diagnostics |> SchemaErrors.toList |> List.map _.Path

            for expected in [ "reference"; "notify_email"; "tags"; "weightKg"; "boxes" ] do
                test <@ paths |> List.contains (Path.key expected) @>

    [<Fact>]
    let ``record-derived version chains migrate through the generated contract builder`` () =
        let shipmentContract =
            Shipment.contract
                (fun (v1: ShipmentV1) ->
                    Ok
                        { Reference = v1.Reference
                          NotifyEmail = v1.NotifyEmail
                          Items = v1.Items
                          Tags = [ "migrated" ]
                          WeightKg = 1.0m
                          Priority = ShipmentPriority.Standard
                          Delivery = DeliveryMethod.Pickup { Code = "DEFAULT" }
                          Origin = None
                          Boxes = 1 })
                (VersionSource.Field "schemaVersion")

        let rawV1 =
            Data.objectOfMap (Map.ofList
                    [ "schemaVersion", Data.Text "1"
                      "reference", Data.Text "SH-7"
                      "notifyEmail", Data.Text "ada@example.com"
                      "items", Data.objectOfMap (Map.ofList [ "widget", Data.Text "1" ]) ]
            )

        match Contract.parse shipmentContract rawV1 with
        | Ok shipment ->
            test <@ shipment.Reference = "SH-7" @>
            test <@ shipment.Tags = [ "migrated" ] @>
        | Error error -> failwithf "Expected a migrated v1 shipment, got %A" error

    [<Fact>]
    let ``superseded generated versions keep their own frozen schema`` () =
        let parsed =
            ProfileV1.parse (
                Data.objectOfMap (Map.ofList [ "name", Data.Text "Ada"; "email", Data.Text "ada@example.com" ]
                )
            )

        match parsed with
        | Ok v1 -> test <@ v1.Name = "Ada" @>
        | Error diagnostics -> failwithf "Expected a v1 parse, got %A" diagnostics
