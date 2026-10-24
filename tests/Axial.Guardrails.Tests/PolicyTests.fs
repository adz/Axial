namespace Axial.Guardrails.Tests

open System
open System.IO
open Axial.Guardrails
open Axial.Guardrails.Policy
open Swensen.Unquote
open Xunit

module PolicyTests =
    let private withRepository action =
        let root = Path.Combine(Path.GetTempPath(), "Axial.Policy.Tests", Guid.NewGuid().ToString("N"))
        Directory.CreateDirectory(Path.Combine(root, "src", "App", "Effects")) |> ignore
        Directory.CreateDirectory(Path.Combine(root, "tests")) |> ignore
        File.WriteAllText(Path.Combine(root, "src", "App", "App.fsproj"), "<Project />")
        File.WriteAllText(Path.Combine(root, "src", "App", "Domain.fs"), "module Domain")
        File.WriteAllText(Path.Combine(root, "tests", "App.Tests.fsproj"), "<Project />")
        try action root finally Directory.Delete(root, true)

    let private parse root text = Policy.parse root (set [ "effects.ambient"; "domain.async-return" ]) text

    [<Fact>]
    let ``parses the closed version 2 schema and every rule level`` () =
        withRepository (fun root ->
            let actual = parse root """version = 2
baseline = "Axial.Guardrails.baseline"
[default]
role = "domain"
[projects]
test = ["tests/**/*.fsproj"]
[files]
effects = ["src/App/Effects/**"]
[roles.domain.rules]
"effects.ambient" = "forbid"
"domain.async-return" = "deny"
[roles.effects.rules]
"effects.ambient" = "allow"
[roles.test.rules]
"effects.ambient" = "warn"
"""
            test <@ Result.isOk actual @>
            let policy = Result.toOption actual |> Option.get
            test <@ policy.Version = 2 && policy.DefaultRole = Domain @>
            test <@ policy.Rules[Domain]["effects.ambient"] = Forbid @>
            test <@ policy.Rules[Effects]["effects.ambient"] = Allow @>
            test <@ policy.Rules[Test]["effects.ambient"] = Warn @>)

    [<Fact>]
    let ``file role overrides project role which overrides default`` () =
        withRepository (fun root ->
            let text = "version = 2\n[default]\nrole = \"domain\"\n[projects]\nhost = [\"src/App/App.fsproj\"]\n[files]\neffects = [\"src/App/Effects/**\"]"
            let policy = parse root text |> Result.toOption |> Option.get
            test <@ (Policy.resolve policy "src/App/App.fsproj" "src/App/Domain.fs" |> Result.toOption |> Option.get).Role = Host @>
            test <@ (Policy.resolve policy "src/App/App.fsproj" "src/App/Effects/Sql.fs" |> Result.toOption |> Option.get).Role = Effects @>
            test <@ (Policy.resolve policy "other/Other.fsproj" "other/File.fs" |> Result.toOption |> Option.get).Role = Domain @>)

    [<Fact>]
    let ``empty declared effects folder is valid but other unmatched paths fail`` () =
        withRepository (fun root ->
            let effects = parse root """version = 2
[default]
role = "domain"
[files]
effects = ["src/App/Effects/**"]
"""
            let unmatched = parse root """version = 2
[default]
role = "domain"
[files]
host = ["missing/**"]
"""
            test <@ Result.isOk effects @>
            test <@ unmatched = Error [ "policy path 'missing/**' matches nothing" ] @>)

    [<Fact>]
    let ``same-precedence overlapping assignments to different roles fail resolution`` () =
        withRepository (fun root ->
            let text = "version = 2\n[default]\nrole = \"domain\"\n[files]\neffects = [\"src/App/**\"]\nhost = [\"src/App/Domain.fs\"]"
            let policy = parse root text |> Result.toOption |> Option.get
            let actual = Policy.resolve policy "src/App/App.fsproj" "src/App/Domain.fs"
            test <@ actual = Error "conflicting file role assignments for 'src/App/Domain.fs': effects, host" @>)

    [<Theory>]
    [<InlineData("version = 3\n[default]\nrole = \"domain\"", "unsupported policy version 3; expected 2")>]
    [<InlineData("version = 2\n[default]\nrole = \"magic\"", "unknown role 'magic'; expected domain, effects, host, or test")>]
    [<InlineData("version = 2\nwat = \"x\"\n[default]\nrole = \"domain\"", "unknown policy key 'wat'")>]
    [<InlineData("version = 2\nversion = 2\n[default]\nrole = \"domain\"", "line 2: duplicate declaration 'version'")>]
    let ``rejects invalid versions roles unknown keys and duplicates`` text expected =
        withRepository (fun root -> test <@ parse root text = Error [ expected ] @>)

    [<Fact>]
    let ``rejects unknown rules and invalid levels clearly`` () =
        withRepository (fun root ->
            let unknown = parse root """version = 2
[default]
role = "domain"
[roles.domain.rules]
"made.up" = "deny"
"""
            let level = parse root """version = 2
[default]
role = "domain"
[roles.domain.rules]
"effects.ambient" = "error"
"""
            test <@ unknown = Error [ "unknown rule 'made.up'" ] @>
            test <@ level = Error [ "invalid rule level 'error'; expected allow, warn, deny, or forbid" ] @>)

    [<Fact>]
    let ``rejects repository escapes`` () =
        withRepository (fun root ->
            let actual = parse root """version = 2
[default]
role = "domain"
[files]
host = ["../Program.fs"]
"""
            test <@ actual = Error [ "policy path '../Program.fs' escapes the repository root" ] @>)
