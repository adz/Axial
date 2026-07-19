namespace Axial.Schema.Testing.Tests

open Axial

open Axial.Schema
open Axial.Schema.Testing
open FsCheck.FSharp
open Swensen.Unquote
open Xunit
open Axial.Schema.Syntax

module SchemaGenTests =
    type Contact = { Email: string }
    type Kind = Personal | Work
    type Profile = { Age: int; Score: decimal; Active: bool; Contact: Contact; Aliases: string list; Labels: Map<string, string>; Kind: Kind; Note: string option }
    type Category = { Name: string; Children: Category list }

    let private contactSchema () =
        Schema.define<Contact>
        |> fieldWith (Schema.text |> Schema.constrainAll [ Constraint.email ]) "email" _.Email
        |> construct (fun email -> { Email = email })

    let private profileSchema () =
        let kinds = [ EnumCase.create "personal" Personal; EnumCase.create "work" Work ]
        Schema.define<Profile>
        |> fieldWith (Schema.int |> Schema.constrainAll [ Constraint.between 18 90; Constraint.multipleOf 2 ]) "age" _.Age
        |> fieldWith (Schema.decimal |> Schema.constrainAll [ Constraint.between 0m 10m ]) "score" _.Score
        |> fieldWith Schema.bool "active" _.Active
        |> fieldWith (contactSchema ()) "contact" _.Contact
        |> fieldWith ((Schema.listWith (Schema.text |> Schema.constrain (Constraint.minLength 2))) |> Schema.constrainAll [ Constraint.countBetween 1 3 ]) "aliases" _.Aliases
        |> fieldWith ((Schema.mapWith Schema.text) |> Schema.constrainAll [ Constraint.maxCount 2 ]) "labels" _.Labels
        |> fieldWith (Schema.enum kinds) "kind" _.Kind
        |> fieldWith (Schema.option Schema.text) "note" _.Note
        |> construct (fun age score active contact aliases labels kind note ->
            { Age = age; Score = score; Active = active; Contact = contact; Aliases = aliases; Labels = labels; Kind = kind; Note = note })

    [<Fact>]
    let ``generated structured datas satisfy the complete schema`` () =
        let schema = profileSchema ()
        let generator = SchemaGen.raw schema |> Result.defaultWith (failwithf "%A")
        let inputs = Gen.sample 200 generator

        test <@ inputs |> Array.forall (fun input -> (Schema.parse schema input |> Result.isOk)) @>

        let models = inputs |> Array.map (fun input -> Schema.parse schema input |> Result.defaultWith (failwithf "%A"))
        test <@ models |> Array.forall (fun model -> model.Age >= 18 && model.Age <= 90 && model.Age % 2 = 0) @>
        test <@ models |> Array.forall (fun model -> model.Aliases.Length >= 1 && model.Aliases.Length <= 3) @>

    [<Fact>]
    let ``trusted-model generator returns only validated values`` () =
        let generator = SchemaGen.model (profileSchema ()) |> Result.defaultWith (failwithf "%A")
        let models = Gen.sample 100 generator
        test <@ models |> Array.forall (fun (model: Profile) -> model.Contact.Email.Contains "@") @>

    [<Fact>]
    let ``pattern constraints require a caller-owned generator`` () =
        let schema =
            Schema.define<Contact>
            |> fieldWith (Schema.text |> Schema.constrain (Constraint.pattern "^[A-Z]+$")) "email" _.Email
            |> construct (fun email -> { Email = email })

        match SchemaGen.raw schema with
        | Error error -> test <@ error = SchemaGenerationError.UnsupportedConstraint([ "email" ], "pattern") @>
        | Ok _ -> failwith "Expected pattern generation to require a custom generator."

        let custom = Map.ofList [ "email", Gen.constant (Data.Text "AXIAL") ]
        let generated = SchemaGen.rawWith custom schema |> Result.defaultWith (failwithf "%A") |> Gen.sample 10
        test <@ generated |> Array.forall (fun input -> (Schema.parse schema input |> Result.isOk)) @>

    [<Fact>]
    let ``recursive generators terminate at the FsCheck size boundary`` () =
        let rec holder: Lazy<Schema<Category>> =
            lazy
                (Schema.define<Category>
                 |> fieldWith Schema.text "name" _.Name
                 |> fieldWith (Schema.listWith (Schema.defer (fun () -> holder.Value))) "children" _.Children
                 |> construct (fun name children -> { Name = name; Children = children }))

        let schema = holder.Value
        let generator = SchemaGen.raw schema |> Result.defaultWith (failwithf "%A")
        let inputs = Gen.sample 100 generator
        test <@ inputs |> Array.forall (fun input -> (Schema.parse schema input |> Result.isOk)) @>
