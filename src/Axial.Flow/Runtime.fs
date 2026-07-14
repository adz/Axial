namespace Axial.Flow

open System.Threading

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
/// Provides extensible telemetry tags from an environment.
/// </summary>
/// <remarks>
/// Telemetry integrations (such as <c>Activity.trace</c> in <c>Axial.Flow.Telemetry</c>) apply these tags to
/// spans after the standard request/correlation/tenant traits. Implement this when the environment carries
/// ambient identity beyond that trio — session ids, job ids, regions — without needing a trait per concept.
/// </remarks>
type IHasTelemetryTags =
    /// <summary>Tag name/value pairs to stamp onto telemetry spans.</summary>
    abstract TelemetryTags: (string * string) list

/// <summary>
/// Provides a standard way to access the current user context from an environment.
/// </summary>
/// <typeparam name="user">The type of the application-specific user model.</typeparam>
type IHasUser<'user> =
    /// <summary>The current authenticated user, if available.</summary>
    abstract User: 'user option
