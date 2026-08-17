# Release process

All public Axial packages share one pre-1.0 version, taken from the `vX.Y.Z` release tag itself (not from a version committed in the repo). A `vX.Y.Z` tag produces that version of core and every focused add-on listed by `scripts/pack.sh`.

The Reified release train is independent. The two HTTP contract adapters are not packed until public `Reified.*` dependencies are available.

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
```

3. Ensure `.livedocs/history.json` contains every previously published documentation capsule. Add a missing release with `dotnet livedocs history-add <version> --url <capsule-url> --sha256 <sha256>`.
4. Commit and push `main`, then create and push the release tag.

The tag-triggered release workflow validates Axial, captures an immutable FsLiveDocs documentation capsule, publishes the capsule and NuGet packages as release assets, and publishes NuGet packages through the protected `nuget` environment using NuGet.org trusted publishing (OIDC via `NuGet/login@v1`, scoped to the `nuget` GitHub environment and the `NUGET_USER` repo variable — no long-lived API key stored in GitHub). After publishing the GitHub release, it dispatches the LiveDocs workflow with the released capsule URL and checksum. That workflow adds the capsule to a temporary copy of the history index and deploys the complete release history. This ordering prevents Pages from trying to download a capsule before its release asset exists. Commit the new entry to `.livedocs/history.json` before the following release.

The LiveDocs workflow also verifies documentation on pull requests and deploys the current site from `main`.

Repository settings must keep GitHub Pages on the **GitHub Actions** source. GitHub release immutability is a repository setting; `gh release verify` verifies artifact attestations instead and must not be used as an immutability check. The release workflow publishes the GitHub release before dispatching its documentation build.
