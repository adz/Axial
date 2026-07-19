module RefinedValueSchemaExample

open Axial.Schema
open Axial.Schema.Syntax

/// <summary>An email address refined over Axial's text primitive, carrying the well-known email format.</summary>
type Email =
    private
    | Email of string

    static member Schema(_: Email) : Schema<Email> =
        Schema.text
        |> Schema.constrain Constraint.required
        |> Schema.convert Email (fun (Email value) -> value)
        |> Schema.constrain Constraint.email
        |> Schema.withFormat SchemaFormat.email

module Email =
    let create (value: string) = Email value
    let value (Email value) = value

    let schema : Schema<Email> = SchemaDefaults.Resolve()

/// <summary>A bounded-text domain value whose length constraints live on the raw text schema.</summary>
type ContactName =
    private
    | ContactName of string

    static member Schema(_: ContactName) : Schema<ContactName> =
        Schema.text
        |> Schema.constrainAll [ Constraint.minLength 2; Constraint.maxLength 40 ]
        |> Schema.convert ContactName (fun (ContactName value) -> value)

module ContactName =
    let create (value: string) = ContactName value
    let value (ContactName value) = value

    let schema : Schema<ContactName> = SchemaDefaults.Resolve()

/// <summary>A quantity that must always be positive (strictly greater than zero).</summary>
type Quantity =
    private
    | Quantity of int

    static member Schema(_: Quantity) : Schema<Quantity> =
        Schema.int
        |> Schema.constrain (Constraint.greaterThan 0)
        |> Schema.convert Quantity (fun (Quantity value) -> value)

module Quantity =
    let create (value: int) = Quantity value
    let value (Quantity value) = value

    let schema : Schema<Quantity> = SchemaDefaults.Resolve()

/// <summary>A running total that must never go negative, but zero is allowed.</summary>
type Balance =
    private
    | Balance of decimal

    static member Schema(_: Balance) : Schema<Balance> =
        Schema.decimal
        |> Schema.constrain (Constraint.atLeast 0m)
        |> Schema.convert Balance (fun (Balance value) -> value)

module Balance =
    let create (value: decimal) = Balance value
    let value (Balance value) = value

    let schema : Schema<Balance> = SchemaDefaults.Resolve()

type Contact =
    { Email: Email
      Name: ContactName
      Quantity: Quantity
      Balance: Balance }

let contactSchema =
    Schema.define<Contact>
    |> field "email" _.Email
    |> field "name" _.Name
    |> field "quantity" _.Quantity
    |> field "balance" _.Balance
    |> construct (fun email name quantity balance ->
        { Email = email
          Name = name
          Quantity = quantity
          Balance = balance })

let run () =
    let contact =
        { Email = Email.create "ada@example.com"
          Name = ContactName.create "Ada"
          Quantity = Quantity.create 3
          Balance = Balance.create 0m }

    let emailCheck = Email.schema |> SchemaCheck.text
    let nameCheck = ContactName.schema |> SchemaCheck.text
    let quantityCheck = Quantity.schema |> SchemaCheck.ordered<int, _>
    let balanceCheck = Balance.schema |> SchemaCheck.ordered<decimal, _>

    printfn "Email check: %A" (emailCheck contact.Email)
    printfn "Name check: %A" (nameCheck contact.Name)
    printfn "Quantity check: %A" (quantityCheck contact.Quantity)
    printfn "Balance check: %A" (balanceCheck contact.Balance)

    printfn "Invalid email check: %A" (emailCheck (Email.create ""))
    printfn "Invalid name check: %A" (nameCheck (ContactName.create "A"))
    printfn "Invalid quantity check: %A" (quantityCheck (Quantity.create 0))
    printfn "Invalid balance check: %A" (balanceCheck (Balance.create -1m))
