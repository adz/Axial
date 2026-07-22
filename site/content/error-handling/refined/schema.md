---
weight: 50
title: Relation to Schema
description: Using refined values as fields in an Axial.Schema model.
type: docs
---


`Axial.Refined` does not depend on Schema. Refined values can be created and used in ordinary functions without
declaring a schema.

When a model does use `Axial.Schema`, `Schema.refine` connects a schema field to a refined constructor. Input is
parsed and checked before the model is constructed, and failures are returned through the schema's Diagnostics.

```fsharp
let quantity =
    Schema.int
    |> Schema.refine Refine.positiveInt
```

Keep domain-specific refined types in the domain package. The schema describes how external input reaches those
types; it does not own their rules or representation.

See the [Schema guide]({{< relref "/schema/" >}}) for model construction, input handling, and Diagnostics.
