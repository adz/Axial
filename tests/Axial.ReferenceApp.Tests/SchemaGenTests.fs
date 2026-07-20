namespace Axial.ReferenceApp.Tests

open Axial

open System.Text.Json
open Axial.Schema.Codec
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
    let ``schema-generated structured datas parse through the workspace schema`` () =
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
            let reparsed = Schema.parse Contracts.workspaceV2 (Data.ofJsonDocument document)
            reparsed = Ok model

        test <@ Gen.sample 50 generator |> Array.forall roundTrips @>
