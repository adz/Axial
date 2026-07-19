// The generated-wire reference slice: the same boundary discipline as Axial.ReferenceApp,
// but the wire tier is generated from [<DeriveSchema>] records instead of hand-written.
//
// wire/workspace.fs   — user-owned records with constraint attributes (you edit this)
// wire/workspace.g.fs — schemas, parse/validate, Fields, and the contract builder (generated)
//
// This program supplies the only hand-written parts: the v1 -> v2 migration, the strict
// domain mapping, and the head-version write path through a compiled codec.

open System.Text.Json
open Axial.Schema
open Axial.Codec
open Axial.ReferenceApp.Wire

// ---------------------------------------------------------------------------
// Migration: v1 stored one "Name <email>" owner string; v2 wants the email.
// ---------------------------------------------------------------------------

let migrateV1ToV2 (v1: WorkspaceCardV1) : Result<WorkspaceCard, MigrationError> =
    let owner = v1.Owner.Trim()
    let openAngle = owner.IndexOf '<'
    let closeAngle = owner.IndexOf '>'

    if openAngle >= 0 && closeAngle > openAngle then
        Ok
            { Name = v1.Name
              OwnerEmail = owner.Substring(openAngle + 1, closeAngle - openAngle - 1)
              Visibility = Visibility.Private
              Members = [] }
    elif owner.Contains "@" then
        Ok { Name = v1.Name; OwnerEmail = owner; Visibility = Visibility.Private; Members = [] }
    else
        Error(MigrationError.MigrationFailed $"no email address in owner '{v1.Owner}'")

/// The versioned contract, assembled by the generated builder.
let cardContract : Contract<WorkspaceCard> =
    WorkspaceCard.contract migrateV1ToV2 (VersionSource.Field "schemaVersion")

// ---------------------------------------------------------------------------
// Domain mapping: the wire record stays permissive; strictness lives here.
// ---------------------------------------------------------------------------

type TrustedCard = private { Card: WorkspaceCard }

module TrustedCard =
    /// Business rule beyond wire shape: the owner may not also be listed as a member.
    let ofWire (wire: WorkspaceCard) : Result<TrustedCard, string> =
        if wire.Members |> List.contains wire.OwnerEmail then
            Error $"owner {wire.OwnerEmail} must not appear in members"
        else
            Ok { Card = wire }

    let card (trusted: TrustedCard) = trusted.Card

// ---------------------------------------------------------------------------
// Demo driver.
// ---------------------------------------------------------------------------

let private parseJson (json: string) : RawInput =
    use document = JsonDocument.Parse json
    RawInput.ofJsonDocument document

let private showContractParse (label: string) (json: string) =
    printfn "%s" label

    match Contract.parse cardContract (parseJson json) with
    | Ok card -> printfn "  parsed to current version: %A\n" card
    | Error failure -> printfn "  rejected: %A\n" failure

[<EntryPoint>]
let main _ =
    // A v1 payload written before visibility or member lists existed.
    showContractParse
        "v1 payload (owner string migrates to owner_email):"
        """{ "schemaVersion": 1, "name": "Delivery", "owner": "Ada Lovelace <ada@example.org>" }"""

    // A v1 payload whose migration fails: no email to extract.
    showContractParse
        "v1 payload without an email (typed migration failure):"
        """{ "schemaVersion": 1, "name": "Delivery", "owner": "Ada Lovelace" }"""

    // A current payload parses against the head schema directly.
    showContractParse
        "v2 payload (current version):"
        """{ "schemaVersion": 2, "name": "Delivery", "owner_email": "ada@example.org",
             "visibility": "team", "members": ["grace@example.org"] }"""

    // Constraint attributes became schema constraints: bad email, duplicate members.
    showContractParse
        "v2 payload violating generated constraints:"
        """{ "schemaVersion": 2, "name": "Delivery", "owner_email": "not-an-email",
             "members": ["grace@example.org", "grace@example.org"] }"""

    // Strictness beyond the wire lives in the domain map, not in the generated record.
    let wire =
        { Name = "Delivery"
          OwnerEmail = "ada@example.org"
          Visibility = Visibility.Team
          Members = [ "ada@example.org" ] }

    printfn "domain map rejects what the wire allows:\n  %A\n" (TrustedCard.ofWire wire)

    // The generated schema is an ordinary Schema: JSON Schema and codecs come along.
    let codec = Json.compile WorkspaceCard.schema
    let head = { wire with Members = [ "grace@example.org" ] }
    printfn "head-version write through the compiled codec:\n  %s\n" (Json.serialize codec head)

    printfn "generated JSON Schema for the current version:\n%s" (JsonSchema.generate WorkspaceCard.schema)
    0
