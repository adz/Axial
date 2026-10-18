# Documentation review: docs/01-getting-started

## Assumptions

- `docs/03-the-flow-type/06-task-async-interop.md:22` — “Flow cannot pass cancellation into” a started task assumes it was created without a token the caller controls; clarify that the Flow cannot supply its own token retroactively, even if the caller has another cancellation source.

- `docs/02-how-it-compares/01-fstoolkit-errorhandling-comparison.md:107` — Cancellation propagation is attributed to `ColdTask`, but both shown factories ignore the token (`fun _ -> ...`); the repository calls must accept and use it for interruption to reach in-flight work.

- `docs/01-getting-started/_index.md:16` — F# Interactive instructions assume `#r "nuget: Axial"` and `open Axial` suffice, but the first visible block is a `prepare` fixture opening many optional package namespaces. Readers may paste it and get missing-namespace errors after installing only Axial.

## Unclear or misleading

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

- `docs/04-error-handling/_index.md:41` — The single paragraph combines recovery, traced and compound causes, error-type changes, wrapper exceptions, and handler cancellation; split into a short choice guide and a focused compound-cause explanation.

- `docs/03-the-flow-type/06-task-async-interop.md:272` — The summary repeats the interop selection table and section conclusions; retain one quick decision aid and link to the examples.

- `docs/02-how-it-compares/02-task-vs-flow-scenarios.md:8` — Seven scenarios repeat the same source link, unchecked snippet, ZIO correspondence, and three-part guarantee list; a summary matrix with links to individual scenarios would make this comparison easier to scan.

- `docs/01-getting-started/01-why-flow.md:302` — The concluding paragraph repeats the preceding sections' points about service signatures, fork ownership, tokens, and guardrails without adding a next step.

## Reference material in guides

- `docs/03-the-flow-type/07-troubleshooting-types.md:13` — The complete alias expansion table duplicates `01-flow-type.md:35`; link to the canonical table and focus on the diagnostic here.

- `docs/02-how-it-compares/02-task-vs-flow-scenarios.md:107` — The `forkResource` scope rule is a specialized concurrency contract inside a comparison; link to the scopes reference for the rule and keep only the comparison-specific consequence here.

- `docs/01-getting-started/01-why-flow.md:298` — Diagnostic IDs and what each guardrail checks are reference detail; link to the guardrails page rather than maintaining an abbreviated diagnostic list here.

## Missing

- `docs/04-error-handling/_index.md:41` — `Flow.recover`, `Flow.orElseWith`, and `Flow.fold` are introduced without a runnable example showing which causes each handles; add a small typed-failure versus defect example or link to one.

- `docs/03-the-flow-type/03-running-flows.md:47` — The page mentions optional member cancellation tokens but gives no example of running with one or interpreting an interrupted `Exit`; this is a likely boundary task for a Task-based application.
- `docs/03-the-flow-type/08-resources.md:8` — `use!` is recommended alongside `use`, but neither example shows its binding behavior or when to prefer it over scope acquisition.

- `docs/02-how-it-compares/01-fstoolkit-errorhandling-comparison.md:171` — The combined `prepareOrder` example uses undefined `OrderEnv`, `Order`, and `createOrder`; provide their minimal definitions or explicitly label the block schematic so it can be adapted safely.
- `docs/02-how-it-compares/03-effect-ts-comparison.md:33` — Promise-to-`Async` adaptation is recommended but no concrete adapter or link to one is provided; a Fable newcomer cannot act on this guidance from the comparison.

- `docs/01-getting-started/04-first-application.md:11` — “Your First Application” defines `run` but never calls it or shows an entry point, so a newcomer cannot copy the page to run an application.
- `docs/01-getting-started/03-existing-task-application.md:129` — “Register that function with your container” does not show how to register the factory or pass the resolved environment to a real handler; this is the handoff the migration guide promises.

## Suggestions for humans and LLMs

- `docs/04-error-handling/01-bind.md:16` — The visible `prepare` block imports many optional Axial packages unrelated to binding; hide the test fixture or limit copyable imports to those needed by the examples. The same fixture appears in `02-policy.md:13` and `03-defects.md:26`.

- `docs/03-the-flow-type/08-resources.md:10` — Add the required `open` statements before the first code block, as done for the second example, so a first-time reader can paste it independently.
- `docs/03-the-flow-type/04-flow-ce.md:12` — Keep docs-test `prepare` imports out of copyable examples; most listed optional packages are unrelated to the CE demonstration and make the required dependencies hard to infer.

- `docs/02-how-it-compares/02-task-vs-flow-scenarios.md:31` — Render at least one self-contained, checked Task/Flow pair on the page rather than relying entirely on unchecked snippets and external GitHub source links; readers and assistants need a stable copyable comparison.
- `docs/02-how-it-compares/03-effect-ts-comparison.md:63` — Qualify the blanket recommendation to use Axial on every supported target with the relevant target/package support and link to a cross-target setup guide.

- `docs/01-getting-started/_index.md:16` — Hide or separate docs-test `prepare` fixtures from copyable FSI examples; keep the visible snippet limited to namespaces available from the stated install.
- `docs/01-getting-started/03-existing-task-application.md:120` — Prefer `GetRequiredService` in the copyable container example, with the required package/open stated, rather than unchecked `GetService` casts that conceal a missing registration.
