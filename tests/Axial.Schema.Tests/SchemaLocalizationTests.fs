namespace Axial.Tests

open Axial.Data
open Axial.Constraint
open Axial.Schema
open Axial.Schema.Syntax
open Swensen.Unquote
open Xunit

/// <summary>
/// Schema's side of the rendering edge: the typed path becomes the attribute, indexes stay out of resource keys
/// and stay in results, and Schema's own catalogue rides the generic renderer.
/// </summary>
module SchemaLocalizationTests =
    type private Address = { Postcode: string }

    type private Signup =
        { Name: string
          Age: int
          Addresses: Address list }

    let private addressSchema () =
        schema<Address> {
            field "postcode" _.Postcode {
                withSchema (Schema.text |> Schema.constrain (Constraint.lengthBetween 2 40))
            }

            construct (fun postcode -> { Postcode = postcode })
        }

    let private signupSchema () =
        schema<Signup> {
            field "name" _.Name {
                withSchema (Schema.text |> Schema.constrain Constraint.present)
            }

            field "age" _.Age
            field "addresses" _.Addresses { withSchema (Schema.listWith (addressSchema ())) }
            construct (fun name age addresses -> { Name = name; Age = age; Addresses = addresses })
        }

    let private errorsFrom raw =
        match Schema.parse (signupSchema ()) raw with
        | Error errors -> errors
        | Ok _ -> failwith "Expected a failed parse."

    let private input name age postcodes =
        Data.objectOfMap (
            Map.ofList
                [ "name", name
                  "age", age
                  "addresses",
                  Data.List [ for postcode in postcodes -> Data.objectOfMap (Map.ofList [ "postcode", postcode ]) ] ]
        )

    let private lookupOf pairs =
        let table = Map.ofList pairs
        Renderer.ofLookup table.TryFind

    [<Fact>]
    let ``messages return predicates paired with the path that names the field`` () =
        let errors = errorsFrom (input Data.Null (Data.Text "42") [ Data.Text "x" ])

        let rendered =
            errors
            |> SchemaErrors.messages (Renderer.english |> Renderer.context "signup")
            |> List.map (fun (path, message) -> Path.format path, message)

        test <@ rendered |> List.contains ("name", "must be present") @>
        test <@ rendered |> List.contains ("addresses[0].postcode", "must have a size between 2 and 40, but was 1") @>

    [<Fact>]
    let ``full messages compose the attribute noun once`` () =
        let errors = errorsFrom (input Data.Null (Data.Text "42") [ Data.Text "x" ])

        let rendered =
            errors
            |> SchemaErrors.fullMessages (Renderer.english |> Renderer.context "signup")
            |> List.map (fun (path, message) -> Path.format path, message)

        test <@ rendered |> List.contains ("name", "Name must be present") @>
        test <@ rendered |> List.contains ("addresses[0].postcode", "Postcode must have a size between 2 and 40, but was 1") @>

    [<Fact>]
    let ``indexes stay out of resource keys and stay in returned paths`` () =
        // `addresses[0].postcode` and `addresses[7].postcode` are one field. Two catalogue entries for them
        // would be untranslatable in practice, but a path that lost its index would be unusable for a form.
        let errors =
            errorsFrom (input (Data.Text "ok") (Data.Text "42") [ Data.Text "ok"; Data.Text "x" ])

        let seen = ResizeArray<string>()

        let renderer =
            Renderer.Advanced.ofResolver (fun request ->
                seen.Add request.BaseKey
                None)
            |> Renderer.context "signup"

        let rendered = errors |> SchemaErrors.messages renderer |> List.map (fst >> Path.format)

        test <@ rendered = [ "addresses[1].postcode" ] @>
        test <@ seen |> Seq.forall (fun key -> not (key.Contains "[")) @>

        test <@
            seen |> Seq.contains "signup.addresses.postcode.constraint.cardinality.between"
        @>

    [<Fact>]
    let ``a field noun resolves from the folded path`` () =
        let renderer =
            lookupOf
                [ "attribute.signup.addresses.postcode", "Le code postal"
                  "signup.addresses.postcode.constraint.cardinality.between", "doit contenir entre {minimum} et {maximum} caractères"
                  "constraint.fullMessage", "{attribute} {message}" ]
            |> Renderer.context "signup"

        let errors = errorsFrom (input (Data.Text "ok") (Data.Text "42") [ Data.Text "x" ])

        test <@
            errors |> SchemaErrors.fullMessages renderer |> List.map snd
                = [ "Le code postal doit contenir entre 2 et 40 caractères, but was 1" ]
        @>

    [<Fact>]
    let ``a root failure uses the default noun rather than the document context`` () =
        let errors =
            match Schema.parse (signupSchema ()) (Data.Text "not-an-object") with
            | Error errors -> errors
            | Ok _ -> failwith "Expected a failed parse."

        let rendered =
            errors |> SchemaErrors.fullMessages (Renderer.english |> Renderer.context "signup")

        test <@ rendered |> List.map (fst >> Path.format) = [ "" ] @>
        test <@ rendered |> List.map snd = [ "value must be an object" ] @>

    [<Fact>]
    let ``schema's own catalogue renders through the same generic mechanics`` () =
        let errors = errorsFrom (input (Data.Text "ok") (Data.Text "not-an-int") [ Data.Text "ok" ])

        test <@
            errors |> SchemaErrors.messages Renderer.english |> List.map snd = [ "must be a valid int" ]
        @>

        let renderer =
            lookupOf [ "signup.age.schema.invalidFormat", "doit être un nombre entier" ]
            |> Renderer.context "signup"

        test <@
            errors |> SchemaErrors.messages renderer |> List.map snd = [ "doit être un nombre entier" ]
        @>

    [<Fact>]
    let ``every schema key parses and declares arguments its English names`` () =
        let malformed =
            SchemaMessages.keys
            |> List.filter (fun key ->
                match MessageDescriptor.Advanced.tryCreate key Map.empty with
                | Ok _ -> false
                | Error _ -> true)

        test <@ malformed = [] @>

        let mismatched =
            SchemaMessages.keys
            |> List.filter (fun key ->
                let declared = SchemaMessages.arguments[key] |> Set.ofList

                let used =
                    System.Text.RegularExpressions.Regex.Matches(SchemaMessages.english[key], @"\{([A-Za-z]+)\}")
                    |> Seq.map (fun placeholder -> placeholder.Groups[1].Value)
                    |> Set.ofSeq

                used <> declared)

        test <@ mismatched = [] @>

    [<Fact>]
    let ``toStringWith renders one localized line per failure`` () =
        let errors = errorsFrom (input Data.Null (Data.Text "42") [ Data.Text "x" ])

        let text = errors |> SchemaErrors.toStringWith (Renderer.english |> Renderer.context "signup")

        test <@ text.Contains "name: Name must be present" @>
        test <@ text.Contains "addresses[0].postcode: Postcode must have a size between 2 and 40, but was 1" @>
