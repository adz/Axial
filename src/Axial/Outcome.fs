namespace Axial

/// Rendering of arbitrary payload values inside outcome types that stays safe under NativeAOT.
module internal OutcomeText =
    /// A payload's own ToString, with null spelled out.
    ///
    /// F# unions and records get a compiler-generated ToString that walks the value with reflection, which throws
    /// under NativeAOT and full trimming. A hand-written ToString is safe, and so is every ToString under the JIT, so
    /// the value's own rendering is tried first and only a failing one falls back to the type name:
    /// <c>&lt;OrderError&gt;</c>. Callers that need the detail everywhere supply a renderer (<c>Cause.prettyPrint</c>).
    let plain (payload: obj) : string =
        match payload with
        | null -> "null"
        | other ->
#if FABLE_COMPILER
            other.ToString()
#else
            try
                other.ToString()
            with _ ->
                "<" + other.GetType().Name + ">"
#endif

    /// Like <c>plain</c>, with strings quoted so they read as values inside a rendered cause or exit.
    let value (payload: obj) : string =
        match payload with
        | :? string as text -> "\"" + text + "\""
        | other -> plain other

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
    /// NativeAOT and trimming remove. The error value is rendered with its own <c>ToString</c>, or by type name when
    /// that ToString needs reflection the runtime removed; use <c>Cause.prettyPrint</c> to supply a renderer.
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

    /// <summary>Renders the exit without reflection. Values use their own <c>ToString</c>, or their type name when that ToString needs reflection the runtime removed.</summary>
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
