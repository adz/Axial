module Axial.Schema.Http.Tests.Fixtures

open Axial.Schema

type Address = { Street: string; City: string }

type Signup =
    { Name: string
      Age: int
      Address: Address
      Tags: string list }

let addressSchema () =
    Schema.recordFor<Address, _> (fun street city -> { Street = street; City = city })
    |> Schema.field "street" _.Street (Schema.text |> Schema.constrainAll [ Constraint.required; Constraint.maxLength 120 ])
    |> Schema.field "city" _.City (Schema.text |> Schema.constrainAll [ Constraint.required; Constraint.maxLength 80 ])
    |> Schema.build

let signupSchema () =
    Schema.recordFor<Signup, _> (fun name age address tags ->
        { Name = name
          Age = age
          Address = address
          Tags = tags })
    |> Schema.field "name" _.Name (Schema.text |> Schema.constrainAll [ Constraint.required; Constraint.maxLength 80 ])
    |> Schema.field "age" _.Age (Schema.int |> Schema.constrainAll [ Constraint.between 13 120 ])
    |> Schema.field "address" _.Address (addressSchema () |> Schema.constrainAll [ Constraint.required ])
    |> Schema.field "tags" _.Tags (Schema.list Schema.text |> Schema.constrainAll [ Constraint.maxCount 5 ])
    |> Schema.build

let validJson =
    """{"name":"Ada Lovelace","age":36,"address":{"street":"12 Analytical Way","city":"London"},"tags":["vip"]}"""

let invalidJson =
    """{"name":"","age":9,"address":{"street":"12 Analytical Way"},"tags":["a","b","c","d","e","f"]}"""
