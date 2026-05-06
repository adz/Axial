# Functional Architecture, Recast with FsFlow

Mark Seemann's impure/pure/impure sandwich is still the right shape for a workflow that mixes decisions and side effects. The useful part is not the specific technique around it. The useful part is the structure:

- keep the edge effects at the edges
- keep the decision in the middle pure
- make the composition root explicit

FsFlow already gives you the vocabulary for that shape without forcing a second result library or a custom helper world.

- `Check` for pure predicates
- `result {}` for fail-fast composition over standard `FSharp.Core.Result`
- `Guard` for binding check-like or error-bearing sources directly in a computation expression
- `AsyncFlow` for the impure edges

The result is still the same sandwich, but FsFlow makes each layer more explicit.

## The Workflow

We are modelling a two-factor registration flow:

1. If the caller does not have a proof ID, create one and ask the caller to prove ownership of the phone number.
2. If the caller does have a proof ID, verify it.
3. If the proof is valid, complete the registration.
4. If the proof is invalid, create a new proof and ask again.

The domain types can stay small:

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

The important detail is that `RegistrationError` is just a domain error type. FsFlow does not require a special wrapper.

## Start With `Check`

The first place FsFlow helps is not the workflow itself. It is the validation leading into the workflow.

`Check` is the pure predicate layer. It returns ordinary `Result<'value, unit>` values, which means it stays small and easy to compose.

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

If you want the middle of the workflow to stay fail-fast and explicit, `result {}` composes the ordinary `Ok` and `Error` values directly:

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

That is the main point: `result {}` does not reinvent result handling. It gives you a readable way to orchestrate the standard `Result` type that FSharp.Core already provides.

## The Same Story With Operators

The operator story belongs to the same layer.

- `<!>` maps over a successful result
- `<*>` combines independent results
- `>>=` sequences dependent steps

Those names are useful because they describe the shape of the flow, not a separate abstraction. They are just different ways to say "stay in `Result`, keep going when the previous step worked, and stop when it failed."

For a pure registration command, the operator form reads like this:

```fsharp
let createRegistration name mobile =
    { Name = name; Mobile = mobile }

let validateCommandWithOperators (command: RegistrationCommand) : Result<Registration, RegistrationError> =
    createRegistration
    <!> requireName command.Name
    <*> requireMobile command.Mobile
```

When the second step depends on the first, `>>=` is the clearer spelling:

```fsharp
let validateNameThenMobile (command: RegistrationCommand) : Result<Registration, RegistrationError> =
    requireName command.Name
    >>= fun name ->
        requireMobile command.Mobile
        >>= fun mobile ->
            Ok { Name = name; Mobile = mobile }
```

`result {}` is the more maintainable default for most code, but the operators explain the same fail-fast shape in a smaller notation.

## Keep The Decision Pure

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

## Put The Edges In `AsyncFlow`

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
3. `result {}` stays local to validation instead of becoming the thing that drives the whole architecture.

That is the FsFlow version of the sandwich.

## Where `Guard` Fits

`Guard` is the bindable version of the same idea. It is useful when the source already looks like a check or already carries an error.

If the source is check-shaped, `Guard.Of` keeps the source visible:

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

`Check`, `Guard`, and `result {}` are all part of the same progression:

- `Check` expresses the predicate
- `Check.orError` attaches the domain error
- `result {}` composes the ordinary `Result` values fail-fast
- `Guard` lets the same shape bind directly inside the computation expression when the source already comes wrapped

## Testing The Sandwich

Mark Seemann's article uses fakes at the composition root. That is still the right testing story here.

The pure middle gets direct unit tests:

```fsharp
[<Fact>]
let ``a valid proof completes registration`` () =
    let registration = { Name = "Ada"; Mobile = Mobile 123 }

    let decision = decideRegistration true registration

    decision |> should equal (Complete registration)
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

    actual |> should equal (ProofRequired expectedProofId)
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

- `Check` handles pure predicates
- `result {}` handles fail-fast validation over ordinary `Result`
- `Guard` lets source values bind directly inside a computation expression
- `AsyncFlow` carries the impure edges
- the pure middle stays pure

This is the useful part of the Mark Seemann style of architecture: not the particular dependency injection technique, but the separation of decisions from effects.

FsFlow fits that shape because it keeps the abstraction surface small. You do not need to build a second result library to get the architectural win.
