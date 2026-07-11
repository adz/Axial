namespace Axial.Flow.PlatformService

open System
open System.Collections
open System.Collections.Generic
open System.Threading.Tasks
open Axial.Flow

/// Runtime-specific implementations for the otherwise portable service surface.
module internal Platform =
    let random () : IRandom =
        let rng = Random()

        #if FABLE_COMPILER
        { new IRandom with
            member _.Next() = rng.Next()
            member _.NextMax maxExclusive = rng.Next maxExclusive
            member _.NextInt minInclusive maxExclusive = rng.Next(minInclusive, maxExclusive)
            member _.NextDouble() = rng.NextDouble()
            member _.NextBytes buffer =
                for index in 0 .. buffer.Length - 1 do
                    buffer[index] <- byte (rng.Next(0, 256)) }
        #else
        let gate = obj()

        { new IRandom with
            member _.Next() = lock gate rng.Next
            member _.NextMax maxExclusive = lock gate (fun () -> rng.Next maxExclusive)
            member _.NextInt minInclusive maxExclusive = lock gate (fun () -> rng.Next(minInclusive, maxExclusive))
            member _.NextDouble() = lock gate rng.NextDouble
            member _.NextBytes buffer = lock gate (fun () -> rng.NextBytes buffer) }
        #endif

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

    #if !FABLE_COMPILER
    let private tryService<'service> (provider: IServiceProvider) =
        match provider.GetService(typeof<'service>) with
        | null -> Error typeof<'service>.Name
        | service -> Ok (unbox<'service> service)
    #endif

    let servicesFromServiceProvider : Layer<IServiceProvider, BaseRuntimeError, IClock * ILog * IRandom * IGuid * IEnvironmentVariables> =
        #if FABLE_COMPILER
        Layer.fromAsync (fun _ _ ->
            async.Return(Exit.Failure(Cause.Die(PlatformNotSupportedException("IServiceProvider layers are not supported on Fable.")))))
        #else
        Layer.fromValueTask (fun (provider, _) _ ->
            ValueTask<Exit<IClock * ILog * IRandom * IGuid * IEnvironmentVariables, BaseRuntimeError>>(
                task {
                    match tryService<IClock> provider, tryService<ILog> provider, tryService<IRandom> provider,
                          tryService<IGuid> provider, tryService<IEnvironmentVariables> provider with
                    | Ok clock, Ok log, Ok random, Ok guid, Ok environmentVariables ->
                        return Exit.Success(clock, log, random, guid, environmentVariables)
                    | Error name, _, _, _, _ | _, Error name, _, _, _ | _, _, Error name, _, _
                    | _, _, _, Error name, _ | _, _, _, _, Error name ->
                        return Exit.Failure(Cause.Fail(BaseRuntimeError.MissingService name))
                }))
        #endif
