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
    schema<Address> {
        field "street" _.Street {
            withSchema (Schema.text |> Schema.constrainAll [ Constraint.required; Constraint.maxLength 120 ])
        }
        field "city" _.City {
            withSchema (Schema.text |> Schema.constrainAll [ Constraint.required; Constraint.maxLength 80 ])
        }
        construct (fun street city -> { Street = street; City = city })
    }

let signupSchema () =
    schema<Signup> {
        field "name" _.Name {
            withSchema (Schema.text |> Schema.constrainAll [ Constraint.required; Constraint.maxLength 80 ])
        }
        field "age" _.Age {
            withSchema (Schema.int |> Schema.constrainAll [ Constraint.between 13 120 ])
        }
        field "address" _.Address {
            withSchema (addressSchema () |> Schema.constrainAll [ Constraint.required ])
        }
        field "tags" _.Tags {
            withSchema (Schema.listWith Schema.text |> Schema.constrainAll [ Constraint.maxCount 5 ])
        }
        construct (fun name age address tags ->
            { Name = name
              Age = age
              Address = address
              Tags = tags })
    }

let validJson =
    """{"name":"Ada Lovelace","age":36,"address":{"street":"12 Analytical Way","city":"London"},"tags":["vip"]}"""

let invalidJson =
    """{"name":"","age":9,"address":{"street":"12 Analytical Way"},"tags":["a","b","c","d","e","f"]}"""
