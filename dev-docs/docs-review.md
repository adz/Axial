# Documentation review: docs/01-getting-started

## Assumptions

- `docs/02-how-it-compares/01-fstoolkit-errorhandling-comparison.md:107` — Cancellation propagation is attributed to `ColdTask`, but both shown factories ignore the token (`fun _ -> ...`); the repository calls must accept and use it for interruption to reach in-flight work.

- `docs/01-getting-started/_index.md:16` — F# Interactive instructions assume `#r "nuget: Axial"` and `open Axial` suffice, but the first visible block is a `prepare` fixture opening many optional package namespaces. Readers may paste it and get missing-namespace errors after installing only Axial.

## Unclear or misleading

- `docs/02-how-it-compares/02-task-vs-flow-scenarios.md:62` — The retry scenario says the type requires only `IHasHttp`, although the described backoff and deadline also need a clock; clarify the full environment requirement.
- `docs/02-how-it-compares/01-fstoolkit-errorhandling-comparison.md:143` — Checkout claims dependencies are in the environment rather than closures, but the earlier `loadCustomerFlow` example closes over its repository; distinguish the two approaches.

- `docs/01-getting-started/03-existing-task-application.md:101` — The claim that adding a `CheckoutError` case makes every boundary mapping incomplete is undermined by the catch-all `Exit.Failure cause` arm; this match remains exhaustive and the compiler need not flag it.
- `docs/01-getting-started/03-existing-task-application.md:103` — The catch-all uses `failwith`, which creates a new exception rather than re-raising the original defect or preserving cancellation; show a cancellation-aware boundary mapping.
- `docs/01-getting-started/01-why-flow.md:282` — The sample poller forks and immediately returns; the page says child fibers are interrupted when their parent ends, so `registry.DumpAt(clock)` cannot reliably show a live poller from this example.
- `docs/01-getting-started/01-why-flow.md:203` — “The same environment gives the same result” overpromises determinism: live service implementations may read external state or have mutable state.

## Too long

- `docs/02-how-it-compares/02-task-vs-flow-scenarios.md:8` — Seven scenarios repeat the same source link, unchecked snippet, ZIO correspondence, and three-part guarantee list; a summary matrix with links to individual scenarios would make this comparison easier to scan.

- `docs/01-getting-started/01-why-flow.md:302` — The concluding paragraph repeats the preceding sections' points about service signatures, fork ownership, tokens, and guardrails without adding a next step.

## Reference material in guides

- `docs/02-how-it-compares/02-task-vs-flow-scenarios.md:107` — The `forkResource` scope rule is a specialized concurrency contract inside a comparison; link to the scopes reference for the rule and keep only the comparison-specific consequence here.

- `docs/01-getting-started/01-why-flow.md:298` — Diagnostic IDs and what each guardrail checks are reference detail; link to the guardrails page rather than maintaining an abbreviated diagnostic list here.

## Missing

- `docs/02-how-it-compares/01-fstoolkit-errorhandling-comparison.md:171` — The combined `prepareOrder` example uses undefined `OrderEnv`, `Order`, and `createOrder`; provide their minimal definitions or explicitly label the block schematic so it can be adapted safely.
- `docs/02-how-it-compares/03-effect-ts-comparison.md:33` — Promise-to-`Async` adaptation is recommended but no concrete adapter or link to one is provided; a Fable newcomer cannot act on this guidance from the comparison.

- `docs/01-getting-started/04-first-application.md:11` — “Your First Application” defines `run` but never calls it or shows an entry point, so a newcomer cannot copy the page to run an application.
- `docs/01-getting-started/03-existing-task-application.md:129` — “Register that function with your container” does not show how to register the factory or pass the resolved environment to a real handler; this is the handoff the migration guide promises.

## Suggestions for humans and LLMs

- `docs/02-how-it-compares/02-task-vs-flow-scenarios.md:31` — Render at least one self-contained, checked Task/Flow pair on the page rather than relying entirely on unchecked snippets and external GitHub source links; readers and assistants need a stable copyable comparison.
- `docs/02-how-it-compares/03-effect-ts-comparison.md:63` — Qualify the blanket recommendation to use Axial on every supported target with the relevant target/package support and link to a cross-target setup guide.

- `docs/01-getting-started/_index.md:16` — Hide or separate docs-test `prepare` fixtures from copyable FSI examples; keep the visible snippet limited to namespaces available from the stated install.
- `docs/01-getting-started/03-existing-task-application.md:120` — Prefer `GetRequiredService` in the copyable container example, with the required package/open stated, rather than unchecked `GetService` casts that conceal a missing registration.
