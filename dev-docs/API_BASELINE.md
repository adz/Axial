# Axial API baseline

The post-split baseline starts with the `Axial` package identity introduced in commit `47ccb2b6`.

Public packages are listed in `dev-docs/PLAN.md`. The authoritative source inventory is `Axial.slnx`; source and test coverage is checked by `scripts/check-source-inventory.sh`.

Refresh this file at a release checkpoint by recording:

```bash
dotnet build Axial.slnx --configuration Release --nologo -v minimal
dotnet test Axial.slnx --configuration Release --no-build --nologo -v minimal
bash scripts/check-source-inventory.sh
bash scripts/run-aot-probe.sh
bash scripts/check-fable-js-surface.sh
bash scripts/pack.sh
```

Do not compare the new assemblies against pre-split `Axial.Flow*` names; that rename is intentionally breaking before 1.0.
