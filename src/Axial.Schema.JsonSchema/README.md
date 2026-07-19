# Axial.Schema.JsonSchema

JSON Schema document generation over `Axial.Schema` declarations. The generator is a pure
interpreter over schema descriptions: it lowers shapes, declared formats, and portable constraint
metadata to JSON Schema keywords, so one schema declaration drives parsing, validation, and the
published contract.

```fsharp
open Axial.Schema

let document = JsonSchema.generate personSchema
```

Part of the [Axial](https://github.com/adz/Axial) schema family.
