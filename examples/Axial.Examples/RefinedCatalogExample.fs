module RefinedCatalogExample

open System
open Axial.ErrorHandling
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

    let create value : Result<ContactEmail, RefinementError> =
        Refine.withChecks
            "ContactEmail"
            [ Check.String.present; Check.String.email; Check.String.maxLength 254 ]
            ContactEmail
            value

module Sku =
    let value (Sku value) = value

    let create value : Result<Sku, RefinementError> =
        Refine.withChecks
            "Sku"
            [ Check.String.present; Check.String.lengthBetween 3 12; Check.String.matches "^[A-Z0-9-]+$" ]
            Sku
            value

module Rating =
    let value (Rating value) = value

    let create value : Result<Rating, RefinementError> =
        Refine.withCheck "Rating" (Check.Number.between 1 5) Rating value

module UnitPrice =
    let value (UnitPrice value) = value

    let create value : Result<UnitPrice, RefinementError> =
        Refine.withCheck "UnitPrice" (Check.Number.greaterThan 0m) UnitPrice value

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

let parseDiscount (raw: string) : Result<Discount, RefinementError> =
    let parsePercent value =
        Parse.int value
        |> Result.mapError RefinementError.ParseFailed
        |> Result.bind Refine.positiveInt

    Choice.orElse
        Percent
        parsePercent
        Code
        Refine.slug
        (RefinementError.CheckFailed("Discount", [ CheckFailure.InvalidFormat "positive integer percent or slug code" ]))
        raw

let createProductRequest
    rawId
    rawSlug
    rawDisplayName
    rawTags
    rawQuantity
    rawContactEmail
    rawSku
    rawRating
    rawUnitPrice
    rawDiscount
    publishStart
    publishEnd
    : Result<ProductRequest, RefinementError> =
    refine {
        let! parsedId = Parse.int rawId
        let! id = Refine.nonZeroInt parsedId
        let! slug = Refine.slug rawSlug
        let! displayName = Refine.nonBlankString rawDisplayName
        let! tags = rawTags |> List.map Refine.slug |> sequenceResults
        let! distinctTags = Refine.distinctList tags
        let! parsedQuantity = Parse.int rawQuantity
        let! quantity = Refine.positiveInt parsedQuantity
        let! contactEmail = ContactEmail.create rawContactEmail
        let! sku = Sku.create rawSku
        let! parsedRating = Parse.int rawRating
        let! rating = Rating.create parsedRating
        let! parsedUnitPrice = Parse.decimal rawUnitPrice
        let! unitPrice = UnitPrice.create parsedUnitPrice
        let! discount = parseDiscount rawDiscount
        let! publishWindow = Refine.dateTimeOffsetRange publishStart publishEnd

        return {
            Id = ProductId id
            Slug = ProductSlug slug
            DisplayName = DisplayName displayName
            Tags = ProductTags distinctTags
            Quantity = Quantity quantity
            ContactEmail = contactEmail
            Sku = sku
            Rating = rating
            UnitPrice = unitPrice
            Discount = discount
            PublishWindow = { Range = publishWindow }
        }
    }

let run () =
    let start = DateTimeOffset(2026, 6, 28, 9, 0, 0, TimeSpan.Zero)
    let finish = start.AddDays 7.0

    let valid =
        createProductRequest
            "42"
            "axial-guide"
            "Axial Guide"
            [ "fsharp"; "typed-errors" ]
            "3"
            "ada@example.com"
            "AX-42"
            "5"
            "19.95"
            "launch-sale"
            start
            finish

    let invalid =
        createProductRequest
            "0"
            "Bad Slug"
            " "
            [ "fsharp"; "fsharp" ]
            "-1"
            "not-email"
            "x"
            "6"
            "0"
            ""
            finish
            start

    printfn "Refined product result: %A" valid
    printfn "Refined product error: %A" invalid
