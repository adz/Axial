# Documentation review: docs/01-getting-started

## Assumptions

- `docs/10-streams/02-constructing.md:166` — The crawler uses `Map.find` for every discovered URL and assumes all links exist in the in-memory site; a real crawl needs a typed fetch error or an explicit missing-page policy.

- `docs/09-scopes/_index.md:33` — `File.OpenText path` assumes the file exists and acquisition succeeds, but the example has no typed filesystem error or explicit filesystem dependency; label it illustrative rather than a production read pattern.

- `docs/09-scheduling-and-retries/_index.md:126` — The copyable jitter example constructs `System.Random()` directly; the next paragraph recommends injected `IRandom`. Show the service-based wiring so repeated runs and tests do not depend on an ambient random source.

- `docs/08-concurrency-and-state/07-hub.md:168` — The display example polls `Hub.subscriberCount` without a deadline; if the consumer fails before subscribing, the example hangs instead of surfacing its exit. Show a bounded handshake or failure-aware startup.

- `docs/07-layers/02-tutorial.md:142` — The fake connection is created before provisioning and reused; the example assumes a single run. Show acquisition inside the layer for a reusable resource layer.

- `docs/06-services/02-console.md:43` — `Console.readLine` may return null at EOF; `answer.Trim()` assumes interactive input always supplies a line. Show EOF handling in a reusable confirmation example.

- `docs/05-dependencies/04-providing-the-environment.md:52` — `SqlUserStore(connectionString)` is invoked without a definition or indication that it is pseudocode; a newcomer may treat the production record as copyable.

- `docs/03-the-flow-type/06-task-async-interop.md:22` — “Flow cannot pass cancellation into” a started task assumes it was created without a token the caller controls; clarify that the Flow cannot supply its own token retroactively, even if the caller has another cancellation source.

- `docs/02-how-it-compares/01-fstoolkit-errorhandling-comparison.md:107` — Cancellation propagation is attributed to `ColdTask`, but both shown factories ignore the token (`fun _ -> ...`); the repository calls must accept and use it for interruption to reach in-flight work.

- `docs/01-getting-started/_index.md:16` — F# Interactive instructions assume `#r "nuget: Axial"` and `open Axial` suffice, but the first visible block is a `prepare` fixture opening many optional package namespaces. Readers may paste it and get missing-namespace errors after installing only Axial.

## Unclear or misleading

- `docs/10-streams/_index.md:21` — “Upstream cannot outrun” downstream is too absolute for the later producer-fiber and parallel operators; qualify this as bounded backpressure, with `fromCallback` explicitly able to drop values on overflow.
- `docs/10-streams/04-batching-and-parallelism.md:35` — “Does not begin the next batch until the current result list is consumed” could imply per-item consumption of that list; the terminal pulls the next batch after the previous batch has been emitted, regardless of what the caller does with its elements.
- `docs/10-streams/08-time-and-latest.md:83` — The checked search example debounces before `switchMapFlow`, so it never demonstrates interruption of an in-flight request; add a separate example with a request already running when the next input arrives.

- `docs/09-scopes/_index.md:148` — A release defect is said to become a defect “of the flow that closed the scope,” but the page does not show how it combines with an existing failure or appears in `Exit`; clarify the observable outcome here.

- `docs/09-scheduling-and-retries/_index.md:360` — The comment says polling continues “until the flow fails,” but `pollStatus` has error type `Never` and always succeeds; this example runs indefinitely unless interrupted. Show a stopping condition or state explicitly that the caller must interrupt it.
- `docs/09-scheduling-and-retries/_index.md:247` — `resetAfter` calls a long run “successful,” yet the supervision example describes a worker that crashes after ten healthy minutes. Clarify that the threshold measures the duration of the failed run before its failure, not a previous successful execution.

