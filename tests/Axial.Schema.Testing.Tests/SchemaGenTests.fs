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
        |> Schema.fieldWith [ SchemaConstraint.email ] "email" _.Email Value.text
        |> Schema.build

    let private profileSchema () =
        let kinds = [ EnumCase.create "personal" Personal; EnumCase.create "work" Work ]
        Schema.recordFor<Profile, _> (fun age score active contact aliases labels kind note ->
            { Age = age; Score = score; Active = active; Contact = contact; Aliases = aliases; Labels = labels; Kind = kind; Note = note })
        |> Schema.fieldWith [ SchemaConstraint.between 18 90; SchemaConstraint.multipleOf 2 ] "age" _.Age Value.int
        |> Schema.fieldWith [ SchemaConstraint.between 0m 10m ] "score" _.Score Value.decimal
        |> Schema.bool "active" _.Active
        |> Schema.nested "contact" _.Contact (contactSchema ())
        |> Schema.fieldWith [ SchemaConstraint.countBetween 1 3 ] "aliases" _.Aliases (Value.manyOf (Value.text |> Value.withConstraint (SchemaConstraint.minLength 2)))
        |> Schema.fieldWith [ SchemaConstraint.maxCount 2 ] "labels" _.Labels (Value.map Value.text)
        |> Schema.field "kind" _.Kind (Value.enumOf kinds)
        |> Schema.field "note" _.Note (Value.optionOf Value.text)
        |> Schema.build

    [<Fact>]
    let ``generated raw inputs satisfy the complete schema`` () =
        let schema = profileSchema ()
        let generator = SchemaGen.raw schema |> Result.defaultWith (failwithf "%A")
        let inputs = Gen.sample 200 generator

        test <@ inputs |> Array.forall (fun input -> (Model.parse schema input).IsValid) @>

        let models = inputs |> Array.map (fun input -> (Model.parse schema input).Model)
        test <@ models |> Array.forall (fun model -> model.Age >= 18 && model.Age <= 90 && model.Age % 2 = 0) @>
        test <@ models |> Array.forall (fun model -> model.Aliases.Length >= 1 && model.Aliases.Length <= 3) @>

    [<Fact>]
    let ``trusted-model generator returns only validated values`` () =
        let generator = SchemaGen.model (profileSchema ()) |> Result.defaultWith (failwithf "%A")
        let models = Gen.sample 100 generator
        test <@ models |> Array.forall (fun (model: Model<Profile>) -> model.Value.Contact.Email.Contains "@") @>

    [<Fact>]
    let ``pattern constraints require a caller-owned generator`` () =
        let schema =
            Schema.recordFor<Contact, _> (fun email -> { Email = email })
            |> Schema.fieldWith [ SchemaConstraint.pattern "^[A-Z]+$" ] "email" _.Email Value.text
            |> Schema.build

        match SchemaGen.raw schema with
        | Error error -> test <@ error = SchemaGenerationError.UnsupportedConstraint([ "email" ], "pattern") @>
        | Ok _ -> failwith "Expected pattern generation to require a custom generator."

        let custom = Map.ofList [ "email", Gen.constant (RawInput.Scalar "AXIAL") ]
        let generated = SchemaGen.rawWith custom schema |> Result.defaultWith (failwithf "%A") |> Gen.sample 10
        test <@ generated |> Array.forall (fun input -> (Model.parse schema input).IsValid) @>

    [<Fact>]
    let ``recursive generators terminate at the FsCheck size boundary`` () =
        let rec holder: Lazy<Schema<Category>> =
            lazy
                (Schema.recordFor<Category, _> (fun name children -> { Name = name; Children = children })
                 |> Schema.text "name" _.Name
                 |> Schema.field "children" _.Children (Value.manyOf (Value.lazyOf (fun () -> holder.Value)))
                 |> Schema.build)

        let schema = holder.Value
        let generator = SchemaGen.raw schema |> Result.defaultWith (failwithf "%A")
        let inputs = Gen.sample 100 generator
        test <@ inputs |> Array.forall (fun input -> (Model.parse schema input).IsValid) @>
