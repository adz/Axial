namespace Axial.Refined.Tests

open Axial.Check
open Axial.Refined
open Swensen.Unquote
open Xunit

module CatalogTests =
    [<Fact>]
    let ``named refined constructors return check failures directly`` () =
        test <@ Refine.nonBlankString " " = Error [ Blank ] @>

    [<Fact>]
    let ``collection refinements use canonical concrete representations`` () =
        let list = Refine.nonEmptyList [ 1; 2 ] |> Result.defaultWith (failwithf "%A")
        let array = Refine.nonEmptyArray [ 1; 2 ] |> Result.defaultWith (failwithf "%A")
        test <@ list.ToList() = [ 1; 2 ] @>
        test <@ array.ToArray() = [| 1; 2 |] @>

    [<Fact>]
    let ``choice remains an ordinary conversion combinator`` () =
        let parseInt (text: string) = match System.Int32.TryParse text with true, value -> Ok value | _ -> Error "int"
        let result = Choice.orElse id parseInt String.length Ok "neither" "42"
        test <@ result = Ok 42 @>
