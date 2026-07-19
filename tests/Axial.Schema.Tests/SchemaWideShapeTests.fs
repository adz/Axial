namespace Axial.Tests

open Axial
open Axial.Schema
open Axial.Schema.Syntax
open Axial.Validation
open Swensen.Unquote
open Xunit

open type Axial.Schema.Syntax

/// Specs for the unbounded constructor-last chain: field counts beyond the old twelve-field arity cap,
/// and the bare-getter field form (`field _.Name`) with derived camelCase wire names.
module SchemaWideShapeTests =

    type private Wide =
        { F01: int
          F02: int
          F03: int
          F04: int
          F05: int
          F06: int
          F07: int
          F08: int
          F09: int
          F10: int
          F11: int
          F12: int
          F13: string
          F14: string
          F15: bool }

        static member Create a b c d e f g h i j k l m n o =
            { F01 = a
              F02 = b
              F03 = c
              F04 = d
              F05 = e
              F06 = f
              F07 = g
              F08 = h
              F09 = i
              F10 = j
              F11 = k
              F12 = l
              F13 = m
              F14 = n
              F15 = o }

    let private wideSchema =
        Schema.define<Wide>
        |> field "f01" _.F01
        |> field "f02" _.F02
        |> field "f03" _.F03
        |> field "f04" _.F04
        |> field "f05" _.F05
        |> field "f06" _.F06
        |> field "f07" _.F07
        |> field "f08" _.F08
        |> field "f09" _.F09
        |> field "f10" _.F10
        |> field "f11" _.F11
        |> field "f12" _.F12
        |> field "f13" _.F13
        |> field "f14" _.F14
        |> field "f15" _.F15
        |> construct Wide.Create

    [<Fact>]
    let ``fifteen fields close with a single construct call`` () =
        let description = Inspect.model wideSchema
        test <@ description.Fields |> List.length = 15 @>

        test
            <@
                description.Fields |> List.map _.Name = [ "f01"
                                                          "f02"
                                                          "f03"
                                                          "f04"
                                                          "f05"
                                                          "f06"
                                                          "f07"
                                                          "f08"
                                                          "f09"
                                                          "f10"
                                                          "f11"
                                                          "f12"
                                                          "f13"
                                                          "f14"
                                                          "f15" ]
            @>

    [<Fact>]
    let ``a fifteen-field schema parses and checks round trip`` () =
        let expected =
            { F01 = 1
              F02 = 2
              F03 = 3
              F04 = 4
              F05 = 5
              F06 = 6
              F07 = 7
              F08 = 8
              F09 = 9
              F10 = 10
              F11 = 11
              F12 = 12
              F13 = "m"
              F14 = "n"
              F15 = true }

        let input =
            Data.ofMap (
                Map.ofList
                    [ "f01", "1"
                      "f02", "2"
                      "f03", "3"
                      "f04", "4"
                      "f05", "5"
                      "f06", "6"
                      "f07", "7"
                      "f08", "8"
                      "f09", "9"
                      "f10", "10"
                      "f11", "11"
                      "f12", "12"
                      "f13", "m"
                      "f14", "n"
                      "f15", "true" ]
            )

        test <@ Schema.parse wideSchema input = Ok expected @>
        test <@ Schema.check wideSchema expected = Ok expected @>

    // ---- bare-getter fields: `open type Axial.Schema.Syntax` adds the overloaded `field` ----

    type private Contact =
        { Name: string
          Age: int
          Tags: string list }

        static member Create name age tags = { Name = name; Age = age; Tags = tags }

    let private contactSchema =
        Schema.define<Contact>
        |> field _.Name
        |> constrain (minLength 1)
        |> field _.Age
        |> field _.Tags
        |> construct Contact.Create

    [<Fact>]
    let ``bare getters derive camelCased wire names`` () =
        let description = Inspect.model contactSchema
        test <@ description.Fields |> List.map _.Name = [ "name"; "age"; "tags" ] @>

    [<Fact>]
    let ``bare getter fields parse like named fields`` () =
        let input =
            Data.objectOfMap (
                Map.ofList
                    [ "name", Data.Text "Ada"
                      "age", Data.Text "36"
                      "tags", Data.List [ Data.Text "fsharp" ] ]
            )

        match Schema.parse contactSchema input with
        | Ok contact ->
            test <@ contact.Name = "Ada" @>
            test <@ contact.Age = 36 @>
            test <@ contact.Tags = [ "fsharp" ] @>
        | Error errors -> failwithf "Expected a parse, got %A" errors

    [<Fact>]
    let ``a constraint after a bare getter attaches to that field`` () =
        let input =
            Data.objectOfMap (
                Map.ofList
                    [ "name", Data.Text ""
                      "age", Data.Text "36"
                      "tags", Data.List [] ]
            )

        match Schema.parse contactSchema input with
        | Ok contact -> failwithf "Expected a minLength diagnostic, parsed %A" contact
        | Error errors ->
            let flattened = Diagnostics.flatten errors
            test <@ flattened |> List.exists (fun diagnostic -> diagnostic.Path = [ PathSegment.Name "name" ]) @>

    [<Fact>]
    let ``the named field form still works under open type`` () =
        // `open type` must not shadow away the named spelling: both live on the same overloaded member.
        let schema =
            Schema.define<Contact>
            |> field "fullName" _.Name
            |> field _.Age
            |> field _.Tags
            |> construct Contact.Create

        let description = Inspect.model schema
        test <@ description.Fields |> List.map _.Name = [ "fullName"; "age"; "tags" ] @>
