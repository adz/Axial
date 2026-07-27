module RefinedCatalogExample

open Axial.Parse

open System
open Axial.Result
open Axial.Check
open Axial.Check.CheckDSL
open Axial.Refined

type ProductId = ProductId of NonZeroInt
type ProductSlug = ProductSlug of Slug
type DisplayName = DisplayName of NonBlankString
type ProductTags = ProductTags of DistinctList<Slug>
type Quantity = Quantity of PositiveInt
type ContactEmail = private ContactEmail of string
type Sku = private Sku of string
type Rating = private Rating of int
type UnitPrice = private UnitPrice of decimal

module ContactEmail =
    let value (ContactEmail value) = value

    let create value : Result<ContactEmail, CheckFailure list> =
        Check.all [ present; email; maxLength 254 ] value |> Result.map (fun () -> ContactEmail value)

module Sku =
    let value (Sku value) = value

    let create value : Result<Sku, CheckFailure list> =
        Check.all [ present; lengthBetween 3 12; matches "^[A-Z0-9-]+$" ] value |> Result.map (fun () -> Sku value)

module Rating =
    let value (Rating value) = value

    let create value : Result<Rating, CheckFailure list> =
        Check.between 1 5 value |> Result.map (fun () -> Rating value)

module UnitPrice =
    let value (UnitPrice value) = value

    let create value : Result<UnitPrice, CheckFailure list> =
        greaterThan 0m value |> Result.map (fun () -> UnitPrice value)

type Discount =
    | Percent of PositiveInt
    | Code of Slug

type PublishWindow =
    { Range: DateTimeOffsetRange }

type ProductRequest =
    { Id: ProductId
      Slug: ProductSlug
      DisplayName: DisplayName
      Tags: ProductTags
      Quantity: Quantity
      ContactEmail: ContactEmail
      Sku: Sku
      Rating: Rating
      UnitPrice: UnitPrice
      Discount: Discount
      PublishWindow: PublishWindow }

let sequenceResults values =
    let folder next state =
        match next, state with
        | Ok value, Ok values -> Ok(value :: values)
        | Error error, _ -> Error error
        | _, Error error -> Error error

    values
    |> List.foldBack folder
    <| Ok []

let private parseError error = Error(sprintf "%A" error)
let private checkError failures = Error(CheckFailure.describeAll failures)

let parseDiscount (raw: string) : Result<Discount, string> =
    let parsePercent value =
        result {
            let! parsed = Parse.int value |> Result.mapError (sprintf "%A")
            let! positive = Refine.positiveInt parsed |> Result.mapError CheckFailure.describeAll
            return Percent positive
        }
    match parsePercent raw with
    | Ok value -> Ok value
    | Error _ -> Refine.slug raw |> Result.map Code |> Result.mapError CheckFailure.describeAll

let createProductRequest rawId rawSlug rawDisplayName rawTags rawQuantity rawContactEmail rawSku rawRating rawUnitPrice rawDiscount publishStart publishEnd : Result<ProductRequest, string> =
    result {
        let! parsedId = Parse.int rawId |> Result.mapError (sprintf "%A")
        let! id = Refine.nonZeroInt parsedId |> Result.mapError CheckFailure.describeAll
        let! slug = Refine.slug rawSlug |> Result.mapError CheckFailure.describeAll
        let! displayName = Refine.nonBlankString rawDisplayName |> Result.mapError CheckFailure.describeAll
        let! tags = rawTags |> List.map Refine.slug |> sequenceResults |> Result.mapError CheckFailure.describeAll
        let! distinctTags = Refine.distinctList tags |> Result.mapError CheckFailure.describeAll
        let! parsedQuantity = Parse.int rawQuantity |> Result.mapError (sprintf "%A")
        let! quantity = Refine.positiveInt parsedQuantity |> Result.mapError CheckFailure.describeAll
        let! contactEmail = ContactEmail.create rawContactEmail |> Result.mapError CheckFailure.describeAll
        let! sku = Sku.create rawSku |> Result.mapError CheckFailure.describeAll
        let! parsedRating = Parse.int rawRating |> Result.mapError (sprintf "%A")
        let! rating = Rating.create parsedRating |> Result.mapError CheckFailure.describeAll
        let! parsedUnitPrice = Parse.decimal rawUnitPrice |> Result.mapError (sprintf "%A")
        let! unitPrice = UnitPrice.create parsedUnitPrice |> Result.mapError CheckFailure.describeAll
        let! discount = parseDiscount rawDiscount
        let! range = Refine.dateTimeOffsetRange publishStart publishEnd |> Result.mapError CheckFailure.describeAll
        return { Id = ProductId id; Slug = ProductSlug slug; DisplayName = DisplayName displayName; Tags = ProductTags distinctTags; Quantity = Quantity quantity; ContactEmail = contactEmail; Sku = sku; Rating = rating; UnitPrice = unitPrice; Discount = discount; PublishWindow = { Range = range } }
    }


let run () =
    createProductRequest "1" "product" "Product" [ "featured" ] "2" "ada@example.com" "SKU-1" "5" "12.50" "10" DateTimeOffset.UtcNow DateTimeOffset.UtcNow
    |> printfn "Refined catalog: %A"
