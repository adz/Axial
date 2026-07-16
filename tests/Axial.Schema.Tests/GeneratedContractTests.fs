namespace Axial.Tests

open Axial.Validation
open Axial.Schema
open Axial.Tests.Generated
open Swensen.Unquote
open Xunit

/// Behavior tests over the checked-in golden output of `axial schemagen` for the .contract corpus.
/// The generator test suite proves the emitter reproduces these files byte-for-byte; these tests
/// prove the emitted shape actually delivers named-field trusted construction and boundary parsing.
module GeneratedContractTests =

    [<Fact>]
    let ``generated field references update one field immutably`` () =
        let original: Axial.Tests.Generated.Geo = { Lat = 1m; Lon = 2m }
        let changed = Axial.Tests.Generated.Geo.Fields.lat.Set original 3m
        test <@ changed = { Lat = 3m; Lon = 2m } @>
        test <@ original = { Lat = 1m; Lon = 2m } @>

    [<Fact>]
    let ``generated recursive contract parses child trees`` () =
        let raw =
            RawInput.Object(
                Map.ofList
                    [ "name", RawInput.Scalar "root"
                      "children",
                      RawInput.Many
                          [ RawInput.Object(Map.ofList [ "name", RawInput.Scalar "leaf"; "children", RawInput.Many [] ]) ] ]
            )

        match Axial.Tests.Generated.Category.parse raw with
        | parsed when parsed.IsValid ->
            test <@ parsed.Value.Children = [ { Axial.Tests.Generated.Category.Name = "leaf"; Children = [] } ] @>
        | parsed -> failwithf "Expected recursive generated input to parse, got %A" parsed.Errors

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
                |> Diagnostics.flatten
                |> List.map _.Path

            test <@ paths |> List.contains [ PathSegment.Name "email" ] @>
            test <@ paths |> List.contains [ PathSegment.Name "age" ] @>

    [<Fact>]
    let ``validate checks nested optional payloads at nested paths`` () =
        let badGeo =
            match Geo.validate { Lat = 200m; Lon = 10m } with
            | Ok _ -> failwith "Expected latitude out of range."
            | Error diagnostics -> diagnostics

        let paths = badGeo |> Diagnostics.flatten |> List.map _.Path
        test <@ paths = [ [ PathSegment.Name "lat" ] ] @>

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
            let paths = diagnostics |> Diagnostics.flatten |> List.map _.Path
            test <@ paths = [ [ PathSegment.Name "location"; PathSegment.Name "lat" ] ] @>

    [<Fact>]
    let ``parse accepts wire names and enum tags`` () =
        let raw =
            RawInput.Object(
                Map.ofList
                    [ "email", RawInput.Scalar "ada@example.com"
                      "display_name", RawInput.Scalar "Ada"
                      "age", RawInput.Scalar "42"
                      "plan", RawInput.Scalar "pro"
                      "tags", RawInput.Many [ RawInput.Scalar "one" ]
                      "limits", RawInput.Object(Map.ofList [ "daily", RawInput.Scalar "10" ]) ]
            )

        let parsed = Signup.parse raw

        match parsed.Result with
        | Ok signup ->
            test <@ signup.DisplayName = Some "Ada" @>
            test <@ signup.Plan = SignupPlan.Pro @>
            test <@ signup.Location = None @>
        | Error diagnostics -> failwithf "Expected parse success, got %A" diagnostics

    [<Fact>]
    let ``field references carry wire names and typed getters`` () =
        test <@ Signup.Fields.displayName.Name = "display_name" @>
        test <@ Signup.Fields.age.Path = [ PathSegment.Name "age" ] @>

        let draft =
            { Email = "ada@example.com"
              DisplayName = None
              Age = 42
              Plan = SignupPlan.Free
              Tags = []
              Limits = Map.empty
              Location = None }

        test <@ Signup.Fields.email.Get draft = "ada@example.com" @>

    [<Fact>]
    let ``context rules scope diagnostics through generated field references`` () =
        let needsDisplayName (signup: Signup) =
            match signup.DisplayName with
            | Some _ -> Ok()
            | None ->
                ContextRules.failAtField
                    Signup.Fields.displayName
                    (ContextRules.custom "signup.displayName.required" "Pro plans need a display name.")

        let rule =
            (fun (signup: Signup) ->
                if signup.Plan = SignupPlan.Pro then needsDisplayName signup else Ok())

        let proWithoutName =
            { Email = "ada@example.com"
              DisplayName = None
              Age = 42
              Plan = SignupPlan.Pro
              Tags = []
              Limits = Map.empty
              Location = None }

        match ContextRules.apply [ rule ] proWithoutName with
        | Ok _ -> failwith "Expected rule failure."
        | Error diagnostics ->
            let paths = diagnostics |> Diagnostics.flatten |> List.map _.Path
            test <@ paths = [ [ PathSegment.Name "display_name" ] ] @>

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
            RawInput.Object(
                Map.ofList
                    [ "source",
                      RawInput.Object(
                          Map.ofList [ "kind", RawInput.Scalar "invoice"; "reference", RawInput.Scalar "inv-42" ]
                      ) ]
            )

        match (Payment.parse raw).Result with
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
            RawInput.Object(
                Map.ofList
                    [ "schemaVersion", RawInput.Scalar "1"
                      "name", RawInput.Scalar "Ada"
                      "email", RawInput.Scalar "ada@example.com" ]
            )

        match Contract.parse (profileContract ()) rawV1 with
        | Ok profile -> test <@ profile = { Name = "Ada"; Email = "ada@example.com"; MarketingOptIn = false } @>
        | Error error -> failwithf "Expected a migrated v1 payload, got %A" error

    [<Fact>]
    let ``the generated contract builder parses the current version directly`` () =
        let rawV2 =
            RawInput.Object(
                Map.ofList
                    [ "schemaVersion", RawInput.Scalar "2"
                      "name", RawInput.Scalar "Ada"
                      "email", RawInput.Scalar "ada@example.com"
                      "marketing_opt_in", RawInput.Scalar "true" ]
            )

        match Contract.parse (profileContract ()) rawV2 with
        | Ok profile -> test <@ profile.MarketingOptIn @>
        | Error error -> failwithf "Expected a v2 parse, got %A" error

    [<Fact>]
    let ``the generated contract builder rejects versions newer than the chain`` () =
        let rawV3 =
            RawInput.Object(
                Map.ofList
                    [ "schemaVersion", RawInput.Scalar "3"
                      "name", RawInput.Scalar "Ada"
                      "email", RawInput.Scalar "ada@example.com" ]
            )

        test <@ Contract.parse (profileContract ()) rawV3 = Error(ContractError.VersionTooNew(3, 2)) @>

    [<Fact>]
    let ``record-derived schemas parse wire payloads with enums unions and defaults`` () =
        let raw =
            RawInput.Object(
                Map.ofList
                    [ "reference", RawInput.Scalar "SH-42"
                      "notify_email", RawInput.Scalar "ada@example.com"
                      "items", RawInput.Object(Map.ofList [ "widget", RawInput.Scalar "3" ])
                      "tags", RawInput.Many [ RawInput.Scalar "fragile" ]
                      "weightKg", RawInput.Scalar "2.5"
                      "priority", RawInput.Scalar "same-day"
                      "delivery",
                      RawInput.Object(
                          Map.ofList [ "kind", RawInput.Scalar "pickup"; "code", RawInput.Scalar "LOCKER-9" ]
                      )
                      "boxes", RawInput.Scalar "2" ]
            )

        match (Shipment.parse raw).Result with
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
            let paths = diagnostics |> Diagnostics.flatten |> List.map _.Path

            for expected in [ "reference"; "notify_email"; "tags"; "weightKg"; "boxes" ] do
                test <@ paths |> List.contains [ PathSegment.Name expected ] @>

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
            RawInput.Object(
                Map.ofList
                    [ "schemaVersion", RawInput.Scalar "1"
                      "reference", RawInput.Scalar "SH-7"
                      "notifyEmail", RawInput.Scalar "ada@example.com"
                      "items", RawInput.Object(Map.ofList [ "widget", RawInput.Scalar "1" ]) ]
            )

        match Contract.parse shipmentContract rawV1 with
        | Ok shipment ->
            test <@ shipment.Reference = "SH-7" @>
            test <@ shipment.Tags = [ "migrated" ] @>
        | Error error -> failwithf "Expected a migrated v1 shipment, got %A" error

    [<Fact>]
    let ``superseded generated versions keep their own frozen schema and fields`` () =
        let parsed =
            ProfileV1.parse (
                RawInput.Object(
                    Map.ofList [ "name", RawInput.Scalar "Ada"; "email", RawInput.Scalar "ada@example.com" ]
                )
            )

        match parsed.Result with
        | Ok v1 -> test <@ ProfileV1.Fields.name.Get v1 = "Ada" @>
        | Error diagnostics -> failwithf "Expected a v1 parse, got %A" diagnostics
