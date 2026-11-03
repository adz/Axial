/// Facts belong to one immutable FCS file context, never to its filename or a process-wide project snapshot.
module Axial.Guardrails.SymbolIndex

open System
open System.Collections.Concurrent
open System.Collections.Generic
open System.Runtime.CompilerServices
open FSharp.Analyzers.SDK
open FSharp.Compiler.CodeAnalysis
open FSharp.Compiler.Text

// Ephemeron keys permit values (including lazy factories) to refer back to their context without rooting it.
// No file, project or checker is retained after the host releases its contexts.
let private facts = ConditionalWeakTable<CliContext, ConcurrentDictionary<obj, Lazy<obj>>>()

let memo (ctx: CliContext) (key: obj) (create: unit -> 'a) : 'a =
    let file = facts.GetValue(ctx, fun _ -> ConcurrentDictionary())
    file.GetOrAdd(key, fun _ -> lazy (box (create ()))).Value :?> 'a

let private position (p: pos) = struct (p.Line, p.Column)

type FileSymbols(uses: FSharpSymbolUse array) =
    let endings = lazy (
        let result = Dictionary<struct (int * int), FSharpSymbolUse array>()
        uses
        |> Array.groupBy (fun site -> struct (site.Range.EndLine, site.Range.EndColumn))
        |> Array.iter (fun (key, sites) -> result.Add(key, sites))
        result)
    let starts = lazy (uses |> Array.mapi (fun ordinal site -> ordinal, site) |> Array.sortBy (fun (_, site) -> position site.Range.Start))

    member _.All = uses

    member _.EndingAt(location: range) =
        match endings.Value.TryGetValue(struct (location.EndLine, location.EndColumn)) with
        | true, sites ->
            sites |> Array.filter (fun site -> site.Range.StartLine = location.StartLine && site.Range.StartColumn <= location.StartColumn)
        | _ -> [||]

    member _.ContainedIn(location: range) =
        let sorted = starts.Value
        let mutable lo = 0
        let mutable hi = sorted.Length
        while lo < hi do
            let mid = lo + (hi - lo) / 2
            if position (snd sorted[mid]).Range.Start < position location.Start then lo <- mid + 1 else hi <- mid
        let found = ResizeArray<int * FSharpSymbolUse>()
        let mutable index = lo
        while index < sorted.Length && position (snd sorted[index]).Range.Start <= position location.End do
            let ordinal, site = sorted[index]
            if Range.rangeContainsRange location site.Range then found.Add(ordinal, site)
            index <- index + 1
        found.ToArray() |> Array.sortBy fst |> Array.map snd

let forContext (ctx: CliContext) =
    memo ctx "symbols" (fun () -> FileSymbols(ctx.GetAllSymbolUsesOfFile() |> Seq.toArray))