- `docs/08-concurrency-and-state/09-cache.md:105` — “There is no expiry” contradicts the `Cache.makeBoundedWithExpiry` option at line 61. Qualify this as the behavior of the default cache/memoized flow.
- `docs/08-concurrency-and-state/09-cache.md:40` — `readConfigFromDisk` only increments a `Ref`; the comment says the file is read once, but no disk read occurs. Name it as a simulated load so readers do not mistake it for filesystem guidance.
- `docs/08-concurrency-and-state/05-stm.md:36` — `withdraw` and `transferFunds` subtract without checking available funds; the banking example silently permits negative balances, unlike the linked torture test's guarded transfers. Add an insufficient-funds condition or label the operations arithmetic-only.
- `docs/08-concurrency-and-state/10-torture-tests/16-interop.md:30` — “A cancelled token becomes an interruption” omits the distinction between runtime-requested cancellation and a foreign operation throwing cancellation while the runtime token is live; qualify the claim.

- `docs/07-layers/_index.md:121` — “A record cannot express that” overstates the case: a factory returning `Result<AppEnv, BaseRuntimeError>` can model typed startup failure without a layer. Explain the added value of scoped provisioning and `Layer.provide` instead.
- `docs/07-layers/_index.md:131` — A host-created `ILoggerFactory` can still be placed in a record after host startup; the example demonstrates adapting a dependency, not an inability to use records.
- `docs/07-layers/_index.md:54` — “Its lifetime is the runtime's” suggests a longer-lived resource than the later `Layer.provide` boundary, which releases it when the provided flow finishes. Name the provided-flow scope here.

- `docs/06-services/01-platform-services/01-clock.md:103` — The manual-clock example says to advance after a sleeper registers but provides only a comment, not a wait; copying the snippet can race and hang. The later suggestion to implement a mutable clock also omits the already introduced `ManualClock`.
- `docs/06-services/03-filesystem.md:134` — “The `Async` members on `IFileSystem` ... return `Flow`” blurs service methods and workflow wrappers; distinguish the interface's signatures from the `FileSystem.*Async` functions readers call.
- `docs/06-services/04-files.md:5` — “A process crash cannot expose a partially written destination” needs a qualification for failures during rename or when the destination did not previously exist; specify the actual atomicity guarantee and supported filesystems.

- `docs/05-dependencies/04-providing-the-environment.md:83` — The section says to use the container to build an environment and leave it behind, but its only direct example runs `Flow<IServiceProvider, _, _>`; show the conversion into an application record at the edge.
- `docs/05-dependencies/05-tutorials/01-explicit-dependencies.md:52` — `TimedOut` and `Cancelled` are declared as typed errors although the tutorial never produces either; cancellation should remain an interruption unless the workflow owns its cancellation source.
- `docs/05-dependencies/05-tutorials/02-app-record.md:18` — “Reuse The Same Interfaces” is inaccurate: the page redefines the types independently and changes the error union and adds audit; readers cannot simply continue the preceding tutorial's session.
- `docs/05-dependencies/05-tutorials/02-app-record.md:213` — “Only `Flow.run env` names the record” contradicts the `Flow<AppEnv, …>` annotations on each helper; distinguish passing the record value from naming its type.

- `docs/04-error-handling/03-defects.md:24` — “Cause.Interrupt for cancellation” does not distinguish cancellation requested by the Flow runtime from a foreign `OperationCanceledException`; the latter is an expected exception for `attempt*` while the runtime token is live.
- `docs/04-error-handling/02-policy.md:133` — `Policy.compose requireName normalizeName` rejects whitespace-only names before trimming; if the intended rule is “trim, then require a name,” reverse the composition and show the effect on the returned value.
- `docs/04-error-handling/03-defects.md:50` — `File.ReadAllTextAsync` is an ambient filesystem operation in an error-handling example; clarify that this demonstrates interop exception capture, not the recommended explicit filesystem-service boundary.

