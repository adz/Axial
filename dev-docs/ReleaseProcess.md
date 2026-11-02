# Release process

All public Axial packages share one pre-1.0 version, taken from the `vX.Y.Z` release tag itself (not from a version committed in the repo). A `vX.Y.Z` tag produces that version of core and every focused add-on listed by `scripts/pack.sh`.

The Reified release train is independent. The two HTTP contract adapters are packed with public `Reified.*` dependencies; they will move to their own Axial+Reified product before 1.0.

## Prepare

1. Update `NEXT_VERSION` to the version you intend to tag next and add `dev-docs/releases/<version>.md` with that version's release notes. CI fails on `main` (and again defensively in the release workflow) until this file exists for whatever `NEXT_VERSION` currently says — this is deliberate, so a missing notes file is caught before a tag is ever pushed, not after. `NEXT_VERSION` is a planning file only; it is not read by the build. The version that actually ships is whatever you tag with, so the tag must match `NEXT_VERSION` (and thus the notes file already written) or the release job will fail looking for `dev-docs/releases/<tag-version>.md`.
2. Run:

```bash
dotnet build Axial.slnx --configuration Release --nologo -v minimal
dotnet test Axial.slnx --configuration Release --no-build --nologo -v minimal
bash scripts/check-source-inventory.sh
bash scripts/check-fable-js-surface.sh
bash scripts/run-aot-probe.sh
bash scripts/pack.sh
bash scripts/check-docs-conventions.sh
dotnet livedocs test --warn-as-error
dotnet livedocs history-check
```

3. Ensure `.livedocs/history.json` contains every previously published documentation capsule. It is the source of truth for
   the versioned site. Backfill immutable GitHub Release capsules with `dotnet livedocs history-sync adz/Axial --output
   .livedocs/history.json`, then commit the result. Before the tag, `history-check --capsule <candidate> --version <version>`
   must render the candidate with the committed history.
4. Commit the release inputs, then run `axial policy review` and read every loosening and added exception. For the
   first review, use `axial policy review --since v0.11.3`. A human then runs `axial policy approve` (or
   `axial policy approve --yes-i-reviewed`) and commits `Axial.Guardrails.lock`. Approval records the current commit as
   the reviewed point; do not make unreviewed policy changes afterward. Run `axial policy review --check` before tagging.
   The lock is tamper-evident, not a security boundary; enforcement is the repository's choice. Axial chooses to refuse
   publication when this check finds loosening after the reviewed point.
5. Push `main`, then create and push the release tag. The workflow builds and runs the tool from that tag's own
   checkout, including when resuming a release, and checks policy before packaging or publishing.

The tag-triggered release workflow validates Axial, captures an immutable FsLiveDocs documentation capsule, verifies the
candidate against the committed history, and publishes the capsule and NuGet packages as GitHub release assets. It then
switches to the default branch, synchronizes the immutable capsule into `.livedocs/history.json`, verifies that history,
commits it, and explicitly dispatches and awaits the LiveDocs Pages workflow. That workflow renders only the committed
history. NuGet packages publish through the protected `nuget` environment using NuGet.org trusted publishing (OIDC via
`NuGet/login@v1`, scoped to the `nuget` GitHub environment and the `NUGET_USER` repo variable — no long-lived API key
stored in GitHub) only after Pages succeeds.

The LiveDocs workflow also verifies documentation on pull requests and deploys the current site from `main`.

Repository settings must keep GitHub Pages on the **GitHub Actions** source. GitHub release immutability is a repository setting; `gh release verify` verifies artifact attestations instead and must not be used as an immutability check. The release workflow publishes the GitHub release before dispatching its documentation build.
