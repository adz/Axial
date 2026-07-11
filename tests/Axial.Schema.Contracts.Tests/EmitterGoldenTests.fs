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

    let private emitCorpusFile name =
        let contractPath = Path.Combine(corpusDirectory (), name)

        let parsed =
            match Parser.parse contractPath (File.ReadAllText contractPath) with
            | Ok file -> file
            | Error diagnostics -> failwithf "Corpus file %s failed to parse: %A" name diagnostics

        parsed

    [<Fact>]
    let ``the corpus resolves cleanly as one generation set`` () =
        let files =
            Directory.EnumerateFiles(corpusDirectory (), "*.contract")
            |> Seq.sort
            |> Seq.map (Path.GetFileName >> emitCorpusFile)
            |> List.ofSeq

        test <@ Resolver.resolve files = [] @>

    [<Theory>]
    [<InlineData("geo.contract", "geo.g.fs")>]
    [<InlineData("signup.contract", "signup.g.fs")>]
    [<InlineData("payment.contract", "payment.g.fs")>]
    let ``the emitter reproduces every checked-in golden file byte for byte`` (contractName: string) (goldenName: string) =
        let emitted = Emitter.emit "Axial.Tests.Generated" (emitCorpusFile contractName)

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

        let emitted = Emitter.emit "Ns" parsed

        test <@ emitted.Contains "fun ``type`` ``method``" @>
        test <@ emitted.Contains "let ``type`` : FieldRef<Kw, string>" @>
        test <@ emitted.Contains "Type: string" @>
        test <@ emitted.Contains "Method: int" @>
