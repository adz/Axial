---
weight: 3
title: Schema Overview Examples
description: Short examples covering every Schema subsystem and the reason to use each one.
type: docs
---


This page shows each part of the Schema system in a small example, with comments stating when the part is appropriate
and what it preserves.

The examples are a map, not a replacement for the focused guides or API reference.

## Declarations, values, constraints, and formats

```fsharp
open Axial.Schema
open Axial.Schema.DSL

type Address = { City: string }

let addressSchema =
    recordFor<Address, _> (fun city -> { City = city })
    // A field-level constraint belongs here because it is part of Address at every boundary.
    // Benefit: parsing, JSON Schema, inspection, and test generation see the same minimum.
    |> field "city" _.City (text |> constrain (minLength 1))
    |> build

type Customer =
    { Id: System.Guid
      Address: Address
      Aliases: string list
      Labels: Map<string, string>
      Note: string option }

let customerSchema =
    recordFor<Customer, _> (fun id address aliases labels note ->
        { Id = id; Address = address; Aliases = aliases; Labels = labels; Note = note })
    |> field "id" _.Id guid
    // A nested field is appropriate when it is another model with its own constructor and constraints.
    // Benefit: child diagnostics keep the address path and the child schema stays reusable.
    |> field "address" _.Address addressSchema
    // list describes a collection of values; count metadata applies to the collection, not each item.
    |> field "aliases" _.Aliases (list text |> constrain (maxCount 3))
    // map is appropriate for dynamic string keys whose values share one schema.
    |> field "labels" _.Labels (map text)
    // option makes absence part of the declared wire shape instead of a special parser branch.
    |> field "note" _.Note (option text)
    |> build
    // Descriptions are metadata. They do not change parsing.
    |> describe "A customer accepted at the application boundary."

let documentedText =
    text
    // A format tells metadata consumers what the text represents without adding a check.
    |> withFormat (SchemaFormat.create "account-code")
    |> describe "Stable account code"
```

The unqualified vocabulary above comes from `Axial.Schema.DSL`; it expands to the same `Schema.*` and `Constraint.*`
calls, so there is only one declaration model. Open it inside the module that owns schema declarations — names such
as `int` and `decimal` shadow FSharp.Core conversion functions, so qualified `Schema.*` calls stay preferable in
general application code.

Use `Schema.buildResult` instead of `Schema.build` when the model constructor returns `Result`. It is appropriate for
cross-field invariants; parsing and reconstruction invoke that constructor only after individual fields pass.

## Refined values

```fsharp
open Axial.Refined
open Axial.Schema
open Axial.Schema.DSL

type Account = { Name: NonBlankString }

let accountSchema =
    recordFor<Account, _> (fun name -> { Name = name })
    // A refined schema is appropriate when validity should be carried by the field's own type.
    // Benefit: application code receives NonBlankString, while the boundary still parses ordinary text.
    |> field "name" _.Name RefinedSchemas.nonBlankString
    |> build
```

## Tagged unions and enums

```fsharp
open Axial.Schema
open Axial.Schema.DSL

type Card = { LastFour: string }
type Payment = Card of Card | Invoice of string
type State = Draft | Submitted

let cardSchema =
    recordFor<Card, _> (fun lastFour -> { LastFour = lastFour })
    |> field "lastFour" _.LastFour (text |> constrain (lengthBetween 4 4))
    |> build

let paymentValue =
    // A discriminator is appropriate when cases have different payload shapes.
    // Benefit: parsing is deterministic and diagnostics can name both the tag and payload fields.
    union "type" "value"
        [ UnionCase.create "card" Card (function Card value -> Some value | _ -> None) cardSchema
          UnionCase.create "invoice" Invoice (function Invoice value -> Some value | _ -> None) text ]

let stateValue =
    // enum is appropriate for payload-free cases represented by one scalar tag.
    // Benefit: the F# DU remains the model type without an object wrapper on the wire.
    enum [ EnumCase.create "draft" Draft; EnumCase.create "submitted" Submitted ]

type InlinePayment = InlineCard of Card

let inlinePaymentValue =
    // inlineUnion is appropriate when every payload is an object and the tag should sit beside its fields.
    // Benefit: the shorter wire object remains deterministic; construction rejects discriminator collisions.
    inlineUnion "type"
        [ UnionCase.create
              "card"
              InlineCard
              (function InlineCard value -> Some value)
              cardSchema ]
```

## Recursive models

```fsharp
open Axial.Schema
open Axial.Schema.DSL

type Category = { Name: string; Children: Category list }

let categorySchema =
    let rec holder: Lazy<Schema<Category>> =
        lazy
            (recordFor<Category, _> (fun name children -> { Name = name; Children = children })
             |> field "name" _.Name text
             // defer is appropriate only for an edge that closes a model cycle.
             // Benefit: parsers walk finite data, while Inspect and codecs do not expand the schema forever.
             |> field "children" _.Children (list (defer (fun () -> holder.Value)))
             |> build)

    holder.Value
```

