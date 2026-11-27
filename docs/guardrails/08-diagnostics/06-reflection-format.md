---
title: "reflection-format (AXG006): format without reflection"
linkTitle: reflection-format
description: Reports formatting of unions, records and FSharp.Core types that needs reflection, which NativeAOT and trimming remove.
---

# reflection-format (AXG006)

## The problem

An F# union or record gets a generated `ToString()` that calls `sprintf "%+A"`, and `%A` reads the value through
reflection. NativeAOT and trimming remove the metadata it needs. The app works under `dotnet run`, then throws or
prints the wrong text once published. The build gives no warning, because the reflection happens inside FSharp.Core.

```fsharp
type OrderError =
    | OutOfStock of sku: string
    | InvalidQuantity of int

/// Reported: the generated ToString uses reflection.
let describeByReflection (error: OrderError) = $"Order failed: {error}"
```

Give the type its own `ToString`, or match on the cases:

```fsharp
type PaymentError =
    | Declined of reason: string
    | Expired

    override this.ToString() =
        match this with
        | Declined reason -> $"Declined: {reason}"
        | Expired -> "Card expired"

/// Fixed: the override renders the value without reflection.
let describe (error: PaymentError) = $"Payment failed: {error}"
```

With the override, `$"{error}"`, `string error` and `%O` are safe. `%A` is not.

## What it reports

- `x.ToString()`, `string x` and `$"{x}"` when `x` is a union, record, anonymous record or exception without a
  hand-written `ToString`, or FSharp.Core's `option`, `voption`, `list`, `Result`, `Choice`, `Map` or `Set`.
- The same on a type parameter, which may turn out to be a union or record.
- `%A` on anything but a primitive, even with a `ToString` override.
- Those values boxed into `String.Format`, `String.Concat`, `StringBuilder.Append`, `TextWriter.Write`,
  `Console.Write`, `Trace.WriteLine` or `Debug.WriteLine`.

Axial's own public types, such as `Exit`, `Cause` and the error types of each package, render themselves. A payload
inside `Exit` or `Cause` uses its own `ToString`, so give your error types one too. `Cause.prettyPrint` also takes a
renderer.

## Make the compiler help

Compile with `--reflectionfree` to stop F# generating reflection-based `ToString` and to reject `%A`. Axial's
libraries do:

```xml
<PropertyGroup>
  <OtherFlags>$(OtherFlags) --reflectionfree</OtherFlags>
</PropertyGroup>
```

This does not make `printf` itself safe: any `printf` can still fail after trimming. On NativeAOT paths, write with
`Console.WriteLine` or another non-`printf` API.

## When it is deliberate

For code that never runs trimmed, such as a script or a test helper:

```fsharp
let printDiagnostics (diagnostics: Map<string, int list>) =
    printfn "%A" diagnostics // axial-allow-reflection-format: diagnostic helper never runs trimmed
```
