# Documentation review: docs/01-getting-started

## Assumptions

- [fixed] `README.md:33` — `checkoutOrder` pipes `checkout` through `Flow.retry` and `Flow.timeout`, both of which require `'env :> IHasClock`; `CheckoutEnv` had no clock, so the example did not compile. Added `Clock: IClock` and an `IHasClock` implementation to `CheckoutEnv`.
- [fixed] `README.md:75` — The package family lists "layers" as part of core `Axial`, but layers ship in the separate `Axial.Layers` package, not in core `Axial`. Moved "layers" into the supporting-packages sentence.

- [fixed] `docs/http/04-testing.md:72` — `runUserLookup` uses a relative URL but does not configure `client.BaseAddress` until the later, separate `apiClient` snippet. Say the caller must supply a client with a base address or use an absolute URL.

- [fixed] `docs/api/Axial.HubModule.md:3` — “Lossless consumer” assumes a `BackPressure` subscription and a publisher that waits for acceptance; name that strategy so readers do not infer every subscription is lossless.

- [fixed] `docs/15-notes/03-guardrails.md:175` — The suppression guidance assumes readers can inspect repository-relative `src/Axial.Guardrails` files; published NuGet consumers need a public source link or the exact rules documented here.

- [fixed] `docs/14-advanced/04-writing-effect-analyzers.md:85` — The packaging walkthrough assumes access to this repository's `src/Axial.Guardrails` files; a NuGet consumer reading published docs cannot follow it without a public source link or standalone template.

- [fixed] `docs/13-testing/02-runnable-examples.md:15` — Run commands assume the reader is at the repository root with the example projects checked out; say so, since these paths do not work from a NuGet-only application.

- [fixed] `docs/12-platforms-and-hosting/02-node.md:25` — The first Node snippet assumes `AppError`, `program`, and `Live.appLayer` exist without defining or linking their shapes. Label it schematic and link to a complete root-application example.
- [fixed] `docs/12-platforms-and-hosting/07-running-as-a-service.md:30` — The sample records progress using a live monotonic clock but does not show how the job loop shares `recordCompletion` with the watchdog; show the composition point so the health signal represents real work.

- [fixed] `docs/11-observability/01-telemetry/01-javascript.md:50` — The first tracing snippet assumes `checkout`, `order`, `user`, `AppAttributes`, `tenantId`, and `CheckoutError.describe` already exist; none is defined on this page. Label it schematic or provide a minimal runnable workflow and key.

- [fixed] `docs/10-streams/02-constructing.md:166` — The crawler uses `Map.find` for every discovered URL and assumes all links exist in the in-memory site; a real crawl needs a typed fetch error or an explicit missing-page policy.

- [fixed] `docs/09-scopes/_index.md:33` — `File.OpenText path` assumes the file exists and acquisition succeeds, but the example has no typed filesystem error or explicit filesystem dependency; label it illustrative rather than a production read pattern.

- [fixed] `docs/09-scheduling-and-retries/_index.md:126` — The copyable jitter example constructs `System.Random()` directly; the next paragraph recommends injected `IRandom`. Show the service-based wiring so repeated runs and tests do not depend on an ambient random source.

- [fixed] `docs/08-concurrency-and-state/07-hub.md:168` — The display example polls `Hub.subscriberCount` without a deadline; if the consumer fails before subscribing, the example hangs instead of surfacing its exit. Show a bounded handshake or failure-aware startup.

- [fixed] `docs/07-layers/02-tutorial.md:142` — The fake connection is created before provisioning and reused; the example assumes a single run. Show acquisition inside the layer for a reusable resource layer.

- [fixed] `docs/06-services/02-console.md:43` — `Console.readLine` may return null at EOF; `answer.Trim()` assumes interactive input always supplies a line. Show EOF handling in a reusable confirmation example.

- [fixed] `docs/05-dependencies/04-providing-the-environment.md:52` — `SqlUserStore(connectionString)` is invoked without a definition or indication that it is pseudocode; a newcomer may treat the production record as copyable.

- [fixed] `docs/03-the-flow-type/06-task-async-interop.md:22` — “Flow cannot pass cancellation into” a started task assumes it was created without a token the caller controls; clarify that the Flow cannot supply its own token retroactively, even if the caller has another cancellation source.

