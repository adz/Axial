---
weight: 20
title: Validate and Result
description: The progression from `Result<'value, unit>` checks to fail-fast `Result<'value, 'error>`, guarded flow bindings, accumulating `Validation<'value, 'error>`, and diagnostics graphs in FsFlow.
---


# Validate and Result

FsFlow moves through a simple progression:

1. build a pure check with Check (`Result<'value, unit>`)
2. turn that `Result<'value, unit>` into a `Result<'value, ValidationError>` with `Check.orError`
3. use Guard when the same value needs to bind directly inside [`result {}`]({{< relref "builders-result.md" >}}), [`flow {}`]({{< relref "builders-flow.md" >}}), [`asyncFlow {}`]({{< relref "builders-asyncflow.md" >}}), [`taskFlow {}`]({{< relref "taskbuilders-taskflow.md" >}}), or [`validate {}`]({{< relref "builders-validate.md" >}}) while carrying an error
4. use [`result {}`]({{< relref "builders-result.md" >}}) when you want to stop on the first failure
5. use Validation and [`validate {}`]({{< relref "builders-validate.md" >}}) when sibling failures should accumulate into a diagnostics graph

The examples below follow that progression.

## 1. Pure Checks

`Check<'value>` is the reusable predicate layer. It comes in two useful shapes:

- `Check<'value>` when the check preserves a value on success
- `Check<unit>` when the check is a gate and only answers yes/no

If the answer is no, Check returns `Error ()`.

Attach your application error, such as `NameRequired`, with `Check.orError`:

```fsharp
open FsFlow

type ValidationError =
    | NameRequired
    | EmailRequired

let requireName (name: string) : Result<string, ValidationError> =
    name |> Check.notBlank |> Check.orError NameRequired

let requireEmail (email: string) : Result<string, ValidationError> =
    email |> Check.notBlank |> Check.orError EmailRequired
```

Example outcomes:

```fsharp
requireName "Ada"   // Ok "Ada"
requireName ""      // Error NameRequired
requireEmail "a@b"  // Ok "a@b"
requireEmail " "    // Error EmailRequired
```

### Supporting Code

These are the helper functions used in the Guard example below.

```fsharp
type AuthError =
    | MissingPassword
    | Unauthorized

let isPasswordValidAsync : string -> Async<bool> =
    ...

let tryGetUser : string -> Async<Result<string, AuthError>> =
    ...
```

## 2. Guarded Bindings

Guard is the bindable version of the same idea.

Think of it this way:

- Check says whether a predicate succeeds and returns `Ok value` or `Error ()`
- Guard keeps the value visible to the computation expression and carries a failure value such as `MissingPassword`
- the computation expression binds the value, and the guard decides what error comes out if the check fails

Use `Guard.Of` when the value is boolean or check-shaped, and `Guard.MapError` when the value already carries an error of its own.

That matters when the predicate itself is already inside `Async`, `Task`, Flow, AsyncFlow, TaskFlow,
or Validation: Guard lets the CE bind the wrapped value directly. With Check alone, you would
first produce a `Result<'value, unit>` and then attach the error with `Check.orError`.

Without Guard, you usually spell that out in two steps:

```fsharp
let loginWithoutGuard username password =
    asyncFlow {
        let! passwordValid = isPasswordValidAsync password
        do! Check.okIf passwordValid |> Check.orError MissingPassword |> AsyncFlow.fromResult

        let! user =
            tryGetUser username
            |> AsyncFlow.fromAsyncResult
            |> AsyncFlow.mapError (fun _ -> Unauthorized)

        return user
    }

// loginWithoutGuard "ada" "secret"     -> Ok user
// loginWithoutGuard "ada" ""           -> Error MissingPassword
// loginWithoutGuard "unknown" "secret" -> Error Unauthorized
```

With Guard, the same flow reads directly at the binding site:

```fsharp
let login username password =
    asyncFlow {
        do! isPasswordValidAsync password |> Guard.Of MissingPassword
        let! user = tryGetUser username |> Guard.MapError Unauthorized
        return user
    }

// login "ada" "secret"     -> Ok user
// login "ada" ""           -> Error MissingPassword
// login "unknown" "secret" -> Error Unauthorized
```

