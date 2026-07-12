namespace Axial.Tests

open System
open Axial.Schema
open Swensen.Unquote
open Xunit

/// <summary>
/// Proves that value schemas and model schemas carry portable description metadata: it is declarative annotation
/// metadata independent of constraint metadata, it stays inspectable through refinement layers with the nearest
/// declaration winning, and JSON Schema generation pins <c>$schema</c> to draft 2020-12 and lowers descriptions to
/// the <c>description</c>/<c>title</c> keywords.
/// </summary>
module SchemaDescriptionTests =
    type private Email = private Email of string

    module private Email =
        let create (value: string) = Email value
        let value (Email value) = value

        let schema () : Schema<Email> = Schema.text |> Schema.convert create value

    type private Contact = { Email: Email; Name: string }

    [<Fact>]
    let ``value schemas carry no description metadata by default`` () =
        test <@ Schema.description Schema.text = None @>
        test <@ Schema.description (Schema.text |> Schema.convert Email.create Email.value) = None @>

    [<Fact>]
    let ``describe declares inspectable description metadata`` () =
        let schema = Schema.text |> Schema.describe "A display name."

        test <@ Schema.description schema = Some "A display name." @>

    [<Fact>]
    let ``a description declared on the raw schema stays visible through refinement layers`` () =
        let schema =
            Schema.text
            |> Schema.describe "The raw address text."
            |> Schema.convert Email.create Email.value

        test <@ Schema.description schema = Some "The raw address text." @>

    [<Fact>]
    let ``a description declared nearer the refined schema overrides the raw declaration`` () =
        let schema =
            Schema.text
            |> Schema.describe "The raw address text."
            |> Schema.convert Email.create Email.value
            |> Schema.describe "The customer's email address."

        test <@ Schema.description schema = Some "The customer's email address." @>

    [<Fact>]
    let ``describe replaces an earlier declaration on the same schema`` () =
        let schema =
            Schema.text
            |> Schema.describe "First."
            |> Schema.describe "Second."

        test <@ Schema.description schema = Some "Second." @>

    [<Fact>]
    let ``describe rejects empty or whitespace descriptions`` () =
        raises<ArgumentException> <@ Schema.describe "" Schema.text |> ignore @>
        raises<ArgumentException> <@ Schema.describe "   " Schema.text |> ignore @>
        raises<ArgumentException> <@ Schema.describe null Schema.text |> ignore @>

    [<Fact>]
    let ``description accessors raise for null schemas`` () =
        raises<ArgumentNullException> <@ Schema.description Unchecked.defaultof<Schema<string>> |> ignore @>
        raises<ArgumentNullException> <@ Schema.describe "text" Unchecked.defaultof<Schema<string>> |> ignore @>

    [<Fact>]
    let ``generate pins schema to json schema draft 2020-12`` () =
        let schema =
            Schema.recordFor<Contact, _> (fun email name -> { Email = email; Name = name })
            |> Schema.field "email" _.Email (Email.schema ())
            |> Schema.field "name" _.Name Schema.text
            |> Schema.build

        let generated = JsonSchema.generate schema

        test <@ generated.StartsWith "{\"$schema\":\"https://json-schema.org/draft/2020-12/schema\"," @>
        test <@ (JsonSchema.generateValue Schema.text).StartsWith "{\"$schema\":\"https://json-schema.org/draft/2020-12/schema\"," @>

    [<Fact>]
    let ``describe lowers to the json schema description keyword on a field`` () =
        let schema =
            Schema.recordFor<Contact, _> (fun email name -> { Email = email; Name = name })
            |> Schema.field "email" _.Email (Email.schema ())
            |> Schema.field "name" _.Name (Schema.text |> Schema.describe "The contact's full name.")
            |> Schema.build

        let generated = JsonSchema.generate schema

        test <@ generated.Contains "\"name\":{\"description\":\"The contact's full name.\",\"type\":\"string\"}" @>

    [<Fact>]
    let ``Schema.describe lowers to the json schema root title keyword`` () =
        let schema =
            Schema.recordFor<Contact, _> (fun email name -> { Email = email; Name = name })
            |> Schema.field "email" _.Email (Email.schema ())
            |> Schema.field "name" _.Name Schema.text
            |> Schema.build
            |> Schema.describe "A contact record."

        let generated = JsonSchema.generate schema

        test <@ generated.Contains "\"title\":\"A contact record.\"" @>

    [<Fact>]
    let ``Schema.describe rejects empty descriptions and unbuilt schemas`` () =
        let builder =
            Schema.recordFor<Contact, _> (fun email name -> { Email = email; Name = name })
            |> Schema.field "email" _.Email (Email.schema ())
            |> Schema.field "name" _.Name Schema.text

        raises<ArgumentException> <@ Schema.describe "" (Schema.build builder) |> ignore @>
        raises<ArgumentNullException> <@ Schema.describe "title" Unchecked.defaultof<Schema<Contact>> |> ignore @>
