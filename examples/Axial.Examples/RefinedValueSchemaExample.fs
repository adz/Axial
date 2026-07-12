module RefinedValueSchemaExample

open Axial.Schema

/// <summary>An email address refined over Axial's text primitive, carrying the well-known email format.</summary>
type Email = private Email of string

module Email =
    let create (value: string) = Email value
    let value (Email value) = value

    let schema : Schema<Email> =
        Schema.text
        |> Schema.constrain Constraint.required
        |> Schema.convert create value
        |> Schema.constrain Constraint.email
        |> Schema.withFormat SchemaFormat.email

/// <summary>A bounded-text domain value whose length constraints live on the raw text schema.</summary>
type ContactName = private ContactName of string

module ContactName =
    let create (value: string) = ContactName value
    let value (ContactName value) = value

    let schema : Schema<ContactName> =
        Schema.text
        |> Schema.constrainAll [ Constraint.minLength 2; Constraint.maxLength 40 ]
        |> Schema.convert create value

/// <summary>A quantity that must always be positive (strictly greater than zero).</summary>
type Quantity = private Quantity of int

module Quantity =
    let create (value: int) = Quantity value
    let value (Quantity value) = value

    let schema : Schema<Quantity> =
        Schema.int
        |> Schema.constrain (Constraint.greaterThan 0)
        |> Schema.convert create value

/// <summary>A running total that must never go negative, but zero is allowed.</summary>
type Balance = private Balance of decimal

module Balance =
    let create (value: decimal) = Balance value
    let value (Balance value) = value

    let schema : Schema<Balance> =
        Schema.decimal
        |> Schema.constrain (Constraint.atLeast 0m)
        |> Schema.convert create value

type Contact =
    { Email: Email
      Name: ContactName
      Quantity: Quantity
      Balance: Balance }

let contactSchema =
    Schema.recordFor<Contact, _> (fun email name quantity balance ->
        { Email = email
          Name = name
          Quantity = quantity
          Balance = balance })
    |> Schema.field "email" _.Email Email.schema
    |> Schema.field "name" _.Name ContactName.schema
    |> Schema.field "quantity" _.Quantity Quantity.schema
    |> Schema.field "balance" _.Balance Balance.schema
    |> Schema.build

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
