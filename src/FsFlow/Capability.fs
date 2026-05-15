namespace FsFlow

open System
open System.Threading
open System.Threading.Tasks

[<AutoOpen>]
module ResolverExtensions =
    type Flow<'env, 'error, 'value> with
#if FABLE_COMPILER
        static member inline ResolveService
            (projection: 'env -> 'resolve)
            : Flow<'env, 'error, 'resolve> =
            Flow(fun environment _ -> EffectFlow.ofValue (projection environment))

        /// <summary>Reads a dependency from <see cref="IServiceProvider" /> and fails when it is not registered.</summary>
        static member inline ResolveFromProvider
            ()
            : Flow<IServiceProvider, MissingCapability, 'resolve> =
            Flow(fun provider _ ->
                match provider.GetService typeof<'resolve> with
                | null ->
                    EffectFlow.ofError
                        {
                            CapabilityType = typeof<'resolve>
                        }
                | value -> EffectFlow.ofValue (unbox<'resolve> value))
#else
        static member ResolveService
            (projection: 'env -> 'resolve)
            : Flow<'env, 'error, 'resolve> =
            Flow(fun environment _ -> EffectFlow.ofValue (projection environment))

        /// <summary>Reads a dependency from <see cref="IServiceProvider" /> and fails when it is not registered.</summary>
        static member ResolveFromProvider
            ()
            : Flow<IServiceProvider, MissingCapability, 'resolve> =
            Flow(fun provider _ ->
                match provider.GetService typeof<'resolve> with
                | null ->
                    EffectFlow.ofError
                        {
                            CapabilityType = typeof<'resolve>
                        }
                | value -> EffectFlow.ofValue (unbox<'resolve> value))
#endif

        static member ProvideLayer
            (
                layer: Flow<'input, 'error, 'environment>,
                flow: Flow<'environment, 'error, 'value>
            ) : Flow<'input, 'error, 'value> =
            let (Flow layerOperation) = layer
            let (Flow flowOperation) = flow

            Flow(fun environment ct ->
                #if FABLE_COMPILER
                async {
                    let! exit = layerOperation environment ct
                    match exit with
                    | Exit.Success environment -> return! flowOperation environment ct
                    | Exit.Failure cause -> return Exit.Failure cause
                }
                #else
                match (layerOperation environment ct).GetAwaiter().GetResult() with
                | Exit.Success environment -> flowOperation environment ct
                | Exit.Failure cause -> EffectFlow.ofCause cause
                #endif
            )

/// <summary>
/// Helpers for working with capability contracts, runtime/application readers, and layer helpers.
/// These are compatibility and edge-binding helpers; nominal contracts and adapter-generated
/// records are the preferred public model.
/// </summary>
[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
[<RequireQualifiedAccess>]
module Resolver =
    /// <summary>Reads a dependency from the environment using the provided projection.</summary>
    /// <remarks>Use this as a compatibility helper or at the boundary where a projection is still the cleanest shape.</remarks>
    let resolve
        (projection: 'env -> 'resolve)
        : Flow<HostContext<'host, 'appEnv>, 'error, 'resolve> =
        Flow.readAppEnv projection

    /// <summary>Reads the current host from the environment.</summary>
    /// <remarks>Compatibility alias for the host half of <see cref="HostContext{host, appEnv}" />.</remarks>
    let host<'host, 'appEnv, 'error> () : Flow<HostContext<'host, 'appEnv>, 'error, 'host> =
        Flow.readHost id

    /// <summary>Reads the application environment from the environment.</summary>
    /// <remarks>Compatibility alias for the app environment half of <see cref="HostContext{host, appEnv}" />.</remarks>
    let appEnv<'host, 'appEnv, 'error> () : Flow<HostContext<'host, 'appEnv>, 'error, 'appEnv> =
        Flow.readAppEnv id

    /// <summary>Reads a dependency from <see cref="IServiceProvider" /> and fails when it is not registered.</summary>
    /// <remarks>Edge helper only. Prefer a record or nominal contract once the host boundary has been crossed.</remarks>
    #if FABLE_COMPILER
    let inline fromProvider<'resolve> : Flow<IServiceProvider, MissingCapability, 'resolve> =
        Flow.ResolveFromProvider()
    #else
    let fromProvider<'resolve> : Flow<IServiceProvider, MissingCapability, 'resolve> =
        Flow.ResolveFromProvider()
    #endif

/// <summary>
/// Helpers for deriving an environment in one flow and consuming it in another.
/// The layer surface is a boundary primitive, not the main public dependency model.
/// </summary>
[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
[<RequireQualifiedAccess>]
module Layer =
    /// <summary>Provides a derived environment from a layer flow to a downstream flow.</summary>
    /// <remarks>Boundary primitive for composition roots, not the default workflow shape.</remarks>
    let provideLayer
        (layer: Flow<'input, 'error, 'environment>)
        (flow: Flow<'environment, 'error, 'value>)
        : Flow<'input, 'error, 'value> =
        Flow.ProvideLayer(layer, flow)
