// axial-allow-file-AXG001: explicit process-environment service implementation
namespace Axial.PlatformService

open System
open System.Collections
open System.Collections.Generic
open Axial

/// Runtime-specific implementations of the otherwise portable services.
module internal Platform =
    let private dictionary (values: seq<string * string>) =
        #if FABLE_COMPILER
        let lookup = Dictionary<string, string>()
        for name, value in values do lookup[name.ToLowerInvariant()] <- value
        lookup
        #else
        let lookup = Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        for name, value in values do lookup[name] <- value
        lookup
        #endif

    let environmentVariablesFromPairs values : IEnvironmentVariables =
        let lookup = dictionary values
        let key (name: string) =
            #if FABLE_COMPILER
            name.ToLowerInvariant()
            #else
            name
            #endif

        { new IEnvironmentVariables with
            member _.TryGet name =
                match lookup.TryGetValue(key name) with
                | true, value -> Some value
                | false, _ -> None
            member _.Set(name, value) =
                match value with
                | Some value -> lookup[key name] <- value
                | None -> lookup.Remove(key name) |> ignore
            member _.Expand text =
                lookup |> Seq.fold (fun (result: string) pair -> result.Replace("%" + pair.Key + "%", pair.Value)) text
            member _.GetAll() = lookup :> IReadOnlyDictionary<string, string> }

    let environmentVariables () : IEnvironmentVariables =
        #if FABLE_COMPILER
        // Browser JavaScript has no process environment. Hosts that do (for example Node) inject an implementation.
        environmentVariablesFromPairs Seq.empty
        #else
        { new IEnvironmentVariables with
            member _.TryGet name = Option.ofObj (Environment.GetEnvironmentVariable name)
            member _.Set(name, value) = Environment.SetEnvironmentVariable(name, Option.toObj value)
            member _.Expand text = Environment.ExpandEnvironmentVariables text
            member _.GetAll() =
                let values = Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                for entry in Environment.GetEnvironmentVariables() do
                    let entry = entry :?> DictionaryEntry
                    values[string entry.Key] <- string entry.Value
                values :> IReadOnlyDictionary<string, string> }
        #endif


