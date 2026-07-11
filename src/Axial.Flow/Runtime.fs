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
/// Provides a standard way to access the current user context from an environment.
/// </summary>
/// <typeparam name="user">The type of the application-specific user model.</typeparam>
type IHasUser<'user> =
    /// <summary>The current authenticated user, if available.</summary>
    abstract User: 'user option
