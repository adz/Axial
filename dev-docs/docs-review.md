# Documentation review: docs/01-getting-started

## Assumptions

- `docs/01-getting-started/_index.md:16` — F# Interactive instructions assume `#r "nuget: Axial"` and `open Axial` suffice, but the first visible block is a `prepare` fixture opening many optional package namespaces. Readers may paste it and get missing-namespace errors after installing only Axial.

## Unclear or misleading

- `docs/01-getting-started/03-existing-task-application.md:101` — The claim that adding a `CheckoutError` case makes every boundary mapping incomplete is undermined by the catch-all `Exit.Failure cause` arm; this match remains exhaustive and the compiler need not flag it.
- `docs/01-getting-started/03-existing-task-application.md:103` — The catch-all uses `failwith`, which creates a new exception rather than re-raising the original defect or preserving cancellation; show a cancellation-aware boundary mapping.
- `docs/01-getting-started/01-why-flow.md:282` — The sample poller forks and immediately returns; the page says child fibers are interrupted when their parent ends, so `registry.DumpAt(clock)` cannot reliably show a live poller from this example.
- `docs/01-getting-started/01-why-flow.md:203` — “The same environment gives the same result” overpromises determinism: live service implementations may read external state or have mutable state.

## Too long

- `docs/01-getting-started/01-why-flow.md:302` — The concluding paragraph repeats the preceding sections' points about service signatures, fork ownership, tokens, and guardrails without adding a next step.

## Reference material in guides

- `docs/01-getting-started/01-why-flow.md:298` — Diagnostic IDs and what each guardrail checks are reference detail; link to the guardrails page rather than maintaining an abbreviated diagnostic list here.

## Missing

- `docs/01-getting-started/04-first-application.md:11` — “Your First Application” defines `run` but never calls it or shows an entry point, so a newcomer cannot copy the page to run an application.
- `docs/01-getting-started/03-existing-task-application.md:129` — “Register that function with your container” does not show how to register the factory or pass the resolved environment to a real handler; this is the handoff the migration guide promises.

## Suggestions for humans and LLMs

- `docs/01-getting-started/_index.md:16` — Hide or separate docs-test `prepare` fixtures from copyable FSI examples; keep the visible snippet limited to namespaces available from the stated install.
- `docs/01-getting-started/03-existing-task-application.md:120` — Prefer `GetRequiredService` in the copyable container example, with the required package/open stated, rather than unchecked `GetService` casts that conceal a missing registration.