- [fixed] `docs/02-how-it-compares/01-fstoolkit-errorhandling-comparison.md:107` — Cancellation propagation is attributed to `ColdTask`, but both shown factories ignore the token (`fun _ -> ...`); the repository calls must accept and use it for interruption to reach in-flight work.

- [fixed] `docs/01-getting-started/_index.md:16` — F# Interactive instructions assume `#r "nuget: Axial"` and `open Axial` suffice, but the first visible block is a `prepare` fixture opening many optional package namespaces. Readers may paste it and get missing-namespace errors after installing only Axial.

- [fixed] `docs/process/07-worked-examples.md:69` — The checked application example assumes the docs are run inside a Git repository with a reachable `HEAD`; a copied example in a new project can fail. Use a deterministic command or state the prerequisite.

## Unclear or misleading

- [fixed] `README.md:52` — "does not change the signature" plus "a resource that must be released" overpromised: the example shows only retry and timeout, and those require a clock in the environment. Rewrote the prose to name what the example shows and state the `IHasClock` requirement.
- [fixed] `README.md:95` — The trailing sentence "Axial's server adapters serve Reified contracts." duplicated the preceding claim; removed it.

- [fixed] `docs/http/03-reliability.md:95` — The stated 200/400/800ms backoff contradicts the example's 1ms base delay; distinguish that example's 1/2/4ms schedule from the 200ms `withRetries` default.
- [fixed] `docs/http/04-testing.md:59` — `IHttp.Send` returns a Flow, so saying a fake can return `Error(HttpError.TimedOut(...))` suggests the wrong return shape. Show `Flow.fail (HttpError.TimedOut(...))` with the required arguments.
- [fixed] `docs/http/02-responses-and-errors.md:77` — “The transcript is safe to log as-is” follows a claim only about redacting the request line; `Body` and `Text` can contain credentials or personal data. Limit the safety claim to the request line and caution against logging the full response.

- [fixed] `docs/api/Axial.QueueModule.md:11` — “One logical consumer” suggests a queue cannot have multiple competing takers, while the next paragraph discusses many consumers. Say each value goes to one taker, not that only one taker may exist.
- [fixed] `docs/api/Axial.Scope.md:18` — “That scope moves to the caller's scope when the caller receives the value” has no clear antecedent after listing parallel, race, and timed flows; specify which child scope transfers ownership and when, including failure.
- [fixed] `docs/api/Axial.FlowStreamModule.md:12` — Calling `groupedWithin`, `throttle`, `debounce`, and `switchMapFlow` all queue-backed read-ahead operators obscures their different buffering and cancellation behavior. Distinguish operators that prefetch from those that only wait or replace work.

- [fixed] `docs/15-notes/03-guardrails.md:178` — The `AXG008` heading is followed by blocking-wait exemptions at line 182, which belong to `AXG001`; move them under the blocking category so readers do not mistake them for scope diagnostics.
- [fixed] `docs/15-notes/03-guardrails.md:278` — The page says `Fiber.join` returns an already released resource, then says the example “fails only at runtime”; the shown resource is a string, so no runtime failure is demonstrated. Use a disposable resource and checked failure or qualify the claim.

- [fixed] `docs/14-advanced/03-custom-services.md:3` — The description promises `Service.get`, but the tutorial uses `ExchangeRates.service` and never introduces `Service.get`; update the metadata to match the API shown.
- [fixed] `docs/14-advanced/03-custom-services.md:93` — Every exception from `GetUsdToAud`, not just timeouts, becomes `ProviderTimedOut`; `RateUnavailable` is never produced. Classify exceptions explicitly or simplify the error union and claim at line 99.
- [fixed] `docs/14-advanced/01-reusable-packages.md:108` — “F# interface implementations are always explicit” is too broad; qualify the statement to the record's explicit implementations shown here, and demonstrate two colliding `Client` contracts if this is a composability guarantee.

- [fixed] `docs/13-testing/02-runnable-examples.md:76` — Playground comments claim `Ok` results, while the immediately following observed output shows `Success`; align the comments with the actual `Exit` formatting.
- [fixed] `docs/13-testing/02-runnable-examples.md:116` — Running the entire `Axial.Examples` project produces policy and greeting output before supervision output, although the section title and source link imply a standalone supervision example. Explain that the command runs multiple examples.

