# Richer effects: AlgEff and language-ext `Eff<RT, A>` against Axial capabilities

Status: preliminary research (T27), written before T25a ships. The comparison is against the T25 design
(`~/projects/scratch/axial-coord/pi/capabilities-spec.md`, section 10), not against shipped code. Re-check section 5 once
T25a lands. No implementation proposed.

Sources read, not recalled:

- AlgEff, `brianberns/AlgEff` @ `1a3a1db` (the repo's last commit is 2026-02-26; the earlier history runs from 2020). About 700 lines of F#,
  cloned to `~/projects/study/AlgEff/master`.
- language-ext, `louthy/language-ext` @ `54d27a2d` (2026-10-09), cloned to `~/projects/study/language-ext/main`. Read
  `LanguageExt.Core/Effects/Eff`, `Traits/Has`, and `LanguageExt.Sys` (Live and Test runtimes).

## 1. What each one is

### AlgEff (F#)

A free monad. `Program<'ctx,'ret> = Free of Effect<Program<'ctx,'ret>> | Pure of 'ret`. Each effect is a class
inheriting `Effect<'next>` that carries its arguments and a continuation (`LogEffect(str, cont: unit -> 'next)`) and implements
`Map`. `effect { }` builds the tree; nothing runs.

- The phantom `'ctx` marks which effect families a program uses: `Log.write<'ctx when 'ctx :> LogContext>`. Constraints merge
  like Axial's `IHasX` constraints do. They are marker interfaces; the value of `'ctx` is never read.
- A `Handler<'ctx,'ret,'st,'fin>` interprets effects with explicit state. `TryStep` type-tests the effect
  (`:? 'eff`) and returns `Some results` or `None`. Handlers combine (`combine2`..`combine5`) by trying one then the other.
  The loop is `RunMany`: an unhandled effect is a **runtime** `failwithf "Unhandled effect"`.
- Handlers return `List<'ret * 'stx>`, so a handler may call its continuation zero, one or many times. That gives non-determinism
  and backtracking (`NonDetTest`) and a cooperative concurrency handler by interleaving continuations. This is what "algebraic"
  buys: the handler owns the continuation.
- Missing: async, `Task`, cancellation, disposal, error channel, any real concurrency. The only real I/O is `System.Console`. The
  evaluation is `effect.Map(bind f)` per step, with O(depth) rebuild of the tree per bind (the classic left-nested free monad cost).
- Version 1.0.0 on NuGet since 2020, tiny.

### language-ext `Eff<RT, A>` (C#)

`Eff<RT, A>` is a record over `ReaderT<RT, IO, A>`: a reader of the runtime `RT`, over an `IO` that captures exceptions as `Error`.
Not algebraic effects. It is the mtl / "ReaderT over IO" pattern.

- Capabilities are **traits on `RT`**: `RT : Has<Eff<RT>, ConsoleIO>`. `Has<M,V>` has one static abstract member `Ask: K<M,V>`.
  A runtime record declares `Has<Eff<Runtime>, FileIO>, Has<Eff<Runtime>, TimeIO>, ...` and supplies each `Ask`.
- Helpers are generic over the runtime: `Time<RT>.now` requires `RT : Has<Eff<RT>, TimeIO>` and returns `Eff<RT, DateTime>`. The
  cached accessor `Has<M, RT, V>.ask` is a static readonly, so reads allocate nothing.
- Live and Test are two whole `Runtime` records (`Live/Runtime.cs`, `Test/Runtime.cs`) implementing the same traits with different
  `*IO` implementations (a `MemoryConsole`, a `MemorySystemEnvironment`). The test runtime also carries a temp directory.
- `Local<M, V>` provides scoped environment modification (`local`), like Axial's `withClock`-style overrides.
- The `Sys` capability interfaces (`ConsoleIO`, `FileIO`, `TimeIO`) return `IO<A>`, i.e. the capability is itself an effect value.
- The monad also hosts errors (`Fin`/`Error`), resources, and `IO` cancellation; the generic-over-`M` form (`Console<M,RT>`)
  allows different base monads.

## 2. Where Axial already sits

`Flow<'env,'error,'value>` is already a reader (`'env`), error channel (`'error`) and `IO` with cancellation, fibers, scopes and an exit
type, in one type. That is `Eff<RT,A>` plus a typed error, plus structured concurrency, plus a data representation in progress
(`flow-as-data.md`). `IHasX` constraints are language-ext's `Has<M,V>` trait with F# structural subtyping instead of static
abstract members, and they merge cleanly because of the one-interface-per-service rule (`decisions/README.md` 2026-08-10, FS0193).

So the honest delta is small:

| Concern | Axial (T25 design) | language-ext | AlgEff |
|---|---|---|---|
| Capability appears in the type | yes, `'env :> IHasFileSystem` | yes, `RT : Has<Eff<RT>, FileIO>` | yes, `'ctx :> ConsoleContext` (phantom) |
| Who supplies the implementation | the value in `env` | the value `Ask` returns from `RT` | a handler object, outside the program |
| Production vs test | `Live`/`Fake` on the capability; env chosen at root | two whole `Runtime` records | swap handlers |
| Cross-cutting reads (clock, random, guid) | runtime context, not in the type (T25a) | in the type (`TimeIO`) | in the type |
| Missing capability | compile error (constraint) | compile error | compile error for the context, **runtime** error for a missing handler |
| Handler controls the continuation | no | no | yes (zero/many resumes) |
| Async, cancellation, errors, resources | yes | yes | none |
| Per-bind cost | closures/ValueTask, continuation runtime planned | `ReaderT` over `IO` | tree rebuild per bind |

## 3. What is worth taking

1. **language-ext's `Local`.** Scoped environment change is already `Flow.withClock` etc. in T25a. No gap.
2. **A trait-bound capability-set type (`RT`) per application.** language-ext's selling point is that library code names
   only the traits it needs while the application declares one `Runtime`. Axial's equivalent is the hand-written env record with
   `IHasX` lines. `platform-env-class.md` and the deferred env generator target the same boilerplate. Nothing new in language-ext
   beyond their having chosen static abstract members, which Fable 5.x cannot call generically ("Unexpected static
   interface/override call", re-probed 2026-10-30). That alone rules out porting `Has<M,V>`.
3. **`IO`-returning capability interfaces.** language-ext's `ConsoleIO.WriteLine : IO<Unit>` makes the capability itself a value. In
   Axial, capability members already return `Flow`/`Task`; what a Fake gains from returning a description is replay and
   recording, which `Flow.runTest` plus a recording Fake already give. Not worth a change.
4. **AlgEff's handler-owned continuation.** This is the one genuinely new capability: effects that a handler may resume multiple
   times, abort, or reorder. Uses: non-determinism/backtracking, generators, coroutines, a custom scheduler for test, record/replay
   of effect traces, effect-level mocking of *any* call. None of these is an application need in Axial today; each is covered by
   a purpose-built primitive (`Stream`, `Hub`, `Flow.fork`, the explicit-stepping test clock).

## 4. Why not adopt either

- **AlgEff style as the user model** moves effects out of `'env` into a handler list that the type does not show (only the marker
  context). Unhandled effects become runtime errors; the Axial rule is that missing authority is a compile error and authority
  stays in the env (`capabilities-spec.md` §10: "No general public register-any-service API"). It also costs a tree rebuild per
  bind, and has no cancellation, scopes, or typed errors. It conflicts with the performance bar in
  `axial-reliability-then-perf` and the "Flow-as-data must be faster than today" constraint.
- **`Eff<RT,A>` as the model** is what `Flow` already is; adopting it means a single `'env` conflated with the runtime and no typed
  error channel (`Error` is a closed type), which loses the `Flow<'env,'error,_>` signature that Axial's Elm-leaning errors rely on.
- Both want higher-kinded abstraction (`K<M,A>`, generic `M`) that F# cannot express and that Fable cannot compile through static
  abstracts.

## 5. Open for the T25a comparison

To check once T25a has shipped:

- Cost of runtime-context reads vs env reads (spec benchmark) versus language-ext's cached `Has<M,RT,V>.ask` static. If the runtime
  context read is not within noise, language-ext's static-cached trait read is the model to copy at the type level.
- Whether `Flow-as-data` (T26, FIO's continuation runtime) gives handler-style interpretation for free: with the program as data, a
  test interpreter that records or reorders effects is a second *runtime*, not a new effect system. This is the algebraic-effects
  benefit reached from below, and preferable to a user-visible handler API.
- Whether a *narrow* handler hook is wanted: an installed `IFiberObserver`/`ILogSink` style slot already exists. A sanctioned
  "effect trace" observer (every Clock/Random/Guid read, listed in spec §10 as the optional test mode) covers record/replay without
  handlers.
- Only revisit full handlers if an application needs multi-shot continuations. None has asked.

## 6. Recommendation

Do not adopt either. Keep capabilities as `IHasX` + `ICapability` manifests (T25). Treat AlgEff's contribution as an argument
for T26: a Flow that is data lets a test or tool interpret it differently, without a second user-facing effect system. Treat
language-ext's contribution as confirmation of the Platform/runtime-context split: it puts everything in `RT`, and its own `Sys`
package shows the cost (two near-duplicate runtime records, one live and one test) that Axial's per-service `Live`/`Fake` plus
`runTest` avoids.
