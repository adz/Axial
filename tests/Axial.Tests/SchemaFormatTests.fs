namespace Axial.Tests

open System
open Axial.Schema
open Swensen.Unquote
open Xunit

/// <summary>
/// Proves that value schemas carry portable <c>format</c> metadata such as <c>email</c>: the format is declarative
/// annotation metadata for interpreters, it is independent of constraint metadata and executable checks, it stays
/// inspectable through refinement layers with the nearest declaration winning, and formatted value schemas compose
/// with the progressive schema builder like any other value schema.
/// </summary>
module SchemaFormatTests =
    /// <summary>A named refined/domain email type declaring the well-known <c>email</c> format.</summary>
    type private Email = private Email of string

    module private Email =
        let create (value: string) = Email value
        let value (Email value) = value

        let schema : ValueSchema<Email> =
            Value.text
            |> Value.refined create value
            |> Value.withFormat SchemaFormat.email

    type private Contact = { Email: Email; Name: string }

    [<Fact>]
    let ``value schemas carry no format metadata by default`` () =
        test <@ Value.format Value.text = None @>
        test <@ Value.format Value.``int`` = None @>
        test <@ Value.format (Value.text |> Value.refined Email.create Email.value) = None @>

    [<Fact>]
    let ``withFormat declares inspectable format metadata`` () =
        let schema = Value.text |> Value.withFormat SchemaFormat.email

        test <@ Value.format schema = Some SchemaFormat.email @>
        test <@ Value.format schema |> Option.map SchemaFormat.name = Some "email" @>
        test <@ SchemaFormat.name SchemaFormat.email = "email" @>
        test <@ string SchemaFormat.email = "email" @>

    [<Fact>]
    let ``custom formats are created from stable interpreter-facing names`` () =
        let uri = SchemaFormat.create "uri"

        test <@ SchemaFormat.name uri = "uri" @>
        test <@ Value.format (Value.text |> Value.withFormat uri) = Some uri @>

    [<Fact>]
    let ``refined value schemas can declare a format such as email`` () =
        test <@ Value.isRefined Email.schema @>
        test <@ Value.underlyingPrimitiveKind Email.schema = PrimitiveValueKind.Text @>
        test <@ Value.format Email.schema = Some SchemaFormat.email @>

    [<Fact>]
    let ``a format declared on the raw schema stays visible through refinement layers`` () =
        let schema =
            Value.text
            |> Value.withFormat SchemaFormat.email
            |> Value.refined Email.create Email.value

        test <@ Value.format schema = Some SchemaFormat.email @>

    [<Fact>]
    let ``a format declared nearer the refined schema overrides the raw declaration`` () =
        let schema =
            Value.text
            |> Value.withFormat (SchemaFormat.create "raw-text")
            |> Value.refined Email.create Email.value
            |> Value.withFormat SchemaFormat.email

        test <@ Value.format schema = Some SchemaFormat.email @>

    [<Fact>]
    let ``withFormat replaces an earlier declaration on the same schema`` () =
        let schema =
            Value.text
            |> Value.withFormat (SchemaFormat.create "hostname")
            |> Value.withFormat SchemaFormat.email

        test <@ Value.format schema = Some SchemaFormat.email @>

    [<Fact>]
    let ``format metadata is annotation metadata independent of constraint metadata`` () =
        // Declaring a format attaches no constraints, and attaching constraints keeps the format.
        test <@ Value.constraints Email.schema = [] @>

        let schema =
            Value.text
            |> Value.withFormat SchemaFormat.email
            |> Value.withConstraint SchemaConstraint.email

        test <@ Value.format schema = Some SchemaFormat.email @>
        test <@ Value.constraints schema |> List.map SchemaConstraint.code = [ "email" ] @>

        let formattedLast = Value.text |> Value.withConstraint SchemaConstraint.required |> Value.withFormat SchemaFormat.email
        test <@ Value.constraints formattedLast |> List.map SchemaConstraint.code = [ "required" ] @>
        test <@ Value.format formattedLast = Some SchemaFormat.email @>

    [<Fact>]
    let ``formatted value schemas compose with the schema builder like any other value schema`` () =
        let schema =
            Schema.recordFor<Contact, _> (fun email name -> { Email = email; Name = name })
            |> Schema.fieldWith [ SchemaConstraint.required ] "email" _.Email Email.schema
            |> Schema.text "name" _.Name
            |> Schema.build

        match schema.Definition with
        | ModelDefinition model ->
            let email = model.Fields |> List.find (fun field -> ExternalFieldName.value field.ExternalName = "email")
            let name = model.Fields |> List.find (fun field -> ExternalFieldName.value field.ExternalName = "name")
            test <@ email.ValueSchema.Format = Some SchemaFormat.email @>
            test <@ name.ValueSchema.Format = None @>
        | PendingDefinition -> failwith "Expected public schema API to create a model definition."

    [<Fact>]
    let ``SchemaFormat.create validates the format name`` () =
        raises<ArgumentNullException> <@ SchemaFormat.create null |> ignore @>
        raises<ArgumentException> <@ SchemaFormat.create "" |> ignore @>
        raises<ArgumentException> <@ SchemaFormat.create "   " |> ignore @>

    [<Fact>]
    let ``withFormat rejects an unnamed format`` () =
        raises<ArgumentException> <@ Value.withFormat Unchecked.defaultof<SchemaFormat> Value.text |> ignore @>

    [<Fact>]
    let ``format accessors raise for null schemas`` () =
        raises<ArgumentNullException> <@ Value.format Unchecked.defaultof<ValueSchema<string>> |> ignore @>
        raises<ArgumentNullException> <@ Value.withFormat SchemaFormat.email Unchecked.defaultof<ValueSchema<string>> |> ignore @>
