module Axial.Schema.Http.Tests.Fixtures

open Axial.Schema
open Axial.Schema.Syntax

type Address = { Street: string; City: string }

type Signup =
    { Name: string
      Age: int
      Address: Address
      Tags: string list }

let addressSchema () =
    Schema.define<Address>
    |> fieldWith (Schema.text |> Schema.constrainAll [ Constraint.required; Constraint.maxLength 120 ]) "street" _.Street
    |> fieldWith (Schema.text |> Schema.constrainAll [ Constraint.required; Constraint.maxLength 80 ]) "city" _.City
    |> construct (fun street city -> { Street = street; City = city })

let signupSchema () =
    Schema.define<Signup>
    |> fieldWith (Schema.text |> Schema.constrainAll [ Constraint.required; Constraint.maxLength 80 ]) "name" _.Name
    |> fieldWith (Schema.int |> Schema.constrainAll [ Constraint.between 13 120 ]) "age" _.Age
    |> fieldWith (addressSchema () |> Schema.constrainAll [ Constraint.required ]) "address" _.Address
    |> fieldWith (Schema.listWith Schema.text |> Schema.constrainAll [ Constraint.maxCount 5 ]) "tags" _.Tags
    |> construct (fun name age address tags ->
        { Name = name
          Age = age
          Address = address
          Tags = tags })

let validJson =
    """{"name":"Ada Lovelace","age":36,"address":{"street":"12 Analytical Way","city":"London"},"tags":["vip"]}"""

let invalidJson =
    """{"name":"","age":9,"address":{"street":"12 Analytical Way"},"tags":["a","b","c","d","e","f"]}"""
