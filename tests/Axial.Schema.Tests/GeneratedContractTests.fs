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
            test <@ parsed.Model.Children = [ { Axial.Tests.Generated.Category.Name = "leaf"; Children = [] } ] @>
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
            test <@ signup.Value.Email = "ada@example.com" @>
            test <@ signup.Value.Plan = SignupPlan.Pro @>
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
        | Ok trusted -> test <@ trusted.Value.Source = PaymentSource.Card { Number = "4242424242424242" } @>
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

        test <@ emailField.Value.Description = Some "Primary contact address." @>

        let planField =
            description.Fields
            |> List.find (fun field -> field.Name = "plan")

        test <@ planField.Value.Default = Some(box SignupPlan.Free) @>
