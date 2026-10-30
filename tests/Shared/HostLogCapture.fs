namespace Axial.Tests

open System
open System.Collections.Concurrent
open System.Collections.Generic
open Microsoft.Extensions.Logging

type HostLogRecord =
    { Category: string
      Level: LogLevel
      Message: string
      Error: exn option
      Scope: Map<string, obj> }

type HostLogProvider() =
    let entries = ConcurrentQueue<HostLogRecord>()
    let mutable scopes: IExternalScopeProvider = LoggerExternalScopeProvider()
    member _.Entries = entries.ToArray()
    interface ISupportExternalScope with
        member _.SetScopeProvider value = scopes <- value
    interface ILoggerProvider with
        member _.Dispose() = ()
        member _.CreateLogger category =
            { new ILogger with
                member _.IsEnabled level = level >= LogLevel.Information
                member _.BeginScope state = scopes.Push state
                member _.Log(level, _, state, error, formatter) =
                    let values = Dictionary<string, obj>()
                    scopes.ForEachScope(Action<obj, unit>(fun scope _ ->
                        match scope with
                        | :? IEnumerable<KeyValuePair<string, obj>> as pairs ->
                            for pair in pairs do values[pair.Key] <- pair.Value
                        | _ -> ()), ())
                    entries.Enqueue { Category = category; Level = level; Message = formatter.Invoke(state, error)
                                      Error = Option.ofObj error; Scope = values |> Seq.map (fun pair -> pair.Key, pair.Value) |> Map.ofSeq } }
