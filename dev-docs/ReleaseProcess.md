# Release Process

This project uses a coordinated pre-1.0 release train.

## Versioning policy

- `Axial.Flow`, `Axial.ErrorHandling`, `Axial.Refined`, `Axial.Validation`, the umbrella `Axial` package, and the `Axial.Flow.*` add-on packages share one package version before 1.0.
- The shared version is declared once in `Directory.Build.props`.
- Packable project files must not declare their own `<Version>`.
- A tag such as `v0.7.0` produces every public Axial package at version `0.7.0`.
- Empty version bumps are acceptable before 1.0 because the package boundaries are still settling and a single documented version is simpler for users.
- Independent package versioning can be reconsidered once the split is stable, likely at or after 1.0.

## Public package set

The coordinated release currently packs:

- `Axial.Flow`
- `Axial.ErrorHandling`
- `Axial.Refined`
- `Axial.Validation`
- `Axial`
- `Axial.Flow.Console`
- `Axial.Flow.FileSystem`
- `Axial.Flow.Http`
- `Axial.Flow.Process`
- `Axial.Flow.PlatformService`
- `Axial.Flow.Hosting`
- `Axial.Flow.Telemetry`

`Axial` is the umbrella package. It references `Axial.Flow`, `Axial.ErrorHandling`, `Axial.Refined`, and `Axial.Validation`.

The `Axial.Flow.*` add-on packages should depend on `Axial.Flow`, not the umbrella `Axial` package, unless there is a specific reason to expose ErrorHandling, Refined, or Validation APIs.

## Preparing a release

1. Update the shared `<Version>` in `Directory.Build.props`.
2. Update `RELEASE_NOTES.md`.
3. Run the local verification commands:

```bash
dotnet build Axial.slnx --configuration Release --nologo -v minimal
dotnet test tests/Axial.Tests/Axial.Tests.fsproj --configuration Release --no-build --nologo -v minimal
bash scripts/pack.sh
bash scripts/validate-docs.sh
```

For release/deploy checks, also run:

```bash
npm run build --prefix site
```

4. Commit the release-prep changes.
5. Push the release commit to `main`.
6. Create and push the tag:

```bash
git tag v0.7.0
git push origin v0.7.0
```

## CI release behavior

`.github/workflows/release.yml` runs for `v*.*.*` tags.

For a tag build:

- it builds `Axial.slnx`
- it tests `tests/Axial.Tests/Axial.Tests.fsproj`
- it derives the package version from the tag by stripping the leading `v`
- it runs `bash scripts/pack.sh -v <version>`
- it builds the docs site
- it uploads package and docs artifacts
- it creates a GitHub Release with `.nupkg` and `.snupkg` files attached
- it runs a separate `publish-nuget` job that publishes the package artifacts to nuget.org

For manual `workflow_dispatch`, the workflow currently uses `0.7.0` as the fallback package version. Change that fallback before using manual dispatch for a different version.

## NuGet publishing

The release workflow publishes packages to nuget.org only from tags matching `v*.*.*`.

NuGet publishing is isolated in the `publish-nuget` job:

- it runs only after the package job succeeds
- it runs only for tag refs beginning with `refs/tags/v`
- it uses the `nuget` GitHub Environment
- it reads `NUGET_API_KEY` from GitHub Secrets
- it publishes the already-built package artifact instead of rebuilding

Configure the `nuget` environment in GitHub repository settings. For maximum safety, require manual approval for that environment and store `NUGET_API_KEY` as an environment secret rather than a repository-wide secret.

The publish command is equivalent to:

```bash
dotnet nuget push artifacts/package/<package>.nupkg --api-key "$NUGET_API_KEY" --source https://api.nuget.org/v3/index.json
```

The workflow publishes every `.nupkg` and `.snupkg` produced for the release and uses `--skip-duplicate` so rerunning a failed publish job does not fail on packages that already reached nuget.org.

## Local packing

Use the repository version:

```bash
bash scripts/pack.sh
```

Override the version explicitly:

```bash
bash scripts/pack.sh -v 0.7.0
```

Packages are written to `artifacts/package`.
