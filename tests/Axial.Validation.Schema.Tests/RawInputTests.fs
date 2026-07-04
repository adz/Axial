namespace Axial.Tests

open System
open Axial.Validation
open Axial.Validation.Schema
open Swensen.Unquote
open Xunit

module RawInputTests =
    [<Fact>]
    let ``raw input represents missing scalar many and object shapes`` () =
        let input =
            RawInput.Object(
                Map.ofList
                    [ "name", RawInput.Scalar "Ada"
                      "contacts", RawInput.Many [ RawInput.Scalar "ada@example.com"; RawInput.Missing ] ]
            )

        match input with
        | RawInput.Object fields ->
            test <@ fields["name"] = RawInput.Scalar "Ada" @>
            test <@ fields["contacts"] = RawInput.Many [ RawInput.Scalar "ada@example.com"; RawInput.Missing ] @>
        | _ -> failwith "Expected object input."

    [<Fact>]
    let ``raw input object fields are source names not schema diagnostics`` () =
        let input =
            RawInput.Object(
                Map.ofList
                    [ "displayName", RawInput.Scalar "Ada"
                      "empty", RawInput.Missing ]
            )

        match input with
        | RawInput.Object fields ->
            test <@ fields |> Map.containsKey "displayName" @>
            test <@ fields["empty"] = RawInput.Missing @>
        | _ -> failwith "Expected object input."

    [<Fact>]
    let ``input path constructs names and indexes`` () =
        let path =
            InputPath.name "contacts"
            |> InputPath.appendIndex 1
            |> InputPath.appendName "value"

        test
            <@
                path =
                    [ InputPathSegment.Name "contacts"
                      InputPathSegment.Index 1
                      InputPathSegment.Name "value" ]
            @>

        test <@ InputPath.toString path = "contacts[1].value" @>
        test
            <@
                InputPath.toDiagnosticsPath path =
                    [ PathSegment.Name "contacts"
                      PathSegment.Index 1
                      PathSegment.Name "value" ]
            @>

    [<Fact>]
    let ``input path parses names indexes and root path`` () =
        test <@ InputPath.tryParse "" = Some InputPath.empty @>
        test
            <@
                InputPath.tryParse "contacts[1].value" =
                    Some
                        [ InputPathSegment.Name "contacts"
                          InputPathSegment.Index 1
                          InputPathSegment.Name "value" ]
            @>

        test
            <@
                InputPath.parse "[0].name" =
                    [ InputPathSegment.Index 0
                      InputPathSegment.Name "name" ]
            @>

    [<Fact>]
    let ``input path renders and parses quoted names`` () =
        let path =
            [ InputPathSegment.Name "contact.value"
              InputPathSegment.Index 0
              InputPathSegment.Name "display\"name" ]
            |> InputPath.ofSegments

        let rendered = InputPath.toString path

        test <@ rendered = "[\"contact.value\"][0][\"display\\\"name\"]" @>
        test <@ InputPath.parse rendered = path @>

    [<Fact>]
    let ``input path rejects invalid addresses`` () =
        test <@ InputPath.tryParse "." = None @>
        test <@ InputPath.tryParse "contacts[]" = None @>
        test <@ InputPath.tryParse "contacts[-1]" = None @>
        test <@ InputPath.tryParse "contacts[1" = None @>
        raises<ArgumentException> <@ InputPath.name "" |> ignore @>
        raises<ArgumentException> <@ InputPath.index -1 |> ignore @>
