namespace Axial.Tests

open Axial

open System
open System.Collections.Generic
open Microsoft.Extensions.Configuration
open System.Collections.Specialized
open Axial.Schema
open Swensen.Unquote
open Xunit

module DataTests =
    [<Fact>]
    let ``data builds object fields from a list and preserves duplicates`` () =
        let input =
            [ "name", Data.Text "Grace"
              "age", Data.Text "42"
              "name", Data.Text "Ada" ]
            |> Data.objectOfList

        test <@ input = Data.Object [ "name", Data.Text "Grace"; "age", Data.Text "42"; "name", Data.Text "Ada" ] @>
        test <@ Data.lookupPath "name" input = Data.Text "Ada" @>

    [<Fact>]
    let ``data represents null text list and object shapes`` () =
        let input =
            Data.objectOfMap (Map.ofList
                    [ "name", Data.Text "Ada"
                      "contacts", Data.List [ Data.Text "ada@example.com"; Data.Null ] ]
            )

        test <@ Data.lookupPath "name" input = Data.Text "Ada" @>
        test <@ Data.lookupPath "contacts" input = Data.List [ Data.Text "ada@example.com"; Data.Null ] @>

    [<Fact>]
    let ``data object fields retain source names`` () =
        let input =
            Data.objectOfMap (Map.ofList
                    [ "displayName", Data.Text "Ada"
                      "empty", Data.Null ]
            )

        test <@ Data.lookupPath "displayName" input = Data.Text "Ada" @>
        test <@ Data.lookupPath "empty" input = Data.Null @>

    [<Fact>]
    let ``structured data adapts maps to object-shaped scalar fields`` () =
        let input =
            Data.ofMap (
                Map.ofList
                    [ "displayName", "Ada"
                      "email", "ada@example.com" ]
            )

        test <@ Data.lookupPath "displayName" input = Data.Text "Ada" @>
        test <@ Data.lookupPath "email" input = Data.Text "ada@example.com" @>

    [<Fact>]
    let ``structured data adapts name values and preserves repeated names`` () =
        let input =
            Data.ofNameValues
                [ "tag", "fsharp"
                  "tag", "validation"
                  "name", "Ada" ]

        test <@ Data.lookupPath "name" input = Data.Text "Ada" @>
        test <@ Data.lookupPath "tag" input = Data.List [ Data.Text "fsharp"; Data.Text "validation" ] @>

    [<Fact>]
    let ``structured data adapts name value collections`` () =
        let values = NameValueCollection()
        values.Add("tag", "fsharp")
        values.Add("tag", "validation")
        values.Add("name", "Ada")

        let input = Data.ofNameValueCollection values

        test <@ Data.lookupPath "name" input = Data.Text "Ada" @>
        test <@ Data.lookupPath "tag" input = Data.List [ Data.Text "fsharp"; Data.Text "validation" ] @>

    [<Fact>]
    let ``structured data adapts CLI args to named fields flags and positionals`` () =
        let input =
            Data.ofCliArgs
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

        test <@ Data.lookupPath "name" input = Data.Text "Ada" @>
        test <@ Data.lookupPath "active" input = Data.Text "true" @>
        test <@ Data.lookupPath "archived" input = Data.Text "false" @>
        test <@ Data.lookupPath "tag" input = Data.List [ Data.Text "fsharp"; Data.Text "validation" ] @>
        test <@ Data.lookupPath "_" input = Data.List [ Data.Text "import.csv"; Data.Text "--literal" ] @>

    [<Fact>]
    let ``data represents JSON-shaped values directly`` () =
        let input =
            Data.Object
                [ "name", Data.Text "Ada"
                  "active", Data.Bool true
                  "score", Data.Number "42.5"
                  "middleName", Data.Null
                  "contacts",
                  Data.List
                      [ Data.Object [ "value", Data.Text "ada@example.com" ]
                        Data.Object [ "value", Data.Text "+61 400 000 000" ] ] ]
        test <@ Data.lookupPath "name" input = Data.Text "Ada" @>
        test <@ Data.lookupPath "active" input = Data.Bool true @>
        test <@ Data.lookupPath "score" input = Data.Number "42.5" @>
        test <@ Data.lookupPath "middleName" input = Data.Null @>
        test <@ Data.lookupPath "contacts[1].value" input = Data.Text "+61 400 000 000" @>

    [<Fact>]
    let ``structured data adapts flattened configuration keys`` () =
        let input =
            Data.ofConfiguration
                [ "displayName", "Ada"
                  "contacts:0:value", "ada@example.com"
                  "contacts:1:value", "+61 400 000 000"
                  "features:email", "true" ]

        test <@ Data.lookupPath "displayName" input = Data.Text "Ada" @>
        test <@ Data.lookupPath "contacts[0].value" input = Data.Text "ada@example.com" @>
        test <@ Data.lookupPath "contacts[1].value" input = Data.Text "+61 400 000 000" @>
        test <@ Data.lookupPath "features.email" input = Data.Text "true" @>

    [<Fact>]
    let ``later configuration pairs override earlier ones at the same path`` () =
        let input =
            Data.ofConfiguration [ "name", "default"; "age", "1"; "name", "Ada" ]

        test <@ Data.lookupPath "name" input = Data.Text "Ada" @>
        test <@ Data.lookupPath "age" input = Data.Text "1" @>

    [<Fact>]
    let ``a later section or scalar replaces the earlier shape at the same key`` () =
        let sectionWins = Data.ofConfiguration [ "a", "1"; "a:b", "2" ]
        test <@ Data.lookupPath "a.b" sectionWins = Data.Text "2" @>

        let scalarWins = Data.ofConfiguration [ "a:b", "2"; "a", "1" ]
        test <@ Data.lookupPath "a" scalarWins = Data.Text "1" @>

    [<Fact>]
    let ``a null section marker does not override the section's children`` () =
        // IConfiguration.AsEnumerable() emits section keys with null values alongside their children.
        let input =
            Data.ofConfiguration [ "address:city", "London"; "address", null ]

        test <@ Data.lookupPath "address.city" input = Data.Text "London" @>

    [<Fact>]
    let ``real IConfiguration layering round-trips through ofConfigurationPairs`` () =
        let defaults =
            [ "name", "default"
              "address:city", "Nowhere"
              "tags:0", "vip" ]
            |> Seq.map (fun (key, value) -> KeyValuePair(key, value))

        let overrides =
            [ "name", "Ada"; "address:city", "London"; "tags:1", "founder" ]
            |> Seq.map (fun (key, value) -> KeyValuePair(key, value))

        let configuration =
            ConfigurationBuilder()
                .AddInMemoryCollection(defaults)
                .AddInMemoryCollection(overrides)
                .Build()

        let input = Data.ofConfigurationPairs (configuration.AsEnumerable())

        test <@ Data.lookupPath "name" input = Data.Text "Ada" @>
        test <@ Data.lookupPath "address.city" input = Data.Text "London" @>
        test <@ Data.lookupPath "tags[0]" input = Data.Text "vip" @>
        test <@ Data.lookupPath "tags[1]" input = Data.Text "founder" @>

    [<Fact>]
    let ``input path constructs names and indexes`` () =
        let path =
            DataPath.name "contacts"
            |> DataPath.appendIndex 1
            |> DataPath.appendName "value"

        test
            <@
                path =
                    [ DataPathSegment.Name "contacts"
                      DataPathSegment.Index 1
                      DataPathSegment.Name "value" ]
            @>

        test <@ DataPath.toString path = "contacts[1].value" @>
    [<Fact>]
    let ``input path parses names indexes and root path`` () =
        test <@ DataPath.tryParse "" = Some DataPath.empty @>
        test
            <@
                DataPath.tryParse "contacts[1].value" =
                    Some
                        [ DataPathSegment.Name "contacts"
                          DataPathSegment.Index 1
                          DataPathSegment.Name "value" ]
            @>

        test
            <@
                DataPath.parse "[0].name" =
                    [ DataPathSegment.Index 0
                      DataPathSegment.Name "name" ]
            @>

    [<Fact>]
    let ``input path renders and parses quoted names`` () =
        let path =
            [ DataPathSegment.Name "contact.value"
              DataPathSegment.Index 0
              DataPathSegment.Name "display\"name" ]
            |> DataPath.ofSegments

        let rendered = DataPath.toString path

        test <@ rendered = "[\"contact.value\"][0][\"display\\\"name\"]" @>
        test <@ DataPath.parse rendered = path @>

    [<Fact>]
    let ``input path rejects invalid addresses`` () =
        test <@ DataPath.tryParse "." = None @>
        test <@ DataPath.tryParse "contacts[]" = None @>
        test <@ DataPath.tryParse "contacts[-1]" = None @>
        test <@ DataPath.tryParse "contacts[1" = None @>
        raises<ArgumentException> <@ DataPath.name "" |> ignore @>
        raises<ArgumentException> <@ DataPath.index -1 |> ignore @>

    [<Fact>]
    let ``structured data finds nested values by parsed path`` () =
        let input =
            Data.objectOfMap (Map.ofList
                    [ "contacts",
                      Data.List
                          [ Data.objectOfMap (Map.ofList [ "value", Data.Text "ada@example.com" ])
                            Data.objectOfMap (Map.ofList [ "value", Data.Text "+61 400 000 000" ]) ] ]
            )

        let path = DataPath.parse "contacts[1].value"

        test <@ Data.tryFind path input = Some(Data.Text "+61 400 000 000") @>
        test <@ Data.lookup path input = Data.Text "+61 400 000 000" @>

    [<Fact>]
    let ``structured data finds root value by empty path`` () =
        let input = Data.Text "Ada"

        test <@ Data.tryFind DataPath.empty input = Some input @>
        test <@ Data.lookupPath "" input = input @>

    [<Fact>]
    let ``structured data lookup returns missing when path is absent`` () =
        let input =
            Data.objectOfMap (Map.ofList
                    [ "contacts",
                      Data.List [ Data.objectOfMap (Map.ofList [ "value", Data.Text "ada@example.com" ]) ] ]
            )

        test <@ Data.tryFindPath "contacts[1].value" input = None @>
        test <@ Data.lookupPath "contacts[1].value" input = Data.Null @>
        test <@ Data.lookupPath "contacts[0].label" input = Data.Null @>

    [<Fact>]
    let ``structured data lookup returns missing when path shape does not match`` () =
        let input =
            Data.objectOfMap (Map.ofList
                    [ "name", Data.Text "Ada"
                      "contacts", Data.objectOfMap (Map.ofList [ "primary", Data.Text "email" ]) ]
            )

        test <@ Data.tryFindPath "name.value" input = None @>
        test <@ Data.lookupPath "contacts[0]" input = Data.Null @>

    [<Fact>]
    let ``structured data text path lookup rejects invalid addresses`` () =
        let input = Data.Object []

        test <@ Data.tryFindPath "contacts[]" input = None @>
        raises<FormatException> <@ Data.lookupPath "contacts[]" input |> ignore @>

    [<Fact>]
    let ``structured data redisplays scalar and missing values`` () =
        test <@ Data.tryRedisplay (Data.Text "Ada") = Some "Ada" @>
        test <@ Data.redisplay (Data.Text "Ada") = "Ada" @>
        test <@ Data.tryRedisplay Data.Null = Some "" @>
        test <@ Data.redisplay Data.Null = "" @>

    [<Fact>]
    let ``structured data redisplay ignores object and many values`` () =
        let objectInput = Data.objectOfMap (Map.ofList [ "name", Data.Text "Ada" ])
        let manyInput = Data.List [ Data.Text "Ada" ]

        test <@ Data.tryRedisplay objectInput = None @>
        test <@ Data.redisplay objectInput = "" @>
        test <@ Data.tryRedisplay manyInput = None @>
        test <@ Data.redisplay manyInput = "" @>

    [<Fact>]
    let ``structured data redisplays values by parsed and text paths`` () =
        let input =
            Data.objectOfMap (Map.ofList
                    [ "displayName", Data.Text "Ada"
                      "contacts",
                      Data.List
                          [ Data.objectOfMap (Map.ofList [ "value", Data.Text "ada@example.com" ])
                            Data.objectOfMap (Map.ofList [ "value", Data.Text "+61 400 000 000" ]) ] ]
            )

        let path = DataPath.parse "contacts[1].value"

        test <@ Data.tryRedisplayAt path input = Some "+61 400 000 000" @>
        test <@ Data.redisplayAt path input = "+61 400 000 000" @>
        test <@ Data.tryRedisplayPath "displayName" input = Some "Ada" @>
        test <@ Data.redisplayPath "displayName" input = "Ada" @>

    [<Fact>]
    let ``structured data redisplay returns blank for absent or non scalar paths`` () =
        let input =
            Data.objectOfMap (Map.ofList
                    [ "profile", Data.objectOfMap (Map.ofList [ "name", Data.Text "Ada" ])
                      "contacts", Data.List [ Data.Text "ada@example.com" ] ]
            )

        test <@ Data.tryRedisplayPath "missing" input = Some "" @>
        test <@ Data.redisplayPath "missing" input = "" @>
        test <@ Data.tryRedisplayPath "profile" input = None @>
        test <@ Data.redisplayPath "profile" input = "" @>
        test <@ Data.tryRedisplayPath "contacts" input = None @>
        test <@ Data.redisplayPath "contacts" input = "" @>

    [<Fact>]
    let ``structured data redisplay text paths reject invalid addresses consistently`` () =
        let input = Data.Object []

        test <@ Data.tryRedisplayPath "contacts[]" input = None @>
        raises<FormatException> <@ Data.redisplayPath "contacts[]" input |> ignore @>

    [<Fact>]
    let ``structured data adapts system text json documents and elements`` () =
        use document =
            System.Text.Json.JsonDocument.Parse(
                """{"name":"Ada","age":36,"balance":12.50,"newsletter":true,"nickname":null,"tags":["vip","early"],"address":{"city":"London"}}"""
            )

        let input = Data.ofJsonDocument document

        test <@ Data.lookupPath "name" input = Data.Text "Ada" @>
        test <@ Data.lookupPath "age" input = Data.Number "36" @>
        test <@ Data.lookupPath "balance" input = Data.Number "12.50" @>
        test <@ Data.lookupPath "newsletter" input = Data.Bool true @>
        test <@ Data.lookupPath "nickname" input = Data.Null @>
        test <@ Data.lookupPath "tags[1]" input = Data.Text "early" @>
        test <@ Data.lookupPath "address.city" input = Data.Text "London" @>
        test <@ Data.ofJsonElement (document.RootElement.GetProperty "tags") = Data.List [ Data.Text "vip"; Data.Text "early" ] @>
