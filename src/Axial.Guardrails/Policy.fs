namespace Axial.Guardrails

open System
open System.IO
open System.Text
open System.Text.RegularExpressions

/// The closed, versioned Guardrails v2 repository policy.
module Policy =
    type Role = Domain | Effects | Host | Test
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
          Protected: string list }

    type ResolvedFile =
        { Role: Role
          Rules: Map<string, RuleLevel> }

    type private Value = String of string | Integer of int | Strings of string list

    let private roleName = function Domain -> "domain" | Effects -> "effects" | Host -> "host" | Test -> "test"

    let private parseRole value =
        match value with
        | "domain" -> Ok Domain | "effects" -> Ok Effects | "host" -> Ok Host | "test" -> Ok Test
        | _ -> Error $"unknown role '{value}'; expected domain, effects, host, or test"

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
        let mutable entries = Map.empty
        let mutable error = None
        for number, original in text.Replace("\r", "").Split('\n') |> Array.indexed do
            let lineNumber = number + 1
            let line = stripComment original
            if error.IsNone && line <> "" then
                if line.StartsWith("[") && line.EndsWith("]") then
                    table <- line.Substring(1, line.Length - 2).Trim()
                    if table = "" then error <- Some $"line {lineNumber}: empty table name"
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

    let private normalizePath name (path: string) =
        let normalized = path.Replace('\\', '/').TrimStart('/')
        if Path.IsPathRooted path || normalized.Split('/') |> Array.exists ((=) "..") then
            Error $"{name} path '{path}' escapes the repository root"
        else Ok normalized

    let private filesUnder root =
        Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)
        |> Seq.map (fun path -> Path.GetRelativePath(root, path).Replace('\\', '/'))
        |> Seq.toArray

    let private validatePattern root allFiles allowEmpty pattern =
        normalizePath "policy" pattern
        |> Result.bind (fun normalized ->
            let prefix = normalized.Split([| '*'; '?' |], 2)[0]
            let candidate = Path.GetFullPath(Path.Combine(root, prefix.Replace('/', Path.DirectorySeparatorChar)))
            let physical =
                if File.Exists candidate then FileInfo(candidate).ResolveLinkTarget(true)
                elif Directory.Exists candidate then DirectoryInfo(candidate).ResolveLinkTarget(true)
                else null
            if not (isNull physical) && not (physical.FullName.StartsWith(Path.GetFullPath(root) + string Path.DirectorySeparatorChar, StringComparison.Ordinal)) then
                Error $"policy path '{pattern}' follows a symlink outside the repository root"
            elif not allowEmpty && not (allFiles |> Array.exists (fun (path: string) -> (globRegex normalized).IsMatch(path))) then
                Error $"policy path '{pattern}' matches nothing"
            else Ok normalized)

    let parse (repositoryRoot: string) (knownRuleIds: Set<string>) (text: string) : Result<Config, string list> =
        let fail message = Error [ message ]
        match parseDocument text with
        | Error message -> fail message
        | Ok entries ->
            let allowed path =
                path = "version" || path = "baseline" || path = "default.role" || path = "protected.paths"
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
                match version, getString "default.role" true, getString "baseline" false, getStrings "protected.paths" with
                | Error e, _, _, _ | _, Error e, _, _ | _, _, Error e, _ | _, _, _, Error e -> fail e
                | Ok version, Ok(Some defaultName), Ok baseline, Ok protectedPaths ->
                    match parseRole defaultName with
                    | Error e -> fail e
                    | Ok defaultRole ->
                        let allFiles = filesUnder repositoryRoot
                        let assignments prefix allowEffectsEmpty =
                            entries |> Map.toList |> List.choose (fun (key, value) ->
                                if key.StartsWith(prefix + ".") then Some(key.Substring(prefix.Length + 1), value) else None)
                            |> List.fold (fun state (name, value) ->
                                match state, parseRole name, value with
                                | Error e, _, _ -> Error e
                                | _, Error e, _ -> Error e
                                | _, _, Strings patterns ->
                                    patterns |> List.fold (fun paths pattern -> paths |> Result.bind (fun xs -> validatePattern repositoryRoot allFiles (allowEffectsEmpty && name = "effects") pattern |> Result.map (fun p -> p :: xs))) (Ok [])
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
                                protectedPaths |> List.fold (fun state p -> state |> Result.bind (fun xs -> validatePattern repositoryRoot allFiles false p |> Result.map (fun x -> x::xs))) (Ok [])
                                |> Result.mapError List.singleton
                                |> Result.map (fun validatedPaths -> { Version = version; Baseline = baseline; DefaultRole = defaultRole; Projects = projects; Files = files; Rules = rules; Protected = List.rev validatedPaths })
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
