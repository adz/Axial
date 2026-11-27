---
title: "swallowed-exn (AXG010): don't throw exceptions away"
linkTitle: swallowed-exn
description: Reports catch-all handlers that neither use the exception nor rethrow it.
---

# swallowed-exn (AXG010)

## The problem

`with _ -> ()` catches everything: the missing file you expected, and also the out-of-memory, the null reference and
the bug you have not found yet. The program carries on as if nothing happened, and the bug surfaces later somewhere
else, with no trace of where it started.

```fsharp
/// Reported: every failure, expected or not, is silently ignored.
let deleteIfPresentSwallowing (delete: unit -> unit) =
    try delete () with _ -> ()

/// Fixed: only the expected failure is ignored; anything else still surfaces.
let deleteIfPresent (delete: unit -> unit) =
    try delete ()
    with :? System.IO.FileNotFoundException -> ()
```

A narrow catch does not need to bind or use the exception. Do not put the exception in a return value just to silence
the finding.

## What it reports

A catch-all handler (`_`, a bound exception, or `:? exn`) that does not use the exception and does not `raise` or
`reraise` it. Returning, storing, logging the bound value or inspecting it in a `when` guard counts as using it. A
function name such as `report` alone does not. Returning `Error` or `Exit.Failure` without the exception is still
reported.

Test projects are exempt, unless `AxialGuardrailsInTests=all`.

## When it is deliberate

This is a guide finding. When the exception really does not matter, leave a note as the first line inside the
handler:

```fsharp
let acknowledged () =
    try
        failwith "failed"
    with _ ->
        // axial-note swallowed-exn: best-effort cleanup; the failure is reported elsewhere
        ()
```

The finding stays visible as Info with the reason. A note above `with` is misplaced. Without a note, the finding is
an error at a [checkpoint](../allowances-and-notes.html#checkpoints).

A host project that must catch everything at its boundary can be listed under
[`[exemptions] swallowed-exn`](../policy-file.html#exempt-a-host-project-from-a-concurrency-rule).
