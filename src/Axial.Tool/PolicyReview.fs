namespace Axial.Tool

open System
open System.IO
open System.Diagnostics
open System.Text.Json
open System.Text.RegularExpressions
open Axial.Guardrails.Policy
open Axial.Tool.PolicyLock

/// Compares repository state, not the hashes last written to a possibly stale lock.
module PolicyReview =
    type Finding = { Group: string; Detail: string }
    type Review = { Since: string; Loosened: Finding list; Removed: Finding list }

    let private command root executable args =
        let info = ProcessStartInfo(executable)
        info.WorkingDirectory <- root
        info.RedirectStandardOutput <- true
        info.RedirectStandardError <- true
        for arg in args do info.ArgumentList.Add(arg: string)
        use child = Process.Start info
        let output = child.StandardOutput.ReadToEndAsync()
        let errors = child.StandardError.ReadToEndAsync()
        if not (child.WaitForExit(60000)) then
            child.Kill(true)
            failwith $"{executable} timed out"
        if child.ExitCode <> 0 then failwith (errors.Result + output.Result)
        output.Result

    let currentCommit root = command root "git" ["rev-parse"; "--verify"; "HEAD^{commit}"] |> _.Trim()

    let reviewedPoint root =
        match parseLock (Path.Combine(root, LockName)) with
        | Error e -> Error e
        | Ok _ ->
            File.ReadLines(Path.Combine(root, LockName))
            |> Seq.tryPick (fun line -> if line.StartsWith("REVIEWED_POINT  ") then Some(line.Substring(16)) else None)
            |> Ok

    // The lexer deliberately excludes strings, XML docs and nested block comments. Both git and
    // working-tree inventories use it, so line movement does not look like a new exception.
    let private comments (text: string) =
        let mutable depth = 0
        let mutable triple = false
        let mutable verbatim = false
        [ for line in text.Replace("\r\n", "\n").Split('\n') do
            let mutable i = 0
            let mutable quoted = false
            while i < line.Length do
                let at (value: string) = line.IndexOf(value, i, StringComparison.Ordinal) = i
                if depth > 0 then
                    if at "(*" then depth <- depth + 1; i <- i + 2
                    elif at "*)" then depth <- depth - 1; i <- i + 2
                    else i <- i + 1
                elif triple then
                    if at "\"\"\"" then triple <- false; i <- i + 3 else i <- i + 1
                elif verbatim then
                    if at "\"\"" then i <- i + 2
                    elif at "\"" then verbatim <- false; i <- i + 1
                    else i <- i + 1
                elif quoted then
                    if at "\\" then i <- min line.Length (i + 2)
                    elif at "\"" then quoted <- false; i <- i + 1
                    else i <- i + 1
                elif at "///" then i <- line.Length
                elif at "//" then
                    let comment = line.Substring(i).Trim()
                    if Regex.IsMatch(comment, @"\baxial-(?:allow-(?:file-)?AXG[0-9]+|note\s+AXG[0-9]+)\s*:", RegexOptions.IgnoreCase) then yield comment
                    i <- line.Length
                elif at "(*" then depth <- 1; i <- i + 2
                elif at "\"\"\"" then triple <- true; i <- i + 3
                elif at "@\"" then verbatim <- true; i <- i + 2
                elif at "\"" then quoted <- true; i <- i + 1
                else i <- i + 1 ]

    type private State = { Entries: Map<string, Set<string>>; Rules: Map<string, string>; Projects: Map<string, Map<string, string>> }
    let private files root tracked =
        let skipped = set [".git"; "bin"; "obj"; "artifacts"; "output"; ".livedocs"]
        let rec walk directory = seq {
            yield! Directory.EnumerateFiles directory
            for child in Directory.EnumerateDirectories directory do
                if not (skipped.Contains(Path.GetFileName child)) then yield! walk child }
        if tracked then
            command root "git" ["ls-files"; "-z"; "--cached"; "--others"; "--exclude-standard"]
            |> fun output -> output.Split('\000', StringSplitOptions.RemoveEmptyEntries)
            |> Array.filter (fun path -> File.Exists(Path.Combine(root, path)))
            |> Array.distinct |> Array.toList
        else walk root |> Seq.map (fun path -> Path.GetRelativePath(root, path).Replace('\\', '/')) |> Seq.toList

    let private evaluateProject root project extraArgs =
        let output = command root "dotnet" (["msbuild"; project; "-nologo"; "-getProperty:AxialGuardrailsEnabled,AxialGuardrailsRole,IsTestProject,TargetFrameworks"; "-getItem:AxialGuardrail,PackageReference,PackageVersion"] @ extraArgs)
        use json = JsonDocument.Parse output
        let properties = json.RootElement.GetProperty("Properties")
        let items = json.RootElement.GetProperty("Items")
        let mutable settings = properties.EnumerateObject() |> Seq.map (fun p -> p.Name, p.Value.GetString()) |> Map.ofSeq
        let guardrails = items.GetProperty("AxialGuardrail").EnumerateArray() |> Seq.map (fun item -> item.GetProperty("Identity").GetString()) |> Seq.sort |> String.concat ";"
        settings <- settings.Add("AxialGuardrail", guardrails)
        for kind in ["PackageReference"; "PackageVersion"] do
            for item in items.GetProperty(kind).EnumerateArray() do
                if String.Equals(item.GetProperty("Identity").GetString(), "Axial.Guardrails", StringComparison.OrdinalIgnoreCase) then
                    let mutable version = Unchecked.defaultof<JsonElement>
                    if item.TryGetProperty("Version", &version) then settings <- settings.Add("Axial.Guardrails version", version.GetString())
        settings

    let private projectSettings root project =
        let outer = evaluateProject root project []
        let frameworks = outer["TargetFrameworks"].Split(';', StringSplitOptions.RemoveEmptyEntries)
        if frameworks.Length = 0 then outer
        else
            frameworks |> Seq.collect (fun framework ->
                evaluateProject root project [$"-p:TargetFramework={framework}"]
                |> Map.toSeq |> Seq.map (fun (key, value) -> $"{framework}:{key}", value)) |> Map.ofSeq

    let private inspectState root tracked =
        let allFiles = files root tracked
        let config =
            match parse root knownRules (File.ReadAllText(Path.Combine(root, ConfigName))) with
            | Ok config -> config | Error errors -> failwith (String.concat "\n" errors)
        let snapshot = inspect root |> function Ok s -> s | Error e -> failwith e
        let roleName = function Domain -> "domain" | Capabilities -> "capabilities" | Services -> "services" | Host -> "host" | Test -> "test"
        let assignments kind entries =
            entries |> List.collect (fun entry -> entry.Patterns |> List.map (fun pattern -> $"{kind}:{pattern} => {roleName entry.Role}"))
        let suppressionInventory =
            allFiles |> List.filter (fun path -> path.EndsWith(".fs", StringComparison.Ordinal) && not (config.SuppressionIgnores |> List.exists (fun pattern -> matchesGlob pattern path)))
            |> List.collect (fun path ->
                comments (File.ReadAllText(Path.Combine(root, path))) |> List.countBy id
                |> List.collect (fun (text, count) -> [for occurrence in 1..count -> $"{path}\t#{occurrence} {text}"]))
        let baseline = snapshot.Baseline |> List.collect (fun entry -> [for occurrence in 1..entry.Count -> $"{entry.Rule}/{entry.File}/{entry.Fingerprint} #{occurrence}"])
        let automation =
            allFiles |> List.filter (fun path -> path = "scripts/verify.sh" || path.StartsWith(".github/workflows/", StringComparison.Ordinal))
            |> List.map (fun path -> path + "\t" + (System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(Path.Combine(root, path))) |> Convert.ToHexString))
        // Central package versions are visible even before a project references the analyzer.
        let versions =
            allFiles |> List.filter (fun path -> path.EndsWith(".props") || path.EndsWith(".fsproj") || path.EndsWith(".targets"))
            |> List.collect (fun path ->
                Regex.Matches(File.ReadAllText(Path.Combine(root, path)), "<(?:PackageReference|PackageVersion)\\b[^>]*(?:Include|Update)=\"Axial.Guardrails\"[^>]*>")
                |> Seq.cast<Match> |> Seq.map (fun m -> path + ": " + m.Value) |> Seq.toList)
        { Entries =
            [ "suppressions/notes", suppressionInventory
              "roles", ("default => " + roleName config.DefaultRole) :: (assignments "project" config.Projects @ assignments "file" config.Files)
              "runtime", config.Runtime; "boundaries", config.Boundaries; "ignore", config.SuppressionIgnores
              "baseline", baseline; "automation", automation; "analyzer package", versions ]
            |> List.map (fun (name, values) -> name, Set.ofList values) |> Map.ofList
          Rules = snapshot.Rules
          Projects = allFiles |> List.filter (_.EndsWith(".fsproj")) |> List.map (fun project -> project, projectSettings root project) |> Map.ofList }

    let review root since =
        try
            let point =
                match since with
                | Some point -> point
                | None ->
                    match reviewedPoint root with
                    | Error error -> failwith error
                    | Ok None -> failwith "First review requires --since <ref> (for Axial use --since v0.11.3)."
                    | Ok (Some point) -> point
            let commit = command root "git" ["rev-parse"; "--verify"; "--end-of-options"; point + "^{commit}"] |> _.Trim()
            let historicalRoot = Path.Combine(Path.GetTempPath(), "axial-policy-review-" + Guid.NewGuid().ToString("N"))
            Directory.CreateDirectory historicalRoot |> ignore
            try
                let paths = command root "git" ["ls-tree"; "-r"; "--name-only"; "-z"; commit]
                for path in paths.Split('\000', StringSplitOptions.RemoveEmptyEntries) do
                    let target = Path.GetFullPath(Path.Combine(historicalRoot, path))
                    if not (target.StartsWith(historicalRoot + string Path.DirectorySeparatorChar, StringComparison.Ordinal)) then failwith "Unsafe git path"
                    Directory.CreateDirectory(Path.GetDirectoryName target) |> ignore
                    File.WriteAllText(target, command root "git" ["show"; commit + ":" + path])
                let before = inspectState historicalRoot false
                let after = inspectState root true
                let added = ResizeArray<Finding>()
                let removed = ResizeArray<Finding>()
                let add group detail = added.Add { Group = group; Detail = detail }
                let remove group detail = removed.Add { Group = group; Detail = detail }
                for KeyValue(group, current) in after.Entries do
                    let old = before.Entries[group]
                    let display (value: string) = if group = "automation" then value.Split('\t')[0] else value
                    for value in Set.difference current old do add group (display value)
                    for value in Set.difference old current do
                        let detail = display value
                        let replaced = current |> Set.exists (fun entry -> display entry = detail)
                        if group = "automation" || group = "analyzer package" then
                            if not replaced then add group ($"removed {detail}")
                        else remove group detail
                let rank = function "allow" -> 0 | "warn" -> 1 | "deny" -> 2 | "forbid" -> 3 | _ -> 0
                for key in Set.union (before.Rules.Keys |> Set.ofSeq) (after.Rules.Keys |> Set.ofSeq) do
                    let old = before.Rules.TryFind key |> Option.defaultValue "deny"
                    let current = after.Rules.TryFind key |> Option.defaultValue "deny"
                    if rank current < rank old then add "rule levels" $"{key}: {old} -> {current}"
                    elif rank current > rank old then remove "rule levels" $"{key}: {old} -> {current} (tightened)"
                for KeyValue(project, settings) in after.Projects do
                    let old = before.Projects.TryFind project |> Option.defaultValue Map.empty
                    for KeyValue(key, value) in settings do
                        let previous = old.TryFind key |> Option.defaultValue ""
                        if value <> previous then
                            let detail = $"{project}: {key}: {previous} -> {value}"
                            match key.Split(':') |> Array.last with
                            | "TargetFrameworks" -> ()
                            | "AxialGuardrailsEnabled" when not (String.Equals(value, "false", StringComparison.OrdinalIgnoreCase)) -> remove "project settings" detail
                            | "IsTestProject" when not (String.Equals(value, "true", StringComparison.OrdinalIgnoreCase)) -> remove "project settings" detail
                            | "AxialGuardrail" ->
                                let oldItems = previous.Split(';', StringSplitOptions.RemoveEmptyEntries) |> Set.ofArray
                                let newItems = value.Split(';', StringSplitOptions.RemoveEmptyEntries) |> Set.ofArray
                                if not (Set.isSubset oldItems newItems) then add "project settings" detail
                            | _ -> add "project settings" detail
                for KeyValue(project, _) in before.Projects do
                    if not (after.Projects.ContainsKey project) then remove "project settings" project
                Ok { Since = point; Loosened = List.ofSeq added; Removed = List.ofSeq removed }
            finally Directory.Delete(historicalRoot, true)
        with error -> Error error.Message

    let format result =
        let groups = result.Loosened |> List.groupBy _.Group |> List.collect (fun (group, entries) -> (group + ":") :: (entries |> List.map (fun entry -> "  " + entry.Detail)))
        [ yield $"Policy review since {result.Since}"
          if result.Loosened.IsEmpty then yield "No policy loosening."
          yield! groups
          for entry in result.Removed do yield $"removed {entry.Group}: {entry.Detail}" ] |> String.concat Environment.NewLine
