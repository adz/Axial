# T25a: Platform in the runtime, design doc first

Read, in order: `~/projects/scratch/axial-coord/pi/capabilities-spec.md` (esp. section 10, which overrides 1 and 8),
`dev-docs/TASKS.md` T25a, `dev-docs/current-ideas/platform-env-class.md`, `dev-docs/current-ideas/flow-as-data.md`.

Task: write `dev-docs/current-ideas/capabilities.md` covering T25a only (Platform = Clock, Guid, Random; log and telemetry
sink slots; `ISecureRandom` removal; `runTest`; explicit-stepping test clock; migration; invariant tests; benchmark plan;
visibility tradeoff and mitigations). Show the code shapes. Then STOP for the owner's review. No implementation, no TOML or lock edits.

Decided by the owner: Live/Fake names stay; Log is an observation slot, Console default at Info on stderr; no public
register-any-service API; authority stays explicit capabilities.
Open: where the `Axial.Telemetry` bridge lives; `Log` level API shape; whether 0.12 waits for T25a.
Constraints: work on main (docs only), no releases, benchmarks only on a quiet machine, never run `axial policy approve`.
