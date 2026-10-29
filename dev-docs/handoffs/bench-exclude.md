# Benchmark exclusion handoff

Implementation complete; validation blocked by protected targets during the required policy review.

- Added a PackagePropsTests measurement-project test first, committed it, and observed 1 failed / 0 passed before the fix. After the fix: 1 passed / 0 failed.
- Axial.Benchmarks explicitly sets AxialGuardrailsEnabled=false. Removed all 16 AXG001 comments (brief said 12; checkout had 16).
- Removed benchmarks/**/*.fsproj from AXG009/AXG012 exemptions; host listing unchanged. Lock untouched.
- Updated guardrails guide with explicit measurement exclusion and evaluated-setting review.
- Existing PolicyReviewTests already cover enabled true -> false loosening; no duplicate test added.

## Protected-file blocker

Required command `dotnet --roll-forward Major artifacts/bin/Axial.Tool/debug/Axial.Tool.dll policy review --since v0.11.3` fails with MSB5029 in src/Axial.Guardrails/build/Axial.Guardrails.targets:66: Exclude expands to `\**\obj\**`, enumerating the drive. Did not edit targets, as forbidden. Policy confirmation cannot be claimed. Owner/coordinator must resolve this protected-targets issue before continuing.

Solution build timed out after 300 s under shared load; log is in the persistent report directory. Benchmark build, 20x PackagePropsTests, final fail-first script, full axial-verify and done-check remain outstanding. Do not treat this lane as DONE.

Logs: /home/adam/projects/scratch/axial-coord/pi/bench-exclude-{red,green,policy,build}.log.

Human approval: inspect Axial.Guardrails.toml removal of benchmark glob from AXG009 and AXG012 exemptions, plus evaluated project disable. Never ran approval/baseline commands or edited lock/verify/targets.
