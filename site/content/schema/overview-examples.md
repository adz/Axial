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

type Address = { City: string }

let addressSchema =
    Schema.recordFor<Address, _> (fun city -> { City = city })
    // A field-level constraint belongs here because it is part of Address at every boundary.
    // Benefit: parsing, JSON Schema, inspection, and test generation see the same minimum.
    |> Schema.fieldWith [ SchemaConstraint.minLength 1 ] "city" _.City Value.text
    |> Schema.build

type Customer =
    { Id: System.Guid
      Address: Address
      Aliases: string list
      Labels: Map<string, string>
      Note: string option }

let customerSchema =
    Schema.recordFor<Customer, _> (fun id address aliases labels note ->
        { Id = id; Address = address; Aliases = aliases; Labels = labels; Note = note })
    |> Schema.guid "id" _.Id
    // nested is appropriate when a field is another model with its own constructor and constraints.
    // Benefit: child diagnostics keep the address path and the child schema stays reusable.
    |> Schema.nested "address" _.Address addressSchema
    // manyOf describes a list of values; count metadata applies to the collection, not each item.
    |> Schema.fieldWith [ SchemaConstraint.maxCount 3 ] "aliases" _.Aliases (Value.manyOf Value.text)
    // map is appropriate for dynamic string keys whose values share one schema.
    |> Schema.field "labels" _.Labels (Value.map Value.text)
    // optionOf makes absence part of the declared wire shape instead of a special parser branch.
    |> Schema.field "note" _.Note (Value.optionOf Value.text)
    |> Schema.build
    // Descriptions are metadata. They do not change parsing.
    |> Schema.describe "A customer accepted at the application boundary."

let documentedText =
    Value.text
    // A format tells metadata consumers what the text represents without adding a check.
    |> Value.withFormat (SchemaFormat.create "account-code")
    |> Value.describe "Stable account code"

module LocalSchemaVocabulary =
    open Axial.Schema.DSL

    // The DSL is appropriate inside one schema-definition module when repeated prefixes obscure field lines.
    // Benefit: it expands to the same Schema, Value, and SchemaConstraint calls; there is only one declaration model.
    let address =
        recordFor<Address, _> (fun city -> { City = city })
        |> text [ minLength 1 ] "city" _.City
        |> build
```

Use `Schema.buildResult` instead of `Schema.build` when the model constructor returns `Result`. It is appropriate for
cross-field invariants; parsing and reconstruction invoke that constructor only after individual fields pass.

## Refined values

```fsharp
open Axial.Refined
open Axial.Schema

type Account = { Name: NonBlankString }

let accountSchema =
    Schema.recordFor<Account, _> (fun name -> { Name = name })
    // A refined schema is appropriate when validity should be carried by the field's own type.
    // Benefit: application code receives NonBlankString, while the boundary still parses ordinary text.
    |> Schema.field "name" _.Name RefinedSchema.nonBlankString
    |> Schema.build
```

## Tagged unions and enums

```fsharp
open Axial.Schema

type Card = { LastFour: string }
type Payment = Card of Card | Invoice of string
type State = Draft | Submitted

let cardSchema =
    Schema.recordFor<Card, _> (fun lastFour -> { LastFour = lastFour })
    |> Schema.fieldWith [ SchemaConstraint.lengthBetween 4 4 ] "lastFour" _.LastFour Value.text
    |> Schema.build

let paymentValue =
    // A discriminator is appropriate when cases have different payload shapes.
    // Benefit: parsing is deterministic and diagnostics can name both the tag and payload fields.
    Value.union "type" "value"
        [ UnionCase.create "card" Card (function Card value -> Some value | _ -> None) (Value.nested cardSchema)
          UnionCase.create "invoice" Invoice (function Invoice value -> Some value | _ -> None) Value.text ]

let stateValue =
    // enumOf is appropriate for payload-free cases represented by one scalar tag.
    // Benefit: the F# DU remains the model type without an object wrapper on the wire.
    Value.enumOf [ EnumCase.create "draft" Draft; EnumCase.create "submitted" Submitted ]

type InlinePayment = InlineCard of Card

let inlinePaymentValue =
    // unionInline is appropriate when every payload is an object and the tag should sit beside its fields.
    // Benefit: the shorter wire object remains deterministic; construction rejects discriminator collisions.
    Value.unionInline "type"
        [ UnionCase.create
              "card"
              InlineCard
              (function InlineCard value -> Some value)
              (Value.nested cardSchema) ]
```

## Recursive models

```fsharp
open Axial.Schema

type Category = { Name: string; Children: Category list }

let categorySchema =
    let rec holder: Lazy<Schema<Category>> =
        lazy
            (Schema.recordFor<Category, _> (fun name children -> { Name = name; Children = children })
             |> Schema.text "name" _.Name
             // lazyOf is appropriate only for an edge that closes a model cycle.
             // Benefit: parsers walk finite data, while Inspect and codecs do not expand the schema forever.
             |> Schema.field "children" _.Children (Value.manyOf (Value.lazyOf (fun () -> holder.Value)))
             |> Schema.build)

    holder.Value
```

## Parsing, redisplay, validation, and reconstruction

```fsharp
open Axial.Schema

let raw = RawInput.ofNameValues [ "id", "not-a-guid"; "address.city", "" ]
let parsed = Model.parse customerSchema raw

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

// validate is appropriate for named-field construction in application code.
// Benefit: Model<Customer> records that the draft passed the schema and constructor.
let trusted: Result<Model<Customer>, _> = Model.validate customerSchema draft

// reconstruct is appropriate for a complete value read from storage or another serializer.
// Benefit: it rechecks fields and invokes the model constructor again without converting through RawInput.
let reconstructed = Model.reconstruct customerSchema draft
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
// Benefit: generated RawInput is checked by Model.parse, including refined construction and constructor invariants.
let rawGenerator = SchemaGen.raw customerSchema |> Result.defaultWith (failwithf "%A")
let samples = Gen.sample 100 rawGenerator

let custom =
    // Pattern reversal is not guessed. Supply the exact field distribution when metadata is insufficient.
    Map.ofList [ "accountCode", Gen.constant (RawInput.Scalar "AX-100") ]

let withAccountCodes = SchemaGen.rawWith custom customerSchema
```
