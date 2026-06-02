namespace FsFlow

open System
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

/// <summary>Provides synchronous access to the current UTC clock.</summary>
type IClock =
    /// <summary>Returns the current UTC timestamp.</summary>
    abstract UtcNow: unit -> DateTimeOffset

/// <summary>Provides synchronous access to runtime logging.</summary>
type ILog =
    /// <summary>Writes an informational log message.</summary>
    abstract Info: string -> unit

/// <summary>Provides synchronous random-number generation.</summary>
type IRandom =
    /// <summary>Returns a random integer in the requested range.</summary>
    abstract NextInt: minInclusive: int -> maxExclusive: int -> int

/// <summary>Provides synchronous GUID generation.</summary>
type IGuid =
    /// <summary>Returns a new GUID value.</summary>
    abstract NewGuid: unit -> Guid

/// <summary>Provides synchronous environment-variable lookup.</summary>
type IEnvironmentVariables =
    /// <summary>Returns the environment-variable value if it is present.</summary>
    abstract TryGet: name: string -> string option

/// <summary>Internal runtime services owned by the flow execution engine.</summary>
type internal RuntimeContext =
    {
        Scope: Scope
        Annotations: Map<string, string>
        AnnotationSink: string -> string -> unit
    }

/// <summary>Helpers for creating and overriding runtime-owned services.</summary>
[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
[<RequireQualifiedAccess>]
module internal RuntimeContext =
    let create (scope: Scope) : RuntimeContext =
        {
            Scope = scope
            Annotations = Map.empty
            AnnotationSink = fun _ _ -> ()
        }

    let detached : RuntimeContext =
        create (Scope())

    let withScope (scope: Scope) (runtime: RuntimeContext) : RuntimeContext =
        { runtime with Scope = scope }

    let withAnnotation (name: string) (value: string) (runtime: RuntimeContext) : RuntimeContext =
        { runtime with Annotations = runtime.Annotations |> Map.add name value }

    let withAnnotationSink (sink: string -> string -> unit) (runtime: RuntimeContext) : RuntimeContext =
        { runtime with AnnotationSink = sink }

/// <summary>Stores the ambient runtime context for the current execution.</summary>
[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
[<RequireQualifiedAccess>]
module internal RuntimeState =
#if FABLE_COMPILER
    let mutable private currentRuntime = RuntimeContext.detached

    let current () : RuntimeContext = currentRuntime

    let withRuntime (runtime: RuntimeContext) (operation: unit -> 'value) : 'value =
        let previous = currentRuntime
        currentRuntime <- runtime

        try
            operation ()
        finally
            currentRuntime <- previous
#else
    let private currentRuntime = AsyncLocal<RuntimeContext>()

    let current () : RuntimeContext =
        match box currentRuntime.Value with
        | null -> RuntimeContext.detached
        | _ -> currentRuntime.Value

    let withRuntime (runtime: RuntimeContext) (operation: unit -> 'value) : 'value =
        let previous = currentRuntime.Value
        currentRuntime.Value <- runtime

        try
            operation ()
        finally
            currentRuntime.Value <- previous
#endif
