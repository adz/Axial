/// The table of ambient .NET effects that Axial's packages ask callers not to use directly.
///
/// Each rule belongs to a guardrail, named after the package whose service replaces the effect (`Axial.HttpClient`
/// for `new HttpClient`). A rule names the capability that owns the API (if one does), a symbol
/// match, where the match applies, a human message, and the explicit replacement. Which guardrails run, and at which
/// severity, comes from `GuardrailConfig`.
#if AXIAL_TOOL
// Axial.Tool links this file for `axial capabilities`; internal there, so no assembly exposes two copies of its types.
module internal Axial.Guardrails.EffectCatalog
#else
module Axial.Guardrails.EffectCatalog
#endif

/// How a banned symbol is recognized against a resolved FSharpSymbolUse.
type SymbolMatch =
    /// The symbol's declaring entity full name equals this, and it is a constructor call.
    | ConstructorOf of entityFullName: string
    /// The symbol's declaring entity full name equals this, and the compiled member name is one of these.
    | MembersOf of entityFullName: string * memberNames: string list
    /// The symbol's declaring entity full name equals this; any member matches (wildcard).
    | AnyMemberOf of entityFullName: string

/// Where a rule's match counts as a finding.
type RuleScope =
    /// Anywhere in the file.
    | Anywhere
    /// Only inside a `flow { }` computation expression.
    | InsideFlow
    /// Only outside a `flow { }` computation expression.
    | OutsideFlow

type EffectRule =
    { Id: string
      Guardrail: string
      Match: SymbolMatch
      Scope: RuleScope
      Message: string
      Replacement: string
      /// The capability that owns the matched API, by name: files that claim it may call the API, and its advice is
      /// rendered into the finding. None for a rule no capability owns.
      Capability: string option }

/// How a capability treats a use of its API outside its implementation.
type CapabilityLevel =
    /// An AXG001 finding at the file role's `effects.ambient` level, like Axial's own rules. The default.
    | Enforce
    /// An info-level hint, for adopting a capability before its callers are migrated.
    | Guide

/// A service that replaces a raw API: what a caller uses instead, how it reaches the code, and how tests fake it.
/// Axial's own are compiled in below; anyone else's is defined in a `.capability.toml` file that the build names with
/// an `<AxialCapabilityDefinition>` item.
type Capability =
    { Name: string
      /// The guardrail its rules belong to; enabling the guardrail enables them.
      Guardrail: string
      /// The type or module callers use instead, such as `IFileSystem` or `Clock`.
      Service: string
      /// The environment interface that carries the service. Empty for a platform service the runtime provides.
      Env: string
      /// The environment record field that implements `Env`, such as `FileSystem: IFileSystem`.
      Field: string
      /// One call through the service, shown as an example.
      Example: string
      /// The production implementation.
      Live: string
      /// The test implementation.
      Fake: string
      Level: CapabilityLevel
      /// Full paths of the files that claim the capability with `AxialCapability` metadata on their `Compile` item:
      /// the only files that may call its API.
      Implementation: string list }

/// The guardrail that carries Axial's own conventions: the structural analyzers (raise in flow, fixtures, discarded
/// cancellation, reflection formatting, suppression integrity) and the effects the core runtime replaces.
[<Literal>]
let CoreGuardrail = "Axial"

let private rule id guardrail capability matching message replacement =
    { Id = id
      Guardrail = guardrail
      Match = matching
      Scope = Anywhere
      Message = message
      Replacement = replacement
      Capability = Some capability }

/// A rule that registers an id for a dedicated analyzer. No capability owns it, and no symbol ever matches it.
let private marker id matching message replacement =
    { rule id CoreGuardrail "" matching message replacement with Capability = None }

let private platform name service live fake =
    { Name = name; Guardrail = CoreGuardrail; Service = service; Env = ""; Field = ""; Example = ""; Live = live; Fake = fake; Level = Enforce; Implementation = [] }

let private service name guardrail serviceType envType field example live fake =
    { Name = name; Guardrail = guardrail; Service = serviceType; Env = envType; Field = field; Example = example; Live = live; Fake = fake; Level = Enforce; Implementation = [] }

