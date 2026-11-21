/// The table of ambient .NET effects that Axial's packages ask callers not to use directly.
///
/// Each rule belongs to a guardrail, named after the package whose service replaces the effect (`Axial.HttpClient`
/// for `new HttpClient`). A rule names a category for grouping/documentation, a symbol match,
/// where the match applies, a human message, and the explicit replacement. Which guardrails run, and at which
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
      Category: string
      Match: SymbolMatch
      Scope: RuleScope
      Message: string
      Replacement: string
      /// The capability that replaces the matched API, by name; its advice is rendered into the finding.
      Capability: string option }

/// How strongly a capability steers callers away from the API it replaces.
type CapabilityLevel =
    /// An info-level hint that names the service to use. It never fails a build and needs no acknowledgement.
    | Guide
    /// An AXG001 finding at the file role's `effects.ambient` level, like Axial's own rules.
    | Enforce

/// A service that replaces a raw API: what a caller uses instead, how it reaches the code, and how tests fake it.
/// Declared in code for Axial's own services and with `<AxialCapability>` items for anyone else's.
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
      Level: CapabilityLevel }

/// The guardrail that carries Axial's own conventions: the structural analyzers (raise in flow, fixtures, discarded
/// cancellation, reflection formatting, suppression integrity) and the effects the core runtime replaces.
[<Literal>]
let CoreGuardrail = "Axial"

let private rule id guardrail category matching message replacement =
    { Id = id
      Guardrail = guardrail
      Category = category
      Match = matching
      Scope = Anywhere
      Message = message
      Replacement = replacement
      Capability = None }

let private platform name service live fake =
    { Name = name; Guardrail = CoreGuardrail; Service = service; Env = ""; Field = ""; Example = ""; Live = live; Fake = fake; Level = Enforce }

let private service name guardrail serviceType envType field example live fake =
    { Name = name; Guardrail = guardrail; Service = serviceType; Env = envType; Field = field; Example = example; Live = live; Fake = fake; Level = Enforce }

/// Axial's own capabilities. Platform services come from the runtime; the others travel in `'env`.
let builtInCapabilities: Capability list =
    [ platform "clock" "Clock" "Clock.live" "the manual clock of Flow.runTest (TestClock.advance)"
      platform "random" "Random" "Random.live" "the seeded TestPlatform.Random of Flow.runTest"
      platform "guid" "Guid" "Guid.live" "the sequential TestPlatform.Guid of Flow.runTest"
      platform "timezone" "TimeZone" "TimeZone.local" "TestPlatform's UTC time zone under Flow.runTest"
      service "console" "Axial.Console" "IConsole" "IHasConsole" "Console: IConsole" "Console.writeLine \"ready\"" "Console.live" "TestConsole()"
      service "filesystem" "Axial.FileSystem" "IFileSystem" "IHasFileSystem" "FileSystem: IFileSystem" "FileSystem.readAllText path" "FileSystem.live" "TestFileSystem()"
      service "process" "Axial.Process" "IProcess" "IHasProcess" "Process: IProcess" "cmd $\"git status\" |> Process.run" "Process.live fileSystem console" "Process.test ()"
      service "http" "Axial.HttpClient" "IHttp" "IHasHttp" "Http: IHttp" "Http.get url |> Http.send" "Http.live sharedClient" "TestHttp()"
      service "websocket" "Axial.WebSocket" "IWebSocket" "IHasWebSocket" "WebSocket: IWebSocket" "WebSocket.connect url" "WebSocket.live ()" "TestWebSocket inbound" ]

let private capabilityOf (rule: EffectRule) =
    match rule.Category, rule.Id with
    | ("console" | "filesystem" | "process" | "http" | "websocket" | "random" | "guid" | "clock"), _ -> Some rule.Category
    | "sleep", _ -> Some "clock"
    | _, "clock.local-timezone" -> Some "timezone"
    | _ -> None

