namespace Axial.ReferenceApp.Tests

open System.Text.Json
open Axial.Codec
open Axial.ReferenceApp
open Axial.Schema
open Axial.Schema.Testing
open FsCheck.FSharp
open Swensen.Unquote
open Xunit

/// Schema-derived test data: the same declaration that parses boundary input also
/// generates valid boundary input, so property tests need no hand-written fixtures.
module SchemaGenTests =

    [<Fact>]
    let ``schema-generated raw inputs parse through the workspace schema`` () =
        let generator = SchemaGen.raw Contracts.workspaceV2 |> Result.defaultWith (failwithf "%A")
        let inputs = Gen.sample 100 generator

        test <@ inputs |> Array.forall (fun input -> (Schema.parse Contracts.workspaceV2 input |> Result.isOk)) @>

    [<Fact>]
    let ``schema-generated models survive a codec round-trip`` () =
        let generator = SchemaGen.model Contracts.workspaceV2 |> Result.defaultWith (failwithf "%A")
        let codec = Json.compile Contracts.workspaceV2

        let roundTrips (model: WorkspaceV2) =
            let json = Json.serialize codec model
            use document = JsonDocument.Parse json
            let reparsed = Schema.parse Contracts.workspaceV2 (RawInput.ofJsonDocument document)
            reparsed = Ok model

        test <@ Gen.sample 50 generator |> Array.forall roundTrips @>

    [<Fact>]
    let ``schema-generated models satisfy the domain mapping`` () =
        let generator = SchemaGen.model Contracts.workspaceV2 |> Result.defaultWith (failwithf "%A")

        // Every value the schema admits must be representable in the domain: toDomain is total
        // over schema-valid wire values, so generation cannot invent an unmappable state.
        let models = Gen.sample 50 generator
        test <@ models |> Array.map Contracts.toDomain |> Array.length = models.Length @>
