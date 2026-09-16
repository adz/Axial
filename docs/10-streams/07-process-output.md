---
title: Process Output
---

# Process Output

`Axial.Process.Process.stream` is a concrete effectful source. It emits structured stdout and stderr events followed by
a completion event:

```fsharp no-check reason="Application-specific process specification is described in the Process guide"
let errors =
    Process.stream specification
    |> FlowStream.choose (function
        | ProcessEvent.StdErr line -> Some line
        | _ -> None)
    |> FlowStream.runCollect
```

Output is pulled with backpressure rather than captured into one unbounded result. If downstream fails or stops early,
the enclosing stream scope cancels the child process pipeline and waits for cleanup.

The Process package owns process creation and operating-system I/O. Core `FlowStream` owns only the portable pull,
composition, failure, and lifetime mechanics.

Continue with the full [Streaming output](/process/streaming.html) guide for framing, completion transcripts, and
cancellation behavior.
