namespace Axial.Guardrails

open System
open System.IO
open System.Text
open System.Text.RegularExpressions

/// The closed, versioned Guardrails v2 repository policy.
module Policy =
    type Role = Domain | Capabilities | Services | Host | Test
    type RuleLevel = Allow | Warn | Deny | Forbid

    type Assignment =
        { Role: Role
          Patterns: string list }

    type Config =
        { Version: int
          Baseline: string option
          DefaultRole: Role
          Projects: Assignment list
          Files: Assignment list
          Rules: Map<Role, Map<string, RuleLevel>>
          Protected: string list
          Boundaries: string list
          SuppressionIgnores: string list }

    type ResolvedFile =
        { Role: Role
          Rules: Map<string, RuleLevel> }

    type private Value = String of string | Integer of int | Strings of string list

    let private roleName = function Domain -> "domain" | Capabilities -> "capabilities" | Services -> "services" | Host -> "host" | Test -> "test"

    let private parseRole value =
        match value with
        | "domain" -> Ok Domain | "capabilities" -> Ok Capabilities | "services" -> Ok Services | "host" -> Ok Host | "test" -> Ok Test
        | _ -> Error $"unknown role '{value}'; expected capabilities, services, domain, host, or test"

    let private parseLevel value =
        match value with
        | "allow" -> Ok Allow | "warn" -> Ok Warn | "deny" -> Ok Deny | "forbid" -> Ok Forbid
        | _ -> Error $"invalid rule level '{value}'; expected allow, warn, deny, or forbid"

    let private unquote lineNumber (text: string) =
        let text = text.Trim()
        if text.Length >= 2 && text[0] = '"' && text[text.Length - 1] = '"' then
            let inner = text.Substring(1, text.Length - 2)
            if inner.Contains('"') || inner.Contains('\\') then Error $"line {lineNumber}: escapes are not supported"
            else Ok inner
        else Error $"line {lineNumber}: expected a quoted string"

    let private parseValue lineNumber (text: string) =
        let text = text.Trim()
        match Int32.TryParse text with
        | true, value -> Ok(Integer value)
        | _ when text.StartsWith("[") && text.EndsWith("]") ->
            let body = text.Substring(1, text.Length - 2).Trim()
            if body = "" then Ok(Strings [])
            else
                body.Split(',')
                |> Array.map (unquote lineNumber)
                |> Array.fold (fun state item ->
                    match state, item with
                    | Ok values, Ok value -> Ok(value :: values)
                    | Error error, _ | _, Error error -> Error error) (Ok [])
                |> Result.map (List.rev >> Strings)
        | _ -> unquote lineNumber text |> Result.map String

    let private stripComment (line: string) =
        let mutable quoted = false
        let mutable cut = line.Length
        for index in 0 .. line.Length - 1 do
            if line[index] = '"' then quoted <- not quoted
            elif line[index] = '#' && not quoted && cut = line.Length then cut <- index
        line.Substring(0, cut).Trim()

    let private parseDocument (text: string) =
        let mutable table = ""
        let mutable tables = Set.empty
        let mutable entries = Map.empty
        let mutable error = None
        for number, original in text.Replace("\r", "").Split('\n') |> Array.indexed do
            let lineNumber = number + 1
            let line = stripComment original
            if error.IsNone && line <> "" then
                if line.StartsWith("[") && line.EndsWith("]") then
                    table <- line.Substring(1, line.Length - 2).Trim()
                    let allowedTable =
                        table = "default" || table = "projects" || table = "files" || table = "protected" || table = "boundaries" || table = "suppressions"
                        || (table.StartsWith("roles.") && table.EndsWith(".rules") && table.Split('.').Length = 3)
                    if table = "" then error <- Some $"line {lineNumber}: empty table name"
                    elif not allowedTable then error <- Some $"line {lineNumber}: unknown policy table '{table}'"
                    elif tables.Contains table then error <- Some $"line {lineNumber}: table '{table}' is already declared; combine its entries under one declaration"
                    else tables <- tables.Add table
                else
                    let separator = line.IndexOf('=')
                    if separator < 1 then error <- Some $"line {lineNumber}: expected key = value"
                    else
                        let rawKey = line.Substring(0, separator).Trim()
                        let keyResult = if rawKey.StartsWith("\"") then unquote lineNumber rawKey else Ok rawKey
                        match keyResult, parseValue lineNumber (line.Substring(separator + 1)) with
                        | Ok key, Ok value ->
                            let path = if table = "" then key else table + "." + key
                            if entries.ContainsKey path then error <- Some $"line {lineNumber}: duplicate declaration '{path}'"
                            else entries <- entries.Add(path, value)
                        | Error message, _ | _, Error message -> error <- Some message
        match error with Some message -> Error message | None -> Ok entries

    let private globRegex (pattern: string) =
        let output = StringBuilder("^")
        let mutable index = 0
        while index < pattern.Length do
            if index + 2 < pattern.Length && pattern[index] = '*' && pattern[index + 1] = '*' && pattern[index + 2] = '/' then
                output.Append("(?:.*/)?") |> ignore
                index <- index + 3
            elif index + 1 < pattern.Length && pattern[index] = '*' && pattern[index + 1] = '*' then
                output.Append(".*") |> ignore
                index <- index + 2
            elif pattern[index] = '*' then
                output.Append("[^/]*") |> ignore
                index <- index + 1
            elif pattern[index] = '?' then
                output.Append("[^/]") |> ignore
                index <- index + 1
            else
                output.Append(Regex.Escape(string pattern[index])) |> ignore
                index <- index + 1
        Regex(output.Append('$').ToString(), RegexOptions.CultureInvariant)

    /// Match a repository-relative path using policy glob syntax.
    let matchesGlob pattern (path: string) = (globRegex pattern).IsMatch(path)

    let private normalizePath name (path: string) =
        let normalized = path.Replace('\\', '/')
        if Path.IsPathRooted path || path.Contains('\\') || normalized.StartsWith('/') || normalized.Split('/') |> Array.exists (fun part -> part = ".." || part = "") then
            Error $"{name} path '{path}' must be a canonical repository-relative path with no '..' or backslashes"
        else Ok normalized

    let private canonicalRoot root = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar) + string Path.DirectorySeparatorChar
    let private insideRoot root path =
        let candidate = Path.GetFullPath(path)
        candidate = canonicalRoot(root).TrimEnd(Path.DirectorySeparatorChar) || candidate.StartsWith(canonicalRoot root, StringComparison.Ordinal)
    let private resolvedInsideRoot root path =
        let canonicalRootPath = canonicalRoot root |> _.TrimEnd(Path.DirectorySeparatorChar)
        let relative = Path.GetRelativePath(canonicalRootPath, Path.GetFullPath path)
        let mutable current = canonicalRootPath
        let mutable escaped = false
        for segment in relative.Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries) do
            current <- Path.Combine(current, segment)
            let info : FileSystemInfo = if Directory.Exists current then DirectoryInfo(current) else FileInfo(current)
            if info.Exists && info.Attributes.HasFlag FileAttributes.ReparsePoint then
                let resolved = info.ResolveLinkTarget(true)
                if not(isNull resolved) then current <- resolved.FullName
            if not(insideRoot canonicalRootPath current) then escaped <- true
        not escaped && insideRoot canonicalRootPath current

    let private filesUnder root =
        let skipped = set [ ".git"; ".livedocs"; "artifacts"; "bin"; "obj"; "output" ]
        let rec walk directory = seq {
            yield! Directory.EnumerateFiles(directory)
            for child in Directory.EnumerateDirectories(directory) do
                let info = DirectoryInfo child
                if not(skipped.Contains info.Name) && not(info.Attributes.HasFlag FileAttributes.ReparsePoint) then yield! walk child }
        walk root |> Seq.map (fun path -> Path.GetRelativePath(root, path).Replace('\\', '/')) |> Seq.sort |> Seq.toArray

    let private validatePattern root allFiles allowEmpty pattern =
        normalizePath "policy" pattern
        |> Result.bind (fun normalized ->
            let matches = allFiles |> Array.filter (fun (path: string) -> (globRegex normalized).IsMatch(path))
            match matches |> Array.tryFind (fun path -> not(resolvedInsideRoot root (Path.Combine(root, path.Replace('/', Path.DirectorySeparatorChar))))) with
            | Some escaped -> Error $"policy path '{pattern}' matches '{escaped}', which resolves outside the repository root"
            | None when not allowEmpty && matches.Length = 0 -> Error $"policy path '{pattern}' matches nothing"
            | None -> Ok normalized)

    let parse (repositoryRoot: string) (knownRuleIds: Set<string>) (text: string) : Result<Config, string list> =
        let fail message = Error [ message ]
        match parseDocument text with
        | Error message -> fail message
        | Ok entries ->
            let allowed path =
                path = "version" || path = "baseline" || path = "default.role" || path = "protected.paths" || path = "boundaries.files" || path = "suppressions.ignore"
                || path.StartsWith("projects.") || path.StartsWith("files.")
                || (path.StartsWith("roles.") && path.Contains(".rules."))
            match entries |> Map.toSeq |> Seq.tryFind (fst >> allowed >> not) with
            | Some(path, _) -> fail $"unknown policy key '{path}'"
            | None ->
                let getString key required =
                    match entries.TryFind key with
                    | Some(String value) -> Ok(Some value)
                    | None when not required -> Ok None
                    | None -> Error $"missing required key '{key}'"
                    | _ -> Error $"'{key}' must be a string"
                let getStrings key =
                    match entries.TryFind key with None -> Ok [] | Some(Strings values) -> Ok values | _ -> Error $"'{key}' must be an array of strings"
                let version = match entries.TryFind "version" with Some(Integer 2) -> Ok 2 | Some(Integer value) -> Error $"unsupported policy version {value}; expected 2" | Some _ -> Error "'version' must be integer 2" | None -> Error "missing required key 'version'"
                match version, getString "default.role" true, getString "baseline" false, getStrings "protected.paths", getStrings "boundaries.files", getStrings "suppressions.ignore" with
                | Error e, _, _, _, _, _ | _, Error e, _, _, _, _ | _, _, Error e, _, _, _ | _, _, _, Error e, _, _ | _, _, _, _, Error e, _ | _, _, _, _, _, Error e -> fail e
                | Ok version, Ok(Some defaultName), Ok baseline, Ok protectedPaths, Ok boundaryPaths, Ok suppressionIgnores ->
                    let baselineResult =
                        match baseline with
                        | None -> Ok None
                        | Some path ->
                            normalizePath "baseline" path
                            |> Result.bind (fun normalized ->
                                let target = Path.Combine(repositoryRoot, normalized.Replace('/', Path.DirectorySeparatorChar))
                                if not(insideRoot repositoryRoot target) || not(resolvedInsideRoot repositoryRoot target) then Error $"baseline path '{path}' resolves outside the repository root"
                                else Ok(Some normalized))
                    match parseRole defaultName, baselineResult with
                    | Error e, _ | _, Error e -> fail e
                    | Ok defaultRole, Ok baseline ->
                        let needsFiles = entries |> Map.exists (fun key _ -> key.StartsWith("projects.") || key.StartsWith("files.") || key = "protected.paths" || key = "boundaries.files" || key = "suppressions.ignore")
                        let allFiles = if needsFiles then filesUnder repositoryRoot else [||]
                        let assignments prefix allowCapabilitiesEmpty =
                            entries |> Map.toList |> List.choose (fun (key, value) ->
                                if key.StartsWith(prefix + ".") then Some(key.Substring(prefix.Length + 1), value) else None)
                            |> List.fold (fun state (name, value) ->
                                match state, parseRole name, value with
                                | Error e, _, _ -> Error e
                                | _, Error e, _ -> Error e
                                | _, _, Strings patterns ->
                                    patterns |> List.fold (fun paths pattern -> paths |> Result.bind (fun xs -> validatePattern repositoryRoot allFiles (allowCapabilitiesEmpty && name = "capabilities") pattern |> Result.map (fun p -> p :: xs))) (Ok [])
                                    |> Result.bind (fun paths -> state |> Result.map (fun xs -> { Role = (parseRole name |> function Ok role -> role | Error _ -> failwith "validated role"); Patterns = List.rev paths } :: xs))
                                | _, _, _ -> Error $"'{prefix}.{name}' must be an array of strings") (Ok [])
                            |> Result.map List.rev
                        match assignments "projects" false, assignments "files" true with
                        | Error e, _ | _, Error e -> fail e
                        | Ok projects, Ok files ->
                            let ruleEntries = entries |> Map.toList |> List.choose (fun (key, value) -> if key.StartsWith("roles.") then Some(key, value) else None)
                            let rulesResult =
                                ruleEntries |> List.fold (fun state (key, value) -> state |> Result.bind (fun rules ->
                                    let parts = key.Split('.')
                                    if parts.Length < 4 || parts[2] <> "rules" then Error $"unknown policy key '{key}'"
                                    else
                                        let roleText, ruleId = parts[1], String.Join('.', parts[3..])
                                        match parseRole roleText, value with
                                        | Error e, _ -> Error e
                                        | _, _ when not (knownRuleIds.Contains ruleId) -> Error $"unknown rule '{ruleId}'"
                                        | Ok role, String level -> parseLevel level |> Result.map (fun parsed -> rules |> Map.change role (fun old -> Some((defaultArg old Map.empty).Add(ruleId, parsed))))
                                        | _, _ -> Error $"'{key}' must be a rule level string")) (Ok Map.empty)
                            match rulesResult with
                            | Error e -> fail e
                            | Ok rules ->
                                let validatePaths kind paths =
                                    paths |> List.fold (fun state p -> state |> Result.bind (fun xs -> validatePattern repositoryRoot allFiles false p |> Result.map (fun x -> x::xs))) (Ok [])
                                    |> Result.map List.rev
                                let ignores =
                                    suppressionIgnores |> List.fold (fun state p ->
                                        state |> Result.bind (fun xs -> validatePattern repositoryRoot allFiles true p |> Result.map (fun x -> x :: xs))) (Ok [])
                                    |> Result.map List.rev
                                match validatePaths "protected" protectedPaths, validatePaths "boundary" boundaryPaths, ignores with
                                | Error e, _, _ | _, Error e, _ | _, _, Error e -> fail e
                                | Ok protectedFiles, Ok boundaries, Ok ignores ->
                                    Ok { Version = version; Baseline = baseline; DefaultRole = defaultRole; Projects = projects; Files = files; Rules = rules; Protected = protectedFiles; Boundaries = boundaries; SuppressionIgnores = ignores }
                | _ -> fail "invalid policy"

    let private matchingRoles (path: string) assignments =
        assignments |> List.choose (fun assignment -> if assignment.Patterns |> List.exists (fun pattern -> (globRegex pattern).IsMatch(path)) then Some assignment.Role else None) |> Set.ofList

    let resolve (config: Config) (projectPath: string) (filePath: string) : Result<ResolvedFile, string> =
        match normalizePath "project" projectPath, normalizePath "file" filePath with
        | Error e, _ | _, Error e -> Error e
        | Ok project, Ok file ->
            let choose label subject fallback roles =
                if Set.count roles > 1 then
                    let names = roles |> Seq.map roleName |> String.concat ", "
                    Error $"conflicting {label} role assignments for '{subject}': {names}"
                else Ok(if Set.isEmpty roles then fallback else Set.minElement roles)
            choose "project" project config.DefaultRole (matchingRoles project config.Projects)
            |> Result.bind (fun projectRole -> choose "file" file projectRole (matchingRoles file config.Files))
            |> Result.map (fun role -> { Role = role; Rules = config.Rules |> Map.tryFind role |> Option.defaultValue Map.empty })
