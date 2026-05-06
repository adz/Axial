# Functional Architecture, Recast with FsFlow

Mark Seemann has a useful article about refactoring a registration workflow into an impure/pure/impure sandwich. The shape is the important part, not the exact mechanics: keep the decision in the middle pure, keep the side effects at the edges, and make the composition root explicit.

This draft takes that same pathway, but limits the toolset to FsFlow. The point is not to introduce a new architecture library. The point is to show that FsFlow already gives you the pieces you need:

- `Check` for pure predicate checks
- `result {}` for fail-fast composition over standard `FSharp.Core.Result`
- `Guard` for binding check-like or error-bearing sources directly in a computation expression
- `AsyncFlow` for the impure edges

The result is still an impure/pure/impure sandwich, but the sandwich is more explicit about where each concern lives.

## The Workflow

We are modelling a two-factor registration flow:

1. If the caller does not have a proof ID, create one and ask the caller to prove ownership of the phone number.
2. If the caller does have a proof ID, verify it.
3. If the proof is valid, complete the registration.
4. If the proof is invalid, create a new proof and ask again.

The basic domain types can stay small:

```fsharp
open System
open FsFlow

type Mobile = Mobile of int
type ProofId = ProofId of Guid

type Registration =
    { Mobile: Mobile
      Name: string }

type CompleteRegistrationResult =
    | ProofRequired of ProofId
    | RegistrationCompleted

type RegistrationError =
    | MissingName
    | MissingMobile
    | ProofLookupFailed
    | ProofGenerationFailed
    | CompletionFailed
```

The important detail is that `RegistrationError` is a plain domain error type. FsFlow does not require a special error wrapper.

## Start With Plain Result

The first place FsFlow earns its keep is not the workflow itself. It is the validation leading into the workflow.

FsFlow should not replace `FSharp.Core.Result`. That is the default result story.
Instead, `result {}` gives you a computation expression that composes ordinary `Ok` and `Error` values in a readable way.

That means you can keep `Check` as the pure predicate layer:

```fsharp
let requireName (name: string) : Result<string, RegistrationError> =
    name
    |> Check.okIfNotBlank
    |> Check.orError MissingName

let requireMobile (mobile: Mobile option) : Result<Mobile, RegistrationError> =
    mobile
    |> Check.okIfSome
    |> Check.orError MissingMobile
```

If you want the middle of the workflow to stay fail-fast and explicit, you can compose the checks with `result {}`:

```fsharp
type RegistrationCommand =
    { Name: string
      Mobile: Mobile option }

let validateCommand (command: RegistrationCommand) : Result<Registration, RegistrationError> =
    result {
        let! name = requireName command.Name
        let! mobile = requireMobile command.Mobile
        return { Name = name; Mobile = mobile }
    }
```

That is the central idea: `result {}` does not reinvent result handling. It just gives you a clear way to compose the standard `Result` type that FSharp.Core already provides.

## Keep the Decision Pure

The middle of the sandwich should be a pure function. Not `Async`, not `Task`, not `Flow`. Just a decision.

```fsharp
type RegistrationDecision =
    | Complete of Registration
    | RequestProof of Mobile

let decideRegistration (proofIsValid: bool) (registration: Registration) : RegistrationDecision =
    if proofIsValid then
        Complete registration
    else
        RequestProof registration.Mobile
```

That function is the real architectural center. It is tiny on purpose. It can be unit tested without any doubles or runtime plumbing.

## Put The Edges In AsyncFlow

The impure parts live at the edges. In FsFlow, `AsyncFlow` is the natural place for them.

Suppose the outside world gives us three effects:

```fsharp
type Dependencies =
    { CreateProof: Mobile -> Async<ProofId>
      VerifyProof: Mobile -> ProofId -> Async<bool>
      CompleteRegistration: Registration -> Async<unit> }
```

Then the full workflow can be composed as an async flow:

```fsharp
let completeRegistrationWorkflow
    (deps: Dependencies)
    (proofId: ProofId option)
    (command: RegistrationCommand)
    : AsyncFlow<unit, RegistrationError, CompleteRegistrationResult> =
    asyncFlow {
        let! registration = validateCommand command |> AsyncFlow.fromResult

        let! proofIsValid =
            match proofId with
            | None ->
                AsyncFlow.succeed false
            | Some proofId ->
                deps.VerifyProof registration.Mobile proofId
                |> AsyncFlow.fromAsync

        match decideRegistration proofIsValid registration with
        | Complete registration ->
            do! deps.CompleteRegistration registration |> AsyncFlow.fromAsync
            return RegistrationCompleted
        | RequestProof mobile ->
            let! proofId = deps.CreateProof mobile |> AsyncFlow.fromAsync
            return ProofRequired proofId
    }
```

