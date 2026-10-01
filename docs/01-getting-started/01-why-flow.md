---
title: Why Flow?
description: Eight things that change when application work returns Flow instead of Task, shown in code.
---

# Why Flow?

`Flow<'env, 'error, 'value>` is a type for application work that runs asynchronously, can fail in expected ways, can
be cancelled, and uses services. Eight things change when that work returns `Flow` instead of `Task`. Each section
shows the difference in code; the last one is what they add up to.

The examples share these types:

```fsharp prepare
type Cart = { Id: int; Total: decimal }
type Reservation = { CartId: int }
type Payment = { Reference: string }
type Receipt = { Reservation: Reservation; Payment: Payment }

type OrderError =
    | CartNotFound
    | OutOfStock
    | CardDeclined

let receipt reservation payment = { Reservation = reservation; Payment = payment }
```

## 1. Async work and expected errors in one type

With `Task`, an operation that can fail returns `Task<Result<_, _>>`, and every step has to match the result before
the next step can run:

```fsharp
let loadCartTask (id: int) : Task<Result<Cart, OrderError>> =
    Task.FromResult(Ok { Id = id; Total = 40m })

let reserveTask (cart: Cart) : Task<Result<Reservation, OrderError>> =
    Task.FromResult(Ok { CartId = cart.Id })

let chargeTask (cart: Cart) : Task<Result<Payment, OrderError>> =
    Task.FromResult(Ok { Reference = "p-1" })

let checkoutTask (id: int) : Task<Result<Receipt, OrderError>> =
    task {
        match! loadCartTask id with
        | Error error -> return Error error
        | Ok cart ->
            match! reserveTask cart with
            | Error error -> return Error error
            | Ok reservation ->
                match! chargeTask cart with
                | Error error -> return Error error
                | Ok payment -> return Ok(receipt reservation payment)
    }
```

In `flow { }`, `let!` waits for the work and stops at the first expected error:

```fsharp
let loadCart (id: int) : Flow<OrderError, Cart> = Flow.ok { Id = id; Total = 40m }
let reserve (cart: Cart) : Flow<OrderError, Reservation> = Flow.ok { CartId = cart.Id }
let charge (cart: Cart) : Flow<OrderError, Payment> = Flow.ok { Reference = "p-1" }

let checkout (id: int) : Flow<OrderError, Receipt> =
    flow {
        let! cart = loadCart id
        let! reservation = reserve cart
        let! payment = charge cart
        return receipt reservation payment
    }
```

An exception thrown inside the work does not escape as a faulted task. It becomes a defect in the flow's outcome, kept
apart from the expected errors in `OrderError`.

