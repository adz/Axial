namespace Axial.Guardrails.Tests

open System
open System.IO
open Axial.Tool.PolicyLock
open Swensen.Unquote
open Xunit

module PolicyLockTests =
    let private empty =
        { Hashes=Map.empty; Baseline=[]; BaselineRoles=Map.empty; Rules=Map.empty
          Capabilities=Set.empty; Boundaries=Set.empty; Suppressions=Set.empty }

    [<Fact>]
    let ``policy summary reports every reviewed change category`` () =
        let old =
            { empty with Hashes=Map ["Directory.Build.props","old"]
                         Rules=Map ["domain/effects.ambient","deny"] }
        let current =
            { empty with Hashes=Map ["Directory.Build.props","new"]
                         Rules=Map ["domain/effects.ambient","warn"]
                         Baseline=[{Rule="effects.ambient";File="src/App.fs";Fingerprint="abc";Count=1}]
                         Capabilities=set ["src/Capabilities/**"]
                         Boundaries=set ["vendor.props"]
                         Suppressions=set ["src/App.fs:2:// axial-allow-AXG001: reason"] }
        let changes=diff old current
        let summary=formatSummary changes
        test <@ changes.RulesDowngraded.Length=1 @>
        test <@ changes.BaselineAdded.Length=1 @>
        test <@ changes.SuppressionsAdded.Length=1 @>
        test <@ changes.BoundariesAdded.Length=2 @>
        test <@ changes.ProtectedChanged=["Directory.Build.props";LockName] @>
        test <@ summary.Contains("AXIAL GUARDRAILS POLICY CHANGED — BUILD BLOCKED") @>

    [<Fact>]
    let ``ordinary approval refuses baseline growth`` () =
        let current={empty with Baseline=[{Rule="effects.ambient";File="src/App.fs";Fingerprint="abc";Count=1}]}
        let changes=diff empty current
        test <@ validateApproval Normal empty current changes = Error "baseline growth refused; use 'axial policy baseline --rule <id>' only for a newly enabled rule" @>

    [<Fact>]
    let ``baseline approval accepts only the selected newly enabled rule`` () =
        let current={empty with Rules=Map ["domain/effects.ambient","deny"]; Baseline=[{Rule="effects.ambient";File="src/App.fs";Fingerprint="abc";Count=1}]; BaselineRoles=Map ["src/App.fs",set ["domain"]]}
        let changes=diff empty current
        test <@ validateApproval (BaselineRule "effects.ambient") empty current changes = Ok () @>
        let established={empty with Rules=current.Rules}
        test <@ validateApproval (BaselineRule "effects.ambient") established current changes = Error "baseline growth refused: rule 'effects.ambient' was not newly enabled for every affected file's effective role" @>
        let allowed={empty with Rules=Map ["host/effects.ambient","allow"]}
        test <@ validateApproval (BaselineRule "effects.ambient") empty allowed (diff empty {allowed with Baseline=current.Baseline}) |> Result.isError @>
        let secondRole={established with Rules=established.Rules.Add("host/effects.ambient","warn");Baseline=current.Baseline;BaselineRoles=Map ["src/App.fs",set ["host"]]}
        test <@ validateApproval (BaselineRule "effects.ambient") established secondRole (diff established secondRole) = Ok () @>
        let testOnly={current with Rules=Map ["test/effects.ambient","deny"];BaselineRoles=Map ["src/App.fs",set ["domain"]]}
        test <@ validateApproval (BaselineRule "effects.ambient") empty testOnly (diff empty testOnly) |> Result.isError @>

    [<Fact>]
    let ``protected hashes include analyzer configs and Guardrails test build inputs`` () =
        let root=Path.Combine(Path.GetTempPath(),"Axial.Lock.Tests",Guid.NewGuid().ToString("N"))
        try
            Directory.CreateDirectory(Path.Combine(root,"tests","Axial.Guardrails.Tests")) |> ignore
            Directory.CreateDirectory(Path.Combine(root,"build")) |> ignore
            File.WriteAllText(Path.Combine(root,ConfigName),"version = 2\nbaseline = \"Axial.Guardrails.baseline\"\n[default]\nrole = \"domain\"")
            File.WriteAllText(Path.Combine(root,DefaultBaseline),"")
            File.WriteAllText(Path.Combine(root,"Directory.Build.props"),"<Project />")
            File.WriteAllText(Path.Combine(root,"Directory.Build.targets"),"<Project />")
            File.WriteAllText(Path.Combine(root,"nested.globalconfig"),"is_global = true")
            File.WriteAllText(Path.Combine(root,"build","Imported.targets"),"<Project />")
            File.WriteAllText(Path.Combine(root,"tests","Axial.Guardrails.Tests","Tests.fs"),"module Tests")
            File.WriteAllText(Path.Combine(root,"tests","Axial.Guardrails.Tests","Axial.Guardrails.Tests.fsproj"),"<Project />")
            let snapshot=inspect root |> Result.toOption |> Option.get
            for path in ["nested.globalconfig";"build/Imported.targets";"tests/Axial.Guardrails.Tests/Tests.fs"] do
                test <@ snapshot.Hashes.ContainsKey path @>
        finally Directory.Delete(root,true)

    [<Fact>]
    let ``baseline roles use the project assignment before file overrides`` () =
        let root=Path.Combine(Path.GetTempPath(),"Axial.Lock.Tests",Guid.NewGuid().ToString("N"))
        try
            Directory.CreateDirectory(Path.Combine(root,"src","Domain")) |> ignore
            File.WriteAllText(Path.Combine(root,ConfigName),"""version = 2
baseline = "Axial.Guardrails.baseline"
[default]
role = "test"
[projects]
domain = ["src/Domain/*.fsproj"]
[roles.domain.rules]
"effects.ambient" = "deny"
            """)
            File.WriteAllText(Path.Combine(root,DefaultBaseline),"effects.ambient\tsrc/Domain/App.fs\tabc\n")
            File.WriteAllText(Path.Combine(root,"src","Domain","App.fs"),"module App")
            File.WriteAllText(Path.Combine(root,"src","Domain","App.fsproj"),"<Project Sdk=\"Microsoft.NET.Sdk\"><ItemGroup><Compile Include=\"App.fs\" /></ItemGroup></Project>")
            let snapshot=inspect root |> function Ok value -> value | Error error -> failwith error
            test <@ snapshot.BaselineRoles["src/Domain/App.fs"] = set ["domain"] @>
            let changes=diff empty snapshot
            test <@ validateApproval (BaselineRule "effects.ambient") empty snapshot changes = Ok () @>
            let established={empty with Rules=Map ["domain/effects.ambient","deny"]}
            test <@ validateApproval (BaselineRule "effects.ambient") established snapshot changes |> Result.isError @>
        finally Directory.Delete(root,true)

    [<Fact>]
    let ``baseline growth must be authorized for every project role of a shared file`` () =
        let root=Path.Combine(Path.GetTempPath(),"Axial.Lock.Tests",Guid.NewGuid().ToString("N"))
        try
            Directory.CreateDirectory(Path.Combine(root,"src","A")) |> ignore
            Directory.CreateDirectory(Path.Combine(root,"src","Z")) |> ignore
            File.WriteAllText(Path.Combine(root,ConfigName),"""version = 2
baseline = "Axial.Guardrails.baseline"
[default]
role = "domain"
[projects]
host = ["src/Z/*.fsproj"]
[roles.host.rules]
"effects.ambient" = "deny"
            """)
            File.WriteAllText(Path.Combine(root,DefaultBaseline),"effects.ambient\tsrc/Shared.fs\tabc\n")
            File.WriteAllText(Path.Combine(root,"src","Shared.fs"),"module Shared")
            let project="<Project Sdk=\"Microsoft.NET.Sdk\"><ItemGroup><Compile Include=\"../Shared.fs\" /></ItemGroup></Project>"
            File.WriteAllText(Path.Combine(root,"src","A","App.fsproj"),project)
            File.WriteAllText(Path.Combine(root,"src","Z","App.fsproj"),project)
            let snapshot=inspect root |> function Ok value -> value | Error error -> failwith error
            let roleRecords = serialize snapshot |> fun text -> Text.RegularExpressions.Regex.Matches(text,"(?m)^BASELINE_ROLE  src/Shared\\.fs\\t").Count
            test <@ roleRecords = 2 @>
            let changes=diff empty snapshot
            test <@ validateApproval (BaselineRule "effects.ambient") empty snapshot changes |> Result.isError @>
        finally Directory.Delete(root,true)

    [<Fact>]
    let ``strict lock parser requires one first version and unique exact records`` () =
        let path=Path.GetTempFileName()
        try
            File.WriteAllText(path,"SHA256  bad  path\n")
            let missing=parseLock path
            test <@ match missing with Error e -> e.Contains(":1:") && e.Contains("first record") | _ -> false @>
            File.WriteAllText(path,"version = 1\nversion = 1\n")
            test <@ match parseLock path with Error e -> e.Contains(":2:") && e.Contains("duplicate") | _ -> false @>
            File.WriteAllText(path,"version = 1\nBASELINE_ROLE  src/App.fs\tdomain\nBASELINE_ROLE  src/App.fs\tdomain\n")
            test <@ match parseLock path with Error e -> e.Contains("duplicate BASELINE_ROLE") | _ -> false @>
            for malformed in ["SHA256  "+String.replicate 64 "a"+"  "; "BASELINE  \tfile\tfingerprint\t1"; "POLICY  \tdeny"; "CAPABILITY  "; "BOUNDARY  "; "SUPPRESSION  "] do
                File.WriteAllText(path,"version = 1\n"+malformed+"\n")
                test <@ parseLock path |> Result.isError @>
        finally File.Delete path

    [<Fact>]
    let ``lock serialization is deterministic and sorted`` () =
        let snapshot={empty with Hashes=Map ["z",String.replicate 64 "2";"a",String.replicate 64 "1"]; Capabilities=set ["z/**";"a/**"]; Boundaries=set ["src/Host.fs"]; Suppressions=set ["src/Host.fs:1:// axial-allow-AXG001: host boundary"]}
        let first=serialize snapshot
        let second=serialize snapshot
        test <@ first=second @>
        test <@ first.IndexOf("  a") < first.IndexOf("  z") @>
        test <@ first.IndexOf("CAPABILITY  a/**") < first.IndexOf("CAPABILITY  z/**") @>
        let path=Path.GetTempFileName()
        try
            File.WriteAllText(path,first)
            test <@ parseLock path = Ok snapshot @>
        finally File.Delete path