- [fixed] `docs/12-platforms-and-hosting/01-application-lifecycle.md:74` — The text says `appLayer` scopes acquired resources, but the shown layer only constructs a record and acquires nothing. Distinguish this example from a resource-acquiring layer.
- [fixed] `docs/12-platforms-and-hosting/04-browser.md:32` — The unmount snippet begins with `let!` and ends with `}` without an opening `async {`; it cannot be pasted or understood as a complete cleanup callback.
- [fixed] `docs/12-platforms-and-hosting/06-dotnet.md:105` — The Generic Host snippet provisions `Live.appLayer` yet the environment passed to `Hosting.addApp` contains only `Orders`; it never shows how that layer's input and the constructed `AppEnv` agree. Show a coherent end-to-end composition.
- [fixed] `docs/12-platforms-and-hosting/07-running-as-a-service.md:7` — `UseSystemd()` and the `Type=notify` unit imply READY notification, but the page never says which component sends READY or how `SystemdNotify.live` is passed into the host. Explain this wiring before recommending the unit.

- [fixed] `docs/11-observability/_index.md:291` — `FiberDumpTelemetry.record registry` conflicts with the two-argument `FiberDumpTelemetry.record Clock.live registry` shown in the telemetry guide at line 385. Use one consistent call shape.
- [fixed] `docs/11-observability/_index.md:260` — The literal fiber-dump output is separate from the checked example and uses an unrelated three-fiber tree; readers may mistake it for the output of `withPoller`. Mark it as a hypothetical example or generate a verified transcript.
- [fixed] `docs/11-observability/01-telemetry/_index.md:381` — This recommends `Clock.live` for a dump even though the overview's example passes the supplied environment clock. Explain when a live clock is appropriate versus the application's injected clock.

- [fixed] `docs/10-streams/_index.md:21` — “Upstream cannot outrun” downstream is too absolute for the later producer-fiber and parallel operators; qualify this as bounded backpressure, with `fromCallback` explicitly able to drop values on overflow.
- [fixed] `docs/10-streams/04-batching-and-parallelism.md:35` — “Does not begin the next batch until the current result list is consumed” could imply per-item consumption of that list; the terminal pulls the next batch after the previous batch has been emitted, regardless of what the caller does with its elements.
- [fixed] `docs/10-streams/08-time-and-latest.md:83` — The checked search example debounces before `switchMapFlow`, so it never demonstrates interruption of an in-flight request; add a separate example with a request already running when the next input arrives.

- [fixed] `docs/09-scopes/_index.md:148` — A release defect is said to become a defect “of the flow that closed the scope,” but the page does not show how it combines with an existing failure or appears in `Exit`; clarify the observable outcome here.

- [fixed] `docs/09-scheduling-and-retries/_index.md:360` — The comment says polling continues “until the flow fails,” but `pollStatus` has error type `Never` and always succeeds; this example runs indefinitely unless interrupted. Show a stopping condition or state explicitly that the caller must interrupt it.
- [fixed] `docs/09-scheduling-and-retries/_index.md:247` — `resetAfter` calls a long run “successful,” yet the supervision example describes a worker that crashes after ten healthy minutes. Clarify that the threshold measures the duration of the failed run before its failure, not a previous successful execution.

- [fixed] `docs/08-concurrency-and-state/09-cache.md:105` — “There is no expiry” contradicts the `Cache.makeBoundedWithExpiry` option at line 61. Qualify this as the behavior of the default cache/memoized flow.
- [fixed] `docs/08-concurrency-and-state/09-cache.md:40` — `readConfigFromDisk` only increments a `Ref`; the comment says the file is read once, but no disk read occurs. Name it as a simulated load so readers do not mistake it for filesystem guidance.
- [fixed] `docs/08-concurrency-and-state/05-stm.md:36` — `withdraw` and `transferFunds` subtract without checking available funds; the banking example silently permits negative balances, unlike the linked torture test's guarded transfers. Add an insufficient-funds condition or label the operations arithmetic-only.
- [fixed] `docs/08-concurrency-and-state/10-torture-tests/16-interop.md:30` — “A cancelled token becomes an interruption” omits the distinction between runtime-requested cancellation and a foreign operation throwing cancellation while the runtime token is live; qualify the claim.

- [fixed] `docs/07-layers/_index.md:121` — “A record cannot express that” overstates the case: a factory returning `Result<AppEnv, BaseRuntimeError>` can model typed startup failure without a layer. Explain the added value of scoped provisioning and `Layer.provide` instead.
- [fixed] `docs/07-layers/_index.md:131` — A host-created `ILoggerFactory` can still be placed in a record after host startup; the example demonstrates adapting a dependency, not an inability to use records.
- [fixed] `docs/07-layers/_index.md:54` — “Its lifetime is the runtime's” suggests a longer-lived resource than the later `Layer.provide` boundary, which releases it when the provided flow finishes. Name the provided-flow scope here.

