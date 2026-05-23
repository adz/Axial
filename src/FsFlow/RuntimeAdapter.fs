namespace FsFlow

#if !FABLE_COMPILER

open System
open System.Threading

/// <summary>Helpers for projecting a runtime registry into a nominal contract.</summary>
/// <remarks>
/// This bridge is intentionally thin. It exists so the public contract can stay readable while the
/// runtime storage remains tagged and internal.
/// </remarks>
[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
[<RequireQualifiedAccess>]
module internal RuntimeAdapter =
    /// <summary>Projects a registry into the requested contract using the supplied adapter.</summary>
    let provide
        (adapter: Registry -> 'contract)
        (registry: Registry)
        : 'contract =
        adapter registry

    /// <summary>Runs a flow after adapting the registry into a nominal contract.</summary>
    let run
        (registry: Registry)
        (adapter: Registry -> 'contract)
        (flow: Flow<'contract, 'error, 'value>)
        : Effect<'value, 'error> =
        let env = adapter registry
        FlowInternal.invoke flow env CancellationToken.None

#endif
