# Hugo documentation maintenance notes live in [DOCS.md](DOCS.md).

## Release process

1. Update `RELEASE_NOTES.md` and `fsproj` version strings.
2. Commit and push to `main` to trigger GitHub Pages.
3. Tag the release with `git tag v0.x.y && git push origin v0.x.y`.
4. `release.yml` builds NuGet packages and creates a GitHub Release.

> **Note on Service Packages**: The `Axial.Services.*` packages (Console, FileSystem, etc.) are currently considered experimental and are **not** part of the public NuGet release cycle. Their versioning relationship to core `Axial` is TBD. For now, only pack and release the core `Axial` package.
