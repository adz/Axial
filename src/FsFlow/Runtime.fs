namespace FsFlow

open System
open System.Threading
open System.Threading.Tasks
open FsFlow

/// <summary>
/// Captures the two-context shape of a task workflow execution:
/// runtime services, application capabilities, and the cancellation token for the current run.
/// </summary>
/// <remarks>
/// This type is the standard environment carrier for the unified <see cref="T:FsFlow.Flow`3" />.
/// It separates low-level operational concerns (Runtime) from high-level domain dependencies (Environment).
/// </remarks>
/// <typeparam name="runtime">The type that carries runtime concerns, such as logging or metrics.</typeparam>
/// <typeparam name="env">The type that carries application capabilities, such as repositories.</typeparam>
type RuntimeContext<'runtime, 'env> =
    {
        /// <summary>Runtime services for logging, metrics, tracing, or other operational concerns.</summary>
        Runtime: 'runtime

        /// <summary>Application dependencies and capabilities for the workflow.</summary>
        Environment: 'env

        /// <summary>The cancellation token for the current task execution.</summary>
        CancellationToken: CancellationToken
    }

/// <summary>Helpers for building and reshaping <see cref="RuntimeContext{runtime, env}" /> values.</summary>
[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
[<RequireQualifiedAccess>]
module RuntimeContext =
    /// <summary>Creates a runtime context from the supplied runtime services, environment, and cancellation token.</summary>
    /// <param name="runtime">The runtime services of type <c>'runtime</c>.</param>
    /// <param name="environment">The application environment of type <c>'env</c>.</param>
    /// <param name="cancellationToken">The <see cref="T:System.Threading.CancellationToken" />.</param>
    /// <returns>A new <see cref="T:FsFlow.RuntimeContext`2" />.</returns>
    let create
        (runtime: 'runtime)
        (environment: 'env)
        (cancellationToken: CancellationToken)
        : RuntimeContext<'runtime, 'env> =
        {
            Runtime = runtime
            Environment = environment
            CancellationToken = cancellationToken
        }

    /// <summary>Reads the runtime half of a runtime context.</summary>
    /// <param name="context">The <see cref="T:FsFlow.RuntimeContext`2" /> to read.</param>
    /// <returns>The runtime services of type <c>'runtime</c>.</returns>
    let runtime (context: RuntimeContext<'runtime, 'env>) : 'runtime = context.Runtime

    /// <summary>Reads the application environment half of a runtime context.</summary>
    /// <param name="context">The <see cref="T:FsFlow.RuntimeContext`2" /> to read.</param>
    /// <returns>The application environment of type <c>'env</c>.</returns>
    let environment (context: RuntimeContext<'runtime, 'env>) : 'env = context.Environment

    /// <summary>Reads the cancellation token stored in a runtime context.</summary>
    /// <param name="context">The <see cref="T:FsFlow.RuntimeContext`2" /> to read.</param>
    /// <returns>The <see cref="T:System.Threading.CancellationToken" />.</returns>
    let cancellationToken (context: RuntimeContext<'runtime, 'env>) : CancellationToken = context.CancellationToken

    /// <summary>Maps the runtime half of a runtime context.</summary>
    /// <param name="mapper">A function of type <c>'runtime -> 'nextRuntime</c>.</param>
    /// <param name="context">The source context.</param>
    /// <returns>A new context with the mapped runtime services.</returns>
    let mapRuntime
        (mapper: 'runtime -> 'nextRuntime)
        (context: RuntimeContext<'runtime, 'env>)
        : RuntimeContext<'nextRuntime, 'env> =
        {
            Runtime = mapper context.Runtime
            Environment = context.Environment
            CancellationToken = context.CancellationToken
        }

    /// <summary>Maps the application environment half of a runtime context.</summary>
    /// <param name="mapper">A function of type <c>'env -> 'nextEnv</c>.</param>
    /// <param name="context">The source context.</param>
    /// <returns>A new context with the mapped environment.</returns>
    let mapEnvironment
        (mapper: 'env -> 'nextEnv)
        (context: RuntimeContext<'runtime, 'env>)
        : RuntimeContext<'runtime, 'nextEnv> =
        {
            Runtime = context.Runtime
            Environment = mapper context.Environment
            CancellationToken = context.CancellationToken
        }

    /// <summary>Replaces the runtime half of a runtime context.</summary>
    /// <param name="runtime">The new runtime services.</param>
    /// <param name="context">The source context.</param>
    /// <returns>A new context with the replaced runtime services.</returns>
    let withRuntime
        (runtime: 'nextRuntime)
        (context: RuntimeContext<'runtime, 'env>)
        : RuntimeContext<'nextRuntime, 'env> =
        mapRuntime (fun _ -> runtime) context

    /// <summary>Replaces the environment half of a runtime context.</summary>
    /// <param name="environment">The new application environment.</param>
    /// <param name="context">The source context.</param>
    /// <returns>A new context with the replaced environment.</returns>
    let withEnvironment
        (environment: 'nextEnv)
        (context: RuntimeContext<'runtime, 'env>)
        : RuntimeContext<'runtime, 'nextEnv> =
        mapEnvironment (fun _ -> environment) context
