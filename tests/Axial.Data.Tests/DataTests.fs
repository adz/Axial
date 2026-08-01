namespace Axial.Tests

open System
open Axial
open Axial.Data.Syntax
open Swensen.Unquote
open Xunit

module DataTests =
    [<Fact>]
    let ``data cases directly represent recursive structured values`` () =
        let value =
            Data.Object
                [ "name", Data.Text "Ada"
                  "active", Data.Bool true
                  "scores", Data.List [ Data.Number "10"; Data.Number "20" ] ]

        match value with
        | Data.Object fields ->
            test <@ fields[0] = ("name", Data.Text "Ada") @>
            test <@ fields[1] = ("active", Data.Bool true) @>
            test <@ fields[2] = ("scores", Data.List [ Data.Number "10"; Data.Number "20" ]) @>
        | _ -> failwith "Expected object data."

    [<Fact>]
    let ``data syntax converts supported primitive values`` () =
        let identifier = Guid.Parse "00112233-4455-6677-8899-aabbccddeeff"
        let occurredAt = DateTimeOffset(2026, 7, 19, 8, 30, 0, TimeSpan.FromHours 9.5)

        let value =
            data
                [ "name" => "Ada"
                  "age" => 42
                  "visits" => 42L
                  "balance" => 19.95m
                  "ratio" => 1.5
                  "active" => true
                  "id" => identifier
                  "occurredAt" => occurredAt ]

        test
            <@
                value =
                    Data.Object
                        [ "name", Data.Text "Ada"
                          "age", Data.Number "42"
                          "visits", Data.Number "42"
                          "balance", Data.Number "19.95"
                          "ratio", Data.Number "1.5"
                          "active", Data.Bool true
                          "id", Data.Text "00112233-4455-6677-8899-aabbccddeeff"
                          "occurredAt", Data.Text "2026-07-19T08:30:00.0000000+09:30" ]
            @>

    [<Fact>]
    let ``data syntax recursively converts lists and nested objects`` () =
        let value =
            data
                [ "name" => "Ada"
                  "matrix" => [ [ 1; 2 ]; [ 3; 4 ] ]
                  "contacts" =>
                      [ data [ "kind" => "email"; "value" => "ada@example.com" ]
                        data [ "kind" => "phone"; "value" => "+61 400 000 000" ] ] ]

        test
            <@
                value =
                    Data.Object
                        [ "name", Data.Text "Ada"
                          "matrix",
                          Data.List
                              [ Data.List [ Data.Number "1"; Data.Number "2" ]
                                Data.List [ Data.Number "3"; Data.Number "4" ] ]
                          "contacts",
                          Data.List
                              [ Data.Object [ "kind", Data.Text "email"; "value", Data.Text "ada@example.com" ]
                                Data.Object [ "kind", Data.Text "phone"; "value", Data.Text "+61 400 000 000" ] ] ]
            @>

    [<Fact>]
    let ``data syntax maps null strings and null lists to null`` () =
        let text: string = null
        let values = Unchecked.defaultof<string list>

        let value = data [ "text" => text; "values" => values; "explicit" => Data.Null ]

        test <@ value = Data.Object [ "text", Data.Null; "values", Data.Null; "explicit", Data.Null ] @>

    [<Fact>]
    let ``data syntax preserves object field order and duplicate names`` () =
        let value = data [ "name" => "Grace"; "name" => "Ada" ]

        test <@ value = Data.Object [ "name", Data.Text "Grace"; "name", Data.Text "Ada" ] @>

    [<Fact>]
    let ``data syntax formats dates as ISO text`` () =
        let value = data [ "date" => DateOnly(2026, 7, 19) ]

        test <@ value = Data.Object [ "date", Data.Text "2026-07-19" ] @>

    [<Fact>]
    let ``nested object literals do not repeat data`` () =
        let value =
            data [
                "name" => "Ada"
                "address" => [
                    "city" => "Adelaide"
                    "postcode" => 5000
                ]
                "contacts" => [
                    [ "kind" => "email"; "value" => "ada@example.com" ]
                    [ "kind" => "phone"; "value" => "+61 400 000 000" ]
                ]
            ]

        test
            <@
                value =
                    Data.Object [
                        "name", Data.Text "Ada"
                        "address", Data.Object [ "city", Data.Text "Adelaide"; "postcode", Data.Number "5000" ]
                        "contacts", Data.List [
                            Data.Object [ "kind", Data.Text "email"; "value", Data.Text "ada@example.com" ]
                            Data.Object [ "kind", Data.Text "phone"; "value", Data.Text "+61 400 000 000" ]
                        ]
                    ]
            @>

    [<Fact>]
    let ``optional fields spreads and dynamic list expressions preserve order`` () =
        let nickname: string option = None
        let common = data [ "kind" => "example"; "shared" => true ]
        let names = [ "ada"; "grace" ]

        let value =
            data [
                yield! fields common
                "nickname" ?=> nickname
                "deletedAt" => nil

                for name in names do
                    $"user-{name}" => name
            ]

        test
            <@
                value =
                    Data.Object [
                        "kind", Data.Text "example"
                        "shared", Data.Bool true
                        "deletedAt", Data.Null
                        "user-ada", Data.Text "ada"
                        "user-grace", Data.Text "grace"
                    ]
            @>

    [<Fact>]
    let ``exact number syntax validates and preserves lexical tokens`` () =
        test <@ num "1.234567890123456789e+400" = Data.Number "1.234567890123456789e+400" @>
        raises<ArgumentException> <@ num "01" @>
        raises<ArgumentException> <@ num "NaN" @>

    [<Fact>]
    let ``path lookup supports roots indexes quoted names and last duplicate`` () =
        let value =
            Data.Object [
                "name", Data.Text "Grace"
                "name", Data.Text "Ada"
                "metadata", Data.Object [ "build.version", Data.Text "1.2.0" ]
                "items", Data.List [ Data.Text "first" ]
            ]

        test <@ Data.tryFindPath "" value = Some value @>
        test <@ Data.tryFindPath "name" value = Some(Data.Text "Ada") @>
        test <@ Data.tryFindPath "metadata[\"build.version\"]" value = Some(Data.Text "1.2.0") @>
        test <@ Data.tryFindPath "items[0]" value = Some(Data.Text "first") @>
        test <@ Data.tryFindPath "items[1]" value = None @>
        test <@ Data.tryFindPath "items.nope" value = None @>

    [<Fact>]
    let ``patch applies every edit in order without mutating the baseline`` () =
        let baseline =
            data [
                "name" => "Ada"
                "address" => [ "city" => "Adelaide"; "postcode" => 5000 ]
                "roles" => [ "author" ]
                "obsolete" => true
            ]

        let changed =
            baseline
            |> patch [
                set "address.postcode" 5001
                put "plan" "pro"
                append "roles" "billing"
                prepend "roles" "admin"
                insert "roles" 1 "owner"
                rename "address.city" "suburb"
                update "name" (function Data.Text name -> Data.Text(name.ToUpperInvariant()) | value -> value)
                remove "obsolete"
            ]

        test <@ Data.tryFindPath "address.postcode" changed = Some(Data.Number "5001") @>
        test <@ Data.tryFindPath "address.suburb" changed = Some(Data.Text "Adelaide") @>
        test <@ Data.tryFindPath "roles" changed = Some(Data.List [ Data.Text "admin"; Data.Text "owner"; Data.Text "author"; Data.Text "billing" ]) @>
        test <@ Data.tryFindPath "plan" changed = Some(Data.Text "pro") @>
        test <@ Data.tryFindPath "name" changed = Some(Data.Text "ADA") @>
        test <@ Data.tryFindPath "obsolete" changed = None @>
        test <@ Data.tryFindPath "address.postcode" baseline = Some(Data.Number "5000") @>

    [<Fact>]
    let ``failed multi edit patches are atomic and structured`` () =
        let baseline = data [ "name" => "Ada"; "roles" => [ "author" ] ]
        let edits = [ set "name" "Grace"; append "name" "invalid"; set "roles[0]" "admin" ]

        match Data.tryPatch edits baseline with
        | Ok _ -> failwith "Expected patch failure."
        | Error [ failure ] ->
            test <@ failure.EditIndex = 1 @>
            test <@ failure.Path = "name" @>
            test <@ failure.Message.Contains "Expected a list" @>
        | Error failures -> failwith $"Expected one failure, got {failures.Length}."

        test <@ Data.tryFindPath "name" baseline = Some(Data.Text "Ada") @>

    [<Fact>]
    let ``remove set and rename select the last duplicate field`` () =
        let baseline = data [ "name" => "Grace"; "name" => "Ada" ]
        let setResult = baseline |> patch [ set "name" "Margaret" ]
        let renameResult = baseline |> patch [ rename "name" "preferredName" ]
        let removeResult = baseline |> patch [ remove "name" ]
        let expectedSet = data [ "name" => "Grace"; "name" => "Margaret" ]
        let expectedRename = data [ "name" => "Grace"; "preferredName" => "Ada" ]
        let expectedRemove = data [ "name" => "Grace" ]

        test <@ setResult = expectedSet @>
        test <@ renameResult = expectedRename @>
        test <@ removeResult = expectedRemove @>

    [<Fact>]
    let ``variations and matrices preserve declaration order and names`` () =
        let baseline = data [ "plan" => "free"; "region" => "au" ]

        let independent =
            baseline
            |> variants [
                variant "unchanged" []
                variant "pro" [ set "plan" "pro" ]
            ]

        test <@ independent |> List.map _.Name = [ "unchanged"; "pro" ] @>

        let cases =
            baseline
            |> matrix [
                dimension "plan" [ variant "free" []; variant "pro" [ set "plan" "pro" ] ]
                dimension "region" [ variant "AU" []; variant "US" [ set "region" "us" ] ]
            ]

        test
            <@
                cases |> List.map _.Name =
                    [ "plan: free / region: AU"
                      "plan: free / region: US"
                      "plan: pro / region: AU"
                      "plan: pro / region: US" ]
            @>

    [<Fact>]
    let ``matrices reject oversized products before materializing`` () =
        let choices = [ for index in 1..17 -> variant (string index) [] ]
        raises<ArgumentException> <@ data [] |> matrix [ dimension "a" choices; dimension "b" choices ] @>

    [<Fact>]
    let ``exact comparison reports focused lexical order and shape differences`` () =
        let expected = data [ "number" => num "1.0"; "items" => [ 1; 2 ] ]
        let actual = data [ "number" => num "1"; "items" => [ 1; 3 ]; "extra" => true ]
        let differences = Data.diff expected actual

        test <@ differences.Length = 3 @>
        test <@ differences[0].Path = DataPath.parse "number" @>
        test <@ differences[1].Path = DataPath.parse "items[1]" @>
        test <@ differences[2].Cause = DataDifferenceCause.Unexpected @>
        test <@ Data.compare expected expected = Ok() @>

    [<Fact>]
    let ``sparse and recursive matching accumulate produced data mismatches`` () =
        let response =
            data [
                "customer" => [
                    "name" => "Ada"
                    "plan" => "pro"
                    "address" => [ "city" => "Adelaide"; "postcode" => 5000 ]
                ]
                "items" => [
                    [ "sku" => "XYZ"; "quantity" => 1 ]
                    [ "sku" => "ABC"; "quantity" => 2 ]
                ]
                "events" => [
                    [ "id" => "e1"; "createdAt" => "now" ]
                    [ "id" => "e2"; "createdAt" => "later" ]
                ]
            ]

        response
        |> matching [
            at "customer.name" "Ada"
            absent "error"
            at "customer" (containing [
                "plan" => "pro"
                "address" => containing [ "postcode" => 5000 ]
            ])
            at "items" (containingItems [ containing [ "sku" => "ABC"; "quantity" => 2 ] ])
            at "events" (allItems (containing [ "id" => anyText; "createdAt" => anyText ]))
        ]

        match Data.tryMatch [ at "customer.name" "Grace"; at "customer.missing" any ] response with
        | Ok() -> failwith "Expected mismatches."
        | Error mismatches ->
            test <@ mismatches.Length = 2 @>
            test <@ mismatches[0].Path = DataPath.parse "customer.name" @>
            test <@ mismatches[1].Path = DataPath.parse "customer.missing" @>

    [<Fact>]
    let ``list patterns distinguish unordered consumed and ordered semantics`` () =
        let actual = data [ "values" => [ 3; 1; 2; 1 ] ]

        actual |> matching [ at "values" (containingItems [ 1; 1; 3 ]) ]
        actual |> matching [ at "values" (inOrder [ 3; 2 ]) ]
        actual |> matching [ at "values" (someItem (satisfying "an even number" (fun value -> value = Data.Number "2"))) ]

        let insufficientDuplicates = Data.tryMatch [ at "values" (containingItems [ 1; 1; 1 ]) ] actual
        let wrongOrder = Data.tryMatch [ at "values" (inOrder [ 2; 3 ]) ] actual
        test <@ insufficientDuplicates |> Result.isError @>
        test <@ wrongOrder |> Result.isError @>

    [<Fact>]
    let ``partial matching consumes duplicates and backtracks overlapping candidates`` () =
        let actual =
            data [
                "name" => "Grace"
                "name" => "Ada"
                "items" => [
                    [ "x" => 1; "y" => 2 ]
                    [ "x" => 1 ]
                ]
            ]

        actual
        |> matching [
            at "" (containing [ "name" => anyText; "name" => "Ada" ])
            at "items" (containingItems [
                containing [ "x" => 1 ]
                containing [ "x" => 1; "y" => 2 ]
            ])
        ]

    [<Fact>]
    let ``JSON parse render and extraction preserve owned structure`` () =
        let value = Data.Json.parse "{\"n\":1.20e+3,\"n\":2,\"text\":\"Ada\",\"items\":[true,null]}"

        test <@ Data.tryNumberToken (Data.lookupPath "n" value) = Some "2" @>
        test <@ Data.tryText (Data.lookupPath "text" value) = Some "Ada" @>
        test <@ Data.render value = "{\"n\":1.20e+3,\"n\":2,\"text\":\"Ada\",\"items\":[true,null]}" @>
        test <@ Data.Json.parse (Data.render value) = value @>
        test <@ Data.renderIndented value |> fun rendered -> rendered.Contains(Environment.NewLine) || rendered.Contains("\n") @>