/// Axial's own capabilities. Platform services come from the runtime; the others travel in `'env`.
let builtInCapabilities: Capability list =
    [ platform "clock" "Clock" "Clock.live" "the manual clock of Flow.runTest (TestClock.advance)"
      // The runtime's own bridges: running a flow from blocking host code, and sizing work by the machine.
      platform "blocking" "Flow.fromTask (let!/do!), with RunSynchronously or StartAsTask only at the host edge" "Flow.RunSynchronously" ""
      platform "processors" "Parallelism.ofProcessors" "Parallelism.ofProcessors" "an explicit size passed to Parallelism"
      // Secrets, tokens and nonces: no fake can be secure, so the live API stays in the files that claim it.
      platform "secure-random" "A secure-random file (System.Security.Cryptography)" "RandomNumberGenerator" "a fixed key or nonce passed in"
      { platform "environment" "ConfigSource.environment" "ConfigSource.environment prefix" "ConfigSource.environmentOf prefix pairs" with
          Guardrail = "Axial.Configuration" }
      platform "random" "Random" "Random.live" "the seeded TestPlatform.Random of Flow.runTest"
      platform "guid" "Guid" "Guid.live" "the sequential TestPlatform.Guid of Flow.runTest"
      platform "timezone" "TimeZone" "TimeZone.local" "TestPlatform's UTC time zone under Flow.runTest"
      service "console" "Axial.Console" "IConsole" "IHasConsole" "Console: IConsole" "Console.writeLine \"ready\"" "Console.live" "TestConsole()"
      service "filesystem" "Axial.FileSystem" "IFileSystem" "IHasFileSystem" "FileSystem: IFileSystem" "FileSystem.readAllText path" "FileSystem.live" "TestFileSystem()"
      service "process" "Axial.Process" "IProcess" "IHasProcess" "Process: IProcess" "cmd $\"git status\" |> Process.run" "Process.live fileSystem console" "Process.test ()"
      service "http" "Axial.HttpClient" "IHttp" "IHasHttp" "Http: IHttp" "Http.get url |> Http.send" "Http.live sharedClient" "TestHttp()"
      service "websocket" "Axial.WebSocket" "IWebSocket" "IHasWebSocket" "WebSocket: IWebSocket" "WebSocket.connect url" "WebSocket.live ()" "TestWebSocket inbound" ]

