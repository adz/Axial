/// Concurrency laws over generated flows: each round builds random trees of fork, race, zipPar, scope, timeout,
/// interruption, recovery and folds over leaves that succeed, fail, die or hang, runs them, and checks the laws every
/// composition must keep, whatever its shape and interleaving.
module Axial.TortureTest.Laws

open System
open Axial
open Axial.TortureTest.Scenario

/// What a leaf does after a short random sleep.
type Behaviour =
    | Succeed
    | Fail
    | Die
    /// Waits forever; only generated where an enclosing timeout, interruption or scope close ends it.
    | Hang

/// How a fork node ends its child after running its body.
type ForkEnd =
    | Join
    | Interrupt
    /// Leaves the child to the fork's scope, which interrupts it on close.
    | Abandon

/// A generated flow. Every node has an id, so the laws can name the node they caught.
type Node =
    | Leaf of id: int * behaviour: Behaviour * sleepMs: int
    | Then of id: int * first: Node * second: Node
    | ZipPar of id: int * left: Node * right: Node
    | Race of id: int * left: Node * right: Node
    | Fork of id: int * ending: ForkEnd * child: Node * body: Node
    | Scoped of id: int * inner: Node
    | Recover of id: int * inner: Node
    | Fold of id: int * inner: Node
    | Timeout of id: int * afterMs: int * inner: Node
    | Interrupted of id: int * afterMs: int * inner: Node

let idOf node =
    match node with
    | Leaf(id, _, _) | Then(id, _, _) | ZipPar(id, _, _) | Race(id, _, _) | Fork(id, _, _, _) | Scoped(id, _)
    | Recover(id, _) | Fold(id, _) | Timeout(id, _, _) | Interrupted(id, _, _) -> id

let private behaviourName behaviour =
    match behaviour with
    | Succeed -> "ok"
    | Fail -> "fail"
    | Die -> "die"
    | Hang -> "hang"

let private endingName ending =
    match ending with
    | Join -> "join"
    | Interrupt -> "interrupt"
    | Abandon -> "abandon"

/// The node as a short expression, for reproducing a failure by hand.
let rec describe node =
    match node with
    | Leaf(id, behaviour, ms) -> $"{behaviourName behaviour}#{id}({ms}ms)"
    | Then(_, a, b) -> $"({describe a} >>= {describe b})"
    | ZipPar(_, a, b) -> $"zipPar({describe a}, {describe b})"
    | Race(_, a, b) -> $"race({describe a}, {describe b})"
    | Fork(_, ending, child, body) -> $"fork[{endingName ending}]({describe child}; {describe body})"
    | Scoped(id, inner) -> $"scoped#{id}({describe inner})"
    | Recover(_, inner) -> $"recover({describe inner})"
    | Fold(_, inner) -> $"fold({describe inner})"
    | Timeout(_, ms, inner) -> $"timeout {ms}ms ({describe inner})"
    | Interrupted(_, ms, inner) -> $"interrupt {ms}ms ({describe inner})"

// <snippet:torture-laws>
/// Builds a random tree of at most <paramref name="depth" /> levels. A hanging leaf is generated only where
/// <paramref name="bounded" /> says something above will end it.
let generate (round: Round) (depth: int) : Node =
    let next = ref 0
    let fresh () =
        let id = next.Value
        next.Value <- id + 1
        id

    let rec node depth bounded =
        let leaf () =
            let behaviour =
                match round.Next 10 with
                | 0 | 1 -> Fail
                | 2 -> Die
                | 3 when bounded -> Hang
                | _ -> Succeed
            Leaf(fresh (), behaviour, round.Next 3)

        if depth <= 0 || round.Chance 25 then
            leaf ()
        else
            let below = depth - 1
            match round.Next 10 with
            | 0 -> Then(fresh (), node below bounded, node below bounded)
            | 1 -> ZipPar(fresh (), node below bounded, node below bounded)
            | 2 -> Race(fresh (), node below bounded, node below bounded)
            | 3 ->
                let ending = match round.Next 3 with 0 -> Join | 1 -> Interrupt | _ -> Abandon
                // A joined child must end by itself; an interrupted or abandoned one is ended by its fork.
                Fork(fresh (), ending, node below (bounded || ending <> Join), node below bounded)
            | 4 -> Scoped(fresh (), node below bounded)
            | 5 -> Recover(fresh (), node below bounded)
            | 6 -> Fold(fresh (), node below bounded)
            | 7 -> Timeout(fresh (), round.Next 4, node below true)
            | 8 -> Interrupted(fresh (), round.Next 4, node below true)
            | _ -> Then(fresh (), node below bounded, node below bounded)

    node depth false
