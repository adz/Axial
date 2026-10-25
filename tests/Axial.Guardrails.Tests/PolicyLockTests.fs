namespace Axial.Guardrails.Tests

open Axial.Tool.PolicyLock
open Swensen.Unquote
open Xunit

module PolicyLockTests =
    let private empty =
        { Hashes=Map.empty; Baseline=[]; Tests=[]; Rules=Map.empty
          Effects=Set.empty; Boundaries=Set.empty; Suppressions=Set.empty }

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
                         Effects=set ["src/Effects/**"]
                         Boundaries=set ["vendor.props"]
                         Suppressions=set ["src/App.fs:2:// axial-allow-AXG001: reason"] }
        let changes=diff old current
        let summary=formatSummary changes
        test <@ changes.RulesDowngraded.Length=1 @>
        test <@ changes.BaselineAdded.Length=1 @>
        test <@ changes.SuppressionsAdded.Length=1 @>
        test <@ changes.BoundariesAdded.Length=2 @>
        test <@ changes.ProtectedChanged=["Directory.Build.props"] @>
        test <@ changes.TestsRemoved.Length=1 @>
        test <@ summary.Contains("AXIAL GUARDRAILS POLICY CHANGED — BUILD BLOCKED") @>

    [<Fact>]
    let ``ordinary approval refuses baseline growth`` () =
        let current={empty with Baseline=[{Rule="effects.ambient";File="src/App.fs";Fingerprint="abc";Count=1}]}
        let changes=diff empty current
        test <@ validateApproval Normal empty current changes = Error "baseline growth refused; use 'axial policy baseline --rule <id>' only for a newly enabled rule" @>

    [<Fact>]
    let ``baseline approval accepts only the selected newly enabled rule`` () =
        let current={empty with Rules=Map ["domain/effects.ambient","deny"]; Baseline=[{Rule="effects.ambient";File="src/App.fs";Fingerprint="abc";Count=1}]}
        let changes=diff empty current
        test <@ validateApproval (BaselineRule "effects.ambient") empty current changes = Ok () @>
        let established={empty with Rules=current.Rules}
        test <@ validateApproval (BaselineRule "effects.ambient") established current changes = Error "baseline growth refused: rule 'effects.ambient' was not newly enabled" @>

    [<Fact>]
    let ``test decreases need their dedicated acceptance`` () =
        let old={empty with Tests=[{Project="tests/App.Tests/App.Tests.fsproj";Tests=3;Assertions=4}]}
        let current={empty with Tests=[{Project="tests/App.Tests/App.Tests.fsproj";Tests=2;Assertions=3}]}
        let changes=diff old current
        test <@ Result.isError(validateApproval Normal old current changes) @>
        test <@ validateApproval AcceptRemovedTests old current changes = Ok () @>

    [<Fact>]
    let ``lock serialization is deterministic and sorted`` () =
        let snapshot={empty with Hashes=Map ["z","2";"a","1"]; Effects=set ["z/**";"a/**"]}
        let first=serialize snapshot
        let second=serialize snapshot
        test <@ first=second @>
        test <@ first.IndexOf("a  a") < first.IndexOf("2  z") @>
        test <@ first.IndexOf("EFFECT  a/**") < first.IndexOf("EFFECT  z/**") @>
