# Reference Audit

Snapshot after the reference cleanup pass.

## Fixed

- Legacy `https://adz.github.io/Axial/reference/Axial/*.html` links and root-relative
  `/reference/Axial/*.html` links are rewritten to local generated reference pages.
- `Flow` reference pages are grouped into sub-sections:
  - `construction`
  - `environment`
  - `composition`
  - `execution`
  - `resources`
  - `concurrency`
  - `scheduling`
- Service `*.layer` entries resolve to the actual service layer values instead of the generic `layer { }` builder page.
- `Scope` remains present and linked in the reference tree.
- The previous aliased targets now have dedicated pages:
  - `Never`
  - `LogLevel`
  - `RetryPolicy<'error>`
  - `StmBuilder`
  - `Path`

## Remaining Notes

- No Check reserved-word pages remain after the surface redesign removed `Check.not`, `Check.and`, and `Check.or`.
- The generator still reports missing `PredicateModule.String.*` and `PredicateModule.Seq.*` entries from the Check page
  spec. Those are separate stale Check reference entries, not part of the remaining alias cleanup completed here.
