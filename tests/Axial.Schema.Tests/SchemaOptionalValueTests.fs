namespace Axial.Tests

open Axial.Schema
open Swensen.Unquote
open Xunit

/// <summary>
/// Proves that optional value schemas built with <c>Value.optionOf</c> are portable metadata: the payload stays
/// inspectable, JSON Schema generation leaves optional fields out of <c>required</c>, and the contradictory
/// combinations (nested options, the <c>required</c> constraint) are rejected when the schema is built.
/// </summary>
module SchemaOptionalValueTests =
    type private Profile =
        { Name: string
          Nickname: string option }

    let private profileSchema () =
        Schema.recordFor<Profile, _> (fun name nickname -> { Name = name; Nickname = nickname })
        |> Schema.text "name" _.Name
        |> Schema.field
            "nickname"
            _.Nickname
            (Value.optionOf (Value.text |> Value.withConstraint (SchemaConstraint.minLength 2)))
        |> Schema.build

    [<Fact>]
    let ``optionOf describes an optional shape carrying the payload description`` () =
        let description =
            Value.optionOf (Value.text |> Value.withConstraint (SchemaConstraint.maxLength 10))
            |> Inspect.value

        match description.Shape with
        | ValueShape.Optional payload ->
            test <@ payload.Shape = ValueShape.Primitive PrimitiveValueKind.Text @>
            test <@ payload.Constraints |> List.map SchemaConstraint.code = [ "maxLength" ] @>
        | _ -> failwith "Expected an optional value shape."

    [<Fact>]
    let ``optionOf field getter reads the option from an already trusted model`` () =
        let description = Inspect.model (profileSchema ())
        let nickname = description.Fields |> List.find (fun field -> field.Name = "nickname")

        match nickname.Value.Shape with
        | ValueShape.Optional _ -> ()
        | _ -> failwith "Expected the nickname field to describe an optional value."

    [<Fact>]
    let ``json schema generation drops optional fields out of required`` () =
        let generated = JsonSchema.generate (profileSchema ())

        test <@ generated.Contains "\"required\":[\"name\"]" @>
        test <@ generated.Contains "\"nickname\":{\"type\":\"string\",\"minLength\":2}" @>

    [<Fact>]
    let ``optionOf rejects a nested optional payload`` () =
        raises<System.ArgumentException> <@ Value.optionOf (Value.optionOf Value.text) @>

    [<Fact>]
    let ``optionOf rejects a payload carrying the required constraint`` () =
        raises<System.ArgumentException>
            <@ Value.optionOf (Value.text |> Value.withConstraint SchemaConstraint.required) @>

    [<Fact>]
    let ``withConstraint rejects required on an optional value schema`` () =
        raises<System.ArgumentException>
            <@ Value.optionOf Value.text |> Value.withConstraint SchemaConstraint.required @>

    [<Fact>]
    let ``build rejects an optional field carrying the required field constraint`` () =
        raises<System.ArgumentException>
            <@ Schema.recordFor<Profile, _> (fun name nickname -> { Name = name; Nickname = nickname })
               |> Schema.text "name" _.Name
               |> Schema.fieldWith [ SchemaConstraint.required ] "nickname" _.Nickname (Value.optionOf Value.text)
               |> Schema.build @>
