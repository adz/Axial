namespace Axial.Tests

open Axial.Schema
open Swensen.Unquote
open Xunit
open Axial.Schema.Syntax

/// <summary>
/// Proves that optional value schemas built with <c>Schema.option</c> are portable metadata: the payload stays
/// inspectable, JSON Schema generation leaves optional fields out of <c>required</c>, and the contradictory
/// combinations (nested options, the <c>required</c> constraint) are rejected when the schema is built.
/// </summary>
module SchemaOptionalValueTests =
    type private Profile =
        { Name: string
          Nickname: string option }

    let private profileSchema () =
        Schema.define<Profile>
        |> fieldWith Schema.text "name" _.Name
        |> fieldWith (Schema.option (Schema.text |> Schema.constrain (Constraint.minLength 2))) "nickname" _.Nickname
        |> construct (fun name nickname -> { Name = name; Nickname = nickname })

    [<Fact>]
    let ``optionOf describes an optional shape carrying the payload description`` () =
        let description =
            Schema.option (Schema.text |> Schema.constrain (Constraint.maxLength 10))
            |> Inspect.schema

        match description.Shape with
        | SchemaShape.Optional payload ->
            test <@ payload.Shape = SchemaShape.Primitive PrimitiveValueKind.Text @>
            test <@ payload.Constraints |> List.map Constraint.code = [ "maxLength" ] @>
        | _ -> failwith "Expected an optional value shape."

    [<Fact>]
    let ``optionOf field getter reads the option from an already trusted model`` () =
        let description = Inspect.model (profileSchema ())
        let nickname = description.Fields |> List.find (fun field -> field.Name = "nickname")

        match nickname.Schema.Shape with
        | SchemaShape.Optional _ -> ()
        | _ -> failwith "Expected the nickname field to describe an optional value."

    [<Fact>]
    let ``json schema generation drops optional fields out of required`` () =
        let generated = JsonSchema.generate (profileSchema ())

        test <@ generated.Contains "\"required\":[\"name\"]" @>
        test <@ generated.Contains "\"nickname\":{\"type\":\"string\",\"minLength\":2}" @>

    [<Fact>]
    let ``optionOf rejects a nested optional payload`` () =
        raises<System.ArgumentException> <@ Schema.option (Schema.option Schema.text) @>

    [<Fact>]
    let ``optionOf rejects a payload carrying the required constraint`` () =
        raises<System.ArgumentException>
            <@ Schema.option (Schema.text |> Schema.constrain Constraint.required) @>

    [<Fact>]
    let ``withConstraint rejects required on an optional value schema`` () =
        raises<System.ArgumentException>
            <@ Schema.option Schema.text |> Schema.constrain Constraint.required @>

    [<Fact>]
    let ``build rejects an optional field carrying the required field constraint`` () =
        raises<System.ArgumentException>
            <@ Schema.define<Profile>
               |> fieldWith Schema.text "name" _.Name
               |> fieldWith ((Schema.option Schema.text) |> Schema.constrainAll [ Constraint.required ]) "nickname" _.Nickname
               |> construct (fun name nickname -> { Name = name; Nickname = nickname })@>