- [fixed] `docs/06-services/01-platform-services/01-clock.md:103` — The manual-clock example says to advance after a sleeper registers but provides only a comment, not a wait; copying the snippet can race and hang. The later suggestion to implement a mutable clock also omits the already introduced `ManualClock`.
- [fixed] `docs/06-services/03-filesystem.md:134` — "The `Async` members on `IFileSystem` ... return `Flow`" blurs service methods and workflow wrappers; distinguish the interface's signatures from the `FileSystem.*Async` functions readers call.
- [fixed] `docs/06-services/04-files.md:5` — "A process crash cannot expose a partially written destination" needs a qualification for failures during rename or when the destination did not previously exist; specify the actual atomicity guarantee and supported filesystems.

- [fixed] `docs/05-dependencies/04-providing-the-environment.md:83` — The section says to use the container to build an environment and leave it behind, but its only direct example runs `Flow<IServiceProvider, _, _>`; show the conversion into an application record at the edge.
- [fixed] `docs/05-dependencies/05-tutorials/01-explicit-dependencies.md:52` — `TimedOut` and `Cancelled` are declared as typed errors although the tutorial never produces either; cancellation should remain an interruption unless the workflow owns its cancellation source.
- [fixed] `docs/05-dependencies/05-tutorials/02-app-record.md:18` — “Reuse The Same Interfaces” is inaccurate: the page redefines the types independently and changes the error union and adds audit; readers cannot simply continue the preceding tutorial's session.
- [fixed] `docs/05-dependencies/05-tutorials/02-app-record.md:213` — “Only `Flow.run env` names the record” contradicts the `Flow<AppEnv, …>` annotations on each helper; distinguish passing the record value from naming its type.

- [fixed] `docs/04-error-handling/03-defects.md:24` — “Cause.Interrupt for cancellation” does not distinguish cancellation requested by the Flow runtime from a foreign `OperationCanceledException`; the latter is an expected exception for `attempt*` while the runtime token is live.
- [fixed] `docs/04-error-handling/02-policy.md:133` — `Policy.compose requireName normalizeName` rejects whitespace-only names before trimming; if the intended rule is “trim, then require a name,” reverse the composition and show the effect on the returned value.
- [fixed] `docs/04-error-handling/03-defects.md:50` — `File.ReadAllTextAsync` is an ambient filesystem operation in an error-handling example; clarify that this demonstrates interop exception capture, not the recommended explicit filesystem-service boundary.

- [fixed] `docs/03-the-flow-type/01-flow-type.md:28` — Calling every Flow an “immutable, cold description” obscures that `Flow.awaitStartedTask` can wrap work already running; distinguish cold Flow execution from the lifecycle of captured inputs.
- [fixed] `docs/03-the-flow-type/08-resources.md:13` — `File.OpenText path` runs inside the Flow but is an ambient filesystem effect and is not interruptible; the resource example does not model the filesystem dependency or failure channel it teaches elsewhere.
- [fixed] `docs/03-the-flow-type/08-resources.md:54` — “The flow's own outcome is kept” sounds like finalizer defects cannot change the outcome, but the next sentence says they are added; state how combined causes appear at the boundary.

- [fixed] `docs/02-how-it-compares/02-task-vs-flow-scenarios.md:62` — The retry scenario says the type requires only `IHasHttp`, although the described backoff and deadline also need a clock; clarify the full environment requirement.
- [fixed] `docs/02-how-it-compares/01-fstoolkit-errorhandling-comparison.md:143` — Checkout claims dependencies are in the environment rather than closures, but the earlier `loadCustomerFlow` example closes over its repository; distinguish the two approaches.

- [fixed] `docs/01-getting-started/03-existing-task-application.md:101` — The claim that adding a `CheckoutError` case makes every boundary mapping incomplete is undermined by the catch-all `Exit.Failure cause` arm; this match remains exhaustive and the compiler need not flag it.
- [fixed] `docs/01-getting-started/03-existing-task-application.md:103` — The catch-all uses `failwith`, which creates a new exception rather than re-raising the original defect or preserving cancellation; show a cancellation-aware boundary mapping.
- [fixed] `docs/01-getting-started/01-why-flow.md:282` — The sample poller forks and immediately returns; the page says child fibers are interrupted when their parent ends, so `registry.DumpAt(clock)` cannot reliably show a live poller from this example.
- [fixed] `docs/01-getting-started/01-why-flow.md:203` — “The same environment gives the same result” overpromises determinism: live service implementations may read external state or have mutable state.

