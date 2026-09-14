namespace Axial

/// Reflection-free rendering of arbitrary payload values inside outcome types.
module internal OutcomeText =
    /// A payload's own ToString, with strings quoted and null spelled out.
    let value (payload: obj) : string =
        match payload with
        | null -> "null"
        | :? string as text -> "\"" + text + "\""
        | other -> other.ToString()

/// <summary>
/// Represents the cause of a failed workflow.
/// </summary>
/// <typeparam name="error">The type of the domain-specific failure value.</typeparam>
[<RequireQualifiedAccess>]
type Cause<'error> =
    /// <summary>An expected domain-specific failure.</summary>
    | Fail of 'error
    /// <summary>An unexpected defect or panic (e.g., an exception).</summary>
    | Die of exn
    /// <summary>An administrative signal to stop the workflow (e.g., cancellation).</summary>
    | Interrupt
    /// <summary>Two causes happened sequentially; the left cause happened before the right cause.</summary>
    | Then of Cause<'error> * Cause<'error>
    /// <summary>Two causes happened concurrently; neither cause is ordered before the other.</summary>
    | Both of Cause<'error> * Cause<'error>
    /// <summary>A cause annotated with diagnostic trace text.</summary>
    | Traced of Cause<'error> * trace: string

    /// <summary>
    /// Renders the cause tree on one line. Written by hand: the compiler-generated rendering uses reflection that
    /// NativeAOT and trimming remove. The error value is rendered with its own <c>ToString</c>; use
    /// <c>Cause.prettyPrint</c> to supply a renderer.
    /// </summary>
    override this.ToString() =
        match this with
        | Fail error -> $"Fail({OutcomeText.value (box error)})"
        | Die exn ->
#if FABLE_COMPILER
            $"Die({exn.Message})"
#else
            $"Die({exn.GetType().Name}: {exn.Message})"
#endif
        | Interrupt -> "Interrupt"
        | Then(left, right) -> $"Then({left.ToString()}, {right.ToString()})"
        | Both(left, right) -> $"Both({left.ToString()}, {right.ToString()})"
        | Traced(inner, trace) -> $"Traced({inner.ToString()}, {trace})"

/// <summary>
/// Represents the final outcome of a workflow execution.
/// </summary>
/// <typeparam name="value">The type of the success value.</typeparam>
/// <typeparam name="error">The type of the domain-specific failure value.</typeparam>
[<RequireQualifiedAccess>]
type Exit<'value, 'error> =
    /// <summary>The workflow completed successfully.</summary>
    | Success of 'value
    /// <summary>The workflow failed due to a specific cause.</summary>
    | Failure of Cause<'error>

    /// <summary>Renders the exit without reflection (safe under NativeAOT); values use their own <c>ToString</c>.</summary>
    override this.ToString() =
        match this with
        | Success value -> $"Success({OutcomeText.value (box value)})"
        | Failure cause -> $"Failure({cause.ToString()})"

/// <summary>Describes the current lifecycle state of a fiber.</summary>
[<RequireQualifiedAccess>]
type FiberStatus =
    /// <summary>The fiber is currently running.</summary>
    | Running
    /// <summary>The fiber completed with a successful value.</summary>
    | Succeeded
    /// <summary>The fiber completed with a typed failure or defect.</summary>
    | Failed
    /// <summary>The fiber completed with an interruption cause.</summary>
    | Interrupted

    /// <summary>The status name. Written by hand so it stays safe under NativeAOT and trimming.</summary>
    override this.ToString() =
        match this with
        | Running -> "Running"
        | Succeeded -> "Succeeded"
        | Failed -> "Failed"
        | Interrupted -> "Interrupted"
