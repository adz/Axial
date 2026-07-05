namespace Axial.Tests

open System
open Axial.ErrorHandling
open Axial.Schema
open Axial.Validation.Schema
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

        let schema : ValueSchema<Email> =
            Value.text
            |> Value.withConstraint SchemaConstraint.required
            |> Value.refined create value
            |> Value.withConstraint SchemaConstraint.email

    /// <summary>A bounded-text domain value whose constraints all live on the raw text schema.</summary>
    type private ContactName = private ContactName of string

    module private ContactName =
        let create (value: string) = ContactName value
        let value (ContactName value) = value

        let schema : ValueSchema<ContactName> =
            Value.text
            |> Value.withConstraints [ SchemaConstraint.minLength 2; SchemaConstraint.maxLength 40 ]
            |> Value.refined create value

    /// <summary>An email refined a second time over the already refined <c>Email</c> schema.</summary>
    type private NormalizedEmail = private NormalizedEmail of Email

    module private NormalizedEmail =
        let create (email: Email) = NormalizedEmail email
        let value (NormalizedEmail email) = email

        let schema : ValueSchema<NormalizedEmail> =
            Email.schema
            |> Value.refined create value
            |> Value.withConstraint (SchemaConstraint.maxLength 254)

    /// <summary>A bounded number refined over the primitive int schema.</summary>
    type private Age = private Age of int

    module private Age =
        let create (value: int) = Age value
        let value (Age value) = value

        let schema : ValueSchema<Age> =
            Value.int
            |> Value.withConstraints [ SchemaConstraint.atLeast 0; SchemaConstraint.atMost 130 ]
            |> Value.refined create value

    [<Fact>]
    let ``inspectUnderlying projects a refined value to its primitive representation`` () =
        let inspect = Email.schema |> Value.inspectUnderlying<Email, string>
        test <@ inspect (Email.create "ada@example.com") = "ada@example.com" @>

        let inspectAge = Age.schema |> Value.inspectUnderlying<Age, int>
        test <@ inspectAge (Age.create 30) = 30 @>

    [<Fact>]
    let ``inspectUnderlying projects layered refined values through every refinement layer`` () =
        let inspect = NormalizedEmail.schema |> Value.inspectUnderlying<NormalizedEmail, string>
        test <@ inspect (NormalizedEmail.create (Email.create "ada@example.com")) = "ada@example.com" @>

    [<Fact>]
    let ``inspectUnderlying is the identity projection for primitive value schemas`` () =
        let inspectText = Value.text |> Value.inspectUnderlying<string, string>
        test <@ inspectText "Ada" = "Ada" @>

        let inspectInt = Value.int |> Value.inspectUnderlying<int, int>
        test <@ inspectInt 42 = 42 @>

    [<Fact>]
    let ``inspectUnderlying rejects projection types that do not match the underlying primitive kind`` () =
        raises<ArgumentException> <@ Value.inspectUnderlying<Email, int> Email.schema |> ignore @>
        raises<ArgumentException> <@ Value.inspectUnderlying<Age, string> Age.schema |> ignore @>
        raises<ArgumentNullException> <@ Value.inspectUnderlying<Email, string> Unchecked.defaultof<ValueSchema<Email>> |> ignore @>

    [<Fact>]
    let ``allConstraints gathers every layer's constraint metadata foundation-first`` () =
        test <@ Value.allConstraints Email.schema |> List.map SchemaConstraint.code = [ "required"; "email" ] @>
        test <@
            Value.allConstraints NormalizedEmail.schema |> List.map SchemaConstraint.code =
                [ "required"; "email"; "maxLength" ]
        @>

    [<Fact>]
    let ``allConstraints equals constraints for primitive value schemas`` () =
        let bounded = Value.text |> Value.withConstraint (SchemaConstraint.maxLength 40)
        test <@ Value.allConstraints bounded = Value.constraints bounded @>
        test <@ Value.allConstraints Value.text = [] @>
        raises<ArgumentNullException> <@ Value.allConstraints Unchecked.defaultof<ValueSchema<string>> |> ignore @>

    [<Fact>]
    let ``a refined value schema's raw and refined constraint metadata runs as one Check program`` () =
        let check = Email.schema |> ValueSchemaCheck.text

        test <@ check (Email.create "ada@example.com") = Ok () @>
        test <@ check (Email.create "") = Error [ Blank; InvalidFormat "email" ] @>

    [<Fact>]
    let ``raw text constraint metadata checks the refined value's underlying text`` () =
        let check = ContactName.schema |> ValueSchemaCheck.text

        test <@ check (ContactName.create "Ada") = Ok () @>
        test <@ check (ContactName.create "A") = Error [ Length(MinimumLength 2, Some 1) ] @>
        test <@
            check (ContactName.create (String.replicate 41 "a")) =
                Error [ Length(MaximumLength 40, Some 41) ]
        @>

    [<Fact>]
    let ``layered refined value schemas run every layer's checks through composed inspection`` () =
        let check = NormalizedEmail.schema |> ValueSchemaCheck.text

        test <@ check (NormalizedEmail.create (Email.create "ada@example.com")) = Ok () @>

        // The raw text layer's checks fire for blank input; the outer refined layer's maxLength passes at length 0.
        test <@ check (NormalizedEmail.create (Email.create "")) = Error [ Blank; InvalidFormat "email" ] @>

        // The outer refined layer's own constraint fires once the raw layers pass.
        let overlong = String.replicate 250 "a" + "@example.com"

        test <@
            check (NormalizedEmail.create (Email.create overlong)) =
                Error [ Length(MaximumLength 254, Some 262) ]
        @>

    [<Fact>]
    let ``refined value schemas over ordered primitives run range Check programs`` () =
        let check = Age.schema |> ValueSchemaCheck.ordered<int, _>

        test <@ check (Age.create 30) = Ok () @>
        test <@ check (Age.create -1) = Error [ Range(CheckRangeExpectation.AtLeast "0", Some "-1") ] @>
        test <@ check (Age.create 200) = Error [ Range(CheckRangeExpectation.AtMost "130", Some "200") ] @>

    [<Fact>]
    let ``arbitrary Check programs adapt to refined values through fromUnderlying`` () =
        let check =
            Email.schema
            |> ValueSchemaCheck.fromUnderlying (Check.all [ Check.String.present; Check.String.matches "@example\\.com$" ])

        test <@ check (Email.create "ada@example.com") = Ok () @>
        test <@ check (Email.create "ada@example.org") = Error [ InvalidFormat "@example\\.com$" ] @>

    [<Fact>]
    let ``primitive value schemas run Check programs the same way as refined value schemas`` () =
        let check =
            Value.text
            |> Value.withConstraint (SchemaConstraint.minLength 2)
            |> ValueSchemaCheck.text

        test <@ check "Ada" = Ok () @>
        test <@ check "A" = Error [ Length(MinimumLength 2, Some 1) ] @>

    [<Fact>]
    let ``value schema check interpreters reject mismatched primitives and null inputs`` () =
        raises<ArgumentException> <@ Age.schema |> ValueSchemaCheck.text |> ignore @>
        raises<ArgumentException> <@ Email.schema |> ValueSchemaCheck.ordered<int, _> |> ignore @>
        raises<ArgumentNullException> <@ ValueSchemaCheck.text Unchecked.defaultof<ValueSchema<Email>> |> ignore @>
        raises<ArgumentNullException> <@ ValueSchemaCheck.ordered<int, Age> Unchecked.defaultof<ValueSchema<Age>> |> ignore @>
        raises<ArgumentNullException> <@ ValueSchemaCheck.fromUnderlying Unchecked.defaultof<Check<string>> Email.schema |> ignore @>
        raises<ArgumentNullException> <@ ValueSchemaCheck.fromUnderlying Check.String.present Unchecked.defaultof<ValueSchema<Email>> |> ignore @>