// </snippet:torture-laws>

exception LeafDefect of int

/// What one run of a tree recorded, and the laws it broke.
type private Run(nodes: int) =
    let gate = obj ()
    let live = Array.zeroCreate<int> nodes
    let defectSeen = Array.zeroCreate<int> nodes
    let died = Array.zeroCreate<bool> nodes
    // 0 not acquired, 1 acquired, 2 released.
    let resource = Array.zeroCreate<int> nodes
    let violations = Collections.Generic.Dictionary<string, string>()

    member _.Violate (law: string) (detail: string) =
        lock gate (fun () -> if not (violations.ContainsKey law) then violations[law] <- detail)

    member _.Violations = lock gate (fun () -> [ for pair in violations -> pair.Key, pair.Value ])

    member _.Enter(path: int list) = lock gate (fun () -> for id in path do live[id] <- live[id] + 1)
    member _.Leave(path: int list) = lock gate (fun () -> for id in path do live[id] <- live[id] - 1)
    member _.Live(id: int) = lock gate (fun () -> live[id])

    member _.Died(id: int) = lock gate (fun () -> died[id] <- true)

    /// Counts every leaf defect in an outcome that was observed here and not passed on.
    member _.Observe(exit: Exit<'value, 'error>) =
        match exit with
        | Exit.Success _ -> ()
        | Exit.Failure cause ->
            for defect in Cause.defects cause do
                match defect with
                | LeafDefect id -> lock gate (fun () -> defectSeen[id] <- defectSeen[id] + 1)
                | _ -> ()

    member this.Unobserved(defect: exn) = this.Observe(Exit.Failure(Cause.Die defect) : Exit<unit, unit>)

    member _.Acquire(id: int) =
        lock gate (fun () -> resource[id] <- 1)

    member this.Release(id: int) =
        let before = lock gate (fun () -> let before = resource[id] in resource[id] <- 2; before)
        if before <> 1 then this.Violate "cleanup ran exactly once, after its acquire" $"resource {id} released from state {before}"

    member _.Resource(id: int) = lock gate (fun () -> resource[id])

    /// Every leaf that died is seen exactly once, in the root's outcome, an outcome a fork or interrupt observed, or
    /// an unobserved-defect report; and nothing is seen that never died.
    member this.CheckDefects() =
        for id in 0 .. nodes - 1 do
            let seen, didDie = lock gate (fun () -> defectSeen[id], died[id])
            if didDie && seen <> 1 || not didDie && seen <> 0 then
                this.Violate "every defect was reported exactly once" $"leaf {id} died={didDie} seen {seen} times"

let private finalizerOf (action: unit -> unit) : Flow<unit, Never, unit> = finalizer action