- `docs/03-the-flow-type/01-flow-type.md:28` — Calling every Flow an “immutable, cold description” obscures that `Flow.awaitStartedTask` can wrap work already running; distinguish cold Flow execution from the lifecycle of captured inputs.
- `docs/03-the-flow-type/08-resources.md:13` — `File.OpenText path` runs inside the Flow but is an ambient filesystem effect and is not interruptible; the resource example does not model the filesystem dependency or failure channel it teaches elsewhere.
- `docs/03-the-flow-type/08-resources.md:54` — “The flow's own outcome is kept” sounds like finalizer defects cannot change the outcome, but the next sentence says they are added; state how combined causes appear at the boundary.

- `docs/02-how-it-compares/02-task-vs-flow-scenarios.md:62` — The retry scenario says the type requires only `IHasHttp`, although the described backoff and deadline also need a clock; clarify the full environment requirement.
- `docs/02-how-it-compares/01-fstoolkit-errorhandling-comparison.md:143` — Checkout claims dependencies are in the environment rather than closures, but the earlier `loadCustomerFlow` example closes over its repository; distinguish the two approaches.

- `docs/01-getting-started/03-existing-task-application.md:101` — The claim that adding a `CheckoutError` case makes every boundary mapping incomplete is undermined by the catch-all `Exit.Failure cause` arm; this match remains exhaustive and the compiler need not flag it.
- `docs/01-getting-started/03-existing-task-application.md:103` — The catch-all uses `failwith`, which creates a new exception rather than re-raising the original defect or preserving cancellation; show a cancellation-aware boundary mapping.
- `docs/01-getting-started/01-why-flow.md:282` — The sample poller forks and immediately returns; the page says child fibers are interrupted when their parent ends, so `registry.DumpAt(clock)` cannot reliably show a live poller from this example.
- `docs/01-getting-started/01-why-flow.md:203` — “The same environment gives the same result” overpromises determinism: live service implementations may read external state or have mutable state.

## Too long

- `docs/10-streams/02-constructing.md:105` — The two crawler walkthroughs occupy over half the construction guide and introduce HTTP-shaped domain modeling and frontier bookkeeping before the basic source choices are complete; move the crawler to a dedicated recipe and retain a short `unfoldFlow` example here.

- `docs/09-scopes/_index.md:358` — The rules recap repeats the lifetime maps, concurrency table, and fork walkthrough; shorten it to a decision checklist linking back to the detailed sections.

- `docs/09-scheduling-and-retries/_index.md:151` — The long sequence of union, intersect, andThen, output predicates, budgets, reset, retry, and repeat makes the first-use guide hard to scan. Split basic retry/repeat from advanced schedule composition.

- `docs/08-concurrency-and-state/01-fibers.md:244` — “What a fiber owns” repeats the ownership and `forkResource` rules already explained at line 77; consolidate the two sections and keep the graceful-stop walkthrough separate.

- `docs/07-layers/01-layers.md:141` — The function catalog and separate zip/merge discussion interrupt the provisioning walkthrough; keep the choices needed by the example and move the full combinator inventory to reference documentation.

- `docs/06-services/02-console.md:139` — The 49-line full `IConsole` fake overwhelms the console usage guide; move the complete implementation to a test-helper recipe and retain a short assertion or link.

- `docs/05-dependencies/01-the-environment.md:70` — The application-record example, two complete fake implementations, and assertions repeat the dedicated app-record tutorial; keep a minimal projection example here and link to the tutorial.

- `docs/04-error-handling/_index.md:41` — The single paragraph combines recovery, traced and compound causes, error-type changes, wrapper exceptions, and handler cancellation; split into a short choice guide and a focused compound-cause explanation.

- `docs/03-the-flow-type/06-task-async-interop.md:272` — The summary repeats the interop selection table and section conclusions; retain one quick decision aid and link to the examples.

- `docs/02-how-it-compares/02-task-vs-flow-scenarios.md:8` — Seven scenarios repeat the same source link, unchecked snippet, ZIO correspondence, and three-part guarantee list; a summary matrix with links to individual scenarios would make this comparison easier to scan.