- [fixed] `docs/process/01-composition.md:123` — `cmdText` is described as fixed text “that should not be parsed,” but line 173 and the DSL API introduction say it tokenizes/parses fixed text. Correct this sentence so readers do not confuse it with `commandArgs`.
- [fixed] `docs/process/08-running-in-an-application-flow.md:92` — The fake `ProcessResult` gives `StdOutCapture.Text` a nonempty value but `Bytes = [||]`, despite the output guide saying text is decoded from retained bytes. Use matching UTF-8 bytes in the fake so tests do not teach an impossible capture.
- [fixed] `docs/process/07-worked-examples.md:104` — “If ... the consumer stops early” implies any downstream stream consumer stops the process successfully; the streaming guide only guarantees `StoppedEarly` for `ProcessStream.take`/`takeWhile`, while conversion to `FlowStream` discards completion. Name the specific early-stop operators.

## Too long

- [done] `docs/http/02-responses-and-errors.md:33` — The scripted service, decoder, and environment setup occupy nearly 40 lines before the first response example; move reusable test setup to the testing guide and keep a short local example here.
- [done] `docs/http/03-reliability.md:35` — The page repeats the scripted service and decoder from the responses guide before explaining timeouts; share or link the fixture so the retry policy is visible sooner.

- [done] `docs/15-notes/03-guardrails.md:186` — The per-diagnostic walkthrough runs through line 315 after tables already summarize the rules. Keep install, severity, and suppression choices here and move detailed diagnostic contracts into reference pages.

- [done] `docs/14-advanced/01-reusable-packages.md:18` — This package-author page and `03-custom-services.md` repeat the same exchange-rate interface, contract, accessor, helper, and environment. Keep the complete walkthrough in one page and link to it from the other.

- [done] `docs/13-testing/02-runnable-examples.md:125` — The full unrelated greeting and policy output precedes the supervision results; show only the relevant supervised/fiber lines or split the command/output by example.

- [done] `docs/12-platforms-and-hosting/03-runtime-operations.md:10` — The 70-line simulated checkout and repeated runs dominate a page meant to select runtime operations; use a smaller shared workflow and link to dedicated retry/cancellation guidance for details.

- [done] `docs/11-observability/_index.md:37` — The overview repeats the telemetry guide's tracing setup, attributes, fiber metrics, dumps, Aspire walkthrough, and JavaScript setup across nearly 400 lines. Keep the signal map and cross-package decisions here; link to the platform guides for setup and recipes.

- [done] `docs/10-streams/02-constructing.md:105` — The two crawler walkthroughs occupy over half the construction guide and introduce HTTP-shaped domain modeling and frontier bookkeeping before the basic source choices are complete; move the crawler to a dedicated recipe and retain a short `unfoldFlow` example here.

- [done] `docs/09-scopes/_index.md:358` — The rules recap repeats the lifetime maps, concurrency table, and fork walkthrough; shorten it to a decision checklist linking back to the detailed sections.

- [done] `docs/09-scheduling-and-retries/_index.md:151` — The long sequence of union, intersect, andThen, output predicates, budgets, reset, retry, and repeat makes the first-use guide hard to scan. Split basic retry/repeat from advanced schedule composition.

- [done] `docs/08-concurrency-and-state/01-fibers.md:244` — “What a fiber owns” repeats the ownership and `forkResource` rules already explained at line 77; consolidate the two sections and keep the graceful-stop walkthrough separate.

- [done] `docs/07-layers/01-layers.md:141` — The function catalog and separate zip/merge discussion interrupt the provisioning walkthrough; keep the choices needed by the example and move the full combinator inventory to reference documentation.

- [done] `docs/06-services/02-console.md:139` — The 49-line full `IConsole` fake overwhelms the console usage guide; move the complete implementation to a test-helper recipe and retain a short assertion or link.

- [done] `docs/05-dependencies/01-the-environment.md:70` — The application-record example, two complete fake implementations, and assertions repeat the dedicated app-record tutorial; keep a minimal projection example here and link to the tutorial.