[FsToolkit.ErrorHandling](https://demystifyfp.gitbook.io/fstoolkit-errorhandling/)'s `taskResult { }` removes the
same nesting, and `flow { }` reads the same way. The result is still `Task` code, though: cancellation tokens,
failures in parallel work, and cleanup are still handled by hand at every call site. The next three sections cover
those. The [FsToolkit.ErrorHandling comparison](../how-it-compares/fstoolkit-errorhandling-comparison.html) shows how
to use the two together.

## 2. Cancellation is handled the same way everywhere

With `Task`, cancellation is a `CancellationToken` argument that every function must accept and pass on. Running two
loads in parallel with a time limit needs a linked token source, and a failure in one load does not stop the other:

```fsharp
type Profile = { Name: string }
type Order = { Number: int }

type DashboardError =
    | ProfileUnavailable
    | DashboardTimedOut

let loadProfileTask (user: int) (token: CancellationToken) : Task<Profile> =
    task {
        do! Task.Delay(10, token)
        return { Name = "Ada" }
    }

let loadOrdersTask (user: int) (token: CancellationToken) : Task<Order list> =
    task {
        do! Task.Delay(10, token)
        return [ { Number = 1 } ]
    }

let dashboardTask (user: int) (token: CancellationToken) : Task<Profile * Order list> =
    task {
        use limit = CancellationTokenSource.CreateLinkedTokenSource token
        limit.CancelAfter(TimeSpan.FromSeconds 2.0)
        let profile = loadProfileTask user limit.Token
        let orders = loadOrdersTask user limit.Token
        let! _ = Task.WhenAll(profile :> Task, orders :> Task)
        return profile.Result, orders.Result
    }
```

A flow takes no token. The runtime passes cancellation to all the work it starts:

```fsharp
let loadProfile (user: int) : Flow<ClockEnvironment, DashboardError, Profile> =
    Flow.sleep (TimeSpan.FromMilliseconds 10.0) |> Flow.map (fun () -> { Name = "Ada" })

let loadOrders (user: int) : Flow<ClockEnvironment, DashboardError, Order list> =
    Flow.sleep (TimeSpan.FromMilliseconds 10.0) |> Flow.map (fun () -> [ { Number = 1 } ])

let dashboard (user: int) : Flow<ClockEnvironment, DashboardError, Profile * Order list> =
    Flow.zipPar (loadProfile user) (loadOrders user)
    |> Flow.timeout (TimeSpan.FromSeconds 2.0) DashboardTimedOut
```

If `loadOrders` fails, `loadProfile` is interrupted. If the time limit passes, both are interrupted, and whatever they
acquired has been released before `dashboard` returns `DashboardTimedOut`. The same holds for work started with
`Flow.fork`: it runs inside the scope of the flow that started it, and it is interrupted and awaited when that flow
ends. No work outlives the operation that started it, so a request that is cancelled or times out leaves nothing
running.

Retries and timeouts are functions applied to a flow, such as `Flow.retry (Schedule.recurs 3)`. Because a flow is a
description of work rather than work that has started, a retry runs it again from the beginning.

## 3. Dependencies in the signature mark the boundaries

With `Task`, services usually arrive through a constructor or a dependency-injection container. The signature of
`checkoutTask` above does not say that it reserves stock or charges a card.

A flow's first type parameter names the services it uses. Each area of the application declares its own:

```fsharp
type Inventory = { Reserve: Cart -> Result<Reservation, OrderError> }
type Billing = { Charge: Cart -> Result<Payment, OrderError> }
type Shop = { Inventory: Inventory; Billing: Billing }

let reserveStock (cart: Cart) : Flow<Inventory, OrderError, Reservation> =
    Flow.envWith (fun inventory -> inventory.Reserve cart) |> Flow.bind Flow.fromResult

let chargeCard (cart: Cart) : Flow<Billing, OrderError, Payment> =
    Flow.envWith (fun billing -> billing.Charge cart) |> Flow.bind Flow.fromResult

let placeOrder (cart: Cart) : Flow<Shop, OrderError, Receipt> =
    flow {
        let! reservation = reserveStock cart |> Flow.localEnv _.Inventory
        let! payment = chargeCard cart |> Flow.localEnv _.Billing
        return receipt reservation payment
    }
```

`reserveStock` can only reach `Inventory`, and the compiler rejects a call to billing from inside it.
`placeOrder` is where the two areas meet, and `Flow.localEnv` marks each crossing. A test of `reserveStock` supplies an
`Inventory` and nothing else.

## 4. Time, randomness, and IDs are services too

Code that reads `DateTimeOffset.UtcNow` or calls `Guid.NewGuid()` gives a different result on every run, and nothing in
its signature says so. Testing it means waiting, or replacing a static.

In a flow, the clock and the GUID generator are services like any other. Reading them adds their requirement to the
flow's type:

```fsharp
type PlacedOrder = { Id: Guid; PlacedAt: DateTimeOffset }

let stamp<'env when 'env :> IHasClock and 'env :> IHasGuid> : Flow<'env, Never, PlacedOrder> =
    flow {
        let! placedAt = Clock.now
        let! id = Guid.newGuid
        return { Id = id; PlacedAt = placedAt }
    }
```

`Clock.now` requires an environment that has a clock, and the requirement carries up to every flow that calls
`stamp`, whether or not their types are written out. The application supplies the live services; a test supplies fixed
ones and gets the same result every time:

```fsharp
type Fixed =
    { Clock: IClock
      Guid: IGuid }
    interface IHasClock with
        member this.Clock = this.Clock
    interface IHasGuid with
        member this.Guid = this.Guid

let fixedServices =
    { Clock = Clock.fromValue (DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero))
      Guid = Guid.fromValue Guid.Empty }

let placed = stamp |> Flow.run fixedServices
```

This is what keeps a workflow pure in the useful sense: everything it depends on arrives through its environment, so
the same environment gives the same result. With [Axial.Guardrails](../notes/guardrails.html) installed, the build
also warns when code reads the clock, randomness, or environment variables directly instead of through a service.

## 5. Resources belong to the flow that acquired them

With `Task`, `use` releases a resource when the function that opened it returns. A helper that opens a connection
cannot hand it to its caller and still guarantee it is closed, and a resource opened by background work has no owner.

A flow registers each resource with its scope. A helper can acquire one and return it, and the resource stays open
until the scope that ran the helper closes:

```fsharp
type Connection = { Name: string }

let closed = ResizeArray<string>()

let connect (name: string) : Flow<Connection> =
    Flow.scopeAcquireRelease (Flow.ok { Name = name }) (fun connection _ ->
        closed.Add connection.Name
        Task.CompletedTask)

let report : Flow<string> =
    flow {
        let! orders = connect "orders"
        let! billing = connect "billing"
        return $"{orders.Name} and {billing.Name} are open"
    }
    |> Flow.scoped
```

`Flow.scoped` closes both connections when `report` ends, in reverse order, whether it succeeds, fails, or is
interrupted. Work forked inside the scope is interrupted and awaited before its resources are released. The
[scopes guide](../scopes/index.html) compares this with `use` in more detail.

## 6. Streams keep the same rules

`FlowStream` is a stream of values produced by flows. Its operators run under the same cancellation and scope rules,
so a pipeline with parallel workers stops all of them, and releases what they acquired, as soon as the consumer has
enough:

```fsharp
let fetchPage (id: int) : Flow<ClockEnvironment, Never, string> =
    Flow.sleep (TimeSpan.FromMilliseconds 5.0) |> Flow.map (fun () -> $"page {id}")

let firstTen : Flow<ClockEnvironment, Never, string list> =
    FlowStream.fromSeq [ 1..1000 ]
    |> FlowStream.mapFlowPar (Parallelism.bounded 4) fetchPage
    |> FlowStream.take 10
    |> FlowStream.runCollect
```

At most four pages are fetched at a time, and once ten have arrived the remaining fetches are interrupted. The same
pipeline written with `IAsyncEnumerable` and `SemaphoreSlim` has to cancel and await its own workers. The
[streams guide](../streams/index.html) covers batching, time-based operators, and connecting streams to queues.

## 7. You can see what is running

A `Task` does not know which operation started it, and .NET cannot list the tasks that are still running. Every forked
flow is a fiber with an id, a parent, and an optional name. A `FiberRegistry` lists the live ones as a tree, and
annotations travel to every fiber the flow starts:

```fsharp
let registry = FiberRegistry(100)

let poller : Flow<ClockEnvironment, Never, unit> =
    flow {
        let! _ = Flow.sleep (TimeSpan.FromMinutes 1.0) |> Flow.forkNamed "outbox-poller"
        return ()
    }
    |> Flow.annotate "tenant" "acme"
    |> Flow.withFiberRegistry registry
```

`registry.DumpAt(clock)` prints each live fiber with its name, status, age, and annotations, which is what a diagnostics
endpoint or a stuck shutdown needs. `Axial.Telemetry` turns the same information into OpenTelemetry spans and metrics,
including a count of failures in background work that nothing awaited. See [observability](../observability/index.html).

## 8. Fewer states to reason about

The first seven points shrink what a reader, a reviewer, or a test has to consider.

A flow ends in one of three ways: its value, one of the errors named in its type, or an unexpected outcome (a defect or
an interruption). There is no fourth case such as an exception that escapes past a `Result`, or a task still running
after its caller has returned.

Concurrency follows fixed rules instead of per-call-site choices. Every forked fiber belongs to the scope that started
it. Cancellation always arrives as an interruption, never as an error value that one layer handles and the next
ignores. A scope releases its resources in reverse order however it ends. An interrupted take from a queue never loses
a value. Code that uses these pieces does not need to be checked for each interleaving by hand, and the
[torture tests](../concurrency-and-state/torture-tests/index.html) check that the rules hold under random interruption.

The build enforces the conventions the types cannot. [Axial.Guardrails](../notes/guardrails.html) reports direct calls
to the clock, randomness, and other ambient effects (`AXG001`), exceptions raised inside `flow { }` (`AXG003`), task
adapters that drop their cancellation token (`AXG005`), and formatting that breaks under NativeAOT (`AXG006`).

This matters most when the code is written by an LLM coding assistant, or by someone new to the codebase. The
signature says which services exist and which errors are expected, so the assistant has less to guess. The mistakes it
tends to make are harder to make or show up in the build: forked work is owned by its scope even if nobody awaits it,
there is no token to forget to pass, and reading `DateTime.Now` or dropping a token in a task adapter produces a
build warning. Review can then focus on the business logic.

## When to use something else

Validation and other pure transformations need none of this: write them as ordinary functions returning `Result`.
Return `Flow` from application
operations: the ones that call services, can be cancelled, need a timeout or retry, own a resource, or start
background work. A `Task`-based service can be called from a flow directly, so you can adopt Axial one module at a time; see
[Add Axial to an existing Task application](existing-task-application.html).

## Related guides

- [Task vs Flow: seven scenarios](/how-it-compares/task-vs-flow-scenarios.html) compares ownership, cancellation,
  retries, and background work in longer examples.
- [Flow compared with Effect-TS](/how-it-compares/effect-ts-comparison.html) explains the shared model and where F#
  leads to a different API.
- [Compiler-directed, AOT, and Fable](/notes/packages-and-platforms.html) describes the supported runtime targets.
