# Axial.Data API Reference

API reference for the independent `Axial.Data` package.

## Axial

- `Data` — a portable recursive value with `Null`, `Text`, `Number`, `Bool`, `List`, and `Object` cases.
- `Data.Syntax.data` — constructs ordered object fields.
- `Data.Syntax.(=>)` — associates a field name and converts supported primitives or lists into `Data`.

`Axial.Data` has no dependency on Schema, Flow, codecs, or error handling.
