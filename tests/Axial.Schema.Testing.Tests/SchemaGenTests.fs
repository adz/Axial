namespace Axial.Schema.Testing.Tests

open Axial.Schema
open Axial.Schema.Testing
open FsCheck.FSharp
open Swensen.Unquote
open Xunit

module SchemaGenTests =
    type Contact = { Email: string }
    type Kind = Personal | Work
    type Profile = { Age: int; Score: decimal; Active: bool; Contact: Contact; Aliases: string list; Labels: Map<string, string>; Kind: Kind; Note: string option }
    type Category = { Name: string; Children: Category list }

    let private contactSchema () =
        Schema.recordFor<Contact, _> (fun email -> { Email = email })
        |> Schema.field "email" _.Email (Schema.text |> Schema.constrainAll [ Constraint.email ])
        |> Schema.build

    let private profileSchema () =
        let kinds = [ EnumCase.create "personal" Personal; EnumCase.create "work" Work ]
        Schema.recordFor<Profile, _> (fun age score active contact aliases labels kind note ->
            { Age = age; Score = score; Active = active; Contact = contact; Aliases = aliases; Labels = labels; Kind = kind; Note = note })
        |> Schema.field "age" _.Age (Schema.int |> Schema.constrainAll [ Constraint.between 18 90; Constraint.multipleOf 2 ])
        |> Schema.field "score" _.Score (Schema.decimal |> Schema.constrainAll [ Constraint.between 0m 10m ])
        |> Schema.field "active" _.Active Schema.bool
        |> Schema.field "contact" _.Contact (contactSchema ())
        |> Schema.field "aliases" _.Aliases ((Schema.list (Schema.text |> Schema.constrain (Constraint.minLength 2))) |> Schema.constrainAll [ Constraint.countBetween 1 3 ])
        |> Schema.field "labels" _.Labels ((Schema.map Schema.text) |> Schema.constrainAll [ Constraint.maxCount 2 ])
        |> Schema.field "kind" _.Kind (Schema.enum kinds)
        |> Schema.field "note" _.Note (Schema.option Schema.text)
        |> Schema.build

    [<Fact>]
    let ``generated raw inputs satisfy the complete schema`` () =
        let schema = profileSchema ()
        let generator = SchemaGen.raw schema |> Result.defaultWith (failwithf "%A")
        let inputs = Gen.sample 200 generator

        test <@ inputs |> Array.forall (fun input -> (Schema.parse schema input).IsValid) @>

        let models = inputs |> Array.map (fun input -> (Schema.parse schema input).Value)
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
            Schema.recordFor<Contact, _> (fun email -> { Email = email })
            |> Schema.field "email" _.Email (Schema.text |> Schema.constrain (Constraint.pattern "^[A-Z]+$"))
            |> Schema.build

        match SchemaGen.raw schema with
        | Error error -> test <@ error = SchemaGenerationError.UnsupportedConstraint([ "email" ], "pattern") @>
        | Ok _ -> failwith "Expected pattern generation to require a custom generator."

        let custom = Map.ofList [ "email", Gen.constant (RawInput.Scalar "AXIAL") ]
        let generated = SchemaGen.rawWith custom schema |> Result.defaultWith (failwithf "%A") |> Gen.sample 10
        test <@ generated |> Array.forall (fun input -> (Schema.parse schema input).IsValid) @>

    [<Fact>]
    let ``recursive generators terminate at the FsCheck size boundary`` () =
        let rec holder: Lazy<Schema<Category>> =
            lazy
                (Schema.recordFor<Category, _> (fun name children -> { Name = name; Children = children })
                 |> Schema.field "name" _.Name Schema.text
                 |> Schema.field "children" _.Children (Schema.list (Schema.defer (fun () -> holder.Value)))
                 |> Schema.build)

        let schema = holder.Value
        let generator = SchemaGen.raw schema |> Result.defaultWith (failwithf "%A")
        let inputs = Gen.sample 100 generator
        test <@ inputs |> Array.forall (fun input -> (Schema.parse schema input).IsValid) @>