## Parsing, redisplay, validation, and reconstruction

```fsharp
open Axial.Schema

let raw = RawInput.ofNameValues [ "id", "not-a-guid"; "address.city", "" ]
let parsed = Schema.parse customerSchema raw

match parsed.Result with
| Ok customer -> useCustomer customer
| Error _ ->
    // ParsedInput is appropriate at an interactive boundary.
    // Benefit: all path-aware errors and the rejected raw values remain together for redisplay.
    let idErrors = parsed.ErrorsFor "id"
    let originalId = RawInput.tryRedisplayPath "id" parsed.Input
    showErrors idErrors originalId

let draft =
    { Id = System.Guid.NewGuid(); Address = { City = "Adelaide" }; Aliases = []; Labels = Map.empty; Note = None }

// check is appropriate when an assembled value arrived through an uncertain path.
// The successful Result records this operation's trust decision; it is not a durable proof wrapper.
let checked: Result<Customer, _> = Schema.check customerSchema draft

// It rechecks fields and invokes the record schema's constructor without converting through RawInput.
let imported = Schema.check customerSchema draft
```

## Field references and contextual rules

```fsharp
open Axial.Schema

let cityField: FieldRef<Address, string> =
    { Name = "city"
      Get = _.City
      // Set copy-updates a draft. It deliberately does not claim that schema trust still holds.
      Set = fun draft value -> { draft with City = value } }

let renamed = cityField.Set { City = "Adelaide" } "Tarntanya"

let customerAddressField: FieldRef<Customer, Address> =
    { Name = "address"
      Get = _.Address
      Set = fun customer value ->
          { customer with Address = value } }

let deliveryRules =
    [ fun customer ->
        if customer.Address.City = "" then
            // ContextRules is appropriate when a requirement depends on the current operation.
            // Benefit: the intrinsic schema stays stable and the failure still uses a schema-owned path.
            ContextRules.failAtField customerAddressField "delivery requires a city"
        else
            Ok () ]

let deliveryResult = ContextRules.apply deliveryRules draft
```

## Inspection, JSON Schema, and compiled JSON

```fsharp
open Axial.Codec
open Axial.Schema

// Inspect is appropriate for documentation and UI metadata that must not run parsing or checks.
// Benefit: it returns an immutable, finite description tree, including recursion markers.
let description = Inspect.model customerSchema

// JsonSchema is appropriate for publishing the boundary contract to other tools.
// Benefit: it lowers the same names, constraints, descriptions, unions, and recursive references.
let jsonSchemaDocument = JsonSchema.generate customerSchema

// A compiled codec is appropriate for trusted storage or service-to-service hot paths.
// Benefit: it is reflection-free and reusable. It checks wire shape, not Schema constraints.
let codec = Json.compile customerSchema
let json = Json.serialize codec draft
let decoded = Json.deserialize codec json
```

## Versioned contracts

```fsharp
open Axial.Schema

type ConfigV1 = { Version: int; Host: string }
type Config = { Version: int; Host: string; Port: int }

let configContract =
    Contract.create "device-config" 2 configSchema
    // supersedes is appropriate for a frozen older wire record with an explicit typed migration.
    // Benefit: business code receives only Config; representation history stays at the boundary.
    |> Contract.supersedes 1 configV1Schema (fun old ->
        Ok { Version = 2; Host = old.Host; Port = 443 })
    |> Contract.build (VersionSource.Field "version")

match Contract.parse configContract rawConfig with
| Ok trustedConfig -> applyConfig trustedConfig.Value
| Error (ContractError.VersionTooNew(detected, supported)) -> reportFleetSkew detected supported
| Error rejection -> reportContractRejection rejection
```

Use `VersionSource.External` with `Contract.parseWithVersion` when the version comes from a message header or event
metadata. `VersionSource.UnversionedMeans` is for one registered legacy format whose payload has no version field.

## Generated wire contracts

```fsharp
// category.contract
// A .contract file is appropriate when many wire records repeat the same mechanical type/schema code.
// Benefit: schemagen emits checked-in F# with a record, schema, parse/validate functions, and FieldRef lenses.
//
// contract Category.v1 {
//   name: text [ min 1 ]
//   children: list Category.v1
// }
```

Migrations stay in F#. The contract language describes wire shape; it does not contain application expressions.

## Schema-derived test data

```fsharp
open Axial.Schema.Testing
open FsCheck.FSharp

// SchemaGen is appropriate for property tests that need valid boundary inputs rather than arbitrary records.
// Benefit: generated RawInput is checked by Schema.parse, including refined construction and constructor invariants.
let rawGenerator = SchemaGen.raw customerSchema |> Result.defaultWith (failwithf "%A")
let samples = Gen.sample 100 rawGenerator

let custom =
    // Pattern reversal is not guessed. Supply the exact field distribution when metadata is insufficient.
    Map.ofList [ "accountCode", Gen.constant (RawInput.Scalar "AX-100") ]

let withAccountCodes = SchemaGen.rawWith custom customerSchema
```
