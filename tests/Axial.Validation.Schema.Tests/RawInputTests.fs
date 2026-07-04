namespace Axial.Tests

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
