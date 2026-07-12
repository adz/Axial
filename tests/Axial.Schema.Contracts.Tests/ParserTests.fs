namespace Axial.Schema.Contracts.Tests

open Axial.Schema.Contracts
open Swensen.Unquote
open Xunit

module ParserTests =

    let private parseOk source =
        match Parser.parse "test.contract" source with
        | Ok file -> file
        | Error diagnostics -> failwithf "Expected a clean parse, got %A" diagnostics

    let private parseErrors source =
        match Parser.parse "test.contract" source with
        | Ok file -> failwithf "Expected parse errors, got %A" file
        | Error diagnostics -> diagnostics

    [<Fact>]
    let ``parses a representative contract with every field shape`` () =
        let file =
            parseOk
                """
/// A new account request.
contract Signup.v1 {
  /// Primary contact address.
  email: email [ max 254 ]
  displayName as "display_name"?: text [ min 1, max 64 ]
  age: int [ >= 13 ]
  plan: "free" | "pro" = "free"
  tags: list text [ max 8, distinct ]
  limits: map int
  location?: Geo.v1
}
"""

        let contract = file.Contracts |> List.exactlyOne
        test <@ contract.ContractName = "Signup" @>
        test <@ contract.Version = 1 @>
        test <@ contract.Doc = [ "A new account request." ] @>
        test <@ contract.Fields.Length = 7 @>

        let email = contract.Fields.[0]
        test <@ email.FieldName = "email" @>
        test <@ email.FieldType = Primitive PEmail @>
        test <@ email.Doc = [ "Primary contact address." ] @>
        test <@ email.Constraints |> List.map fst = [ MaxSize 254 ] @>

        let displayName = contract.Fields.[1]
        test <@ displayName.WireName = Some "display_name" @>
        test <@ displayName.Optional @>
        test <@ displayName.Constraints |> List.map fst = [ MinSize 1; MaxSize 64 ] @>

        let age = contract.Fields.[2]
        test <@ age.Constraints |> List.map fst = [ AtLeast(LInt 13) ] @>

        let plan = contract.Fields.[3]
        test <@ plan.FieldType = LiteralUnion [ "free"; "pro" ] @>
        test <@ plan.Default = Some(LString "free") @>

        let tags = contract.Fields.[4]
        test <@ tags.FieldType = ListOf(Primitive PText) @>
        test <@ tags.Constraints |> List.map fst = [ MaxSize 8; Distinct ] @>

        test <@ contract.Fields.[5].FieldType = MapOf(Primitive PInt) @>

        let location = contract.Fields.[6]
        test <@ location.Optional @>
        test <@ location.FieldType = Reference { RefName = "Geo"; RefVersion = 1 } @>

    [<Fact>]
    let ``parses union blocks with contract reference cases`` () =
        let file =
            parseOk
                """
contract Payment.v1 {
  source: union kind {
    card: Card.v1
    invoice: Invoice.v2
  }
}
"""

        let contract = file.Contracts |> List.exactlyOne
        let source = contract.Fields |> List.exactlyOne

        match source.FieldType with
        | UnionBlock(discriminator, cases) ->
            test <@ discriminator = "kind" @>
            test <@ cases |> List.map _.CaseTag = [ "card"; "invoice" ] @>
            test <@ cases.[1].CaseRef = { RefName = "Invoice"; RefVersion = 2 } @>
        | other -> failwithf "Expected a union block, got %A" other

    [<Fact>]
    let ``parses negative numbers exclusive bounds and multipleOf`` () =
        let file =
            parseOk
                """
contract Geo.v1 {
  lat: decimal [ >= -90, < 90.5 ]
  step: int [ > 0, multipleOf 5 ]
}
"""

        let contract = file.Contracts |> List.exactlyOne
        test <@ contract.Fields.[0].Constraints |> List.map fst = [ AtLeast(LInt -90); LessThan(LDecimal 90.5m) ] @>
        test <@ contract.Fields.[1].Constraints |> List.map fst = [ GreaterThan(LInt 0); MultipleOf(LInt 5) ] @>

    [<Fact>]
    let ``parses multiple contracts and annotations from one file`` () =
        let file =
            parseOk
                """
contract Card.v1 {
  number: text [ min 12 ]
}

/// Documented.
contract Payment.v2 {
  @deprecated "use sources"
  card?: Card.v1
}
"""

        test <@ file.Contracts |> List.map (fun contract -> contract.ContractName, contract.Version) = [ "Card", 1; "Payment", 2 ] @>

        let card = file.Contracts.[1].Fields |> List.exactlyOne
        test <@ card.Annotations |> List.map _.AnnotationName = [ "deprecated" ] @>
        test <@ card.Annotations.Head.AnnotationValue = Some(LString "use sources") @>

    [<Fact>]
    let ``reports line-precise diagnostics for malformed field lines`` () =
        let diagnostics =
            parseErrors
                """
contract Broken.v1 {
  good: text
  bad text
}
"""

        let diagnostic = diagnostics |> List.exactlyOne
        test <@ diagnostic.Line = 4 @>
        test <@ diagnostic.Message.Contains "':'" @>

    [<Fact>]
    let ``reports unknown types with a versioned-reference hint`` () =
        let diagnostics =
            parseErrors
                """
contract Broken.v1 {
  size: integer
}
"""

        let diagnostic = diagnostics |> List.exactlyOne
        test <@ diagnostic.Message.Contains "integer.v1" @>

    [<Fact>]
    let ``reports unterminated contracts and union blocks`` () =
        let diagnostics =
            parseErrors
                """
contract Broken.v1 {
  source: union kind {
    card: Card.v1
"""

        test <@ diagnostics |> List.exists (fun diagnostic -> diagnostic.Message.Contains "union block") @>
        test <@ diagnostics |> List.exists (fun diagnostic -> diagnostic.Message.Contains "never closed") @>

    [<Theory>]
    [<InlineData("contract Huge.v999999999999 {\n  value: int\n}")>]
    [<InlineData("contract Huge.v1 {\n  value: int [ >= 999999999999 ]\n}")>]
    [<InlineData("contract Huge.v1 {\n  value: decimal [ >= 999999999999999999999999999999999999 ]\n}")>]
    [<InlineData("contract Huge.v1 {\n  value: text [ min 999999999999 ]\n}")>]
    let ``numeric overflow is reported instead of escaping the Result API`` source =
        let diagnostics = parseErrors source
        test <@ diagnostics |> List.exists (fun diagnostic -> diagnostic.Message.Contains "out of range") @>