let private builtInRuleTable: EffectRule list =
    [ rule "random.new" CoreGuardrail "random" (ConstructorOf "System.Random")
          "constructs System.Random directly, which makes behavior depend on ambient, untestable randomness; secrets, tokens, and nonces come from System.Security.Cryptography in a file that claims the secure-random capability"
          "Axial.IRandom (Random.service / Random.nextDouble); for secrets, System.Security.Cryptography in a file that claims the secure-random capability"

      rule "random.cryptographic" CoreGuardrail "secure-random"
          (MembersOf(
              "System.Security.Cryptography.RandomNumberGenerator",
              [ "Create"; "GetBytes"; "GetNonZeroBytes"; "Fill"; "GetInt32"; "GetHexString"; "GetString"; "GetItems"; "Shuffle" ]
          ))
          "uses RandomNumberGenerator directly, which makes security-sensitive values depend on ambient, untestable randomness"
          "System.Security.Cryptography in a file that claims the secure-random capability"

      rule "guid.new" CoreGuardrail "guid" (MembersOf("System.Guid", [ "NewGuid" ]))
          "calls Guid.NewGuid() directly, which makes generated identifiers untestable"
          "Axial.IGuid (Guid.service / Guid.newGuid)"

      rule "clock.datetime" CoreGuardrail "clock"
          (MembersOf("System.DateTime", [ "get_Now"; "Now"; "get_UtcNow"; "UtcNow"; "get_Today"; "Today" ]))
          "reads the ambient system clock through System.DateTime, which makes timing untestable"
          "Axial.IClock (Clock.now)"

      rule "clock.datetimeoffset" CoreGuardrail "clock"
          (MembersOf("System.DateTimeOffset", [ "get_Now"; "Now"; "get_UtcNow"; "UtcNow" ]))
          "reads the ambient system clock through System.DateTimeOffset, which makes timing untestable"
          "Axial.IClock (Clock.now)"

      rule "clock.local-timezone" CoreGuardrail "timezone"
          (MembersOf("System.TimeZoneInfo", [ "get_Local"; "Local" ]))
          "reads TimeZoneInfo.Local, which depends on the machine's configured time zone"
          "TimeZone.local, or TimeZone.toLocal with an explicit TimeZone value"

      rule "clock.stopwatch" CoreGuardrail "clock" (AnyMemberOf "System.Diagnostics.Stopwatch")
          "measures time with System.Diagnostics.Stopwatch directly, which makes durations untestable"
          "Axial.IClock (Clock.timed / Clock.elapsed)"

      rule "environment.state" "Axial.Configuration" "environment"
          (MembersOf(
              "System.Environment",
              [ "GetEnvironmentVariable"
                "GetEnvironmentVariables"
                "SetEnvironmentVariable"
                "get_MachineName"
                "MachineName"
                "get_UserName"
                "UserName"
                "get_OSVersion"
                "OSVersion"
                "get_CurrentDirectory"
                "CurrentDirectory" ]
          ))
          "reads or writes ambient process/OS environment state directly"
          "ConfigSource.environment / Config.required from Axial.Configuration, or an explicit config value passed through 'env"

      rule "environment.processor-count" CoreGuardrail "processors"
          (MembersOf("System.Environment", [ "get_ProcessorCount"; "ProcessorCount" ]))
          "reads Environment.ProcessorCount directly to size concurrency"
          "Parallelism.ofProcessors (fun n -> ...)"

      rule "clock.task-delay" CoreGuardrail "clock" (MembersOf("System.Threading.Tasks.Task", [ "Delay" ]))
          "calls Task.Delay directly, which makes scheduled waits untestable and bypasses fiber interruption"
          "Flow.sleep / Schedule, or Axial.IClock.Sleep for a raw delay"

      rule "sleep.thread" CoreGuardrail "clock" (MembersOf("System.Threading.Thread", [ "Sleep" ]))
          "blocks a thread with Thread.Sleep, an ambient and untestable delay outside the fiber scheduler"
          "Flow.sleep / Schedule"

      { rule "blocking.get-result" CoreGuardrail "blocking"
            (MembersOf("System.Runtime.CompilerServices.TaskAwaiter`1", [ "GetResult" ]))
            "blocks a thread on a task with .GetAwaiter().GetResult() inside a flow, which stalls the workflow and ignores interruption"
            "let! (bind the task with Flow.fromTask / ColdTask), or Flow.fromBlocking for synchronous work" with
            Scope = InsideFlow }

      { rule "blocking.get-result-unit" CoreGuardrail "blocking"
            (MembersOf("System.Runtime.CompilerServices.TaskAwaiter", [ "GetResult" ]))
            "blocks a thread on a task with .GetAwaiter().GetResult() inside a flow, which stalls the workflow and ignores interruption"
            "do! (bind the task with Flow.fromTask / ColdTask), or Flow.fromBlocking for synchronous work" with
            Scope = InsideFlow }

      { rule "blocking.task-result" CoreGuardrail "blocking"
            (MembersOf("System.Threading.Tasks.Task`1", [ "get_Result"; "Result" ]))
            "blocks a thread on a task with .Result inside a flow, which stalls the workflow and ignores interruption"
            "let! (bind the task with Flow.fromTask / ColdTask), or Flow.fromBlocking for synchronous work" with
            Scope = InsideFlow }

      { rule "blocking.async" CoreGuardrail "blocking"
            (MembersOf("Microsoft.FSharp.Control.FSharpAsync", [ "RunSynchronously" ]))
            "blocks the calling thread waiting for an async computation"
            "Async.StartAsTask and await the task at the host boundary" with Scope = OutsideFlow }

      { rule "blocking.task-wait" CoreGuardrail "blocking"
            (MembersOf("System.Threading.Tasks.Task", [ "Wait" ]))
            "blocks the calling thread waiting for a task"
            "await the task asynchronously" with Scope = OutsideFlow }

      { rule "blocking.task-result-outside" CoreGuardrail "blocking"
            (MembersOf("System.Threading.Tasks.Task`1", [ "get_Result"; "Result" ]))
            "blocks the calling thread waiting for a task result"
            "await the task asynchronously" with Scope = OutsideFlow }

      { rule "blocking.value-task-result" CoreGuardrail "blocking"
            (MembersOf("System.Threading.Tasks.ValueTask`1", [ "get_Result"; "Result" ]))
            "blocks the calling thread waiting for a value task result"
            "await the value task asynchronously" with Scope = OutsideFlow }

      { rule "blocking.get-result-outside" CoreGuardrail "blocking"
            (MembersOf("System.Runtime.CompilerServices.TaskAwaiter`1", [ "GetResult" ]))
            "blocks the calling thread waiting for a task"
            "await the task asynchronously" with Scope = OutsideFlow }

      { rule "blocking.get-result-unit-outside" CoreGuardrail "blocking"
            (MembersOf("System.Runtime.CompilerServices.TaskAwaiter", [ "GetResult" ]))
            "blocks the calling thread waiting for a task"
            "await the task asynchronously" with Scope = OutsideFlow }

      // Dedicated analyzers implement AXG009–AXG012; these entries register their ids.
      marker "concurrency.raw" (MembersOf("Axial.__RawConcurrency", []))
          "uses raw concurrency" "Flow concurrency or a reasoned OS bridge"
      marker "exception.swallowed" (MembersOf("Axial.__SwallowedException", []))
          "discards an exception" "propagate, record, or report the exception"
      marker "cancellation.none" (MembersOf("Axial.__UncancellableWork", []))
          "disables cancellation" "the owning cancellation token"
      marker "concurrency.discarded" (MembersOf("Axial.__DiscardedWork", []))
          "discards concurrent work" "await or retain ownership"
      // AXG013 is structural; no ambient-effect symbol match is registered here.
      marker "test.wait-for-facts" (MembersOf("Axial.__WaitForFacts", []))
          "Wait for a fact, not elapsed time."
          "Complete a Deferred or TaskCompletionSource and wait with a deadline, or step the test clock."

      // AXG008 is syntactic; this entry registers its id.
      marker "scope.run" (MembersOf("Axial.__ScopeFromRun", []))
          "returns a scope-owned service" "use the service within its run"

      rule "console.any" "Axial.Console" "console" (AnyMemberOf "System.Console")
          "touches System.Console directly, which makes output/input an untestable ambient effect"
          "Axial.Console's IConsole service"

      rule "filesystem.file" "Axial.FileSystem" "filesystem" (AnyMemberOf "System.IO.File")
          "touches System.IO.File directly, bypassing the explicit filesystem service"
          "Axial.FileSystem's IFileSystem service"

      rule "filesystem.directory" "Axial.FileSystem" "filesystem" (AnyMemberOf "System.IO.Directory")
          "touches System.IO.Directory directly, bypassing the explicit filesystem service"
          "Axial.FileSystem's IFileSystem service"

      rule "filesystem.watcher" "Axial.FileSystem" "filesystem" (ConstructorOf "System.IO.FileSystemWatcher")
          "constructs a filesystem watcher directly, bypassing the explicit filesystem service"
          "Axial.FileSystem's IFileSystem.Watch service"

      rule "process.start" "Axial.Process" "process" (MembersOf("System.Diagnostics.Process", [ "Start" ]))
          "starts an OS process directly, bypassing the explicit process service"
          "Axial.Process's IProcess service"

      rule "websocket.new-client" "Axial.WebSocket" "websocket" (ConstructorOf "System.Net.WebSockets.ClientWebSocket")
          "constructs a WebSocket outside the mockable service boundary" "Axial.WebSocket IWebSocket service"
      rule "http.new-client" "Axial.HttpClient" "http" (ConstructorOf "System.Net.Http.HttpClient")
          "constructs HttpClient directly, which exhausts sockets when repeated and cannot be replaced in tests"
          "Axial.HttpClient's IHttp service (Http.live with one shared HttpClient)" ]

