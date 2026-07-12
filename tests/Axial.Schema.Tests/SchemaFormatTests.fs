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

        let schema () : Schema<Email> =
            Schema.text
            |> Schema.convert create value
            |> Schema.withFormat SchemaFormat.email

    type private Contact = { Email: Email; Name: string }

    [<Fact>]
    let ``value schemas carry no format metadata by default`` () =
        test <@ Schema.format Schema.text = None @>
        test <@ Schema.format Schema.int = None @>
        test <@ Schema.format (Schema.text |> Schema.convert Email.create Email.value) = None @>

    [<Fact>]
    let ``withFormat declares inspectable format metadata`` () =
        let schema = Schema.text |> Schema.withFormat SchemaFormat.email

        test <@ Schema.format schema = Some SchemaFormat.email @>
        test <@ Schema.format schema |> Option.map SchemaFormat.name = Some "email" @>
        test <@ SchemaFormat.name SchemaFormat.email = "email" @>
        test <@ string SchemaFormat.email = "email" @>

    [<Fact>]
    let ``custom formats are created from stable interpreter-facing names`` () =
        let uri = SchemaFormat.create "uri"

        test <@ SchemaFormat.name uri = "uri" @>
        test <@ Schema.format (Schema.text |> Schema.withFormat uri) = Some uri @>

    [<Fact>]
    let ``refined value schemas can declare a format such as email`` () =
        test <@ Schema.isRefined (Email.schema ()) @>
        test <@ Schema.underlyingPrimitiveKind (Email.schema ()) = PrimitiveValueKind.Text @>
        test <@ Schema.format (Email.schema ()) = Some SchemaFormat.email @>

    [<Fact>]
    let ``a format declared on the raw schema stays visible through refinement layers`` () =
        let schema =
            Schema.text
            |> Schema.withFormat SchemaFormat.email
            |> Schema.convert Email.create Email.value

        test <@ Schema.format schema = Some SchemaFormat.email @>

    [<Fact>]
    let ``a format declared nearer the refined schema overrides the raw declaration`` () =
        let schema =
            Schema.text
            |> Schema.withFormat (SchemaFormat.create "raw-text")
            |> Schema.convert Email.create Email.value
            |> Schema.withFormat SchemaFormat.email

        test <@ Schema.format schema = Some SchemaFormat.email @>

    [<Fact>]
    let ``withFormat replaces an earlier declaration on the same schema`` () =
        let schema =
            Schema.text
            |> Schema.withFormat (SchemaFormat.create "hostname")
            |> Schema.withFormat SchemaFormat.email

        test <@ Schema.format schema = Some SchemaFormat.email @>

    [<Fact>]
    let ``format metadata is annotation metadata independent of constraint metadata`` () =
        // Declaring a format attaches no constraints, and attaching constraints keeps the format.
        test <@ Schema.constraints (Email.schema ()) = [] @>

        let schema =
            Schema.text
            |> Schema.withFormat SchemaFormat.email
            |> Schema.constrain Constraint.email

        test <@ Schema.format schema = Some SchemaFormat.email @>
        test <@ Schema.constraints schema |> List.map Constraint.code = [ "email" ] @>

        let formattedLast = Schema.text |> Schema.constrain Constraint.required |> Schema.withFormat SchemaFormat.email
        test <@ Schema.constraints formattedLast |> List.map Constraint.code = [ "required" ] @>
        test <@ Schema.format formattedLast = Some SchemaFormat.email @>

    [<Fact>]
    let ``formatted value schemas compose with the schema builder like any other value schema`` () =
        let schema =
            Schema.recordFor<Contact, _> (fun email name -> { Email = email; Name = name })
            |> Schema.field "email" _.Email ((Email.schema ()) |> Schema.constrainAll [ Constraint.required ])
            |> Schema.field "name" _.Name Schema.text
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
        raises<ArgumentException> <@ Schema.withFormat Unchecked.defaultof<SchemaFormat> Schema.text |> ignore @>

    [<Fact>]
    let ``format accessors raise for null schemas`` () =
        raises<ArgumentNullException> <@ Schema.format Unchecked.defaultof<Schema<string>> |> ignore @>
        raises<ArgumentNullException> <@ Schema.withFormat SchemaFormat.email Unchecked.defaultof<Schema<string>> |> ignore @>