The important part is not the syntax trick. The important part is the shape:

- the source value still participates in the CE
- the failure value is attached explicitly
- the CE decides how to bind the source

## 3. Fail-Fast Result

Use [`result {}`]({{< relref "builders-result.md" >}}) when the next step depends on the previous one and you want the first `Error MissingName` or `Error MissingEmail` to stop the workflow.

```fsharp
type UserInputError =
    | MissingName
    | MissingEmail

let validateInput name email =
    result {
        let! validName = name |> Check.notBlank |> Check.orError MissingName
        let! validEmail = email |> Check.notBlank |> Check.orError MissingEmail
        return validName, validEmail
    }
```

Example outcomes:

```fsharp
validateInput "Ada" "ada@example.com"  // Ok ("Ada", "ada@example.com")
validateInput "" "ada@example.com"     // Error MissingName
validateInput "Ada" ""                 // Error MissingEmail
```

[`result {}`]({{< relref "builders-result.md" >}}) is fail-fast:

- if the first step fails, later steps do not run
- the error stays in a single `Result<'value, 'error>` value

## 4. Accumulating Validation

Use Validation and [`validate {}`]({{< relref "builders-validate.md" >}}) when sibling checks are independent and you want every failure collected into a `Validation<'value, RegistrationError>` graph.
Use `and!` for the sibling checks you want merged together. Plain `let!` and `do!` are
sequential: if one step fails, later steps in that chain are not evaluated.

```fsharp
type Registration =
    { Name: string
      Email: string }

type RegistrationError =
    | NameRequired
    | EmailRequired

let validateRegistration input =
    validate {
        let! name = input.Name |> Check.notBlank |> Check.orError NameRequired
        and! email = input.Email |> Check.notBlank |> Check.orError EmailRequired
        return { Name = name; Email = email }
    }
```

Example outcomes:

```fsharp
validateRegistration { Name = "Ada"; Email = "ada@example.com" }
// Ok { Name = "Ada"; Email = "ada@example.com" }

validateRegistration { Name = ""; Email = "" }
// Error {
//   Errors = [ NameRequired; EmailRequired ]
//   Children = Map []
// }
```

That last line is the key difference from [`result {}`]({{< relref "builders-result.md" >}}):

- [`result {}`]({{< relref "builders-result.md" >}}) stops at the first error
- [`validate {}`]({{< relref "builders-validate.md" >}}) keeps going and merges sibling failures when you use `and!`
- plain `let!` and `do!` still short-circuit on the first failure in their sequence

The current builder emits root-level diagnostics for direct `Check.orError` results unless you
wrap the work in a scoped helper like `validate.key`, `validate.index`, or `validate.name`.
Inside those scoped blocks, you can `return!` the Result directly. You do not need
`Validation.fromResult` there.

## 5. Diagnostics Graph

Validation does not store a flat list of strings.
It stores a `Diagnostics<'error>` graph.

That graph is what lets FsFlow remember where each failure came from in nested data.
The `Name` in `PathSegment.Name` is an explicit branch label in that graph; it is not inferred
from an F# variable name.

The path inside each diagnostic uses `PathSegment` values:

- `Key "customer"` for a named field or record property
- `Index 0` for a list, array, or sequence position
- `Name "Name"` for a named validation branch

The structure is:

```fsharp
type PathSegment =
    | Key of string
    | Index of int
    | Name of string

type Diagnostic<'error> =
    { Path: PathSegment list
      Error: 'error }

type Diagnostics<'error> =
    { Errors: 'error list
      Children: Map<PathSegment, Diagnostics<'error>> }
```

For a simple validation, the graph may have only root-level items:

```fsharp
validateRegistration { Name = ""; Email = "" }
// Error {
//   Errors = [ NameRequired; EmailRequired ]
//   Children = Map []
// }
```

That is a root-level validation node, not a keyed tree branch.

