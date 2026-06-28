module RefinedCatalogExample

open System
open Axial.Refined

type ProductId = ProductId of NonZeroInt
type ProductSlug = ProductSlug of Slug
type DisplayName = DisplayName of NonBlankString
type ProductTags = ProductTags of DistinctList<Slug>
type Quantity = Quantity of PositiveInt

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
        (RefinementError.InvalidFormat("Discount", "Expected a positive integer percent or slug code."))
        raw

let createProductRequest
    rawId
    rawSlug
    rawDisplayName
    rawTags
    rawQuantity
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
        let! discount = parseDiscount rawDiscount
        let! publishWindow = Refine.dateTimeOffsetRange publishStart publishEnd

        return {
            Id = ProductId id
            Slug = ProductSlug slug
            DisplayName = DisplayName displayName
            Tags = ProductTags distinctTags
            Quantity = Quantity quantity
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
            ""
            finish
            start

    printfn "Refined product result: %A" valid
    printfn "Refined product error: %A" invalid