- [done] `docs/04-error-handling/_index.md:41` — The single paragraph combines recovery, traced and compound causes, error-type changes, wrapper exceptions, and handler cancellation; split into a short choice guide and a focused compound-cause explanation.

- [done] `docs/03-the-flow-type/06-task-async-interop.md:272` — The summary repeats the interop selection table and section conclusions; retain one quick decision aid and link to the examples.

- [done] `docs/02-how-it-compares/02-task-vs-flow-scenarios.md:8` — Seven scenarios repeat the same source link, unchecked snippet, ZIO correspondence, and three-part guarantee list; a summary matrix with links to individual scenarios would make this comparison easier to scan.

- [done] `docs/01-getting-started/01-why-flow.md:302` — The concluding paragraph repeats the preceding sections' points about service signatures, fork ownership, tokens, and guardrails without adding a next step.

- [done] `docs/process/03-streaming.md:129` — The queue/hub, merge/zip, fold, filtering, stderr provenance, line limits, and ordinary-stream conversion examples make this first-use guide 286 lines long. Keep a short line-consumption path here and split FlowStream integration into a recipe.

## Reference material in guides

- [done] `docs/http/02-responses-and-errors.md:120` — The exhaustive `HttpError` case match acts as a case catalog in a response walkthrough; keep a few handling choices here and link the full union contract in API reference.

- [done] `docs/15-notes/03-guardrails.md:97` — `AxialGuardrailRule` metadata syntax and matching grammar are package-author reference material within an adoption guide; link to a dedicated rule-authoring reference from the install path.

- [done] `docs/14-advanced/04-writing-effect-analyzers.md:87` — NuGet layout, `PackageDownload`, and MSBuild target/CLI exit-code details are packaging reference material without a runnable project; link to a maintained template and keep the guide focused on adapting a rule and verifying a diagnostic.

- [done] `docs/12-platforms-and-hosting/09-elmish.md:7` — The page lists command, subscription, inbox, shutdown, and test API contracts without a worked Elmish program; move the exhaustive operation inventory to reference and keep a first-use lifecycle example here.

- [done] `docs/11-observability/_index.md:204` — The complete fiber instrument table and registry method inventory at lines 273–288 are reference contracts duplicated alongside the telemetry guide; retain a short operational interpretation and link to the telemetry reference for the inventories.

- [done] `docs/10-streams/06-consuming.md:63` — The six-step lifecycle and per-operator scope ownership rules read as a scope contract; keep the practical cleanup guarantee here and link to the scopes guide for the detailed ordering.

- [done] `docs/09-scopes/_index.md:233` — The combinator-by-combinator scope transfer table is a specialized contract better maintained in API reference; keep the value-received ownership rule and link to per-combinator details.

- [done] `docs/09-scheduling-and-retries/_index.md:388` — The 18-row signature table duplicates the guide's descriptions and belongs in API reference; keep a short selection guide and link to the signatures.

- [done] `docs/08-concurrency-and-state/03-ref.md:92` — The full signature table repeats the immediately preceding examples of each `Ref` operation; link to API reference and retain the atomicity and get/set pitfall in this guide.
- [done] `docs/08-concurrency-and-state/05-stm.md:114` — Two signature tables restate the `TRef` and `STM` overview; move exhaustive signatures to API reference and use this space for `retry`/`orElse` behavior.

- [done] `docs/07-layers/01-layers.md:143` — The table enumerates `map`, `mapError`, `widenError`, `bind`, `zip`, `zipPar`, `merge`, `map2`, and `map3` without showing most in use; link to their API reference and retain the composition rules that matter here.

- [done] `docs/06-services/03-filesystem.md:131` — The catalog of async methods, metadata setters, symlinks, directory enumerators, and path helpers across lines 131–197 is API-reference material; keep representative usage and link to a searchable operation reference.

- [done] `docs/05-dependencies/03-service-contracts.md:146` — The subtle `#T` type-variable rule is reference material in a conceptual guide; keep a brief warning here and link to a focused signature reference with examples.

- [done] `docs/03-the-flow-type/07-troubleshooting-types.md:13` — The complete alias expansion table duplicates `01-flow-type.md:35`; link to the canonical table and focus on the diagnostic here.

- [done] `docs/02-how-it-compares/02-task-vs-flow-scenarios.md:107` — The `forkResource` scope rule is a specialized concurrency contract inside a comparison; link to the scopes reference for the rule and keep only the comparison-specific consequence here.