let builtInRules: EffectRule list =
    builtInRuleTable

/// Every guardrail that Axial.Guardrails ships rules for.
let builtInGuardrails: Set<string> =
    builtInRules |> List.map _.Guardrail |> Set.ofList |> Set.add CoreGuardrail

/// Parses a symbol match written in a rules file: `ctor:Type`, `member:Type::A,B`, or `any:Type`.
let parseMatch (text: string) : SymbolMatch option =
    let text = text.Trim()

    if text.StartsWith "ctor:" then
        Some(ConstructorOf(text.Substring 5))
    elif text.StartsWith "any:" then
        Some(AnyMemberOf(text.Substring 4))
    elif text.StartsWith "member:" then
        match text.Substring(7).Split([| "::" |], System.StringSplitOptions.None) with
        | [| entity; members |] ->
            let names = members.Split(',') |> Array.map _.Trim() |> Array.filter ((<>) "") |> List.ofArray
            if List.isEmpty names then None else Some(MembersOf(entity, names))
        | _ -> None
    else
        None

let private describe =
    function
    | ConstructorOf entity -> $"constructs {entity} directly"
    | MembersOf(entity, members) -> $"""calls {entity}.{String.concat "/" (members |> List.map (fun m -> m.Replace("get_", "")))} directly"""
    | AnyMemberOf entity -> $"uses {entity} directly"

