namespace Axial.Schema.Contracts.Tests

open System.IO
open Axial.Schema.Contracts
open Swensen.Unquote
open Xunit

/// The corpus under tests/Axial.Schema.Tests/contracts is the emitter's contract: the checked-in
/// .g.fs files compile into Axial.Schema.Tests and pass behavior tests there, and these tests prove
/// the emitter reproduces them byte for byte, so generator changes cannot silently drift the shape.
module EmitterGoldenTests =

    let private corpusDirectory () =
        Path.Combine(__SOURCE_DIRECTORY__, "..", "Axial.Schema.Tests", "contracts")
        |> Path.GetFullPath

    let private parseCorpusFile name =
        let path = Path.Combine(corpusDirectory (), name)

        let result =
            if name.EndsWith ".contract" then
                Parser.parse path (File.ReadAllText path)
            else
                Records.parse SchemaNaming.CamelCase path (File.ReadAllText path)

        match result with
        | Ok file -> file
        | Error diagnostics -> failwithf "Corpus file %s failed to parse: %A" name diagnostics

    [<Fact>]
    let ``the corpus resolves cleanly as one generation set`` () =
        let files =
            Directory.EnumerateFiles(corpusDirectory (), "*.*")
            |> Seq.filter (fun path -> path.EndsWith ".contract" || (path.EndsWith ".fs" && not (path.EndsWith ".g.fs")))
            |> Seq.sort
            |> Seq.map (Path.GetFileName >> parseCorpusFile)
            |> List.ofSeq

        test <@ Resolver.resolve files = [] @>

    [<Theory>]
    [<InlineData("geo.contract", "geo.g.fs")>]
    [<InlineData("signup.contract", "signup.g.fs")>]
    [<InlineData("payment.contract", "payment.g.fs")>]
    [<InlineData("category.contract", "category.g.fs")>]
    [<InlineData("profile.contract", "profile.g.fs")>]
    [<InlineData("shipment.fs", "shipment.g.fs")>]
    let ``the emitter reproduces every checked-in golden file byte for byte`` (contractName: string) (goldenName: string) =
        let file = parseCorpusFile contractName
        let emitted = Emitter.emit "Axial.Tests.Generated" [ file ] file

        let golden =
            (File.ReadAllText(Path.Combine(corpusDirectory (), goldenName))).Replace("\r\n", "\n")

        test <@ emitted = golden @>

    [<Fact>]
    let ``reserved F# keywords are escaped in generated bindings`` () =
        let parsed =
            match
                Parser.parse "keywords.contract"
                    """
contract Kw.v1 {
  type: text
  method: int
}
"""
            with
            | Ok file -> file
            | Error diagnostics -> failwithf "Expected a clean parse, got %A" diagnostics

        let emitted = Emitter.emit "Ns" [ parsed ] parsed

        test <@ emitted.Contains "fun ``type`` ``method``" @>
        test <@ emitted.Contains "let ``type`` : FieldRef<Kw, string>" @>
        test <@ emitted.Contains "Type: string" @>
        test <@ emitted.Contains "Method: int" @>

    [<Fact>]
    let ``a schema constructor replaces the record literal in recordFor`` () =
        let parsed =
            match
                Records.parse SchemaNaming.CamelCase "wire.fs"
                    """
namespace My.Wire

open Axial.Schema.Derive

[<DeriveSchema; SchemaConstructor "Order.create">]
type Order = { Sku: string; Quantity: int }
"""
            with
            | Ok file -> file
            | Error diagnostics -> failwithf "Expected a clean parse, got %A" diagnostics

        let emitted = Emitter.emit "Ns" [ parsed ] parsed

        test <@ emitted.Contains "Schema.recordFor<Order, _> (fun sku quantity -> Order.create sku quantity)" @>
        test <@ not (emitted.Contains "{ Sku = sku") @>