- [done] `docs/01-getting-started/01-why-flow.md:298` — Diagnostic IDs and what each guardrail checks are reference detail; link to the guardrails page rather than maintaining an abbreviated diagnostic list here.

## Missing

- [done] `docs/process/04-failures-and-transcripts.md:12` — The purported `ProcessError` failure-case table omits `OutputTooLarge` and `OutputFailed`, which the output guide explicitly names. Include both or label the table non-exhaustive and link their contracts.

- [done] `docs/packages/index.md:24` — Supporting packages are unnamed and have no direct first-use links on the package landing page; readers must leave the package index to discover which package provides console, filesystem, hosting, or telemetry support.

- [done] `docs/http/_index.md:66` — The HTTP landing page has no install/package guidance or link to setup before its first workflow; name the required package and link to the live-service wiring on the testing page.
- [done] `docs/http/01-requests.md:57` — Secret redaction examples do not explain that ordinary `query`/`header` values and JSON bodies are not automatically secret; show when to choose `secretQuery`/`secretHeader` and avoid treating the whole plan as safe for arbitrary user data.
- [done] `docs/http/03-reliability.md:69` — Timeout behavior is described but never exercised with a checked slow-service example; show the resulting `HttpError.TimedOut` and distinguish it from runtime interruption.

- `docs/api/Axial.ScheduleModule.md:4` — `Schedule` is described as deciding after each run, but the page never explains what its input/output mean for `retry` versus `repeat`, or whether `recurs n` means n retries or n total runs; add a small counted example.
- `docs/api/Axial.State.SubscriptionRefModule.md:3` — The page promises every update to each running stream but does not explain what happens when a subscriber's chosen overflow strategy fills; qualify delivery guarantees by strategy and link to its behavior.

- [done] `docs/15-notes/01-packages-and-platforms.md:12` — The platform table omits `Axial.Layers`, `Axial.Guardrails`, and both HTTP host adapters even though the dependency table or adjacent notes name them; include their runtime/build-time status or state why they are excluded.
- `docs/15-notes/03-guardrails.md:32` — No command or example shows how to confirm that a consumer project actually runs the analyzer and fails at error severity; provide a minimal deliberate diagnostic and expected build result.

- `docs/14-advanced/04-writing-effect-analyzers.md:79` — No test or build command checks that the analyzer catches a direct constructor call, honors a suppression, and fails a consuming build at error severity; add a minimal consumer verification path.
- `docs/14-advanced/01-reusable-packages.md:112` — The package guide recommends typed errors but its only runnable operation uses `ColdTask`, leaving exceptions as defects; show a checked typed-error package operation or link directly to the typed-error example in the tutorial.

- `docs/13-testing/_index.md:7` — The testing landing page says to supply fakes or replace layers but offers no minimal test with a fake environment, `Flow.run`, and an assertion; link directly to or include a first test independent of HTTP.
- `docs/13-testing/02-runnable-examples.md:8` — The page says generation builds and runs examples but does not explain whether the literal observed-output blocks are verified against those runs or maintained manually; distinguish executable snippets from checked transcripts.

- `docs/12-platforms-and-hosting/_index.md:10` — Navigation omits runtime operations, running as a service, and Elmish even though all three are pages in this section; link them for discoverability.
- `docs/12-platforms-and-hosting/05-platform-services.md:37` — `BaseRuntime.live` and `BaseRuntime.fromServiceProvider` are recommended without a checked example showing their layer input, provisioning, or typed startup failure; add one composition example or a direct link to one.
- `docs/12-platforms-and-hosting/09-elmish.md:3` — No install instruction or minimal runtime construction and dispatch-thread `Post` example accompanies the Elmish APIs; show enough setup to run a first command.

- `docs/11-observability/01-telemetry/01-javascript.md:19` — “Install the OpenTelemetry SDK, exporter, and context manager” gives no package names or minimal Node/browser bootstrap; the first runnable step depends on host setup that the page never demonstrates. Give one concrete bootstrap or a direct setup link.
- `docs/11-observability/01-telemetry/_index.md:339` — Queue metrics are described but have no checked measurement example showing observed values or subscription cleanup; add a minimal collector example or link to one.

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

