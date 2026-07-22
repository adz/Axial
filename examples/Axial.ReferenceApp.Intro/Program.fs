// The introductory reference app: a conference registration desk built with only
// Axial.ErrorHandling. No schemas, no Flow — plain Result with your own error union,
// terse checks, fail-fast refined construction, and accumulated form validation.
//
// Read it top to bottom:
//   1. single-field checks that stay ordinary Result values
//   2. a fail-fast result {} pipeline for dependent steps
//   3. refine {} constructing refined domain values from raw strings
//   4. validate {} accumulating every sibling failure with named paths

open Axial
open Axial.ErrorHandling
open Axial.ErrorHandling.CheckDSL
open Axial.Validation
open Axial.Refined

// ---------------------------------------------------------------------------
// 1. Checks: reusable named constraints, your own error type in the signature.
// ---------------------------------------------------------------------------

type BadgeError =
    | NameTooShort
    | NameTooLong

/// A badge name must print on one line: 3 to 40 characters.
let validateBadgeName (name: string) : Result<string, BadgeError> =
    name
    |> minLength 3
    |> orError NameTooShort
    |> Result.bind (fun name -> name |> maxLength 40 |> orError NameTooLong)

// ---------------------------------------------------------------------------
// 2. result {}: fail-fast sequencing of dependent steps.
// ---------------------------------------------------------------------------

type TicketError =
    | UnknownTier of string
    | QuantityNotANumber of string
    | QuantityOutOfRange of int

type Tier =
    | General
    | Speaker

let parseTier (raw: string) : Result<Tier, TicketError> =
    match raw.Trim().ToLowerInvariant() with
    | "general" -> Ok General
    | "speaker" -> Ok Speaker
    | other -> Error(UnknownTier other)

/// Steps depend on each other, so the first failure stops the pipeline.
let parseTicketRequest (rawTier: string) (rawQuantity: string) : Result<Tier * int, TicketError> =
    result {
        let! tier = parseTier rawTier
        let! quantity = Parse.int rawQuantity |> orError (QuantityNotANumber rawQuantity)
        do! (quantity >= 1 && quantity <= 6) |> Result.requireTrue (QuantityOutOfRange quantity)
        return tier, quantity
    }

// ---------------------------------------------------------------------------
// 3. refine {}: raw strings become refined domain values, fail-fast.
// ---------------------------------------------------------------------------

type AttendeeId = AttendeeId of PositiveInt
type ContactEmail = ContactEmail of NonBlankString

type Contact = { Id: AttendeeId; Email: ContactEmail }

/// Both parses must succeed before a Contact can exist; the types carry the proof.
let createContact (rawId: string) (rawEmail: string) : Result<Contact, RefinementError> =
    refine {
        let! parsedId = Parse.int rawId
        let! positiveId = Refine.positiveInt parsedId
        let! email = Refine.nonBlankString rawEmail
        return { Id = AttendeeId positiveId; Email = ContactEmail email }
    }

// ---------------------------------------------------------------------------
// 4. validate {}: independent fields report every failure together, with paths.
// ---------------------------------------------------------------------------

type RegistrationForm =
    { Name: string
      Email: string
      Company: string
      Tier: string
      Quantity: string }

type FormError =
    | Required
    | InvalidEmail
    | BadName of BadgeError
    | BadTicket of TicketError

type Registration =
    { BadgeName: string
      Email: string
      Company: string option
      Tier: Tier
      Quantity: int }

/// One pass over the form returns every field failure, each attached to its field name.
let validateRegistration (form: RegistrationForm) : Validation<Registration, FormError> =
    validate {
        let! name =
            validate.name "name" { return! validateBadgeName form.Name |> mapError BadName }

        and! email =
            validate.name "email" {
                let! presentEmail = form.Email |> present |> orError Required
                return! presentEmail |> email |> orError InvalidEmail
            }

        and! ticket =
            validate.name "ticket" { return! parseTicketRequest form.Tier form.Quantity |> mapError BadTicket }

        let company = if form.Company.Trim() = "" then None else Some(form.Company.Trim())
        let tier, quantity = ticket

        return
            { BadgeName = name
              Email = email
              Company = company
              Tier = tier
              Quantity = quantity }
    }

// ---------------------------------------------------------------------------
// Demo driver: run each stage over good and bad inputs and print the outcomes.
// ---------------------------------------------------------------------------

let private show (label: string) (value: 'value) =
    printfn "%s\n  %A\n" label value

[<EntryPoint>]
let main _ =
    show "Check (valid badge name):" (validateBadgeName "Ada Lovelace")
    show "Check (too short):" (validateBadgeName "Al")

    show "result {} (valid ticket):" (parseTicketRequest "speaker" "2")
    show "result {} (quantity out of range):" (parseTicketRequest "general" "9")
    show "result {} (unknown tier stops first):" (parseTicketRequest "vip" "9")

    show "refine {} (valid contact):" (createContact "41" "ada@example.org")
    show "refine {} (zero id fails fast):" (createContact "0" "ada@example.org")

    let goodForm =
        { Name = "Ada Lovelace"
          Email = "ada@example.org"
          Company = "Analytical Engines Ltd"
          Tier = "speaker"
          Quantity = "1" }

    let badForm =
        { Name = "Al"
          Email = "not-an-email"
          Company = ""
          Tier = "general"
          Quantity = "9" }

    show "validate {} (valid form):" (validateRegistration goodForm)

    match validateRegistration badForm |> Validation.toResult with
    | Ok registration -> show "validate {} unexpectedly succeeded:" registration
    | Error diagnostics ->
        printfn "validate {} (bad form) reports every field at once:"
        printfn "%s" (Diagnostics.toString diagnostics)

    0