/// Reads the small TOML subset a capability definition uses: `key = "text"` and `key = [ "a", "b" ]` (one line or
/// several), with `#` comments. Anything else is an error, so a typo never silently drops a field.
let private readDefinitionTable (text: string) : Result<Map<string, string list>, string> =
    let lines = text.Replace("\r\n", "\n").Split('\n')
    let strings (lineNumber: int) (body: string) =
        // Quoted strings separated by commas; nothing else is allowed between them.
        let rec scan (index: int) (found: string list) (expectValue: bool) =
            if index >= body.Length then Ok(List.rev found, expectValue)
            else
                match body[index] with
                | ' ' | '\t' -> scan (index + 1) found expectValue
                | ',' when not expectValue -> scan (index + 1) found true
                | '#' -> Ok(List.rev found, expectValue)
                | '"' when expectValue ->
                    let close = body.IndexOf('"', index + 1)
                    if close < 0 then Error $"line {lineNumber}: unterminated string"
                    else scan (close + 1) (body.Substring(index + 1, close - index - 1) :: found) false
                | other -> Error $"line {lineNumber}: unexpected '{other}'"
        scan 0 [] true
    let rec go (index: int) (table: Map<string, string list>) =
        if index >= lines.Length then Ok table
        else
            let line = lines[index].Trim()
            let lineNumber = index + 1
            if line = "" || line.StartsWith "#" then go (index + 1) table
            else
                match line.IndexOf '=' with
                | separator when separator > 0 ->
                    let key = line.Substring(0, separator).Trim()
                    let value = line.Substring(separator + 1).Trim()
                    if table.ContainsKey key then Error $"line {lineNumber}: '{key}' is set more than once"
                    elif value.StartsWith "[" then
                        // Gather the array up to its closing bracket, across lines.
                        let rec gather (at: int) (acc: string) =
                            let close = acc.IndexOf ']'
                            if close >= 0 then Ok(acc.Substring(0, close), acc.Substring(close + 1), at)
                            elif at + 1 >= lines.Length then Error $"line {lineNumber}: '{key}' has no closing ]"
                            else gather (at + 1) (acc + "\n" + lines[at + 1])
                        match gather index (value.Substring 1) with
                        | Error error -> Error error
                        | Ok(inside, rest, last) when rest.Trim() <> "" && not (rest.Trim().StartsWith "#") ->
                            Error $"line {last + 1}: unexpected text after ]"
                        | Ok(inside, _, last) ->
                            let items =
                                inside.Split('\n')
                                |> Array.map (fun part -> strings lineNumber part)
                                |> Array.fold (fun state next ->
                                    match state, next with
                                    | Ok acc, Ok(found, _) -> Ok(acc @ found)
                                    | Error e, _ | _, Error e -> Error e) (Ok [])
                            match items with
                            | Ok found -> go (last + 1) (table.Add(key, found))
                            | Error error -> Error error
                    else
                        match strings lineNumber value with
                        | Ok([ single ], false) -> go (index + 1) (table.Add(key, [ single ]))
                        | Ok _ -> Error $"line {lineNumber}: '{key}' must be one quoted string or a [ ] list"
                        | Error error -> Error error
                | _ -> Error $"line {lineNumber}: expected key = value"
    go 0 Map.empty

