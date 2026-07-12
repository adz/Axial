namespace Axial.Tests

open System
open Axial.ErrorHandling
open Axial.Schema
open Swensen.Unquote
open Xunit

/// <summary>
/// Proves that refined value schemas can run executable <c>Check</c> programs: a trusted refined value projects
/// through every refinement layer to its underlying primitive representation, the constraint metadata carried by
/// every layer lowers into one executable check over the refined value, and arbitrary primitive-level check programs
/// adapt to run against refined values the same way.
/// </summary>
module SchemaRefinedValueCheckTests =
    /// <summary>An email address whose raw text schema and refined schema each carry constraint metadata.</summary>
    type private Email = private Email of string

    module private Email =
        let create (value: string) = Email value
        let value (Email value) = value

        let schema : Schema<Email> =
            Schema.text
            |> Schema.constrain Constraint.required
            |> Schema.convert create value
            |> Schema.constrain Constraint.email

    /// <summary>A bounded-text domain value whose constraints all live on the raw text schema.</summary>
    type private ContactName = private ContactName of string

    module private ContactName =
        let create (value: string) = ContactName value
        let value (ContactName value) = value

        let schema : Schema<ContactName> =
            Schema.text
            |> Schema.constrainAll [ Constraint.minLength 2; Constraint.maxLength 40 ]
            |> Schema.convert create value

    /// <summary>An email refined a second time over the already refined <c>Email</c> schema.</summary>
    type private NormalizedEmail = private NormalizedEmail of Email

    module private NormalizedEmail =
        let create (email: Email) = NormalizedEmail email
        let value (NormalizedEmail email) = email

        let schema : Schema<NormalizedEmail> =
            Email.schema
            |> Schema.convert create value
            |> Schema.constrain (Constraint.maxLength 254)

    /// <summary>A bounded number refined over the primitive int schema.</summary>
    type private Age = private Age of int

    module private Age =
        let create (value: int) = Age value
        let value (Age value) = value

        let schema : Schema<Age> =
            Schema.int
            |> Schema.constrainAll [ Constraint.atLeast 0; Constraint.atMost 130 ]
            |> Schema.convert create value

    [<Fact>]
    let ``inspectUnderlying projects a refined value to its primitive representation`` () =
        let inspect = Email.schema |> Schema.inspectUnderlying<Email, string>
        test <@ inspect (Email.create "ada@example.com") = "ada@example.com" @>

        let inspectAge = Age.schema |> Schema.inspectUnderlying<Age, int>
        test <@ inspectAge (Age.create 30) = 30 @>

    [<Fact>]
    let ``inspectUnderlying projects layered refined values through every refinement layer`` () =
        let inspect = NormalizedEmail.schema |> Schema.inspectUnderlying<NormalizedEmail, string>
        test <@ inspect (NormalizedEmail.create (Email.create "ada@example.com")) = "ada@example.com" @>

    [<Fact>]
    let ``inspectUnderlying is the identity projection for primitive value schemas`` () =
        let inspectText = Schema.text |> Schema.inspectUnderlying<string, string>
        test <@ inspectText "Ada" = "Ada" @>

        let inspectInt = Schema.int |> Schema.inspectUnderlying<int, int>
        test <@ inspectInt 42 = 42 @>

    [<Fact>]
    let ``inspectUnderlying rejects projection types that do not match the underlying primitive kind`` () =
        raises<ArgumentException> <@ Schema.inspectUnderlying<Email, int> Email.schema |> ignore @>
        raises<ArgumentException> <@ Schema.inspectUnderlying<Age, string> Age.schema |> ignore @>
        raises<ArgumentNullException> <@ Schema.inspectUnderlying<Email, string> Unchecked.defaultof<Schema<Email>> |> ignore @>

    [<Fact>]
    let ``allConstraints gathers every layer's constraint metadata foundation-first`` () =
        test <@ Schema.allConstraints Email.schema |> List.map Constraint.code = [ "required"; "email" ] @>
        test <@
            Schema.allConstraints NormalizedEmail.schema |> List.map Constraint.code =
                [ "required"; "email"; "maxLength" ]
        @>

    [<Fact>]
    let ``allConstraints equals constraints for primitive value schemas`` () =
        let bounded = Schema.text |> Schema.constrain (Constraint.maxLength 40)
        test <@ Schema.allConstraints bounded = Schema.constraints bounded @>
        test <@ Schema.allConstraints Schema.text = [] @>
        raises<ArgumentNullException> <@ Schema.allConstraints Unchecked.defaultof<Schema<string>> |> ignore @>

    [<Fact>]
    let ``a refined value schema's raw and refined constraint metadata runs as one Check program`` () =
        let check = Email.schema |> SchemaCheck.text

        test <@ check (Email.create "ada@example.com") = Ok(Email.create "ada@example.com") @>
        test <@ check (Email.create "") = Error [ Required; InvalidFormat "email" ] @>

    [<Fact>]
    let ``raw text constraint metadata checks the refined value's underlying text`` () =
        let check = ContactName.schema |> SchemaCheck.text

        test <@ check (ContactName.create "Ada") = Ok(ContactName.create "Ada") @>
        test <@ check (ContactName.create "A") = Error [ InvalidLength(MinimumLength 2, Some 1) ] @>
        test <@
            check (ContactName.create (String.replicate 41 "a")) =
                Error [ InvalidLength(MaximumLength 40, Some 41) ]
        @>

    [<Fact>]
    let ``layered refined value schemas run every layer's checks through composed inspection`` () =
        let check = NormalizedEmail.schema |> SchemaCheck.text

        test <@ check (NormalizedEmail.create (Email.create "ada@example.com")) = Ok(NormalizedEmail.create (Email.create "ada@example.com")) @>

        // The raw text layer's checks fire for blank input; the outer refined layer's maxLength passes at length 0.
        test <@ check (NormalizedEmail.create (Email.create "")) = Error [ Required; InvalidFormat "email" ] @>

        // The outer refined layer's own constraint fires once the raw layers pass.
        let overlong = String.replicate 250 "a" + "@example.com"

        test <@
            check (NormalizedEmail.create (Email.create overlong)) =
                Error [ InvalidLength(MaximumLength 254, Some 262) ]
        @>

    [<Fact>]
    let ``refined value schemas over ordered primitives run range Check programs`` () =
        let check = Age.schema |> SchemaCheck.ordered<int, _>

        test <@ check (Age.create 30) = Ok(Age.create 30) @>
        test <@ check (Age.create -1) = Error [ OutOfRange(CheckRangeExpectation.AtLeast "0", Some "-1") ] @>
        test <@ check (Age.create 200) = Error [ OutOfRange(CheckRangeExpectation.AtMost "130", Some "200") ] @>

    [<Fact>]
    let ``arbitrary Check programs adapt to refined values through fromUnderlying`` () =
        let check =
            Email.schema
            |> SchemaCheck.fromUnderlying (Check.all [ Check.String.present; Check.String.matches "@example\\.com$" ])

        test <@ check (Email.create "ada@example.com") = Ok(Email.create "ada@example.com") @>
        test <@ check (Email.create "ada@example.org") = Error [ InvalidFormat "@example\\.com$" ] @>

    [<Fact>]
    let ``primitive value schemas run Check programs the same way as refined value schemas`` () =
        let check =
            Schema.text
            |> Schema.constrain (Constraint.minLength 2)
            |> SchemaCheck.text

        test <@ check "Ada" = Ok "Ada" @>
        test <@ check "A" = Error [ InvalidLength(MinimumLength 2, Some 1) ] @>

    [<Fact>]
    let ``value schema check interpreters reject mismatched primitives and null inputs`` () =
        raises<ArgumentException> <@ Age.schema |> SchemaCheck.text |> ignore @>
        raises<ArgumentException> <@ Email.schema |> SchemaCheck.ordered<int, _> |> ignore @>
        raises<ArgumentNullException> <@ SchemaCheck.text Unchecked.defaultof<Schema<Email>> |> ignore @>
        raises<ArgumentNullException> <@ SchemaCheck.ordered<int, Age> Unchecked.defaultof<Schema<Age>> |> ignore @>
        raises<ArgumentNullException> <@ SchemaCheck.fromUnderlying Unchecked.defaultof<Check<string>> Email.schema |> ignore @>
        raises<ArgumentNullException> <@ SchemaCheck.fromUnderlying Check.String.present Unchecked.defaultof<Schema<Email>> |> ignore @>
