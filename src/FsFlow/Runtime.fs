namespace FsFlow

open System
open System.Threading
open System.Threading.Tasks
open FsFlow

/// <summary>
/// Provides a standard way to access a unique request identifier from an environment.
/// </summary>
type IHasRequestId =
    /// <summary>The unique identifier for the current request.</summary>
    abstract RequestId: string

/// <summary>
/// Provides a standard way to access a correlation identifier from an environment.
/// </summary>
type IHasCorrelationId =
    /// <summary>The correlation identifier linking multiple related requests.</summary>
    abstract CorrelationId: string option

/// <summary>
/// Provides a standard way to access a tenant identifier from an environment.
/// </summary>
type IHasTenantId =
    /// <summary>The identifier for the current tenant or organization.</summary>
    abstract TenantId: string option

/// <summary>
/// Provides a standard way to access the current user context from an environment.
/// </summary>
/// <typeparam name="user">The type of the application-specific user model.</typeparam>
type IHasUser<'user> =
    /// <summary>The current authenticated user, if available.</summary>
    abstract User: 'user option

/// <summary>
/// Captures the two-context shape of a task workflow execution:
/// host services, application capabilities, and the cancellation token for the current run.
/// </summary>
/// <remarks>
/// This type is the execution carrier above the adapter layer for the unified
/// <see cref="T:FsFlow.Flow`3" />.
/// It separates low-level operational concerns (Host) from high-level domain dependencies
/// (AppEnv).
/// </remarks>
/// <typeparam name="host">The type that carries host concerns, such as logging or metrics.</typeparam>
/// <typeparam name="appEnv">The type that carries application capabilities, such as repositories.</typeparam>
type HostContext<'host, 'appEnv> =
    {
        /// <summary>Host services for logging, metrics, tracing, or other operational concerns.</summary>
        Host: 'host

        /// <summary>Application dependencies and capabilities for the workflow.</summary>
        AppEnv: 'appEnv

        /// <summary>The cancellation token for the current task execution.</summary>
        CancellationToken: CancellationToken
    }

/// <summary>Helpers for building and reshaping <see cref="HostContext{host, appEnv}" /> values.</summary>
[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
[<RequireQualifiedAccess>]
module HostContext =
    /// <summary>Creates a host context from the supplied host services, app environment, and cancellation token.</summary>
    /// <param name="host">The host services of type <c>'host</c>.</param>
    /// <param name="appEnv">The application environment of type <c>'appEnv</c>.</param>
    /// <param name="cancellationToken">The <see cref="T:System.Threading.CancellationToken" />.</param>
    /// <returns>A new <see cref="T:FsFlow.HostContext`2" />.</returns>
    let create
        (host: 'host)
        (appEnv: 'appEnv)
        (cancellationToken: CancellationToken)
        : HostContext<'host, 'appEnv> =
        {
            Host = host
            AppEnv = appEnv
            CancellationToken = cancellationToken
        }

    /// <summary>Reads the host half of a host context.</summary>
    /// <param name="context">The <see cref="T:FsFlow.HostContext`2" /> to read.</param>
    /// <returns>The host services of type <c>'host</c>.</returns>
    let host (context: HostContext<'host, 'appEnv>) : 'host = context.Host

    /// <summary>Reads the application environment half of a host context.</summary>
    /// <param name="context">The <see cref="T:FsFlow.HostContext`2" /> to read.</param>
    /// <returns>The application environment of type <c>'appEnv</c>.</returns>
    let appEnv (context: HostContext<'host, 'appEnv>) : 'appEnv = context.AppEnv

    /// <summary>Reads the cancellation token stored in a host context.</summary>
    /// <param name="context">The <see cref="T:FsFlow.HostContext`2" /> to read.</param>
    /// <returns>The <see cref="T:System.Threading.CancellationToken" />.</returns>
    let cancellationToken (context: HostContext<'host, 'appEnv>) : CancellationToken = context.CancellationToken

    /// <summary>Maps the host half of a host context.</summary>
    /// <param name="mapper">A function of type <c>'host -> 'nextHost</c>.</param>
    /// <param name="context">The source context.</param>
    /// <returns>A new context with the mapped host services.</returns>
    let mapHost
        (mapper: 'host -> 'nextHost)
        (context: HostContext<'host, 'appEnv>)
        : HostContext<'nextHost, 'appEnv> =
        {
            Host = mapper context.Host
            AppEnv = context.AppEnv
            CancellationToken = context.CancellationToken
        }

    /// <summary>Maps the application environment half of a host context.</summary>
    /// <param name="mapper">A function of type <c>'appEnv -> 'nextAppEnv</c>.</param>
    /// <param name="context">The source context.</param>
    /// <returns>A new context with the mapped app environment.</returns>
    let mapAppEnv
        (mapper: 'appEnv -> 'nextAppEnv)
        (context: HostContext<'host, 'appEnv>)
        : HostContext<'host, 'nextAppEnv> =
        {
            Host = context.Host
            AppEnv = mapper context.AppEnv
            CancellationToken = context.CancellationToken
        }

    /// <summary>Replaces the host half of a host context.</summary>
    /// <param name="host">The new host services.</param>
    /// <param name="context">The source context.</param>
    /// <returns>A new context with the replaced host services.</returns>
    let withHost
        (host: 'nextHost)
        (context: HostContext<'host, 'appEnv>)
        : HostContext<'nextHost, 'appEnv> =
        mapHost (fun _ -> host) context

    /// <summary>Replaces the application environment half of a host context.</summary>
    /// <param name="appEnv">The new application environment.</param>
    /// <param name="context">The source context.</param>
    /// <returns>A new context with the replaced app environment.</returns>
    let withAppEnv
        (appEnv: 'nextAppEnv)
        (context: HostContext<'host, 'appEnv>)
        : HostContext<'host, 'nextAppEnv> =
        mapAppEnv (fun _ -> appEnv) context
