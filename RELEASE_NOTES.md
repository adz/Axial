# Release notes

## Next

Axial is now the standalone workflow product.

- `Axial.Flow` is renamed to `Axial`.
- Flow add-ons are renamed from `Axial.Flow.*` to `Axial.*`.
- Constraint, Refined, Parse, Result, Data, Schema, codecs, and contract tooling moved to the separate [Reified](https://github.com/adz/Reified) repository.
- `Axial.Hosting.AspNetCore` and `Axial.Hosting.GenHttp` remain optional integration adapters over Reified HTTP contracts.
- The public documentation and release pipeline now cover only Axial workflow packages.

This is a pre-1.0 breaking change. No compatibility packages or namespace aliases are retained.