- `docs/01-getting-started/01-why-flow.md:302` — The concluding paragraph repeats the preceding sections' points about service signatures, fork ownership, tokens, and guardrails without adding a next step.

## Reference material in guides

- `docs/10-streams/06-consuming.md:63` — The six-step lifecycle and per-operator scope ownership rules read as a scope contract; keep the practical cleanup guarantee here and link to the scopes guide for the detailed ordering.

- `docs/09-scopes/_index.md:233` — The combinator-by-combinator scope transfer table is a specialized contract better maintained in API reference; keep the value-received ownership rule and link to per-combinator details.

- `docs/09-scheduling-and-retries/_index.md:388` — The 18-row signature table duplicates the guide's descriptions and belongs in API reference; keep a short selection guide and link to the signatures.

- `docs/08-concurrency-and-state/03-ref.md:92` — The full signature table repeats the immediately preceding examples of each `Ref` operation; link to API reference and retain the atomicity and get/set pitfall in this guide.
- `docs/08-concurrency-and-state/05-stm.md:114` — Two signature tables restate the `TRef` and `STM` overview; move exhaustive signatures to API reference and use this space for `retry`/`orElse` behavior.

- `docs/07-layers/01-layers.md:143` — The table enumerates `map`, `mapError`, `widenError`, `bind`, `zip`, `zipPar`, `merge`, `map2`, and `map3` without showing most in use; link to their API reference and retain the composition rules that matter here.

- `docs/06-services/03-filesystem.md:131` — The catalog of async methods, metadata setters, symlinks, directory enumerators, and path helpers across lines 131–197 is API-reference material; keep representative usage and link to a searchable operation reference.

- `docs/05-dependencies/03-service-contracts.md:146` — The subtle `#T` type-variable rule is reference material in a conceptual guide; keep a brief warning here and link to a focused signature reference with examples.

- `docs/03-the-flow-type/07-troubleshooting-types.md:13` — The complete alias expansion table duplicates `01-flow-type.md:35`; link to the canonical table and focus on the diagnostic here.

- `docs/02-how-it-compares/02-task-vs-flow-scenarios.md:107` — The `forkResource` scope rule is a specialized concurrency contract inside a comparison; link to the scopes reference for the rule and keep only the comparison-specific consequence here.

- `docs/01-getting-started/01-why-flow.md:298` — Diagnostic IDs and what each guardrail checks are reference detail; link to the guardrails page rather than maintaining an abbreviated diagnostic list here.

## Missing

- `docs/10-streams/_index.md:28` — The guide list omits `08-time-and-latest.md` and `09-resources-and-results.md`; add both so a new reader can discover timed operators and resource-backed streams without guessing URLs.
- `docs/10-streams/02-constructing.md:49` — Callback overflow is explained but no checked example fills the capacity or shows how to handle `CallbackOverflow.Fail`; demonstrate the observable dropped-value and failure behavior.
- `docs/10-streams/09-resources-and-results.md:73` — `runTryHead`, `runTryLast`, and `runCount` are described without a checked result example, including what an empty stream returns; add a small example of each.

- `docs/09-scopes/_index.md:144` — Release failures and interruption safety are described but never exercised in a checked example; show a failing body plus a defective release and demonstrate the resulting `Exit` and cleanup order.

- `docs/09-scheduling-and-retries/_index.md:353` — Repetition has no checked example showing that the schedule receives the previous successful value or that a failure ends the loop; add a bounded example with `whileInput` and an observable exit.
- `docs/09-scheduling-and-retries/_index.md:131` — The advice to use `IRandom` does not show how to project it from the environment into `jitteredWith`'s `(unit -> float)` callback; give a concrete environment-bound example.

