namespace Axial.Schema.Contracts.Tests

open Axial.Schema.Contracts
open Swensen.Unquote
open Xunit

/// Specs for the record frontend: F# source with [<WireSchema>] records lowering into the shared
/// ContractDecl AST. Parsing is syntax-only, so these tests feed source strings directly.
module RecordsTests =

    let private parse source =
        match Records.parse WireNaming.CamelCase "wire.fs" source with
        | Ok file -> file
        | Error diagnostics -> failwithf "Expected a clean parse, got %A" diagnostics

    let private parseErrors source =
        match Records.parse WireNaming.CamelCase "wire.fs" source with
        | Ok file -> failwithf "Expected diagnostics, got a clean parse of %d contracts" (List.length file.Contracts)
        | Error diagnostics -> diagnostics |> List.map _.Message

    [<Fact>]
    let ``a bare marked record lowers to a shape-only version-1 contract`` () =
        let file =
            parse
                """
namespace My.Wire

open Axial.Schema.Wire

[<WireSchema>]
type Order = { Sku: string; Quantity: int }
"""

        test <@ file.Namespace = Some "My.Wire" @>

        match file.Contracts with
        | [ contract ] ->
            test <@ contract.ContractName = "Order" @>
            test <@ contract.Version = 1 @>
            test <@ not contract.OwnsType @>
            test <@ contract.ExternalTypeName = Some "Order" @>
            test <@ contract.Fields |> List.map _.FieldName = [ "Sku"; "Quantity" ] @>
            test <@ contract.Fields |> List.map FieldDecl.wireName = [ "sku"; "quantity" ] @>
        | contracts -> failwithf "Expected one contract, got %A" contracts

    [<Fact>]
    let ``unmarked records and other declarations are ignored`` () =
        let file =
            parse
                """
namespace My.Wire

open Axial.Schema.Wire

type NotWire = { A: int }

[<WireSchema>]
type Order = { Sku: string }

module Helpers =
    let f x = x + 1
"""

        test <@ file.Contracts |> List.map _.ContractName = [ "Order" ] @>

    [<Fact>]
    let ``attributes lower to the shared constraint vocabulary with exact decimals`` () =
        let file =
            parse
                """
namespace My.Wire

open Axial.Schema.Wire

[<WireSchema>]
type Order =
    { [<Pattern "^SKU-">] Sku: string
      [<AtLeast 0.5>] Weight: decimal
      [<Min 1; Distinct>] Tags: string list
      [<Default 3; AtMost 10>] Boxes: int
      [<Email>] Contact: string }
"""

        let fields = file.Contracts.Head.Fields
        let byName name = fields |> List.find (fun field -> field.FieldName = name)

        test <@ (byName "Sku").Constraints |> List.map fst = [ Pattern "^SKU-" ] @>
        test <@ (byName "Weight").Constraints |> List.map fst = [ AtLeast(LDecimal 0.5m) ] @>
        test <@ (byName "Tags").Constraints |> List.map fst = [ MinSize 1; Distinct ] @>
        test <@ (byName "Boxes").Constraints |> List.map fst = [ AtMost(LInt 10) ] @>
        test <@ (byName "Boxes").Default = Some(LInt 3) @>
        test <@ (byName "Contact").FieldType = Primitive PEmail @>

    [<Fact>]
    let ``option fields become optional and doc comments carry through`` () =
        let file =
            parse
                """
namespace My.Wire

open Axial.Schema.Wire

/// An order.
[<WireSchema>]
type Order =
    { /// Optional customer note.
      Note: string option }
"""

        let contract = file.Contracts.Head
        test <@ contract.Doc = [ "An order." ] @>
        test <@ contract.Fields.Head.Optional @>
        test <@ contract.Fields.Head.FieldType = Primitive PText @>
        test <@ contract.Fields.Head.Doc = [ "Optional customer note." ] @>

    [<Fact>]
    let ``the XxxVn convention builds a chain only when the bare name is marked`` () =
        let chained =
            parse
                """
namespace My.Wire

open Axial.Schema.Wire

[<WireSchema>]
type OrderV1 = { Sku: string }

[<WireSchema>]
type Order = { Sku: string; Quantity: int }
"""

        test <@ chained.Contracts |> List.map (fun c -> c.ContractName, c.Version) = [ "Order", 1; "Order", 2 ] @>

        let standalone =
            parse
                """
namespace My.Wire

open Axial.Schema.Wire

[<WireSchema>]
type ApolloV2 = { Crew: int }
"""

        test <@ standalone.Contracts |> List.map (fun c -> c.ContractName, c.Version) = [ "ApolloV2", 1 ] @>

    [<Fact>]
    let ``chain attribute arguments override the naming convention`` () =
        let file =
            parse
                """
namespace My.Wire

open Axial.Schema.Wire

[<WireSchema(Chain = "Order", Version = 1)>]
type LegacyOrder = { Sku: string }

[<WireSchema(Chain = "Order", Version = 2)>]
type Order = { Sku: string; Quantity: int }
"""

        let contracts = file.Contracts
        test <@ contracts |> List.map (fun c -> c.ContractName, c.Version, c.ExternalTypeName) = [ "Order", 1, Some "LegacyOrder"; "Order", 2, Some "Order" ] @>

    [<Fact>]
    let ``nullary unions lower to enums and tagged unions to inline unions`` () =
        let file =
            parse
                """
namespace My.Wire

open Axial.Schema.Wire

[<WireSchema>]
type Card = { Number: string }

[<WireSchema>]
type Invoice = { Reference: string }

[<WireUnion "kind">]
type Source =
    | ByCard of Card
    | ByInvoice of Invoice

type Plan =
    | Free
    | [<WireName "super-pro">] Pro

[<WireSchema>]
type Signup = { Plan: Plan; Source: Source }
"""

        let fields = file.Contracts |> List.find (fun c -> c.ContractName = "Signup") |> _.Fields

        match (fields |> List.find (fun f -> f.FieldName = "Plan")).FieldType with
        | ExternalEnum(typeName, cases) ->
            test <@ typeName = "Plan" @>
            test <@ cases = [ { EnumTag = "free"; EnumFsCase = "Free" }; { EnumTag = "super-pro"; EnumFsCase = "Pro" } ] @>
        | other -> failwithf "Expected an enum, got %A" other

        match (fields |> List.find (fun f -> f.FieldName = "Source")).FieldType with
        | ExternalUnion(typeName, discriminator, cases) ->
            test <@ typeName = "Source" @>
            test <@ discriminator = "kind" @>
            test <@ cases |> List.map (fun c -> c.ExtTag, c.ExtFsCase, c.ExtRef.RefName) = [ "byCard", "ByCard", "Card"; "byInvoice", "ByInvoice", "Invoice" ] @>
        | other -> failwithf "Expected a tagged union, got %A" other

    [<Fact>]
    let ``unsupported field types are rejected with guidance`` () =
        let messages =
            parseErrors
                """
namespace My.Wire

open Axial.Schema.Wire

[<WireSchema>]
type Order =
    { Weight: float
      Count: int64
      Rows: int[]
      Pair: int * int }
"""

        test <@ messages |> List.exists (fun m -> m.Contains "'decimal'") @>
        test <@ messages |> List.exists (fun m -> m.Contains "'int'") @>
        test <@ messages |> List.exists (fun m -> m.Contains "use 'list'") @>
        test <@ messages |> List.exists (fun m -> m.Contains "unsupported wire field type") @>

    [<Fact>]
    let ``marked records must be public namespace-level records`` () =
        let privateRecord =
            parseErrors
                """
namespace My.Wire

open Axial.Schema.Wire

[<WireSchema>]
type Order = private { Sku: string }
"""

        test <@ privateRecord |> List.exists (fun m -> m.Contains "must be public") @>

        let nested =
            parseErrors
                """
namespace My.Wire

open Axial.Schema.Wire

module Inner =
    [<WireSchema>]
    type Order = { Sku: string }
"""

        test <@ nested |> List.exists (fun m -> m.Contains "namespace level") @>

        let topLevelModule =
            parseErrors
                """
module My.Wire

open Axial.Schema.Wire

[<WireSchema>]
type Order = { Sku: string }
"""

        test <@ topLevelModule |> List.exists (fun m -> m.Contains "namespace declaration") @>

    [<Fact>]
    let ``generic records and non-records cannot be marked`` () =
        let generic =
            parseErrors
                """
namespace My.Wire

open Axial.Schema.Wire

[<WireSchema>]
type Box<'t> = { Value: string }
"""

        test <@ generic |> List.exists (fun m -> m.Contains "cannot be generic") @>

        let union =
            parseErrors
                """
namespace My.Wire

open Axial.Schema.Wire

[<WireSchema>]
type Choice =
    | A
    | B
"""

        test <@ union |> List.exists (fun m -> m.Contains "unions participate as field types") @>

    [<Fact>]
    let ``references stay within the file and unions need proper payloads`` () =
        let unknownReference =
            parseErrors
                """
namespace My.Wire

open Axial.Schema.Wire

[<WireSchema>]
type Order = { Location: Elsewhere }
"""

        test <@ unknownReference |> List.exists (fun m -> m.Contains "unknown wire field type 'Elsewhere'") @>

        let badUnionPayload =
            parseErrors
                """
namespace My.Wire

open Axial.Schema.Wire

[<WireUnion "kind">]
type Source =
    | Inline of string

[<WireSchema>]
type Order = { Source: Source }
"""

        test <@ badUnionPayload |> List.exists (fun m -> m.Contains "exactly one [<WireSchema>] record payload") @>

    [<Fact>]
    let ``a self-referencing record emits a deferred schema`` () =
        let file =
            parse
                """
namespace My.Wire

open Axial.Schema.Wire

[<WireSchema>]
type Category =
    { Name: string
      Children: Category list }
"""

        test <@ Resolver.resolve [ file ] = [] @>
        let emitted = Emitter.emit "Fallback" [ file ] file
        test <@ emitted.Contains "let rec schema" @>
        test <@ emitted.Contains "Schema.list (Schema.defer (fun () -> schema))" @>
        test <@ not (emitted.Contains "type Category") @>

    [<Fact>]
    let ``chain overrides emit against the user's actual type names`` () =
        let file =
            parse
                """
namespace My.Wire

open Axial.Schema.Wire

[<WireSchema(Chain = "Order", Version = 1)>]
type LegacyOrder = { Sku: string }

[<WireSchema(Chain = "Order", Version = 2)>]
type Order = { Sku: string; Quantity: int }
"""

        test <@ Resolver.resolve [ file ] = [] @>
        let emitted = Emitter.emit "Fallback" [ file ] file
        test <@ emitted.Contains "module LegacyOrder" @>
        test <@ emitted.Contains "Schema<LegacyOrder>" @>
        test <@ emitted.Contains "(migrateV1ToV2: LegacyOrder -> Result<Order, MigrationError>)" @>
        test <@ emitted.Contains "|> Contract.supersedes 1 LegacyOrder.schema migrateV1ToV2" @>
        test <@ emitted.Contains "namespace My.Wire" @>

    [<Fact>]
    let ``marked records outside the file's first namespace are rejected`` () =
        let messages =
            parseErrors
                """
namespace My.Wire

open Axial.Schema.Wire

[<WireSchema>]
type Order = { Sku: string }

namespace My.Other

open Axial.Schema.Wire

[<WireSchema>]
type Stray = { A: int }
"""

        test <@ messages |> List.exists (fun m -> m.Contains "'Stray'" && m.Contains "one namespace per wire file") @>

    [<Fact>]
    let ``snake case naming policy applies to fields and enum tags`` () =
        let file =
            match
                Records.parse WireNaming.SnakeCase "wire.fs"
                    """
namespace My.Wire

open Axial.Schema.Wire

[<WireSchema>]
type Order = { MarketingOptIn: bool }
"""
            with
            | Ok file -> file
            | Error diagnostics -> failwithf "Expected a clean parse, got %A" diagnostics

        test <@ file.Contracts.Head.Fields |> List.map FieldDecl.wireName = [ "marketing_opt_in" ] @>
