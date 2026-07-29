namespace Axial.Tests

open Axial.Schema
open Swensen.Unquote
open Xunit
open Axial.Schema.Syntax

/// <summary>
/// Proves that optional value schemas built with <c>Schema.option</c> are portable metadata: the payload stays
/// inspectable, JSON Schema generation leaves optional fields out of <c>required</c>, and supply and value
/// presence remain independently expressible.
/// </summary>
module SchemaOptionalValueTests =
    type private Profile =
        { Name: string
          Nickname: string option }

    let private profileSchema () =
        schema<Profile> {
            field "name" _.Name
            field "nickname" _.Nickname {
                withSchema (Schema.option (Schema.text |> Schema.constrain (Constraint.minLength 2)))
            }
            construct (fun name nickname -> { Name = name; Nickname = nickname })
        }

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
    let ``option payload may require present text without requiring Some`` () =
        let schema = Schema.option (Schema.text |> Schema.constrain Constraint.present)
        let description = Inspect.schema schema

        match description.Shape with
        | SchemaShape.Optional payload -> test <@ payload.Constraints |> List.map Constraint.code = [ "present" ] @>
        | _ -> failwith "Expected an optional value shape."

    [<Fact>]
    let ``present may constrain the option itself`` () =
        let presentOption: SchemaConstraint<string option> = Constraint.present
        let constrained = Schema.option Schema.text |> Schema.constrain presentOption
        test <@ Schema.constraints constrained |> List.map Constraint.code = [ "present" ] @>

    [<Fact>]
    let ``present option makes the field required in JSON Schema`` () =
        let presentOption: SchemaConstraint<string option> = Constraint.present
        let constrained =
            schema<Profile> {
                field "name" _.Name
                field "nickname" _.Nickname {
                    withSchema (Schema.option Schema.text |> Schema.constrain presentOption)
                }
                construct (fun name nickname -> { Name = name; Nickname = nickname })
            }

        let generated = JsonSchema.generate constrained
        test <@ generated.Contains "\"required\":[\"name\",\"nickname\"]" @>

    [<Fact>]
    let ``supplied option rejects omission independently of its content`` () =
        let suppliedOption: SchemaConstraint<string option> = Constraint.supplied
        let constrained =
            schema<Profile> {
                field "name" _.Name
                field "nickname" _.Nickname {
                    withSchema (Schema.option Schema.text |> Schema.constrain suppliedOption)
                }
                construct (fun name nickname -> { Name = name; Nickname = nickname })
            }

        let omitted = Axial.Data.Object [ "name", Axial.Data.Text "Ada" ]
        let explicitNull = Axial.Data.Object [ "name", Axial.Data.Text "Ada"; "nickname", Axial.Data.Null ]

        test <@ Schema.parse constrained omitted |> Result.isError @>
        test <@ Schema.parse constrained explicitNull = Ok { Name = "Ada"; Nickname = None } @>
