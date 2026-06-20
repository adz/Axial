# Axial Capability Design Bundle

This bundle contains capability-design research documents for Axial.

Start with `../PLAN.md` for current architecture.

> Historical research notice: this folder predates the 2026-06-02 explicit-services-and-layers redesign. Use
> `../scope-layer-redesign.md` and `../PLAN.md` for current direction. Treat any `RuntimeContext`, registry-backed,
> ambient-core, or `Flow.service` / `Flow.inject` guidance here as research history unless explicitly restated in the
> current plan.

Within this research bundle, `NEW-APPROACH.md` is the closest match to the active direction: optional capability
families for explicit, typed, testable .NET/system effects, while user domain dependencies stay plain by default.
`CAPS_SUMMARY.md`, `CAPS_RECOMMENDED_MODEL.md`, and `CAPS_RECOMMENDED_WALKTHROUGH.md` keep the research path that
led here. Some of those files still discuss `RuntimeContext<'runtime, 'env>` or registry-backed provisioning; treat
that as research context, not current implementation.

## Files

- `NEW-APPROACH.md` — closest research document to the current direction.
- `CAPS_RECOMMENDED_MODEL.md` — compressed description of the leveled model; contains historical `RuntimeContext`
  examples.
- `CAPS_RECOMMENDED_WALKTHROUGH.md` — step-by-step research walkthrough; contains historical `RuntimeContext`
  examples.
- `CAPS_SUMMARY.md` — comparison, recommendation, and 1.0 advice.
- Earlier capability-plan draft based on structural accessors; now historical.
- `CAPS-BOILERPLATE.md` — explicit record/slice baseline.
- `CAPS-ISERVICEPROVIDER.md` — pragmatic `IServiceProvider` model.
- `CAPS-SIMPLE-RECORD-SRTP.md` — conservative strict SRTP record model.
- `CAPS-STRUCTURAL-ACCESSORS.md` — structural accessor model, using accessor functions instead of trait aliases.
- `CAPS-STRUCTURAL-SP-BRIDGE.md` — bridge strategy for DI integration.
- `CAPS-EXPLICIT-HYBRID.md` — explicit interface/record hybrid baseline.
- `probe.fsx` — minimal compiler probe showing the invalid trait-alias idea.

## Historical Recommendation

The active direction is:

```text
Make Axial-provided runtime/system effects explicit, typed, ambient, and locally overridable.
Keep runtime/system services out of end-user 'env signatures.
Keep user domain dependencies as records/provider/env by default.
Ship optional capability-family NuGets for Core, Context, Observability, FileSystem, Console, Http, and Process.
Use IHas<'T> only where reusable strict app dependency contracts pay for themselves.
```

The older structural-accessor documents remain useful research, especially for understanding why SRTP member
constraints are not the default public capability surface.

## Key Research Correction

Earlier drafts proposed reusable “Trait Aliases” for SRTP member constraints. Compiler probing showed this is not
valid F#. That correction still matters, even though the current recommendation now moves away from SRTP as the
main 1.0 capability model.
