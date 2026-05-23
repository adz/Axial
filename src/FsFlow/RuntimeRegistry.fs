namespace FsFlow

#if !FABLE_COMPILER

open System
open System.Collections.Generic

/// <summary>Identifies a runtime service by CLR type and optional tag.</summary>
type internal ServiceKey =
    {
        Type: Type
        Tag: string option
    }

/// <summary>Internal runtime service registry used by the adapter and layer machinery.</summary>
/// <remarks>
/// The registry is an implementation detail. User code should see nominal contracts, not this store.
/// </remarks>
type internal Registry =
    {
        Services: Dictionary<ServiceKey, obj>
        Scope: Scope
    }

/// <summary>Registry helpers for inserting, reading, and replacing runtime services.</summary>
[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
[<RequireQualifiedAccess>]
module internal Registry =
    let empty (scope: Scope) : Registry =
        {
            Services = Dictionary()
            Scope = scope
        }

    let private createKey<'T> (tag: string option) =
        {
            Type = typeof<'T>
            Tag = tag
        }

    let private cloneServices (services: Dictionary<ServiceKey, obj>) =
        Dictionary<ServiceKey, obj>(services)

    let add<'T>
        (tag: string option)
        (service: 'T)
        (registry: Registry)
        : Registry =
        let key = createKey<'T> tag
        let services = cloneServices registry.Services
        services[key] <- box service

        {
            registry with
                Services = services
        }

    let replace<'T>
        (tag: string option)
        (service: 'T)
        (registry: Registry)
        : Registry =
        add tag service registry

    let tryGet<'T>
        (tag: string option)
        (registry: Registry)
        : 'T option =
        let key = createKey<'T> tag
        match registry.Services.TryGetValue key with
        | true, value -> Some(unbox<'T> value)
        | false, _ -> None

    let get<'T>
        (tag: string option)
        (registry: Registry)
        : 'T =
        match tryGet<'T> tag registry with
        | Some value -> value
        | None ->
            let tagText =
                match tag with
                | None -> "un-tagged"
                | Some value -> $"tag '{value}'"

            raise (
                KeyNotFoundException(
                    $"Service of type '{typeof<'T>.FullName}' with {tagText} was not found in the runtime registry."))

#endif