- `docs/08-concurrency-and-state/05-stm.md:18` — `STM.retry` and `STM.orElse` are named but never demonstrated in the guide; add a runnable wait-for-funds/fallback example showing when retry wakes and how interruption ends the wait.
- `docs/08-concurrency-and-state/09-cache.md:61` — Expiry and bounded eviction are introduced without an example or an explanation of what happens to an in-flight load when invalidated/evicted. Show a clock-controlled expiry and concurrent invalidation case.
- `docs/08-concurrency-and-state/_index.md:8` — The landing page advertises Schedule and FlowStream but provides no links to their guides; link to the actual sections so new readers can locate them.

- `docs/07-layers/01-layers.md:156` — The text tells readers to map differing startup errors before providing but gives no example of combining a `Never` layer and a typed-error layer, or of mapping distinct error unions; show the conversion at the composition site.
- `docs/07-layers/01-layers.md:209` — Parallel failure claims `Cause.Both`, but there is no checked example showing two failing branches or how callers inspect the combined cause.

- `docs/06-services/_index.md:36` — The section navigation omits `04-files.md` and `07-websocket.md`; first-time readers cannot discover those capabilities from this landing page.
- `docs/06-services/04-files.md:7` — `AppendLog.openLog` is described as scoped but no runnable example shows acquiring, appending, replaying, and closing it; include a minimal end-to-end example and required environment.
- `docs/06-services/07-websocket.md:3` — No example shows opening a session, sending/receiving a message, handling `WebSocketError`, or supplying a live/test service; the page cannot guide a first integration.

- `docs/05-dependencies/05-tutorials/02-app-record.md:208` — The app-record test asserts success, email, and audit but not the saved order or failure paths; demonstrate that the moved dependency still receives the right order and that failure short-circuits later effects.
- `docs/05-dependencies/04-providing-the-environment.md:124` — Layers promise resource cleanup, but this page has no acquisition/release example or direct link to a scoped-resource walkthrough at the point of that claim.

- `docs/04-error-handling/_index.md:41` — `Flow.recover`, `Flow.orElseWith`, and `Flow.fold` are introduced without a runnable example showing which causes each handles; add a small typed-failure versus defect example or link to one.

- `docs/03-the-flow-type/03-running-flows.md:47` — The page mentions optional member cancellation tokens but gives no example of running with one or interpreting an interrupted `Exit`; this is a likely boundary task for a Task-based application.
- `docs/03-the-flow-type/08-resources.md:8` — `use!` is recommended alongside `use`, but neither example shows its binding behavior or when to prefer it over scope acquisition.

- `docs/02-how-it-compares/01-fstoolkit-errorhandling-comparison.md:171` — The combined `prepareOrder` example uses undefined `OrderEnv`, `Order`, and `createOrder`; provide their minimal definitions or explicitly label the block schematic so it can be adapted safely.
- `docs/02-how-it-compares/03-effect-ts-comparison.md:33` — Promise-to-`Async` adaptation is recommended but no concrete adapter or link to one is provided; a Fable newcomer cannot act on this guidance from the comparison.

- `docs/01-getting-started/04-first-application.md:11` — “Your First Application” defines `run` but never calls it or shows an entry point, so a newcomer cannot copy the page to run an application.
- `docs/01-getting-started/03-existing-task-application.md:129` — “Register that function with your container” does not show how to register the factory or pass the resolved environment to a real handler; this is the handoff the migration guide promises.

## Suggestions for humans and LLMs

- `docs/10-streams/02-constructing.md:12` — Trim or hide the visible docs-test fixture: optional Browser, Node, telemetry, and filesystem imports obscure the packages needed for stream construction. The same fixture appears in `08-time-and-latest.md` and `09-resources-and-results.md`.
- `docs/10-streams/09-resources-and-results.md:39` — The copyable file-reader example opens `File.OpenText` directly under a `Resource<unit, Never, _>` despite possible I/O exceptions; use the filesystem service and a typed error for a recommended application pattern, or label the snippet as cleanup-only demonstration.

- `docs/09-scopes/_index.md:10` — Hide or narrow the visible docs-test `prepare` fixture: Browser, Node, telemetry, and filesystem imports appear before basic scope examples and obscure which packages a reader actually needs.

