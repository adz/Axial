namespace Axial.Guardrails.Tests

open System
open System.IO
open Axial.Tool.PolicyLock
open Swensen.Unquote
open Xunit

module PolicyLockTests =
    let private empty =
        { Hashes=Map.empty; Baseline=[]; BaselineRoles=Map.empty; Tests=[]; Rules=Map.empty
          Capabilities=Set.empty; Boundaries=Set.empty; Suppressions=Set.empty }

    [<Fact>]
    let ``policy summary reports every reviewed change category`` () =
        let old =
            { empty with Hashes=Map ["Directory.Build.props","old"]
                         Rules=Map ["domain/effects.ambient","deny"]
                         Tests=[{Project="tests/App.Tests/App.Tests.fsproj";Tests=3;Assertions=4}] }
        let current =
            { empty with Hashes=Map ["Directory.Build.props","new"]
                         Rules=Map ["domain/effects.ambient","warn"]
                         Baseline=[{Rule="effects.ambient";File="src/App.fs";Fingerprint="abc";Count=1}]
                         Tests=[{Project="tests/App.Tests/App.Tests.fsproj";Tests=2;Assertions=3}]
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
        test <@ changes.TestsRemoved.Length=1 @>
        test <@ summary.Contains("AXIAL GUARDRAILS POLICY CHANGED — BUILD BLOCKED") @>

    [<Fact>]
    let ``ordinary approval refuses baseline growth`` () =
        let current={empty with Baseline=[{Rule="effects.ambient";File="src/App.fs";Fingerprint="abc";Count=1}]}
        let changes=diff empty current
        test <@ validateApproval Normal empty current changes = Error "baseline growth refused; use 'axial policy baseline --rule <id>' only for a newly enabled rule" @>

    [<Fact>]
    let ``baseline approval accepts only the selected newly enabled rule`` () =
        let current={empty with Rules=Map ["domain/effects.ambient","deny"]; Baseline=[{Rule="effects.ambient";File="src/App.fs";Fingerprint="abc";Count=1}]; BaselineRoles=Map ["src/App.fs","domain"]}
        let changes=diff empty current
        test <@ validateApproval (BaselineRule "effects.ambient") empty current changes = Ok () @>
        let established={empty with Rules=current.Rules}
        test <@ validateApproval (BaselineRule "effects.ambient") established current changes = Error "baseline growth refused: rule 'effects.ambient' was not newly enabled for every affected file's effective role" @>
        let allowed={empty with Rules=Map ["host/effects.ambient","allow"]}
        test <@ validateApproval (BaselineRule "effects.ambient") empty allowed (diff empty {allowed with Baseline=current.Baseline}) |> Result.isError @>
        let secondRole={established with Rules=established.Rules.Add("host/effects.ambient","warn");Baseline=current.Baseline;BaselineRoles=Map ["src/App.fs","host"]}
        test <@ validateApproval (BaselineRule "effects.ambient") established secondRole (diff established secondRole) = Ok () @>
        let testOnly={current with Rules=Map ["test/effects.ambient","deny"];BaselineRoles=Map ["src/App.fs","domain"]}
        test <@ validateApproval (BaselineRule "effects.ambient") empty testOnly (diff empty testOnly) |> Result.isError @>

    [<Fact>]
    let ``test decreases need their dedicated acceptance`` () =
        let old={empty with Tests=[{Project="tests/App.Tests/App.Tests.fsproj";Tests=3;Assertions=4}]}
        let current={empty with Tests=[{Project="tests/App.Tests/App.Tests.fsproj";Tests=2;Assertions=3}]}
        let changes=diff old current
        test <@ Result.isError(validateApproval Normal old current changes) @>
        test <@ validateApproval AcceptRemovedTests old current changes = Ok () @>

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
    let ``test ratchet ignores trivia strings and inactive code and counts assertion aliases`` () =
        let source="""// [<Fact>] Assert.True(false)
let text = "[<Fact>] Assert.True(false)"
#if false
[<Fact>]
let dead () = Assert.True(false)
#endif
let verify = Assert.True
let neverCalled () = verify(true)
[<Fact>]
let live () = Assert.True(true)
"""
        test <@ countTestSource source = (1,1) @>

    [<Fact>]
    let ``strict lock parser requires one first version and unique exact records`` () =
        let path=Path.GetTempFileName()
        try
            File.WriteAllText(path,"SHA256  bad  path\n")
            let missing=parseLock path
            test <@ match missing with Error e -> e.Contains(":1:") && e.Contains("first record") | _ -> false @>
            File.WriteAllText(path,"version = 1\nversion = 1\n")
            test <@ match parseLock path with Error e -> e.Contains(":2:") && e.Contains("duplicate") | _ -> false @>
            File.WriteAllText(path,"version = 1\nTEST  p\t1\t2\nTEST  p\t1\t2\n")
            test <@ match parseLock path with Error e -> e.Contains("duplicate TEST") | _ -> false @>
            for malformed in ["SHA256  "+String.replicate 64 "a"+"  "; "BASELINE  \tfile\tfingerprint\t1"; "TEST  \t1\t2"; "POLICY  \tdeny"; "CAPABILITY  "; "BOUNDARY  "; "SUPPRESSION  "] do
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