/// Turns a tree into an instrumented flow. Each leaf counts itself live in every enclosing node while it runs; each
/// node checks, as it ends by any outcome, that none of its leaves is still running.
let rec private compile (run: Run) (path: int list) (node: Node) : Flow<unit, string, int> =
    let id = idOf node
    let here = id :: path
    let ended (law: string) =
        finalizerOf (fun () ->
            let still = run.Live id
            if still <> 0 then run.Violate law $"node {id} ended with {still} leaf(s) running")

    let child = compile run here
    match node with
    | Leaf(_, behaviour, ms) ->
        let entered = ref false
        flow {
            do! Flow.delay (fun () -> entered.Value <- true; run.Enter here; Flow.ok ())
            do! Flow.sleep (TimeSpan.FromMilliseconds(float ms))
            match behaviour with
            | Succeed -> return id
            | Fail -> return! Flow.fail $"leaf {id}"
            | Die ->
                run.Died id
                return! Flow.die (LeafDefect id)
            | Hang -> return! Flow.never
        }
        |> Flow.ensuring (finalizerOf (fun () -> if entered.Value then run.Leave here))
    | Then(_, first, second) ->
        flow {
            let! a = child first
            let! b = child second
            return a + b
        }
    | ZipPar(_, left, right) ->
        Flow.zipPar (child left) (child right) |> Flow.map (fun (a, b) -> a + b)
        |> Flow.ensuring (ended "a flow that starts work waits for it")
    | Race(_, left, right) ->
        Flow.race (child left) (child right)
        |> Flow.ensuring (ended "a race's loser settles before the race returns")
    | Fork(_, ending, forked, body) ->
        flow {
            let! fiber = Flow.fork (child forked)
            let! b = child body
            match ending with
            | Join ->
                let! a = Fiber.join fiber
                return a + b
            | Interrupt ->
                let! exit = Fiber.interrupt fiber
                run.Observe exit
                return b
            | Abandon -> return b
        }
        |> Flow.scoped
        |> Flow.ensuring (ended "a flow that starts work waits for it")
    | Scoped(_, inner) ->
        let resource =
            Resource.create
                (Flow.delay (fun () -> run.Acquire id; Flow.ok id))
                (fun acquired -> finalizerOf (fun () -> run.Release acquired))
        flow {
            let! _ = Flow.scopeResource resource
            return! child inner
        }
        |> Flow.scoped
        |> Flow.ensuring (finalizerOf (fun () ->
            if run.Resource id = 1 then run.Violate "cleanup ran exactly once, after its acquire" $"resource {id} still held after its scope"))
    | Recover(_, inner) ->
        let interrupted = ref false
        child inner
        |> Flow.onInterrupt (finalizerOf (fun () -> interrupted.Value <- true))
        |> Flow.recover (fun _ -> Flow.ok 0)
        |> Flow.map (fun value ->
            if interrupted.Value then run.Violate "interruption is never swallowed by recover or fold" $"recover {id} succeeded after its source was interrupted"
            value)
    | Fold(_, inner) ->
        let interrupted = ref false
        child inner
        |> Flow.onInterrupt (finalizerOf (fun () -> interrupted.Value <- true))
        |> Flow.fold (fun value -> value) (fun _ -> -1)
        |> Flow.map (fun value ->
            if interrupted.Value then run.Violate "interruption is never swallowed by recover or fold" $"fold {id} succeeded after its source was interrupted"
            value)
    | Timeout(_, ms, inner) ->
        child inner
        |> Flow.timeoutOption (TimeSpan.FromMilliseconds(float ms))
        |> Flow.map (Option.defaultValue -2)
        |> Flow.ensuring (ended "a flow that starts work waits for it")
    | Interrupted(_, ms, inner) ->
        let interrupted = ref false
        flow {
            let! fiber = child inner |> Flow.onInterrupt (finalizerOf (fun () -> interrupted.Value <- true)) |> Flow.fork
            do! Flow.sleep (TimeSpan.FromMilliseconds(float ms))
            let! exit = Fiber.interrupt fiber
            run.Observe exit
            match exit with
            | Exit.Success value ->
                if interrupted.Value then run.Violate "interruption is never swallowed by recover or fold" $"interrupted {id} succeeded after its source was interrupted"
                return value
            | Exit.Failure _ -> return -3
        }
        |> Flow.scoped
        |> Flow.ensuring (ended "interrupting a fiber waits for it to end")

let rec private size node =
    match node with
    | Leaf _ -> 1
    | Then(_, a, b) | ZipPar(_, a, b) | Race(_, a, b) | Fork(_, _, a, b) -> 1 + size a + size b
    | Scoped(_, a) | Recover(_, a) | Fold(_, a) | Timeout(_, _, a) | Interrupted(_, _, a) -> 1 + size a

/// The laws, each held on every generated tree of the round.
let laws =
    [ "a flow that starts work waits for it"
      "a race's loser settles before the race returns"
      "interrupting a fiber waits for it to end"
      "cleanup ran exactly once, after its acquire"
      "every defect was reported exactly once"
      "interruption is never swallowed by recover or fold" ]

/// Runs one tree and returns the laws it broke, with the detail and the tree.
let runTree (node: Node) : Flow<unit, Never, (string * string) list> =
    flow {
        let run = Run(size node + 1)
        let observer = { FiberObserver.none with OnUnobservedDefect = fun _ defect -> run.Unobserved defect }
        let! exit = compile run [] node |> Flow.withFiberObserver observer |> exitOf
        run.Observe exit
        run.CheckDefects()
        return run.Violations |> List.map (fun (law, detail) -> law, $"{detail} in {describe node}")
    }

let run (round: Round) : Flow<unit, Never, Check list> =
    flow {
        let! broken =
            [ 1 .. round.Size 40 ]
            |> Flow.traverse (fun _ -> runTree (generate round 5))
        let broken = List.concat broken
        return
            [ for law in laws do
                  match broken |> List.tryFind (fun (name, _) -> name = law) with
                  | None -> check law true
                  | Some(_, detail) ->
                      check law false
                      // The first tree that broke it, so the failure can be reproduced by hand.
                      check $"{law}: {detail}" false ]
    }

let scenario : Scenario =
    { Name = "laws"
      Title = "generated flows keep the concurrency laws"
      Run = run }