- `docs/process/_index.md:6` — The first executable `printf` example gives no package-install or platform prerequisite; link the package/platform setup and note that the checked native-command examples need a Unix-like toolchain.
- `docs/process/06-fable.md:9` — The only Fable sample defines a Flow but never supplies an `IProcess` implementation or runs it; provide a minimal fake/worker-backed implementation and a checked Fable project or label the fragment as construction-only.
- `docs/process/02-output-capture.md:80` — The output cap example prints exactly five bytes with a five-byte limit, so it never demonstrates the advertised failure. Add a checked six-byte example matching `OutputTooLarge` and its stage/channel.

## Suggestions for humans and LLMs

- `docs/http/03-reliability.md:206` — The copyable payment workflow generates an ambient GUID. Accept an idempotency key from the caller or an explicit generator service so retries across separate invocations can reuse the same key and tests can control it.
- `docs/http/04-testing.md:16` — The repeated visible `prepare` imports include threading and task namespaces not used by the first fake; hide test-only setup or narrow imports to make copyable dependencies clear.

- `docs/api/Axial.FlowStreamModule.md:8` — Show one minimal source → operator → terminal → `Flow.run` pipeline in the API introduction; otherwise the first example requires following a guide before an assistant can construct a stream.

- `docs/15-notes/01-packages-and-platforms.md:35` — Link package names to their first-use installation/setup guides; the matrix tells readers what runs where but not which package to install for a given host.
- `docs/15-notes/03-guardrails.md:154` — Mark the live `IClock` implementation as an illustrative boundary fragment rather than a complete copyable implementation; it omits other interface members and does not show the required imports.

- `docs/14-advanced/03-custom-services.md:18` — Hide or narrow the visible checked-example fixture: optional Browser, Node, telemetry, and filesystem imports obscure which packages the currency example requires; the package guide repeats the same fixture at line 18.
- `docs/14-advanced/04-writing-effect-analyzers.md:30` — The illustrative symbol rule and analyzer are `no-check` fragments; provide a link to a versioned, compilable analyzer template so coding assistants need not guess omitted helpers and project wiring.

- `docs/13-testing/02-runnable-examples.md:61` — Replace the repeated `|> fun workflow -> workflow |> Flow.run env` pipeline with the direct `greetingFlow |> Flow.run env` form; it is easier to copy and does not imply an extra adapter is needed.

- `docs/12-platforms-and-hosting/01-application-lifecycle.md:13` — Hide or narrow the docs-test fixture: Browser, Node, telemetry, and filesystem imports obscure the packages needed for `App`; the same broad fixture appears in runtime operations and .NET hosting.
- `docs/12-platforms-and-hosting/02-node.md:45` — Fable-only examples are unchecked and depend on undefined application values; provide a small Fable-verified sample project for Node and browser hosting so assistants can copy working entry points.
- `docs/12-platforms-and-hosting/07-running-as-a-service.md:1` — Add front matter and a concrete Generic Host registration example before the watchdog details; the current page starts with deployment configuration rather than a copyable service setup.

- `docs/11-observability/01-telemetry/_index.md:43` — Hide or trim the visible `prepare` fixture: optional Browser, Node, filesystem, and JavaScript telemetry imports appear before the .NET-only tracing setup and obscure required packages. The overview repeats the same broad fixture at line 51.
- `docs/11-observability/01-telemetry/_index.md:67` — The first host setup snippet uses an undefined `builder` and unchecked extension methods; show the host setup context and required `open` statements or label it as a fragment of an ASP.NET Core app.

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

- `docs/process/01-composition.md:10` — Narrow or hide the repeated visible `prepare` imports (`Console`, `FileSystem`, threading, tasks) in command-only examples; assistants copying them may infer optional packages are required. The same fixture recurs in output, streaming, failures, scripts, and worked examples.
- `docs/process/09-dsl-recipes.md:66` — File-routing recipes create temporary files using ambient `System.IO.File`; label this as docs-test setup rather than an application pattern, since the application guide recommends an explicit filesystem dependency.

## Code issues

- `examples/Axial.Comparisons/RetryBudget.fs:89` — `WithFlow.fetchRate` is annotated `when 'env :> IHasHttp`, but its body pipes through `Flow.retry` and `Flow.timeout`, which both require `IHasClock`; the compiler silently adds that constraint to the emitted signature (the compiled `fetchRate[env]` carries both `IHasHttp` and `IHasClock`). Update the annotation and the `/// Flow<...> where 'env :> IHasHttp.` doc comment at line 84 to name both interfaces, so readers of the linked comparison source do not think only HTTP is required.
