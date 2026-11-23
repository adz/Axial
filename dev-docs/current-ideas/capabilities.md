# Capabilities: what is still an idea

Status: sketch. What shipped (platform services in the runtime, capability definitions, claims, allowances and review
by direction) is documented in `docs/06-services/01-platform-services/` and `docs/guardrails/03-rule-authoring.md`; its
lasting decisions are in `dev-docs/decisions/README.md` ("Capabilities decide where raw APIs may be called").

## Console output outside `IConsole`

`printf`, `printfn`, `eprintf`, `eprintfn` and the `stdout`/`stderr` values write to the console without going through
`IConsole`, and no rule reports them. Add them to the `console` capability's rules, so only claimed files and `host`
files may use them. `sprintf` and `failwithf` stay allowed: they write nothing.

Measure first: count the new findings in examples and tests, and decide whether examples become `host` files or use
`Console.writeLine`.

## Exit

`exit` and `Environment.Exit` end the process without running scope finalizers. A core rule whose advice is "return
the `Exit` from `DotNetApp.run`" fits. `IConsole` gains no exit member.

## Finding capabilities on NuGet

A package that ships a `.capability.toml` should be findable, so a team reaches for an existing capability before
writing its own.

- **Tags at pack time.** When a project with `AxialCapabilityDefinition` items packs, the Axial.Guardrails targets add
  the tags `axial-capability` and `axial-capability-<name>`. Authors do nothing extra.
- **`axial capabilities search <text>`.** Queries the configured NuGet sources for `tags:axial-capability <text>`
  (nuget.org and most private feeds support tag queries), then reads each match's `.capability.toml` from the package
  to show the service, the APIs it replaces, the environment field, the fake and the level.
- **From the finding.** When AXG001 reports an API no capability covers, the advice adds
  "or search for one: `axial capabilities search <namespace>`".
- **Safe to install.** A definition only tightens: it blocks more APIs and cannot claim the consumer's files. An
  overlap with an existing capability fails the build with the reason, and policy review treats a new definition as
  free.

Limits: search matches package text and tags, not the toml inside, so searching by API name finds a package only when
its author mentions it. Per-namespace tags would help but bloat the tag list; a curated index would be exact but needs
an owner. Start with tags. The search is only as useful as the packages that exist, so early on it mostly finds
Axial's own.
