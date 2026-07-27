namespace Axial.Refined.Tests

open Axial.Check
open Axial.Refined
open Swensen.Unquote
open Xunit

module RefinementTests =
    type CustomerId = private CustomerId of int with member this.Value = let (CustomerId value) = this in value

    let private customerId = Refinement.define (Constraint.greaterThan 0) CustomerId _.Value

    [<Fact>]
    let ``create checks before construction and returns check failures directly`` () =
        test <@ Refinement.create customerId 2 |> Result.map _.Value = Ok 2 @>
        test <@ Refinement.create customerId 0 = Error [ OutOfRange(CheckRangeExpectation.GreaterThan "0", Some "0") ] @>

    [<Fact>]
    let ``successful refinement projects to its original underlying value`` () =
        let refined = Refinement.create customerId 3 |> Result.defaultWith (failwithf "%A")
        test <@ Refinement.underlying customerId refined = 3 @>

    [<Fact>]
    let ``define retains constraints and defineWithCheck does not`` () =
        test <@ Refinement.constraints customerId |> List.map (Constraint.details >> _.Code) = [ "greaterThan" ] @>
        let metadataFree = Refinement.defineWithCheck Check.String.present id id
        test <@ Refinement.constraints metadataFree = [] @>

    [<Fact>]
    let ``defineAll rejects an empty constraint list`` () =
        raises<System.ArgumentException> <@ Refinement.defineAll [] id id @>

    [<Fact>]
    let ``defineAll accumulates failures against the same value`` () =
        let refinement = Refinement.defineAll [ Constraint.minLength 3; Constraint.pattern "^[a-z]+$" ] id id
        match Refinement.create refinement "1" with
        | Error failures -> test <@ failures.Length = 2 @>
        | Ok _ -> failwith "Expected failures."
