namespace Axial.Flow

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
