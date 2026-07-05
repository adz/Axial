namespace Axial.Tests

open System
open Axial.ErrorHandling
open Axial.Schema
open Axial.Validation.Schema
open Swensen.Unquote
open Xunit

module SchemaConstraintCheckTests =
    [<Fact>]
    let ``text schema constraints lower to executable Check programs`` () =
        let check =
            SchemaConstraintCheck.text
                [ SchemaConstraint.required
                  SchemaConstraint.minLength 2
                  SchemaConstraint.maxLength 20
                  SchemaConstraint.email
                  SchemaConstraint.pattern "^[^@]+@example.com$"
                  SchemaConstraint.notEqualTo "root@example.com"
                  SchemaConstraint.oneOf [ "ada@example.com"; "grace@example.com" ] ]

        test <@ check "ada@example.com" = Ok () @>
        test <@
            check "" =
                Error
                    [ Blank
                      Length(MinimumLength 2, Some 0)
                      InvalidFormat "email"
                      InvalidFormat "^[^@]+@example.com$"
                      Equality(EqualTo "ada@example.com|grace@example.com", Some "") ]
        @>
        test <@ check "root@example.com" |> Result.mapError (List.contains (Equality(NotEqualTo "root@example.com", Some "root@example.com"))) = Error true @>
        test <@ SchemaConstraintCheck.tryText SchemaConstraint.optional |> Option.isNone @>

    [<Fact>]
    let ``ordered schema constraints lower to executable Check programs`` () =
        let check =
            SchemaConstraintCheck.ordered<int>
                [ SchemaConstraint.between 10 20
                  SchemaConstraint.greaterThan 12
                  SchemaConstraint.lessThan 18
                  SchemaConstraint.atLeast 13
                  SchemaConstraint.atMost 17
                  SchemaConstraint.notEqualTo 15 ]

        test <@ check 16 = Ok () @>
        test <@
            check 10 =
                Error
                    [ Range(CheckRangeExpectation.GreaterThan "12", Some "10")
                      Range(CheckRangeExpectation.AtLeast "13", Some "10") ]
        @>
        test <@ check 15 |> Result.mapError (List.contains (Equality(NotEqualTo "15", Some "15"))) = Error true @>
        test <@ SchemaConstraintCheck.tryOrdered<int> (SchemaConstraint.minLength 3) |> Option.isNone @>

    [<Fact>]
    let ``sequence schema constraints lower to executable Check programs`` () =
        let check =
            SchemaConstraintCheck.sequence<int>
                [ SchemaConstraint.minCount 2
                  SchemaConstraint.maxCount 3
                  SchemaConstraint.distinct ]

        test <@ check [ 1; 2 ] = Ok () @>
        test <@
            check [ 1; 1; 2; 3 ] =
                Error
                    [ CheckFailure.Count(CheckCountExpectation.MaximumCount 3, Some 4)
                      CustomCode "seq.distinct" ]
        @>
        test <@ SchemaConstraintCheck.trySequence<int> SchemaConstraint.email |> Option.isNone @>

    [<Fact>]
    let ``schema constraint lowerers ignore unsupported metadata and reject null inputs`` () =
        let customMaxLengthWithWrongArgumentType =
            SchemaConstraint.createWithArguments "maxLength" [ "maximum", box "not-an-int" ]

        test <@ SchemaConstraintCheck.tryText customMaxLengthWithWrongArgumentType |> Option.isNone @>
        test <@ SchemaConstraintCheck.text [ SchemaConstraint.optional ] "anything" = Ok () @>
        raises<ArgumentNullException> <@ SchemaConstraintCheck.tryText null |> ignore @>
        raises<ArgumentNullException> <@ SchemaConstraintCheck.text null |> ignore @>
        raises<ArgumentNullException> <@ SchemaConstraintCheck.text [ null ] |> ignore @>
