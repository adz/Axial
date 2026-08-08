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
```

3. Add the release to `.livedocs/history-manifest.json`. Its tag, API model asset name, checksum asset name, and
   schema version must match the version in `Directory.Build.props`.
4. Commit, push `main`, then create and push the release tag.

The tag-triggered release workflow validates Axial, extracts the schema-versioned API model, publishes the model and
checksum as immutable release assets, verifies every manifest entry, rebuilds all documentation versions from their
Git tags with the current FsLiveDocs renderer, deploys a GitHub Pages artifact, and publishes NuGet packages through
the protected `nuget` environment using `NUGET_API_KEY`. Tags and release assets are durable inputs; Pages output is
disposable.

Repository settings must keep GitHub Pages on the **GitHub Actions** source and immutable releases enabled. The
workflow calls `gh release verify` after publishing and fails if GitHub does not report the release as immutable.
