namespace Axial.Tests

open System
open System.IO
open System.Reflection
open System.Text.RegularExpressions
open Microsoft.FSharp.Core
open Swensen.Unquote
open Xunit

/// Keeps the torture scenarios (examples/Axial.TortureTest) covering the runtime and concurrency API: every public
/// member of the modules below must be used by some scenario, or be listed in `excluded` with the reason.
module TortureCoverageTests =
    /// Modules whose every public member the torture scenarios must exercise. App hosting and Telemetry are left out:
    /// they format and route data, and do not schedule, share, or coordinate anything.
    let private covered () =
        [ "Axial.Flow"; "Axial.FiberModule"; "Axial.FiberSlotModule"; "Axial.DeferredModule"; "Axial.SemaphoreModule"
          "Axial.State.RefModule"; "Axial.State.TRefModule"; "Axial.State.STM"; "Axial.State.SubscriptionRefModule"
          "Axial.QueueModule"; "Axial.DequeueModule"; "Axial.HubModule"; "Axial.PublishResultModule"
          "Axial.CacheModule"; "Axial.FlowStreamModule"; "Axial.ScheduleModule"; "Axial.RetryModule"
          "Axial.ParallelismModule"; "Axial.ResourceModule"; "Axial.Exit"; "Axial.Cause"; "Axial.PolicyModule"
          "Axial.FiberObserverModule"; "Axial.FiberDumpModule"; "Axial.BindModule"; "Axial.ColdTaskModule"
          "Axial.Layers.LayerModule" ]

    /// Members deliberately left out, each with the reason.
    let private excluded () : Map<string, string> = Map.empty

    let private sourceText () =
        let directory = Path.Combine(__SOURCE_DIRECTORY__, "..", "..", "examples", "Axial.TortureTest")
        Directory.GetFiles(directory, "*.fs") |> Array.map File.ReadAllText |> String.concat "\n"

    let private sourceModuleName (moduleType: Type) =
        let suffixed =
            moduleType.GetCustomAttributes(typeof<CompilationRepresentationAttribute>, false)
            |> Array.exists (fun attribute ->
                (attribute :?> CompilationRepresentationAttribute).Flags.HasFlag CompilationRepresentationFlags.ModuleSuffix)

        if suffixed && moduleType.Name.EndsWith "Module" then moduleType.Name.Substring(0, moduleType.Name.Length - 6)
        else moduleType.Name

    let private sourceMemberName (memberInfo: MemberInfo) =
        match memberInfo.GetCustomAttributes(typeof<CompilationSourceNameAttribute>, false) with
        | [| attribute |] -> (attribute :?> CompilationSourceNameAttribute).SourceName
        | _ -> memberInfo.Name

    /// Every public member of the covered modules, as the source spells it: "Queue.offer", "Flow.(>>=)".
    let private publicMembers () : string list =
        let assemblies = [ typeof<Axial.Never>.Assembly; typeof<Axial.Layers.Pool<int>>.Assembly ]

        [ for assembly in assemblies do
              for moduleType in assembly.GetExportedTypes() do
                  if List.contains (moduleType.FullName.Replace('+', '.')) (covered ()) then
                      let moduleName = sourceModuleName moduleType
                      let flags = BindingFlags.Public ||| BindingFlags.Static ||| BindingFlags.DeclaredOnly

                      let methods =
                          moduleType.GetMethods flags
                          |> Array.filter (fun methodInfo -> not methodInfo.IsSpecialName)
                          |> Array.map (fun methodInfo -> sourceMemberName methodInfo)

                      let properties = moduleType.GetProperties flags |> Array.map sourceMemberName

                      for name in Array.append methods properties |> Array.distinct do
                          // "@" marks closures and "$W" the witness-passing copies of inline members.
                          if not (name.Contains "@" || name.EndsWith "$W") then
                              if name.StartsWith "op_" || name.StartsWith "(" then
                                  yield $"{moduleName}.({name.Trim('(', ')', ' ')})"
                              else
                                  yield $"{moduleName}.{name}" ]
        |> List.distinct
        |> List.sort

    let private usedIn (source: string) (qualified: string) =
        Regex.IsMatch(source, Regex.Escape qualified + @"(?![A-Za-z0-9_])")

    [<Fact>]
    let ``The torture scenarios use every runtime and concurrency API`` () =
        let source = sourceText ()
        let members = publicMembers ()
        let excluded = excluded ()
        let missing = members |> List.filter (fun name -> not (usedIn source name) && not (excluded.ContainsKey name))
        test <@ members.Length > 200 @>
        test <@ missing = [] @>
