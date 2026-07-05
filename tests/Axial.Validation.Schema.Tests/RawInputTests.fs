namespace Axial.Tests

open System
open System.Collections.Specialized
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
    let ``raw input adapts maps to object-shaped scalar fields`` () =
        let input =
            RawInput.ofMap (
                Map.ofList
                    [ "displayName", "Ada"
                      "email", "ada@example.com" ]
            )

        test <@ RawInput.lookupPath "displayName" input = RawInput.Scalar "Ada" @>
        test <@ RawInput.lookupPath "email" input = RawInput.Scalar "ada@example.com" @>

    [<Fact>]
    let ``raw input adapts name values and preserves repeated names`` () =
        let input =
            RawInput.ofNameValues
                [ "tag", "fsharp"
                  "tag", "validation"
                  "name", "Ada" ]

        test <@ RawInput.lookupPath "name" input = RawInput.Scalar "Ada" @>
        test <@ RawInput.lookupPath "tag" input = RawInput.Many [ RawInput.Scalar "fsharp"; RawInput.Scalar "validation" ] @>

    [<Fact>]
    let ``raw input adapts name value collections`` () =
        let values = NameValueCollection()
        values.Add("tag", "fsharp")
        values.Add("tag", "validation")
        values.Add("name", "Ada")

        let input = RawInput.ofNameValueCollection values

        test <@ RawInput.lookupPath "name" input = RawInput.Scalar "Ada" @>
        test <@ RawInput.lookupPath "tag" input = RawInput.Many [ RawInput.Scalar "fsharp"; RawInput.Scalar "validation" ] @>

    [<Fact>]
    let ``raw input adapts CLI args to named fields flags and positionals`` () =
        let input =
            RawInput.ofCliArgs
                [ "--name"
                  "Ada"
                  "--tag=fsharp"
                  "--tag"
                  "validation"
                  "--active"
                  "--no-archived"
                  "import.csv"
                  "--"
                  "--literal" ]

        test <@ RawInput.lookupPath "name" input = RawInput.Scalar "Ada" @>
        test <@ RawInput.lookupPath "active" input = RawInput.Scalar "true" @>
        test <@ RawInput.lookupPath "archived" input = RawInput.Scalar "false" @>
        test <@ RawInput.lookupPath "tag" input = RawInput.Many [ RawInput.Scalar "fsharp"; RawInput.Scalar "validation" ] @>
        test <@ RawInput.lookupPath "_" input = RawInput.Many [ RawInput.Scalar "import.csv"; RawInput.Scalar "--literal" ] @>

    [<Fact>]
    let ``raw input adapts JSON-like values`` () =
        let input =
            JsonLikeValue.Object(
                Map.ofList
                    [ "name", JsonLikeValue.String "Ada"
                      "active", JsonLikeValue.Bool true
                      "score", JsonLikeValue.Number "42.5"
                      "middleName", JsonLikeValue.Null
                      "contacts",
                      JsonLikeValue.Array
                          [ JsonLikeValue.Object(Map.ofList [ "value", JsonLikeValue.String "ada@example.com" ])
                            JsonLikeValue.Object(Map.ofList [ "value", JsonLikeValue.String "+61 400 000 000" ]) ] ]
            )
            |> RawInput.ofJsonLikeValue

        test <@ RawInput.lookupPath "name" input = RawInput.Scalar "Ada" @>
        test <@ RawInput.lookupPath "active" input = RawInput.Scalar "true" @>
        test <@ RawInput.lookupPath "score" input = RawInput.Scalar "42.5" @>
        test <@ RawInput.lookupPath "middleName" input = RawInput.Missing @>
        test <@ RawInput.lookupPath "contacts[1].value" input = RawInput.Scalar "+61 400 000 000" @>

    [<Fact>]
    let ``raw input adapts flattened configuration keys`` () =
        let input =
            RawInput.ofConfiguration
                [ "displayName", "Ada"
                  "contacts:0:value", "ada@example.com"
                  "contacts:1:value", "+61 400 000 000"
                  "features:email", "true" ]

        test <@ RawInput.lookupPath "displayName" input = RawInput.Scalar "Ada" @>
        test <@ RawInput.lookupPath "contacts[0].value" input = RawInput.Scalar "ada@example.com" @>
        test <@ RawInput.lookupPath "contacts[1].value" input = RawInput.Scalar "+61 400 000 000" @>
        test <@ RawInput.lookupPath "features.email" input = RawInput.Scalar "true" @>

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

    [<Fact>]
    let ``raw input finds nested values by parsed path`` () =
        let input =
            RawInput.Object(
                Map.ofList
                    [ "contacts",
                      RawInput.Many
                          [ RawInput.Object(Map.ofList [ "value", RawInput.Scalar "ada@example.com" ])
                            RawInput.Object(Map.ofList [ "value", RawInput.Scalar "+61 400 000 000" ]) ] ]
            )

        let path = InputPath.parse "contacts[1].value"

        test <@ RawInput.tryFind path input = Some(RawInput.Scalar "+61 400 000 000") @>
        test <@ RawInput.lookup path input = RawInput.Scalar "+61 400 000 000" @>

    [<Fact>]
    let ``raw input finds root value by empty path`` () =
        let input = RawInput.Scalar "Ada"

        test <@ RawInput.tryFind InputPath.empty input = Some input @>
        test <@ RawInput.lookupPath "" input = input @>

    [<Fact>]
    let ``raw input lookup returns missing when path is absent`` () =
        let input =
            RawInput.Object(
                Map.ofList
                    [ "contacts",
                      RawInput.Many [ RawInput.Object(Map.ofList [ "value", RawInput.Scalar "ada@example.com" ]) ] ]
            )

        test <@ RawInput.tryFindPath "contacts[1].value" input = None @>
        test <@ RawInput.lookupPath "contacts[1].value" input = RawInput.Missing @>
        test <@ RawInput.lookupPath "contacts[0].label" input = RawInput.Missing @>

    [<Fact>]
    let ``raw input lookup returns missing when path shape does not match`` () =
        let input =
            RawInput.Object(
                Map.ofList
                    [ "name", RawInput.Scalar "Ada"
                      "contacts", RawInput.Object(Map.ofList [ "primary", RawInput.Scalar "email" ]) ]
            )

        test <@ RawInput.tryFindPath "name.value" input = None @>
        test <@ RawInput.lookupPath "contacts[0]" input = RawInput.Missing @>

    [<Fact>]
    let ``raw input text path lookup rejects invalid addresses`` () =
        let input = RawInput.Object Map.empty

        test <@ RawInput.tryFindPath "contacts[]" input = None @>
        raises<FormatException> <@ RawInput.lookupPath "contacts[]" input |> ignore @>

    [<Fact>]
    let ``raw input redisplays scalar and missing values`` () =
        test <@ RawInput.tryRedisplay (RawInput.Scalar "Ada") = Some "Ada" @>
        test <@ RawInput.redisplay (RawInput.Scalar "Ada") = "Ada" @>
        test <@ RawInput.tryRedisplay RawInput.Missing = Some "" @>
        test <@ RawInput.redisplay RawInput.Missing = "" @>

    [<Fact>]
    let ``raw input redisplay ignores object and many values`` () =
        let objectInput = RawInput.Object(Map.ofList [ "name", RawInput.Scalar "Ada" ])
        let manyInput = RawInput.Many [ RawInput.Scalar "Ada" ]

        test <@ RawInput.tryRedisplay objectInput = None @>
        test <@ RawInput.redisplay objectInput = "" @>
        test <@ RawInput.tryRedisplay manyInput = None @>
        test <@ RawInput.redisplay manyInput = "" @>

    [<Fact>]
    let ``raw input redisplays values by parsed and text paths`` () =
        let input =
            RawInput.Object(
                Map.ofList
                    [ "displayName", RawInput.Scalar "Ada"
                      "contacts",
                      RawInput.Many
                          [ RawInput.Object(Map.ofList [ "value", RawInput.Scalar "ada@example.com" ])
                            RawInput.Object(Map.ofList [ "value", RawInput.Scalar "+61 400 000 000" ]) ] ]
            )

        let path = InputPath.parse "contacts[1].value"

        test <@ RawInput.tryRedisplayAt path input = Some "+61 400 000 000" @>
        test <@ RawInput.redisplayAt path input = "+61 400 000 000" @>
        test <@ RawInput.tryRedisplayPath "displayName" input = Some "Ada" @>
        test <@ RawInput.redisplayPath "displayName" input = "Ada" @>

    [<Fact>]
    let ``raw input redisplay returns blank for absent or non scalar paths`` () =
        let input =
            RawInput.Object(
                Map.ofList
                    [ "profile", RawInput.Object(Map.ofList [ "name", RawInput.Scalar "Ada" ])
                      "contacts", RawInput.Many [ RawInput.Scalar "ada@example.com" ] ]
            )

        test <@ RawInput.tryRedisplayPath "missing" input = Some "" @>
        test <@ RawInput.redisplayPath "missing" input = "" @>
        test <@ RawInput.tryRedisplayPath "profile" input = None @>
        test <@ RawInput.redisplayPath "profile" input = "" @>
        test <@ RawInput.tryRedisplayPath "contacts" input = None @>
        test <@ RawInput.redisplayPath "contacts" input = "" @>

    [<Fact>]
    let ``raw input redisplay text paths reject invalid addresses consistently`` () =
        let input = RawInput.Object Map.empty

        test <@ RawInput.tryRedisplayPath "contacts[]" input = None @>
        raises<FormatException> <@ RawInput.redisplayPath "contacts[]" input |> ignore @>

    [<Fact>]
    let ``raw input adapts system text json documents and elements`` () =
        use document =
            System.Text.Json.JsonDocument.Parse(
                """{"name":"Ada","age":36,"balance":12.50,"newsletter":true,"nickname":null,"tags":["vip","early"],"address":{"city":"London"}}"""
            )

        let input = RawInput.ofJsonDocument document

        test <@ RawInput.lookupPath "name" input = RawInput.Scalar "Ada" @>
        test <@ RawInput.lookupPath "age" input = RawInput.Scalar "36" @>
        test <@ RawInput.lookupPath "balance" input = RawInput.Scalar "12.50" @>
        test <@ RawInput.lookupPath "newsletter" input = RawInput.Scalar "true" @>
        test <@ RawInput.lookupPath "nickname" input = RawInput.Missing @>
        test <@ RawInput.lookupPath "tags[1]" input = RawInput.Scalar "early" @>
        test <@ RawInput.lookupPath "address.city" input = RawInput.Scalar "London" @>
        test <@ RawInput.ofJsonElement (document.RootElement.GetProperty "tags") = RawInput.Many [ RawInput.Scalar "vip"; RawInput.Scalar "early" ] @>