For nested API responses, the root `validate { }` block stays at the current node. To point failures back
to a specific field or list item, use the builder-scoped helpers `validate.key`, `validate.index`,
and `validate.name` inside the computation expression. Those wrappers prefix the diagnostics
produced inside the block.

For sequences of items, `Validation.traverseIndexed` is the indexed helper: it runs a binder for
each item, prefixes the diagnostics with `Index i`, and collects the validated values.

```fsharp
type Address = { City: string }
type Customer =
    { Name: string
      Address: Address
      Lines: string list }

let validateAddress address =
    validate.key "address" {
        let! city =
            validate.name "City" {
                return! address.City |> Check.notBlank |> Check.orError "City required"
            }

        return { address with City = city }
    }

let validateCustomer customer =
    validate.key "customer" {
        let! name =
            validate.name "Name" {
                return! customer.Name |> Check.notBlank |> Check.orError "Name required"
            }

        and! address = validateAddress customer.Address

        and! lines =
            validate.key "lines" {
                return!
                    customer.Lines
                    |> Validation.traverseIndexed (fun index line ->
                        validate.name "Name" {
                            return! line |> Check.notBlank |> Check.orError $"Line {index} name required"
                        }
                    )
            }

        return
            { customer with
                Name = name
                Address = address
                Lines = lines }
    }
```

The runnable version of the JSON-shaped example below lives in
`examples/FsFlow.Examples/DiagnosticsExample.fs`.

Example request payload:

```json
{
  "requestId": "",
  "customer": {
    "name": "",
    "address": { "city": "" },
    "lines": [ { "name": "" } ]
  }
}
```

Example outcome:

```fsharp
validateCreateCustomerRequest badRequest
|> Validation.toResult
|> Result.mapError (toApiErrors >> fun payload -> JsonSerializer.Serialize(payload, JsonSerializerOptions(WriteIndented = true)))
// Error
// {
//   "errors": [
//     { "path": "customer.address.City", "message": "City required" },
//     { "path": "customer.lines.[0].Name", "message": "Line 0 name required" },
//     { "path": "customer.Name", "message": "Name required" },
//     { "path": "RequestId", "message": "RequestId required" }
//   ]
// }
```

That shape mirrors the JSON request:

- `requestId` becomes a top-level field error when it is missing or blank
- `customer.name` is a child field error under the request body
- `customer.address.city` is a nested field error under `customer`
- `customer.lines.[0].name` is a list item field error under `customer`

If you need a flat list for reporting, `Diagnostics.flatten` walks the graph and reconstructs the full paths.
That is the common JSON API pattern: a single `errors` array with a `path` and `message` per item.

If you want a terminal-friendly view instead of JSON, `Diagnostics.toString` renders the same graph as a compact YAML-like tree.

The runnable example uses `System.Text.Json` in `examples/FsFlow.Examples/DiagnosticsExample.fs` to shape the API response.

### Scoping An Existing Validation

If you already have a `Validation<'value, 'error>` value, the `Validation.key`, `Validation.index`,
and `Validation.name` functions prefix it after the fact.

```fsharp
let validateAddress address =
    validate {
        let! city = address.City |> Check.notBlank |> Check.orError "City required"
        return { address with City = city }
    }
    |> Validation.key "address"

let validateCustomer customer =
    validate {
        let! name = customer.Name |> Check.notBlank |> Check.orError "Name required"
        and! address = validateAddress customer.Address
        return { customer with Name = name; Address = address }
    }
    |> Validation.key "customer"
```

## 6. Bringing The Pieces Together

The practical rule is:

- use Check when you are still expressing the predicate as `Result<'value, unit>`
- use `Check.orError` when you want that `Result<'value, unit>` to become a `Result<'value, 'error>`
- use Guard when the same value needs to bind inside a CE while carrying an error value like `MissingPassword`
- use [`result {}`]({{< relref "builders-result.md" >}}) when the workflow is ordered and fail-fast
- use `Validation<'value, 'error>` and [`validate {}`]({{< relref "builders-validate.md" >}}) when sibling failures should accumulate

That gives you one path from predicate, to result, to guarded flow binding, to accumulating diagnostics.
