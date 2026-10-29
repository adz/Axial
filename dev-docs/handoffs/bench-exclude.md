# Benchmark exclusion handoff

## Implementation

Axial.Benchmarks explicitly sets AxialGuardrailsEnabled=false. Removed all 16 AXG001 comments (brief said 12; checkout had 16). Removed benchmarks/**/*.fsproj from AXG009/AXG012 exemptions; host listing unchanged. Lock untouched.

PackagePropsTests measurement-project test was committed first and failed before the project fix, then passed. Existing PolicyReviewTests cover enabled true -> false loosening.

## Query-mode diagnosis and fix

Plain `dotnet build benchmarks/Axial.Benchmarks` succeeds with zero warnings/errors. MSBuild query mode (`-getProperty` / `-getItem`) instead validates exclusions in inactive imported item groups, with an uninitialized _AxialGuardrailsPolicyGlobRoot. Setting Enabled=false globally does not avoid that validation. Supplying the target's existing project-directory fallback fixes query mode without changing the evaluated Enabled value or executing the disabled group. PolicyReview now supplies that safe glob root when querying projects. No targets change.

PolicyReview also records per-project evaluation failures as review findings, preserving other projects' results; --check fails on those findings. Historical projects are evaluated even for pre-policy references, so --since v0.11.3 reports the benchmark's true -> false setting change rather than copying current settings into history.

Two real-target/error-isolation tests were committed and both failed before the tool fix, then both passed after it. Required policy review now exits 0 and lists `benchmarks/Axial.Benchmarks/Axial.Benchmarks.fsproj: AxialGuardrailsEnabled: true -> false`.

User guide and 0.12 release notes document review error handling.

## Validation

Solution Debug build exits 0, 0 errors, no AXG warnings; policy-drift warnings remain expected pending human approval. Plain benchmark build exits 0 with 0 warnings/errors. Final 20x test-class runs and full verification results are recorded in the persistent report, along with fail-first and done-check results when complete.

Human approval: inspect Axial.Guardrails.toml removal of benchmark glob from AXG009 and AXG012 exemptions, plus evaluated project disable. Never ran approval/baseline commands or edited lock/verify/targets.

Logs/report: /home/adam/projects/scratch/axial-coord/pi/bench-exclude*.log and bench-exclude.report.md.