There are three things to notice here:

1. The workflow does not hide the effects.
2. The decision is still pure.
3. `result {}` remains a local tool for validation, not the thing that drives the whole architecture.

That is the FsFlow version of the sandwich.

## When Guard Helps

Sometimes a dependency already returns a shape that should bind directly inside the computation expression.

If the source is already a check-like value, `Guard.Of` keeps the source visible:

```fsharp
let requireVerifiedMobile (mobile: Mobile option) : AsyncFlow<unit, RegistrationError, Mobile> =
    asyncFlow {
        let! mobile = mobile |> Guard.Of MissingMobile
        return mobile
    }
```

If the source already carries a meaningful error, `Guard.MapError` keeps the source shape and remaps the error:

```fsharp
let loadProof
    (lookup: ProofId -> Async<Result<bool, exn>>)
    (proofId: ProofId)
    : AsyncFlow<unit, RegistrationError, bool> =
    asyncFlow {
        let! isValid = lookup proofId |> Guard.MapError (fun _ -> ProofLookupFailed)
        return isValid
    }
```

This matters because it keeps the workflow honest. You do not have to flatten everything into a custom helper module just to make the computation expression readable.

## Testing The Sandwich

Mark Seemann’s article uses fakes at the composition root. That is still the right testing story here.

The pure middle gets direct unit tests:

```fsharp
[<Fact>]
let ``a valid proof completes registration`` () =
    let registration = { Name = "Ada"; Mobile = Mobile 123 }

    let decision = decideRegistration true registration

    decision = Complete registration |> should equal true
```

The composition root gets characterization tests with small fakes.

```fsharp
type Fake2FA() =
    let mutable proofs = Map.empty<Mobile, ProofId * bool>

    member _.CreateProof mobile =
        async {
            let proofId =
                match Map.tryFind mobile proofs with
                | Some (proofId, _) -> proofId
                | None ->
                    let proofId = ProofId(Guid.NewGuid())
                    proofs <- Map.add mobile (proofId, false) proofs
                    proofId
            return proofId
        }

    member _.VerifyProof mobile proofId =
        async {
            match Map.tryFind mobile proofs with
            | Some (existingProofId, isVerified) when existingProofId = proofId ->
                return isVerified
            | _ ->
                return false
        }

    member _.VerifyMobile mobile =
        match Map.tryFind mobile proofs with
        | Some (proofId, _) -> proofs <- Map.add mobile (proofId, true) proofs
        | None -> ()

type FakeRegistrationDB() =
    let mutable registrations = List.empty<Registration>

    member _.CompleteRegistration registration =
        async {
            registrations <- registration :: registrations
        }

    member _.Registrations = registrations |> List.rev
```

Then the fixture is a thin composition root:

```fsharp
let createFixture () =
    let twoFA = Fake2FA()
    let db = FakeRegistrationDB()

    let deps =
        { CreateProof = twoFA.CreateProof
          VerifyProof = twoFA.VerifyProof
          CompleteRegistration = db.CompleteRegistration }

    deps, twoFA, db
```

The tests exercise the edge behaviour, but the middle stays pure:

```fsharp
[<Fact>]
let ``missing proof id asks for proof`` () = async {
    let deps, twoFA, db = createFixture ()
    let command = { Name = "Ada"; Mobile = Some (Mobile 123) }

    let! actual =
        completeRegistrationWorkflow deps None command
        |> AsyncFlow.run ()

    let! expectedProofId = twoFA.CreateProof (Mobile 123)

    actual = ProofRequired expectedProofId |> should equal true
    db.Registrations |> should beEmpty
}
```

The workflow remains easy to reason about because the test names mirror the branches:

- missing proof ID
- valid proof ID
- invalid proof ID

Each test checks one thing about the effectful edges and one thing about the final decision.

## Why This Fits FsFlow

FsFlow is at its best when it helps you avoid inventing a separate helper world for every shape of program.

That is why the architecture here stays simple:

- `Check` handles pure predicates.
- `result {}` handles fail-fast validation over ordinary `Result`.
- `Guard` lets source values bind directly inside a computation expression.
- `AsyncFlow` carries the impure edges.
- The pure middle stays pure.

This is the useful part of the Mark Seemann style of architecture: not the particular dependency injection technique, but the separation of decisions from effects.

FsFlow is a good fit for that because it keeps the abstraction surface small. You do not need to build a second result library to get the architectural win.

