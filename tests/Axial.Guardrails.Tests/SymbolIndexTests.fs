namespace Axial.Guardrails.Tests

open System
open System.Runtime.CompilerServices
open Axial.Guardrails
open FSharp.Compiler.Text
open Xunit

module SymbolIndexTests =
    [<Fact>]
    let ``range indexes preserve every symbol from linear queries`` () =
        task {
            let! ctx = AnalyzerTestContext.createWithAxial true "module M\nopen System\nopen Axial\nlet cache () = Cache.make (fun k -> Flow.ok k) |> Flow.run ()\nlet f () = try failwith \"x\" with :? exn -> ()\nlet t () = System.Threading.Tasks.Task.FromResult 1 |> ignore\n"
            let symbols = ctx.GetAllSymbolUsesOfFile() |> Seq.toArray
            let index = SymbolIndex.forContext ctx
            Assert.Same(index, SymbolIndex.forContext ctx)
            Assert.Equal(symbols.Length, index.All.Length)
            for site in symbols do
                for start in [ 0; site.Range.StartColumn ] do
                    let r = Range.mkRange ctx.FileName (Position.mkPos site.Range.StartLine start) site.Range.End
                    let expectedEnd = symbols |> Array.filter (fun s -> s.Range.End = r.End && s.Range.StartLine = r.StartLine && s.Range.StartColumn <= r.StartColumn)
                    let expectedInside = symbols |> Array.filter (fun s -> Range.rangeContainsRange r s.Range)
                    let identity (sites: FSharp.Compiler.CodeAnalysis.FSharpSymbolUse seq) =
                        sites |> Seq.map (fun s -> s.Range, s.Symbol.DisplayName, s.IsFromDefinition) |> Seq.toList
                    Assert.Equal<(range * string * bool) list>(identity expectedEnd, identity (index.EndingAt r))
                    Assert.Equal<(range * string * bool) list>(identity expectedInside, identity (index.ContainedIn r))
        }

    [<MethodImpl(MethodImplOptions.NoInlining)>]
    let private abandonedIndex () =
        let ctx = AnalyzerTestContext.create "module M\nlet f x = x + 1\n" |> _.GetAwaiter().GetResult()
        let index = SymbolIndex.forContext ctx
        // Force both indexes and a memo whose closure captures its key (ephemeron cycle).
        index.EndingAt ctx.ParseFileResults.ParseTree.Range |> Seq.length |> ignore
        SymbolIndex.memo ctx "retention-witness" (fun () -> ctx) |> ignore
        WeakReference(ctx), WeakReference(index)

    [<Fact>]
    let ``indexes and their captured contexts are collectible`` () =
        let references = [ for _ in 1 .. 12 -> abandonedIndex () ]
        for _ in 1 .. 3 do
            GC.Collect(2, GCCollectionMode.Forced, true, true)
            GC.WaitForPendingFinalizers()
        Assert.All(references, fun (ctx, index) ->
            Assert.False(ctx.IsAlive, "symbol cache retained its file context")
            Assert.False(index.IsAlive, "symbol cache retained its index"))
