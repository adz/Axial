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

        test <@ check "ada@example.com" = Ok "ada@example.com" @>
        test <@
            check "" =
                Error
                    [ Required
                      InvalidLength(MinimumLength 2, Some 0)
                      InvalidFormat "email"
                      InvalidFormat "^[^@]+@example.com$"
                      NotOneOf "ada@example.com|grace@example.com" ]
        @>
        test <@ check "root@example.com" |> Result.mapError (List.contains (Custom "notEqualTo:root@example.com")) = Error true @>
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

        test <@ check 16 = Ok 16 @>
        test <@
            check 10 =
                Error
                    [ OutOfRange(CheckRangeExpectation.GreaterThan "12", Some "10")
                      OutOfRange(CheckRangeExpectation.AtLeast "13", Some "10") ]
        @>
        test <@ check 15 |> Result.mapError (List.contains (Custom "notEqualTo:15")) = Error true @>
        test <@ SchemaConstraintCheck.tryOrdered<int> (SchemaConstraint.minLength 3) |> Option.isNone @>

    [<Fact>]
    let ``zero-relative schema constraints lower to executable Check programs`` () =
        test <@ SchemaConstraintCheck.ordered<int> [ SchemaConstraint.positive<int> () ] 1 = Ok 1 @>
        test <@
            SchemaConstraintCheck.ordered<int> [ SchemaConstraint.positive<int> () ] 0 =
                Error [ OutOfRange(CheckRangeExpectation.GreaterThan "0", Some "0") ]
        @>
        test <@ SchemaConstraintCheck.ordered<int> [ SchemaConstraint.nonNegative<int> () ] 0 = Ok 0 @>
        test <@
            SchemaConstraintCheck.ordered<int> [ SchemaConstraint.nonNegative<int> () ] -1 =
                Error [ OutOfRange(CheckRangeExpectation.AtLeast "0", Some "-1") ]
        @>
        test <@ SchemaConstraintCheck.ordered<int> [ SchemaConstraint.negative<int> () ] -1 = Ok -1 @>
        test <@
            SchemaConstraintCheck.ordered<int> [ SchemaConstraint.negative<int> () ] 0 =
                Error [ OutOfRange(CheckRangeExpectation.LessThan "0", Some "0") ]
        @>
        test <@ SchemaConstraintCheck.ordered<int> [ SchemaConstraint.nonPositive<int> () ] 0 = Ok 0 @>
        test <@
            SchemaConstraintCheck.ordered<int> [ SchemaConstraint.nonPositive<int> () ] 1 =
                Error [ OutOfRange(CheckRangeExpectation.AtMost "0", Some "1") ]
        @>

    [<Fact>]
    let ``sequence schema constraints lower to executable Check programs`` () =
        let check =
            SchemaConstraintCheck.sequence<int>
                [ SchemaConstraint.minCount 2
                  SchemaConstraint.maxCount 3
                  SchemaConstraint.distinct ]

        test <@ check [ 1; 2 ] = Ok [ 1; 2 ] @>
        test <@
            check [ 1; 1; 2; 3 ] =
                Error
                    [ CheckFailure.InvalidCount(CheckCountExpectation.MaximumCount 3, Some 4)
                      Duplicate ]
        @>
        test <@ SchemaConstraintCheck.trySequence<int> SchemaConstraint.email |> Option.isNone @>

    [<Fact>]
    let ``contains schema constraint lowers to an executable Check program`` () =
        let check = SchemaConstraintCheck.sequence<int> [ SchemaConstraint.contains 2 ]

        test <@ check [ 1; 2; 3 ] = Ok [ 1; 2; 3 ] @>
        test <@ check [ 1; 3 ] = Error [ NotOneOf "2" ] @>

    [<Fact>]
    let ``schema constraint lowerers ignore unsupported metadata and reject null inputs`` () =
        let customMaxLengthWithWrongArgumentType =
            SchemaConstraint.createWithArguments "maxLength" [ "maximum", box "not-an-int" ]

        test <@ SchemaConstraintCheck.tryText customMaxLengthWithWrongArgumentType |> Option.isNone @>
        test <@ SchemaConstraintCheck.text [ SchemaConstraint.optional ] "anything" = Ok "anything" @>
        raises<ArgumentNullException> <@ SchemaConstraintCheck.tryText null |> ignore @>
        raises<ArgumentNullException> <@ SchemaConstraintCheck.text null |> ignore @>
        raises<ArgumentNullException> <@ SchemaConstraintCheck.text [ null ] |> ignore @>
