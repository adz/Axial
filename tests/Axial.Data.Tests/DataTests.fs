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
