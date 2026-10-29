namespace Axial.Guardrails.Tests

open System
open System.IO
open Axial.Tool.PolicyLock
open Swensen.Unquote
open Xunit

module PolicyLockTests =
    let private empty =
        { Version=2; BaselinePath=DefaultBaseline; Hashes=Map.empty; Baseline=[]; BaselineRoles=Map.empty; Rules=Map.empty
          Capabilities=Set.empty; Boundaries=Set.empty; Suppressions=Set.empty }

    let private withSuppressionRepository action =
        let root=Path.Combine(Path.GetTempPath(),"Axial.Lock.Tests",Guid.NewGuid().ToString("N"))
        try
            Directory.CreateDirectory(Path.Combine(root,"src")) |> ignore
            File.WriteAllText(Path.Combine(root,ConfigName),"version = 2\nbaseline = \"Axial.Guardrails.baseline\"\n[default]\nrole = \"domain\"")
            File.WriteAllText(Path.Combine(root,DefaultBaseline),"")
            let write name (text: string) = File.WriteAllText(Path.Combine(root,"src",name),text)
            let inspectCurrent () = inspect root |> function Ok value -> value | Error error -> failwith error
            let approved () =
                let path=Path.Combine(root,LockName)
                File.WriteAllText(path,serialize (inspectCurrent ()))
                parseLock path |> function Ok value -> value | Error error -> failwith error
            action root write inspectCurrent approved
        finally Directory.Delete(root,true)

    [<Theory>]
    [<InlineData("module App\n// axial-allow-AXG001: live boundary\n")>]
    [<InlineData("module App\n\n\n\n// axial-allow-AXG001: live boundary\n")>]
    let ``suppression line movement does not require approval`` source =
        withSuppressionRepository (fun _ write current approved ->
            write "App.fs" "module App\n\n// axial-allow-AXG001: live boundary\n"
            let old=approved ()
            write "App.fs" source
            test <@ diff old (current ()) |> isEmpty @>)

    [<Theory>]
    [<InlineData("tests/Axial.Guardrails.Tests")>]
    [<InlineData("tests/Axial.Guardrails.Consumer")>]
    [<InlineData("tests/Axial.PrintfFixture")>]
    [<InlineData("benchmarks/Axial.Comparative")>]
    let ``fixture suppression text in skipped projects does not require approval`` directory =
        withSuppressionRepository (fun root write current approved ->
            let fixtureDirectory=Path.Combine(root,directory)
            Directory.CreateDirectory(fixtureDirectory) |> ignore
            let fixture=Path.Combine(fixtureDirectory,"Fixture.fs")
            File.WriteAllText(fixture,"module Fixture\n")
            File.AppendAllText(Path.Combine(root,ConfigName),"\n[suppressions]\nignore = [\"tests/Axial.Guardrails*/**\", \"tests/Axial.PrintfFixture/**\", \"benchmarks/Axial.Comparative/**\"]\n")
            let old=approved ()
            let text="let fixture = \"// axial-allow-AXG001: x\"\n"
            File.AppendAllText(fixture,text)
            test <@ diff old (current ()) |> isEmpty @>
            write "App.fs" text
            let changes=diff old (current ())
            test <@ changes.SuppressionsAdded.Length=1 @>
            test <@ not (isEmpty changes) @>)

    [<Fact>]
    let ``fixture text without configured ignore is a suppression`` () =
        withSuppressionRepository (fun root _ current approved ->
            let directory=Path.Combine(root,"tests/Axial.Guardrails.Tests")
            Directory.CreateDirectory(directory) |> ignore
            let old=approved ()
            File.WriteAllText(Path.Combine(directory,"Fixture.fs"),"let fixture = \"// axial-allow-AXG001: x\"\n")
            test <@ (diff old (current ())).SuppressionsAdded.Length=1 @>)

    [<Fact>]
    let ``new suppression requires approval`` () =
        withSuppressionRepository (fun _ write current approved ->
            write "App.fs" "module App\n"
            let old=approved ()
            write "App.fs" "module App\n// axial-allow-AXG001: live boundary\n"
            let changes=diff old (current ())
            test <@ changes.SuppressionsAdded.Length=1 @>
            test <@ not (isEmpty changes) @>)

    [<Fact>]
    let ``second identical suppression counts as an addition without counting line movement`` () =
        withSuppressionRepository (fun _ write current approved ->
            write "App.fs" "module App\n// axial-allow-AXG001: live boundary\n"
            let old=approved ()
            write "App.fs" "module App\n\n// axial-allow-AXG001: live boundary\n\n// axial-allow-AXG001: live boundary\n"
            let changes=diff old (current ())
            test <@ changes.SuppressionsAdded.Length=1 @>
            test <@ not (isEmpty changes) @>)

    [<Theory>]
    [<InlineData("// axial-allow-AXG002: live boundary")>]
    [<InlineData("// axial-allow-AXG001: different reason")>]
    let ``changed suppression rule or reason replaces the reviewed identity`` replacement =
        withSuppressionRepository (fun _ write current approved ->
            write "App.fs" "module App\n// axial-allow-AXG001: live boundary\n"
            let old=approved ()
            write "App.fs" ("module App\n"+replacement+"\n")
            let snapshot=current ()
            let changes=diff old snapshot
            test <@ Set.difference old.Suppressions snapshot.Suppressions |> Set.count = 1 @>
            test <@ changes.SuppressionsAdded.Length=1 @>
            test <@ not (isEmpty changes) @>)

    [<Fact>]
    let ``moving suppression to another file requires approval`` () =
        withSuppressionRepository (fun _ write current approved ->
            write "App.fs" "module App\n// axial-allow-AXG001: live boundary\n"
            let old=approved ()
            write "App.fs" "module App\n"
            write "Other.fs" "module Other\n// axial-allow-AXG001: live boundary\n"
            let snapshot=current ()
            test <@ Set.difference old.Suppressions snapshot.Suppressions |> Set.count = 1 @>
            test <@ (diff old snapshot).SuppressionsAdded.Length=1 @>
            test <@ diff old snapshot |> isEmpty |> not @>)

    [<Fact>]
    let ``removing suppression strengthens policy without approval`` () =
        withSuppressionRepository (fun _ write current approved ->
            write "App.fs" "module App\n// axial-allow-AXG001: live boundary\n"
            let old=approved ()
            write "App.fs" "module App\n"
            let snapshot=current ()
            let changes=diff old snapshot
            test <@ changes.SuppressionsAdded.IsEmpty @>
            test <@ isEmpty changes @>
            test <@ validateApproval Normal old snapshot changes = Ok () @>)

    [<Fact>]
    let ``shrinking baseline strengthens policy without approval`` () =
        withSuppressionRepository (fun root _ current approved ->
            let path=Path.Combine(root,DefaultBaseline)
            File.WriteAllText(path,"effects.ambient\tsrc/App.fs\tabc x2\n")
            let old=approved ()
            File.WriteAllText(path,"effects.ambient\tsrc/App.fs\tabc\n")
            test <@ diff old (current ()) |> isEmpty @>
            File.WriteAllText(path,"")
            test <@ diff old (current ()) |> isEmpty @>)

    [<Fact>]
    let ``legacy line numbered suppression lock is readable but requires reapproval`` () =
        withSuppressionRepository (fun root write current _ ->
            write "App.fs" "module App\n// axial-allow-AXG001: live boundary\n"
            let snapshot=current ()
            let legacy={snapshot with Suppressions=set ["src/App.fs:2:// axial-allow-AXG001: live boundary"]}
            let path=Path.Combine(root,LockName)
            // Literal version 1 header keeps this a legacy fixture after the writer migrates.
            let records=(serialize legacy).Split('\n') |> Array.skip 1 |> String.concat "\n"
            File.WriteAllText(path,"version = 1\n"+records)
            let parsed=parseLock path |> function Ok value -> value | Error error -> failwith error
            test <@ diff parsed snapshot |> isEmpty |> not @>)

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
        test <@ summary.Contains("AXIAL GUARDRAILS POLICY CHANGED") @>

    [<Fact>]
    let ``readable summary groups suppressions and lists every item without a diff command`` () =
        let items = [ for n in 1..74 -> $"src/App.fs\t{n}\t// axial-allow-AXG001: reason" ]
        let changes = { diff empty empty with SuppressionsAdded=items @ ["src/Other.fs\t1\t// axial-allow-AXG002: other"] }
        let summary = formatSummary changes
        let lines = summary.Split(System.Environment.NewLine)
        test <@ lines |> Array.filter ((=) "    src/App.fs") |> Array.length = 1 @>
        test <@ lines |> Array.contains "      #1  // axial-allow-AXG001: reason" @>
        test <@ lines |> Array.contains "      #74  // axial-allow-AXG001: reason" @>
        test <@ lines |> Array.contains "    src/Other.fs" @>
        test <@ not (summary.Contains("and ")) @>
        test <@ changes.SuppressionsAdded = items @ ["src/Other.fs\t1\t// axial-allow-AXG002: other"] @>

    [<Fact>]
    let ``readable summary puts other category items on separate indented lines`` () =
        let changes = { diff empty empty with RulesDowngraded=["rule A";"rule B"]; ProtectedChanged=["a.props";"b.props"]; BoundariesAdded=["src/**";"vendor/**"] }
        let lines = (formatSummary changes).Split(System.Environment.NewLine)
        for item in ["rule A";"rule B";"a.props";"b.props";"src/**";"vendor/**"] do
            test <@ lines |> Array.contains ("    " + item) @>
        test <@ lines |> Array.exists (fun line -> line.Trim() = "baseline entries added:  none") @>

    [<Fact>]
    let ``readable summary keeps JSON message formatting unchanged`` () =
        let changes = { diff empty empty with SuppressionsAdded=["a.fs\t1\tcomment";"a.fs\t2\tcomment"] }
        let expected =
            [ "AXIAL GUARDRAILS POLICY CHANGED"
              "  rules downgraded:        none"
              "  baseline entries added:  none"
              "  suppressions added:      a.fs\t1\tcomment; a.fs\t2\tcomment"
              "  boundaries added:        none"
              "  protected files changed: none" ] |> String.concat System.Environment.NewLine
        test <@ formatJsonSummary changes = expected @>

    [<Fact>]
    let ``runtime exemptions require lock approval`` () =
        let root = Path.Combine(Path.GetTempPath(), "Axial.Runtime.Lock.Tests", Guid.NewGuid().ToString("N"))
        try
            Directory.CreateDirectory root |> ignore
            File.WriteAllText(Path.Combine(root, DefaultBaseline), "")
            File.WriteAllText(Path.Combine(root, "Runtime.fs"), "module Runtime")
            let policy = "version = 2\n[default]\nrole = \"domain\"\n"
            File.WriteAllText(Path.Combine(root, ConfigName), policy)
            let before = inspect root |> Result.toOption |> Option.get
            File.WriteAllText(Path.Combine(root, ConfigName), policy + "[runtime]\nfiles = [\"Runtime.fs\"]\n")
            let after = inspect root |> Result.toOption |> Option.get
            let changes = diff before after
            test <@ changes.BoundariesAdded = [ "runtime:Runtime.fs" ] @>
            test <@ not (isEmpty changes) @>
            test <@ changes.ProtectedChanged |> List.contains ConfigName @>
        finally Directory.Delete(root, true)

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
    let ``protected hashes include analyzer configs but exclude Guardrails test build inputs`` () =
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
            for path in ["nested.globalconfig";"build/Imported.targets";"Directory.Build.targets"] do
                test <@ snapshot.Hashes.ContainsKey path @>
            let current () = inspect root |> Result.toOption |> Option.get
            let source=Path.Combine(root,"tests","Axial.Guardrails.Tests","Tests.fs")
            let project=Path.ChangeExtension(source,"fsproj")
            test <@ not (snapshot.Hashes.ContainsKey "tests/Axial.Guardrails.Tests/Tests.fs") @>
            File.WriteAllText(source,"module Edited")
            test <@ diff snapshot (current ()) |> isEmpty @>
            File.WriteAllText(project,"<Project />")
            test <@ diff snapshot (current ()) |> isEmpty @>
            File.Delete source
            File.Delete project
            File.Delete(Path.Combine(root,"tests","Axial.Guardrails.Tests","Axial.Guardrails.Tests.fsproj"))
            test <@ diff snapshot (current ()) |> isEmpty @>
            File.WriteAllText(Path.Combine(root,"Directory.Build.targets"),"<Project><!-- changed --></Project>")
            test <@ (diff snapshot (current ())).ProtectedChanged=["Directory.Build.targets"] @>
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
