// Axial.Schema installed alone. Schema declares dependencies on Constraint, Data, Parse, and Refined
// directly, so a single PackageReference must be enough to declare a model AND to build the Data
// input it parses. Referencing only Axial.Schema here is the point of the fixture.

open Axial.Constraint
open Axial.Data
open Axial.Schema

type Signup = { Email: string; Age: int }

module private Signup =
    let create email age = { Email = email; Age = age }

let private signupSchema =
    schema<Signup> {
        field "email" _.Email {
            withSchema Schema.text
            constrain (Constraint.minLength 3)
        }

        field "age" _.Age {
            withSchema Schema.int
            constrain (Constraint.atLeast 0)
        }

        construct Signup.create
    }

let private input email age =
    Data.Object [ "email", Data.Text email; "age", Data.Number(string age) ]

[<EntryPoint>]
let main _ =
    let accepted = Schema.parse signupSchema (input "ada@example.com" 42)
    let rejected = Schema.parse signupSchema (input "no" -1)

    match accepted, rejected with
    | Ok signup, Error _ when signup.Email = "ada@example.com" && signup.Age = 42 ->
        printfn "Consumer.Schema OK"
        0
    | other ->
        eprintfn "Consumer.Schema FAILED: %A" other
        1
