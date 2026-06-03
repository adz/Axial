namespace FsFlow

open System
open System.Collections.Generic
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

/// <summary>Provides synchronous access to explicit workflow logging.</summary>
type ILog =
    /// <summary>Writes a log message at the requested level.</summary>
    abstract Log: level: LogLevel -> message: string -> unit

/// <summary>Provides synchronous random-number generation.</summary>
type IRandom =
    /// <summary>Returns a non-negative random integer.</summary>
    abstract Next: unit -> int

    /// <summary>Returns a non-negative random integer less than the supplied maximum.</summary>
    abstract NextMax: maxExclusive: int -> int

    /// <summary>Returns a random integer in the requested range.</summary>
    abstract NextInt: minInclusive: int -> maxExclusive: int -> int

    /// <summary>Returns a random floating-point number greater than or equal to 0.0 and less than 1.0.</summary>
    abstract NextDouble: unit -> float

    /// <summary>Fills the supplied buffer with random bytes.</summary>
    abstract NextBytes: buffer: byte array -> unit

/// <summary>Provides synchronous GUID generation.</summary>
type IGuid =
    /// <summary>Returns a new GUID value.</summary>
    abstract NewGuid: unit -> Guid

/// <summary>Provides synchronous environment-variable access.</summary>
type IEnvironmentVariables =
    /// <summary>Returns the environment-variable value if it is present.</summary>
    abstract TryGet: name: string -> string option

    /// <summary>Sets, updates, or clears an environment-variable value.</summary>
    abstract Set: name: string * value: string option -> unit

    /// <summary>Expands environment-variable references in the supplied text.</summary>
    abstract Expand: text: string -> string

    /// <summary>Returns all visible environment variables as name/value pairs.</summary>
    abstract GetAll: unit -> IReadOnlyDictionary<string, string>

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