let private definitionKeys = set [ "name"; "service"; "env"; "field"; "example"; "live"; "fake"; "level"; "guardrail"; "replaces" ]

/// Parses a capability definition file (`<name>.capability.toml`) into the capability and the rules derived from its
/// `replaces` list. Defaults: `env` is `IHas<Name>`, `field` is `<Name>: <Service>` where `<Name>` is the service
/// without a leading `I`, the level is `enforce`, and the guardrail is the capability's name. Files claim it with
/// `AxialCapability` metadata on their `Compile` item.
let parseDefinition (text: string) : Result<Capability * EffectRule list, string> =
    match readDefinitionTable text with
    | Error error -> Error error
    | Ok table ->
        let one key = table.TryFind key |> Option.bind List.tryHead |> Option.map _.Trim() |> Option.defaultValue ""
        let name = one "name"
        let serviceType = one "service"
        let bare = if serviceType.Length > 1 && serviceType[0] = 'I' && System.Char.IsUpper serviceType[1] then serviceType.Substring 1 else serviceType
        let orDefault fallback value = if value = "" then fallback else value
        let replaces = table.TryFind "replaces" |> Option.defaultValue [] |> List.map _.Trim() |> List.filter ((<>) "")
        let parsed = replaces |> List.map (fun text -> text, parseMatch text)
        let unknown = table |> Map.keys |> Seq.filter (fun key -> not (definitionKeys.Contains key)) |> List.ofSeq
        let level =
            match (one "level").ToLowerInvariant() with
            | "" | "enforce" -> Ok Enforce
            | "guide" -> Ok Guide
            | other -> Error $"capability '{name}': level must be guide or enforce, not '{other}'"
        match level, parsed |> List.tryFind (snd >> Option.isNone) with
        | _ when not unknown.IsEmpty ->
            Error $"""unknown key {String.concat ", " unknown}; a definition has {String.concat ", " (Set.toList definitionKeys)}"""
        | Error error, _ -> Error error
        | _ when name = "" -> Error "the definition has no name"
        | _ when serviceType = "" -> Error $"capability '{name}' has no service"
        | _ when replaces.IsEmpty -> Error $"capability '{name}' replaces nothing: list the APIs its service wraps, such as replaces = [ \"any:Acme.Gateway.Client\" ]"
        | _, Some(text, _) -> Error $"capability '{name}': cannot read replaces entry '{text}'; use ctor:Type, member:Type::A,B or any:Type"
        | Ok level, None ->
            let capability =
                { Name = name
                  Guardrail = one "guardrail" |> orDefault name
                  Service = serviceType
                  Env = one "env" |> orDefault ("IHas" + bare)
                  Field = one "field" |> orDefault (bare + ": " + serviceType)
                  Example = one "example"
                  Live = one "live"
                  Fake = one "fake"
                  Level = level
                  Implementation = [] }
            let rules =
                parsed
                |> List.mapi (fun index (_, matching) ->
                    let matching = Option.get matching
                    { Id = $"{name}.{index + 1}"
                      Guardrail = capability.Guardrail
                      Match = matching
                      Scope = Anywhere
                      Message = $"{describe matching}, which the {name} capability wraps"
                      Replacement = serviceType
                      Capability = Some name })
            Ok(capability, rules)

/// What a rule matches, for listings: a type, or a type's named members.
let ruleTarget (rule: EffectRule) =
    match rule.Match with
    | MembersOf(entity, members) ->
        let names = members |> List.map (fun name -> name.Replace("get_", "")) |> List.distinct |> String.concat "/"
        $"{entity}.{names}"
    | ConstructorOf entity -> $"new {entity}"
    | AnyMemberOf entity -> entity

/// The type a match targets, for overlap checks.
let matchedType =
    function
    | ConstructorOf entity
    | MembersOf(entity, _)
    | AnyMemberOf entity -> entity

/// A match in the rules-file grammar that `parseMatch` reads.
let formatMatch =
    function
    | ConstructorOf entity -> $"ctor:{entity}"
    | MembersOf(entity, members) -> $"""member:{entity}::{String.concat "," members}"""
    | AnyMemberOf entity -> $"any:{entity}"