let private builtInRuleTable: EffectRule list =
    [ rule "random.new" CoreGuardrail "random" (ConstructorOf "System.Random")
          "constructs System.Random directly, which makes behavior depend on ambient, untestable randomness; use System.Security.Cryptography inside capability code for secrets, tokens, and nonces"
          "Axial.IRandom (Random.service / Random.nextDouble); for secrets, use System.Security.Cryptography inside capability code"

      rule "random.cryptographic" CoreGuardrail "secure-random"
          (MembersOf(
              "System.Security.Cryptography.RandomNumberGenerator",
              [ "Create"; "GetBytes"; "GetNonZeroBytes"; "Fill"; "GetInt32"; "GetHexString"; "GetString"; "GetItems"; "Shuffle" ]
          ))
          "uses RandomNumberGenerator directly, which makes security-sensitive values depend on ambient, untestable randomness"
          "use System.Security.Cryptography inside capability code"

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

      rule "clock.local-timezone" CoreGuardrail "environment"
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

      rule "environment.processor-count" CoreGuardrail "environment"
          (MembersOf("System.Environment", [ "get_ProcessorCount"; "ProcessorCount" ]))
          "reads Environment.ProcessorCount directly to size concurrency"
          "Parallelism.ofProcessors (fun n -> ...)"

      rule "clock.task-delay" CoreGuardrail "clock" (MembersOf("System.Threading.Tasks.Task", [ "Delay" ]))
          "calls Task.Delay directly, which makes scheduled waits untestable and bypasses fiber interruption"
          "Flow.sleep / Schedule, or Axial.IClock.Sleep for a raw delay"

      rule "sleep.thread" CoreGuardrail "sleep" (MembersOf("System.Threading.Thread", [ "Sleep" ]))
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

      // Dedicated analyzers implement AXG009–AXG012; catalog entries register their categories.
      rule "concurrency.raw" CoreGuardrail "concurrency" (MembersOf("Axial.__RawConcurrency", []))
          "uses raw concurrency" "Flow concurrency or a reasoned OS bridge"
      rule "exception.swallowed" CoreGuardrail "exception" (MembersOf("Axial.__SwallowedException", []))
          "discards an exception" "propagate, record, or report the exception"
      rule "cancellation.none" CoreGuardrail "cancellation" (MembersOf("Axial.__UncancellableWork", []))
          "disables cancellation" "the owning cancellation token"
      rule "concurrency.discarded" CoreGuardrail "concurrency" (MembersOf("Axial.__DiscardedWork", []))
          "discards concurrent work" "await or retain ownership"
      // AXG013 is structural; no ambient-effect symbol match is registered here.
      rule "test.wait-for-facts" CoreGuardrail "test-wait" (MembersOf("Axial.__WaitForFacts", []))
          "Wait for a fact, not elapsed time."
          "Complete a Deferred or TaskCompletionSource and wait with a deadline, or step the test clock."

      // AXG008 is syntactic; this entry registers its suppression category.
      rule "scope.run" CoreGuardrail "scope" (MembersOf("Axial.__ScopeFromRun", []))
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
    builtInRuleTable |> List.map (fun rule -> { rule with Capability = capabilityOf rule })

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

/// Parses one declared capability from a rules-file line (see `GuardrailConfig.parse`) into the capability and the
/// rules derived from its `replaces` list. Defaults: `Env` is `IHas<Name>`, `Field` is `<Name>: <Service>` where
/// `<Name>` is the service without a leading `I`, the level is `guide`, and the guardrail is the capability's name.
let parseCapability (name: string) (fields: string list) : Result<Capability * EffectRule list, string> =
    let field index = fields |> List.tryItem index |> Option.map _.Trim() |> Option.defaultValue ""
    let name = name.Trim()
    let serviceType = field 0
    let bare = if serviceType.Length > 1 && serviceType[0] = 'I' && System.Char.IsUpper serviceType[1] then serviceType.Substring 1 else serviceType
    let orDefault fallback value = if value = "" then fallback else value
    let level =
        match (field 7).ToLowerInvariant() with
        | "" | "guide" -> Ok Guide
        | "enforce" -> Ok Enforce
        | other -> Error $"capability '{name}': Level must be guide or enforce, not '{other}'"
    let replaces = (field 6).Split('|') |> Array.map _.Trim() |> Array.filter ((<>) "") |> List.ofArray
    let parsed = replaces |> List.map (fun text -> text, parseMatch text)
    match level, parsed |> List.tryFind (snd >> Option.isNone) with
    | Error error, _ -> Error error
    | _ when name = "" -> Error "an AxialCapability item has no name (Include)"
    | _ when serviceType = "" -> Error $"capability '{name}' has no Service"
    | _ when replaces.IsEmpty -> Error $"capability '{name}' replaces nothing: set Replaces to the APIs its service wraps, such as any:Acme.Gateway.Client"
    | _, Some(text, _) -> Error $"capability '{name}': cannot read Replaces entry '{text}'; use ctor:Type, member:Type::A,B or any:Type"
    | Ok level, None ->
        let capability =
            { Name = name
              Guardrail = field 8 |> orDefault name
              Service = serviceType
              Env = field 1 |> orDefault ("IHas" + bare)
              Field = field 2 |> orDefault (bare + ": " + serviceType)
              Example = field 3
              Live = field 4
              Fake = field 5
              Level = level }
        let rules =
            parsed
            |> List.mapi (fun index (_, matching) ->
                let matching = Option.get matching
                { Id = $"{name}.{index + 1}"
                  Guardrail = capability.Guardrail
                  Category = name.ToLowerInvariant()
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

/// Whether a source file declares or implements the capability's service (`type IPayments`, `interface IPayments with`,
/// `{ new IPayments with ... }`). That file is where the raw API belongs, so guide findings skip it.
let implementsService (capability: Capability) (source: string) =
    let service = System.Text.RegularExpressions.Regex.Escape capability.Service
    System.Text.RegularExpressions.Regex.IsMatch(source, $@"\b(type|interface|new)\s+{service}\b")
