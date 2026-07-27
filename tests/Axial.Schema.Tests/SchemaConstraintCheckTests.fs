namespace Axial.Tests

open System
open Axial.Check
open Axial.Schema
open Swensen.Unquote
open Xunit

module ConstraintCheckTests =
    [<Fact>]
    let ``text schema constraints lower to executable Check programs`` () =
        let check =
            ConstraintCheck.text
                [ Constraint.required
                  Constraint.minLength 2
                  Constraint.maxLength 20
                  Constraint.email
                  Constraint.pattern "^[^@]+@example.com$"
                  Constraint.notEqualTo "root@example.com"
                  Constraint.oneOf [ "ada@example.com"; "grace@example.com" ] ]

        test <@ check "ada@example.com" = Ok () @>
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
        test <@ ConstraintCheck.tryText Constraint.optional |> Option.isNone @>

    [<Fact>]
    let ``ordered schema constraints lower to executable Check programs`` () =
        let check =
            ConstraintCheck.ordered<int>
                [ Constraint.between 10 20
                  Constraint.greaterThan 12
                  Constraint.lessThan 18
                  Constraint.atLeast 13
                  Constraint.atMost 17
                  Constraint.notEqualTo 15 ]

        test <@ check 16 = Ok () @>
        test <@
            check 10 =
                Error
                    [ OutOfRange(CheckRangeExpectation.GreaterThan "12", Some "10")
                      OutOfRange(CheckRangeExpectation.AtLeast "13", Some "10") ]
        @>
        test <@ check 15 |> Result.mapError (List.contains (Custom "notEqualTo:15")) = Error true @>
        test <@ ConstraintCheck.tryOrdered<int> (Constraint.minLength 3) |> Option.isNone @>

    [<Fact>]
    let ``zero-relative schema constraints lower to executable Check programs`` () =
        test <@ ConstraintCheck.ordered<int> [ Constraint.positive<int> () ] 1 = Ok () @>
        test <@
            ConstraintCheck.ordered<int> [ Constraint.positive<int> () ] 0 =
                Error [ OutOfRange(CheckRangeExpectation.GreaterThan "0", Some "0") ]
        @>
        test <@ ConstraintCheck.ordered<int> [ Constraint.nonNegative<int> () ] 0 = Ok () @>
        test <@
            ConstraintCheck.ordered<int> [ Constraint.nonNegative<int> () ] -1 =
                Error [ OutOfRange(CheckRangeExpectation.AtLeast "0", Some "-1") ]
        @>
        test <@ ConstraintCheck.ordered<int> [ Constraint.negative<int> () ] -1 = Ok () @>
        test <@
            ConstraintCheck.ordered<int> [ Constraint.negative<int> () ] 0 =
                Error [ OutOfRange(CheckRangeExpectation.LessThan "0", Some "0") ]
        @>
        test <@ ConstraintCheck.ordered<int> [ Constraint.nonPositive<int> () ] 0 = Ok () @>
        test <@
            ConstraintCheck.ordered<int> [ Constraint.nonPositive<int> () ] 1 =
                Error [ OutOfRange(CheckRangeExpectation.AtMost "0", Some "1") ]
        @>

    [<Fact>]
    let ``sequence schema constraints lower to executable Check programs`` () =
        let check =
            ConstraintCheck.sequence<int>
                [ Constraint.minCount 2
                  Constraint.maxCount 3
                  Constraint.distinct ]

        test <@ check [ 1; 2 ] = Ok () @>
        test <@
            check [ 1; 1; 2; 3 ] =
                Error
                    [ CheckFailure.InvalidCount(CheckCountExpectation.MaximumCount 3, Some 4)
                      Duplicate ]
        @>
        test <@ ConstraintCheck.trySequence<int> Constraint.email |> Option.isNone @>

    [<Fact>]
    let ``contains schema constraint lowers to an executable Check program`` () =
        let check = ConstraintCheck.sequence<int> [ Constraint.contains 2 ]

        test <@ check [ 1; 2; 3 ] = Ok () @>
        test <@ check [ 1; 3 ] = Error [ NotOneOf "2" ] @>

    [<Fact>]
    let ``schema constraint lowerers ignore unsupported metadata and reject null inputs`` () =
        let customMaxLengthWithWrongArgumentType =
            Constraint.createWithArguments "maxLength" [ "maximum", box "not-an-int" ]

        test <@ ConstraintCheck.tryText customMaxLengthWithWrongArgumentType |> Option.isNone @>
        test <@ ConstraintCheck.text [ Constraint.optional ] "anything" = Ok () @>
        raises<ArgumentNullException> <@ ConstraintCheck.tryText null |> ignore @>
        raises<ArgumentNullException> <@ ConstraintCheck.text null |> ignore @>
        raises<ArgumentNullException> <@ ConstraintCheck.text [ null ] |> ignore @>

    [<Fact>]
    let ``every executable metadata case has an explicit Check lowering`` () =
        let textConstraints =
            [ Constraint.required
              Constraint.minLength 1
              Constraint.maxLength 10
              Constraint.lengthBetween 1 10
              Constraint.email
              Constraint.trimmed
              Constraint.pattern ".+"
              Constraint.oneOf [ "a" ]
              Constraint.notEqualTo "b" ]

        let orderedConstraints =
            [ Constraint.between 1 10
              Constraint.greaterThan 0
              Constraint.lessThan 11
              Constraint.atLeast 1
              Constraint.atMost 10
              Constraint.notEqualTo 5 ]

        let sequenceConstraints =
            [ Constraint.count 1
              Constraint.minCount 1
              Constraint.maxCount 2
              Constraint.countBetween 1 2
              Constraint.distinct
              Constraint.contains 1 ]

        textConstraints
        |> List.iter (fun constraint' ->
            test <@ ConstraintCheck.tryText constraint' |> Option.isSome @>)

        orderedConstraints
        |> List.iter (fun constraint' ->
            test <@ ConstraintCheck.tryOrdered<int> constraint' |> Option.isSome @>)

        sequenceConstraints
        |> List.iter (fun constraint' ->
            test <@ ConstraintCheck.trySequence<int> constraint' |> Option.isSome @>)

        test <@ ConstraintCheck.tryText Constraint.optional |> Option.isNone @>
        test <@ ConstraintCheck.tryText (Constraint.create "custom") |> Option.isNone @>
