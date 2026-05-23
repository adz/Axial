namespace FsFlow

#if !FABLE_COMPILER

open System
open System.Threading
open System.Threading.Tasks

/// <summary>
/// Represents a runtime provisioning step that consumes a registry and produces a value or contract.
/// </summary>
/// <remarks>
/// Layers are the composition unit for resource acquisition, tagging, and local overrides.
/// </remarks>
type internal Layer<'error, 'output> =
    Registry -> CancellationToken -> Effect<'output, 'error>

/// <summary>Layer helpers for composition and registry-driven provisioning.</summary>
[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
[<RequireQualifiedAccess>]
module internal RuntimeLayer =
    let local
        (mapping: Registry -> Registry)
        (layer: Layer<'error, 'output>)
        : Layer<'error, 'output> =
        fun registry cancellationToken ->
            layer (mapping registry) cancellationToken

    let run
        (layer: Layer<'error, 'output>)
        (registry: Registry)
        (cancellationToken: CancellationToken)
        : Effect<'output, 'error> =
        #if FABLE_COMPILER
        async {
            try
                return! layer registry cancellationToken
            finally
                (registry.Scope :> IDisposable).Dispose()
        }
        #else
        ValueTask<Exit<'output, 'error>>(
            task {
                try
                    let! exit = layer registry cancellationToken
                    return exit
                finally
                    (registry.Scope :> IDisposable).Dispose()
            })
        #endif

    let map
        (mapper: 'output -> 'next)
        (layer: Layer<'error, 'output>)
        : Layer<'error, 'next> =
        fun registry cancellationToken ->
            layer registry cancellationToken
            |> EffectFlow.map mapper

    let bind
        (binder: 'output -> Layer<'error, 'next>)
        (layer: Layer<'error, 'output>)
        : Layer<'error, 'next> =
        fun registry cancellationToken ->
            #if FABLE_COMPILER
            async {
                let! exit = layer registry cancellationToken

                match exit with
                | Exit.Success value ->
                    return! binder value registry cancellationToken
                | Exit.Failure cause ->
                    return Exit.Failure cause
            }
            #else
            ValueTask<Exit<'next, 'error>>(
                task {
                    let! exit = layer registry cancellationToken

                    match exit with
                    | Exit.Success value ->
                        let! nextExit = binder value registry cancellationToken
                        return nextExit
                    | Exit.Failure cause ->
                        return Exit.Failure cause
                })
            #endif

    let provide
        (layer: Layer<'error, 'output>)
        (flow: 'output -> 'workflow)
        : Layer<'error, 'workflow> =
        fun registry cancellationToken ->
            #if FABLE_COMPILER
            async {
                let! exit = layer registry cancellationToken
                match exit with
                | Exit.Success value -> return Exit.Success (flow value)
                | Exit.Failure cause -> return Exit.Failure cause
            }
            #else
            ValueTask<Exit<'workflow, 'error>>(
                task {
                    let! exit = layer registry cancellationToken
                    match exit with
                    | Exit.Success value -> return Exit.Success (flow value)
                    | Exit.Failure cause -> return Exit.Failure cause
                })
            #endif

#endif
