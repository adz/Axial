# Release process

All public Axial packages inherit one pre-1.0 version from `Directory.Build.props`. A `vX.Y.Z` tag produces that version of core and every focused add-on listed by `scripts/pack.sh`.

The Reified release train is independent. The two HTTP contract adapters are not packed until public `Reified.*` dependencies are available.

## Prepare

1. Update `<Version>` in `Directory.Build.props` and `RELEASE_NOTES.md`.
2. Run:

```bash
dotnet build Axial.slnx --configuration Release --nologo -v minimal
dotnet test Axial.slnx --configuration Release --no-build --nologo -v minimal
bash scripts/check-source-inventory.sh
bash scripts/check-fable-js-surface.sh
bash scripts/run-aot-probe.sh
bash scripts/pack.sh
bash scripts/validate-docs.sh
npm run build --prefix site
```

3. Commit, push `main`, then create and push the release tag.

The release workflow uploads packages and documentation, creates the GitHub release, and publishes NuGet artifacts through the protected `nuget` environment using `NUGET_API_KEY`.