- `docs/09-scheduling-and-retries/_index.md:25` — Hide or trim the docs-test `prepare` fixture: imports of Browser, Node, filesystem, and telemetry before the first schedule example make the required packages look much broader than `Axial` and the clock service.

- `docs/08-concurrency-and-state/10-torture-tests/_index.md:40` — The scenario pages embed `no-check` snippet directives and link to an external example; give first-time readers a short checked, self-contained representative invariant on the landing page before sending them to the full runner.
- `docs/08-concurrency-and-state/01-fibers.md:23` — Hide or narrow the broad `prepare` fixture of optional Hosting, Browser, Node, filesystem, and telemetry imports; the same setup recurs on supervision, deferred/semaphore, hub, and cache pages and obscures required packages.

- `docs/07-layers/_index.md:10` — Hide or reduce the repeated `prepare` fixture: optional Browser, Node, telemetry, and filesystem imports appear before the first layer example, obscuring the required packages. The same fixture appears in both other layers pages.
- `docs/07-layers/02-tutorial.md:167` — Use a checked acquisition/release example with a fresh connection per provision, rather than a prebuilt object registered for disposal, to make ownership and repeated runs safe to copy.

- `docs/06-services/01-platform-services/01-clock.md:8` — Hide or trim the repeated docs-test `prepare` fixture: optional Browser, Node, filesystem, and telemetry imports obscure the packages needed for the clock example; the same fixture appears on logging, environment variables, console, filesystem, and existing-services pages.
- `docs/06-services/01-platform-services/02-logging.md:8` — Link or demonstrate the `MicrosoftLogging`/`FiberLogging` adapter and explain their package imports; the opening introduces them before teaching basic `ILog` and never uses them in a snippet.
- `docs/06-services/04-files.md:1` — Add front matter, package/install information, and code blocks consistent with neighboring service pages; this and `07-websocket.md` currently read as dense API notes rather than first-use guides.

- `docs/05-dependencies/_index.md:13` — Hide or trim the repeated docs-test `prepare` fixture: it imports optional hosting, browser, Node, telemetry, and other packages that the dependency example does not need. The same fixture appears on every page in this section.
- `docs/05-dependencies/05-tutorials/01-explicit-dependencies.md:87` — Explain why `ColdTask` is used for the sender while the repository uses `Flow.fromTaskResult`, and show token forwarding if cancellation of in-flight work is intended; both factories discard the token.

- `docs/04-error-handling/01-bind.md:16` — The visible `prepare` block imports many optional Axial packages unrelated to binding; hide the test fixture or limit copyable imports to those needed by the examples. The same fixture appears in `02-policy.md:13` and `03-defects.md:26`.

- `docs/03-the-flow-type/08-resources.md:10` — Add the required `open` statements before the first code block, as done for the second example, so a first-time reader can paste it independently.
- `docs/03-the-flow-type/04-flow-ce.md:12` — Keep docs-test `prepare` imports out of copyable examples; most listed optional packages are unrelated to the CE demonstration and make the required dependencies hard to infer.

- `docs/02-how-it-compares/02-task-vs-flow-scenarios.md:31` — Render at least one self-contained, checked Task/Flow pair on the page rather than relying entirely on unchecked snippets and external GitHub source links; readers and assistants need a stable copyable comparison.
- `docs/02-how-it-compares/03-effect-ts-comparison.md:63` — Qualify the blanket recommendation to use Axial on every supported target with the relevant target/package support and link to a cross-target setup guide.

- `docs/01-getting-started/_index.md:16` — Hide or separate docs-test `prepare` fixtures from copyable FSI examples; keep the visible snippet limited to namespaces available from the stated install.
- `docs/01-getting-started/03-existing-task-application.md:120` — Prefer `GetRequiredService` in the copyable container example, with the required package/open stated, rather than unchecked `GetService` casts that conceal a missing registration.
