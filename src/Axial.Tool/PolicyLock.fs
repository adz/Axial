namespace Axial.Tool

open System
open System.IO
open System.Diagnostics
open System.Security.Cryptography
open System.Text
open System.Text.RegularExpressions
open Axial.Guardrails.Policy

module PolicyLock =
    [<Literal>]
    let ConfigName = "Axial.Guardrails.toml"
    [<Literal>]
    let DefaultBaseline = "Axial.Guardrails.baseline"
    [<Literal>]
    let LockName = "Axial.Guardrails.lock"

    type BaselineEntry = { Rule: string; File: string; Fingerprint: string; Count: int }
    type TestCount = { Project: string; Tests: int; Assertions: int }
    type Snapshot =
        { Hashes: Map<string,string>; Baseline: BaselineEntry list; BaselineRoles: Map<string,string>; Tests: TestCount list
          Rules: Map<string,string>; Capabilities: Set<string>; Boundaries: Set<string>; Suppressions: Set<string> }
    type ChangeSummary =
        { RulesDowngraded: string list; BaselineAdded: BaselineEntry list; SuppressionsAdded: string list
          BoundariesAdded: string list; ProtectedChanged: string list; TestsRemoved: string list }
    type Approval = Normal | AcceptRemovedTests | BaselineRule of string

    let knownRules = set [
        "effects.ambient"; "domain.async-return"; "domain.whole-environment"; "domain.service-provider"; "test.integrity"
        "blocking.async"; "blocking.get-result"; "blocking.get-result-outside"; "blocking.get-result-unit"; "blocking.get-result-unit-outside"
        "blocking.task-result"; "blocking.task-result-outside"; "blocking.task-wait"; "blocking.value-task-result"; "clock.datetime"
        "clock.datetimeoffset"; "clock.local-timezone"; "clock.stopwatch"; "clock.task-delay"; "console.any"; "environment.processor-count"
        "environment.state"; "filesystem.directory"; "filesystem.file"; "filesystem.watcher"; "guid.new"; "http.new-client"
        "process.start"; "random.cryptographic"; "random.new"; "scope.run"; "sleep.thread"; "websocket.new-client" ]

    let private relative root path = Path.GetRelativePath(root, path).Replace('\\','/')
    let private repositoryFiles root =
        let git = ProcessStartInfo("git", "ls-files -z --cached --others --exclude-standard")
        git.WorkingDirectory <- root
        git.RedirectStandardOutput <- true
        git.UseShellExecute <- false
        use childProcess = Process.Start git
        let output = childProcess.StandardOutput.ReadToEnd()
        childProcess.WaitForExit()
        if childProcess.ExitCode = 0 then
            output.Split('\000',StringSplitOptions.RemoveEmptyEntries) |> Array.map(fun path -> Path.Combine(root,path))
        else
            let skipped = set [ ".git"; ".livedocs"; "artifacts"; "bin"; "obj"; "output" ]
            let rec walk directory = seq {
                yield! Directory.EnumerateFiles(directory)
                for child in Directory.EnumerateDirectories(directory) do
                    if not(skipped.Contains(Path.GetFileName child)) then yield! walk child }
            walk root |> Seq.toArray
    let private sha256 path =
        use stream = File.OpenRead path
        SHA256.HashData(stream) |> Convert.ToHexString |> fun x -> x.ToLowerInvariant()

    let private baselineEntries path =
        if not (File.Exists path) then [] else
        File.ReadLines path |> Seq.choose (fun line ->
            let line = line.Trim()
            if line = "" || line.StartsWith("#") then None else
            let parts = line.Split('\t')
            if parts.Length < 3 then failwith $"malformed baseline entry: {line}"
            let m = Regex.Match(parts[2], "^(.*?)(?: x([0-9]+))?$")
            Some { Rule=parts[0]; File=parts[1]; Fingerprint=m.Groups[1].Value; Count=if m.Groups[2].Success then int m.Groups[2].Value else 1 })
        |> Seq.sortBy (fun x -> x.Rule,x.File,x.Fingerprint) |> Seq.toList

    let private policyRules (config: Config) =
        config.Rules |> Seq.collect (fun roleRules -> roleRules.Value |> Seq.map (fun rule ->
            let role = match roleRules.Key with Domain->"domain"|Capabilities->"capabilities"|Services->"services"|Host->"host"|Test->"test"
            let level = match rule.Value with Allow->"allow"|Warn->"warn"|Deny->"deny"|Forbid->"forbid"
            $"{role}/{rule.Key}", level)) |> Map.ofSeq

    let private sourceFilesForProject (project: string) =
        let directory = Path.GetDirectoryName project
        let explicitFiles = Regex.Matches(File.ReadAllText project, "Compile\\s+Include=\"([^\"]+\\.fs)\"") |> Seq.cast<Match> |> Seq.map (fun m -> Path.GetFullPath(Path.Combine(directory,m.Groups[1].Value))) |> Seq.filter File.Exists |> Seq.toArray
        if explicitFiles.Length > 0 then explicitFiles else Directory.GetFiles(directory,"*.fs",SearchOption.TopDirectoryOnly)

    // Test counts are ratcheted from the completed test run by scripts/check-test-ratchet.py.
    // Preserve the approved baseline here; policy inspection must not infer execution from source syntax.
    let private testCounts root =
        let lockPath=Path.Combine(root,LockName)
        if not(File.Exists lockPath) then [] else
        File.ReadLines lockPath
        |> Seq.choose(fun line ->
            if not(line.StartsWith("TEST  ",StringComparison.Ordinal)) then None else
            match line.Substring(6).Split('\t') with
            | [|project;tests;assertions|] ->
                match Int32.TryParse tests,Int32.TryParse assertions with
                | (true,t),(true,a) -> Some {Project=project;Tests=t;Assertions=a}
                | _ -> None
            | _ -> None)
        |> Seq.sortBy _.Project |> Seq.toList

    let private suppressions root (files: string array) =
        files |> Seq.filter (fun p -> p.EndsWith(".fs",StringComparison.Ordinal))
        |> Seq.collect (fun path -> File.ReadLines path |> Seq.mapi (fun i line -> path,i+1,line))
        |> Seq.choose (fun (path,line,text) -> if Regex.IsMatch(text,"//\\s*axial-allow-AXG[0-9]+:") then Some $"{relative root path}:{line}:{text.Trim()}" else None)
        |> Set.ofSeq

    let private mandatoryPaths root (files: string array) baseline config =
        let relativeFiles = files |> Array.map(relative root)
        let guardrailTestDirs = relativeFiles |> Array.filter(fun p->p.EndsWith(".fsproj") && p.StartsWith("tests/Axial.Guardrails")) |> Array.map(fun p->Path.GetDirectoryName(p).Replace('\\','/'))
        seq {
            yield ConfigName; yield baseline
            yield! relativeFiles |> Seq.filter(fun p ->
                let name=Path.GetFileName p
                name=".editorconfig" || name.EndsWith(".globalconfig",StringComparison.OrdinalIgnoreCase)
                || name.StartsWith("Directory.Build.",StringComparison.Ordinal)
                || p.EndsWith(".props",StringComparison.Ordinal) || p.EndsWith(".targets",StringComparison.Ordinal)
                || ((p.EndsWith(".fsproj") || p.EndsWith(".fs")) && guardrailTestDirs |> Array.exists(fun d->p.StartsWith(d+"/",StringComparison.Ordinal))))
            for pattern in config.Protected do
                let rx = "^" + Regex.Escape(pattern).Replace("\\*\\*",".*").Replace("\\*","[^/]*").Replace("\\?","[^/]") + "$"
                yield! files |> Seq.map (relative root) |> Seq.filter (fun p -> Regex.IsMatch(p,rx)) }
        |> Seq.distinct |> Seq.filter ((<>) LockName) |> Seq.sort |> Seq.toList

    let inspect root =
        try
            let configPath = Path.Combine(root,ConfigName)
            if not(File.Exists configPath) then Error $"{ConfigName} not found; run 'axial init'" else
            match Axial.Guardrails.Policy.parse root knownRules (File.ReadAllText configPath) with
            | Error errors -> Error(String.concat Environment.NewLine errors)
            | Ok config ->
                let baseline = defaultArg config.Baseline DefaultBaseline
                let files = repositoryFiles root
                let protectedPaths = mandatoryPaths root files baseline config
                let missing = protectedPaths |> List.filter (fun p -> not(File.Exists(Path.Combine(root,p))))
                if not missing.IsEmpty then Error $"protected file not found: {missing.Head}" else
                let hashes = protectedPaths |> Seq.map (fun p -> p,sha256(Path.Combine(root,p))) |> Map.ofSeq
                let capabilities = config.Files |> List.filter (fun x -> x.Role=Capabilities) |> List.collect _.Patterns |> Set.ofList
                let boundaries = config.Boundaries |> Set.ofList
                let baselineEntries = baselineEntries(Path.Combine(root,baseline))
                let roleName = function Domain->"domain" | Capabilities->"capabilities" | Services->"services" | Host->"host" | Test->"test"
                let sourceProjects =
                    files
                    |> Seq.filter (fun path -> path.EndsWith(".fsproj",StringComparison.Ordinal))
                    |> Seq.collect (fun project -> sourceFilesForProject project |> Seq.map (fun source -> relative root source, relative root project))
                    |> Map.ofSeq
                let baselineRoles =
                    baselineEntries
                    |> Seq.choose(fun entry ->
                        sourceProjects.TryFind entry.File
                        |> Option.bind(fun project -> resolve config project entry.File |> Result.toOption)
                        |> Option.map(fun resolved->entry.File,roleName resolved.Role))
                    |> Map.ofSeq
                Ok { Hashes=hashes; Baseline=baselineEntries; BaselineRoles=baselineRoles; Tests=testCounts root; Rules=policyRules config; Capabilities=capabilities; Boundaries=boundaries; Suppressions=suppressions root files }
        with ex -> Error ex.Message

    let serialize snapshot =
        seq {
            yield "version = 1"
            for KeyValue(path,hash) in snapshot.Hashes do yield $"SHA256  {hash}  {path}"
            for x in snapshot.Baseline do yield $"BASELINE  {x.Rule}\t{x.File}\t{x.Fingerprint}\t{x.Count}"
            for KeyValue(file,role) in snapshot.BaselineRoles do yield $"BASELINE_ROLE  {file}\t{role}"
            for x in snapshot.Tests do yield $"TEST  {x.Project}\t{x.Tests}\t{x.Assertions}"
            for KeyValue(rule,level) in snapshot.Rules do yield $"POLICY  {rule}\t{level}"
            for x in snapshot.Capabilities do yield $"CAPABILITY  {x}"
            for x in snapshot.Boundaries do yield $"BOUNDARY  {x}"
            for x in snapshot.Suppressions do yield $"SUPPRESSION  {x}" }
        |> String.concat "\n" |> fun x -> x+"\n"

    let parseLock path =
        try
            if not(File.Exists path) then Error $"{LockName} not found; run 'axial init'" else
            let lines=File.ReadAllLines path
            let fail line message = failwith $"{path}:{line}: {message}; regenerate the lock with 'axial policy approve' after review"
            if lines.Length=0 || lines[0]<>"version = 1" then fail 1 "the first record must be exactly 'version = 1'"
            let mutable s={ Hashes=Map.empty; Baseline=[]; BaselineRoles=Map.empty; Tests=[]; Rules=Map.empty; Capabilities=Set.empty; Boundaries=Set.empty; Suppressions=Set.empty }
            let unique line kind key exists = if exists then fail line $"duplicate {kind} record '{key}'"
            for index in 1..lines.Length-1 do
                let lineNumber=index+1
                let line=lines[index]
                if line="version = 1" || line.StartsWith("version = ") then fail lineNumber "duplicate or unsupported version record"
                elif line.StartsWith("SHA256  ") then
                    let p=line.Split("  ",StringSplitOptions.None)
                    if p.Length<>3 || p[1].Length<>64 || String.IsNullOrWhiteSpace p[2] || p[1] |> Seq.exists(fun c->not(Uri.IsHexDigit c)) then fail lineNumber "SHA256 requires exactly a 64-hex hash and non-empty path"
                    unique lineNumber "SHA256" p[2] (s.Hashes.ContainsKey p[2]); s <- {s with Hashes=s.Hashes.Add(p[2],p[1])}
                elif line.StartsWith("BASELINE  ") then
                    let p=line.Substring(10).Split('\t')
                    if p.Length<>4 || p[0..2] |> Array.exists String.IsNullOrWhiteSpace then fail lineNumber "BASELINE requires non-empty rule, file, fingerprint, and count fields"
                    let ok,count=Int32.TryParse p[3]
                    if not ok || count<1 then fail lineNumber "BASELINE count must be a positive integer"
                    unique lineNumber "BASELINE" (String.concat "/" p[0..2]) (s.Baseline|>List.exists(fun x->x.Rule=p[0]&&x.File=p[1]&&x.Fingerprint=p[2])); s <- {s with Baseline={Rule=p[0];File=p[1];Fingerprint=p[2];Count=count}::s.Baseline}
                elif line.StartsWith("BASELINE_ROLE  ") then
                    let p=line.Substring(15).Split('\t')
                    if p.Length<>2 || String.IsNullOrWhiteSpace p[0] || not(set["domain";"capabilities";"host";"test"] |> Set.contains p[1]) then fail lineNumber "BASELINE_ROLE requires a non-empty file and valid role"
                    unique lineNumber "BASELINE_ROLE" p[0] (s.BaselineRoles.ContainsKey p[0]); s <- {s with BaselineRoles=s.BaselineRoles.Add(p[0],p[1])}
                elif line.StartsWith("TEST  ") then
                    let p=line.Substring(6).Split('\t')
                    if p.Length<>3 || String.IsNullOrWhiteSpace p[0] then fail lineNumber "TEST requires a non-empty project, tests, and assertions fields"
                    let okT,tests=Int32.TryParse p[1]
                    let okA,assertions=Int32.TryParse p[2]
                    if not okT || not okA || tests<0 || assertions<0 then fail lineNumber "TEST counts must be non-negative integers"
                    unique lineNumber "TEST" p[0] (s.Tests|>List.exists(fun x->x.Project=p[0])); s <- {s with Tests={Project=p[0];Tests=tests;Assertions=assertions}::s.Tests}
                elif line.StartsWith("POLICY  ") then
                    let p=line.Substring(8).Split('\t')
                    if p.Length<>2 || String.IsNullOrWhiteSpace p[0] || not(set["allow";"warn";"deny";"forbid"]|>Set.contains p[1]) then fail lineNumber "POLICY requires a non-empty rule and allow/warn/deny/forbid field"
                    unique lineNumber "POLICY" p[0] (s.Rules.ContainsKey p[0]); s <- {s with Rules=s.Rules.Add(p[0],p[1])}
                elif line.StartsWith("CAPABILITY  ") then
                    let value=line.Substring(12)
                    if String.IsNullOrWhiteSpace value then fail lineNumber "CAPABILITY requires a non-empty value"
                    unique lineNumber "CAPABILITY" value (s.Capabilities.Contains value)
                    s <- {s with Capabilities=s.Capabilities.Add value}
                elif line.StartsWith("BOUNDARY  ") then
                    let value=line.Substring(10)
                    if String.IsNullOrWhiteSpace value then fail lineNumber "BOUNDARY requires a non-empty value"
                    unique lineNumber "BOUNDARY" value (s.Boundaries.Contains value)
                    s <- {s with Boundaries=s.Boundaries.Add value}
                elif line.StartsWith("SUPPRESSION  ") then
                    let value=line.Substring(13)
                    if String.IsNullOrWhiteSpace value then fail lineNumber "SUPPRESSION requires a non-empty value"
                    unique lineNumber "SUPPRESSION" value (s.Suppressions.Contains value)
                    s <- {s with Suppressions=s.Suppressions.Add value}
                elif line.Trim()<>"" then fail lineNumber "unknown or malformed lock record"
            Ok {s with Baseline=List.sortBy (fun x->x.Rule,x.File,x.Fingerprint) s.Baseline; Tests=List.sortBy _.Project s.Tests}
        with ex -> Error ex.Message

    let diff old current =
        let rank = function "allow"->0|"warn"->1|"deny"->2|"forbid"->3|_-> -1
        let downgraded = old.Rules |> Seq.choose(fun x -> current.Rules.TryFind x.Key |> Option.filter(fun n -> rank n < rank x.Value) |> Option.map(fun n -> $"{x.Key}: {x.Value} -> {n}")) |> Seq.toList
        let oldBase=old.Baseline |> Seq.map(fun x -> (x.Rule,x.File,x.Fingerprint),x.Count) |> Map.ofSeq
        let baselineAdded=current.Baseline |> List.filter(fun x -> x.Count > (oldBase.TryFind(x.Rule,x.File,x.Fingerprint) |> Option.defaultValue 0))
        let currentTests=current.Tests|>Seq.map(fun x->x.Project,x)|>Map.ofSeq
        let removed=old.Tests |> List.choose(fun o ->
            let now=currentTests.TryFind o.Project |> Option.defaultValue {Project=o.Project;Tests=0;Assertions=0}
            if now.Tests<o.Tests || now.Assertions<o.Assertions then Some $"{o.Project}: tests {o.Tests} -> {now.Tests}, assertions {o.Assertions} -> {now.Assertions}" else None)
        { RulesDowngraded=downgraded; BaselineAdded=baselineAdded; SuppressionsAdded=Set.difference current.Suppressions old.Suppressions|>Set.toList
          BoundariesAdded=Set.union (Set.difference current.Capabilities old.Capabilities) (Set.difference current.Boundaries old.Boundaries)|>Set.toList
          ProtectedChanged=(Set.union (old.Hashes |> Map.keys |> Set.ofSeq) (current.Hashes |> Map.keys |> Set.ofSeq) |> Set.filter(fun p->old.Hashes.TryFind p<>current.Hashes.TryFind p)|>Set.toList) @ (if downgraded.IsEmpty && baselineAdded.IsEmpty && removed.IsEmpty && old.Suppressions=current.Suppressions && old.Capabilities=current.Capabilities && old.Boundaries=current.Boundaries then [] else [LockName]); TestsRemoved=removed }

    let isEmpty x = x.RulesDowngraded.IsEmpty && x.BaselineAdded.IsEmpty && x.SuppressionsAdded.IsEmpty && x.BoundariesAdded.IsEmpty && x.ProtectedChanged.IsEmpty && x.TestsRemoved.IsEmpty
    let formatSummary x =
        let show label values =
            let joined = String.concat "; " values
            if List.isEmpty values then $"  {label,-24} none" else $"  {label,-24} {joined}"
        [ "AXIAL GUARDRAILS POLICY CHANGED — BUILD BLOCKED"
          show "rules downgraded:" x.RulesDowngraded
          show "baseline entries added:" (x.BaselineAdded|>List.map(fun b -> $"{b.Rule}/{b.File}/{b.Fingerprint} x{b.Count}"))
          show "suppressions added:" x.SuppressionsAdded; show "boundaries added:" x.BoundariesAdded
          show "protected files changed:" x.ProtectedChanged; show "tests removed:" x.TestsRemoved ] |> String.concat Environment.NewLine

    let validateApproval approval old current changes =
        if not changes.TestsRemoved.IsEmpty && approval<>AcceptRemovedTests then Error "test/assertion count decreased; rerun only after human review with --accept-removed-tests"
        elif not changes.BaselineAdded.IsEmpty then
            match approval with
            | BaselineRule rule ->
                let reporting = function "warn"|"deny"|"forbid" -> true | _ -> false
                let entryNewlyEnabled entry =
                    let currentRole=current.BaselineRoles.TryFind entry.File
                    let oldRole=old.BaselineRoles.TryFind entry.File |> Option.orElse currentRole
                    match oldRole,currentRole with
                    | Some beforeRole,Some afterRole ->
                        let before=old.Rules.TryFind($"{beforeRole}/{rule}") |> Option.exists reporting
                        let after=current.Rules.TryFind($"{afterRole}/{rule}") |> Option.exists reporting
                        not before && after
                    | _ -> false
                if changes.BaselineAdded |> List.exists(fun x->x.Rule<>rule) then Error "baseline growth refused: entries exist outside the selected rule"
                elif changes.BaselineAdded |> List.exists(entryNewlyEnabled >> not) then Error $"baseline growth refused: rule '{rule}' was not newly enabled for every affected file's effective role"
                else Ok()
            | _ -> Error "baseline growth refused; use 'axial policy baseline --rule <id>' only for a newly enabled rule"
        else Ok()

    let writeAtomic path text =
        let temp=path+"."+Guid.NewGuid().ToString("N")+".tmp"
        File.WriteAllText(temp,text,new UTF8Encoding(false)); File.Move(temp,path,true)
