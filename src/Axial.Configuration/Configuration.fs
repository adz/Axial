namespace Axial.Configuration

open System
open System.Collections.Generic
open Axial
open Axial.FileSystem
open Axial.PlatformService
open Axial.Process
open Axial.State
open Reified
open Reified.Refinements

/// <summary>One immutable merged configuration snapshot.</summary>
type IConfig =
    /// <summary>The merged structured data before schema decoding.</summary>
    abstract Data: Data
    /// <summary>The source names and path spellings in precedence order, for messages only.</summary>
    abstract Sources: (string * (string list -> string)) list

/// <summary>An application environment that carries a merged configuration snapshot.</summary>
#if FABLE_COMPILER
[<Fable.Core.Mangle>]
#endif
type IHasConfig =
    abstract Config: IConfig

/// <summary>Every configuration failure is a value; loading and reading never throw.</summary>
[<RequireQualifiedAccess>]
type ConfigError =
    /// <summary>A source could not be read or parsed; <c>SourceFailed</c> names the layer.</summary>
    | SourceFailed of source: string * message: string
    /// <summary>A schema decode rejected the merged data; every issue is retained, keyed by <see cref="T:Reified.SchemaPath" />.</summary>
    | Invalid of SchemaErrors
    /// <summary>A plain string read found no value at the path.</summary>
    | Missing of path: string
    /// <summary>A plain string read found an object or list at the path.</summary>
    | NotText of path: string
    /// <summary>A plain string read used a path that is not valid <see cref="T:Reified.DataPath" /> syntax.</summary>
    | InvalidPath of path: string

module ConfigError =
    let private segments (path: string) =
        match DataPath.tryParse path with
        | Some parsed -> parsed |> List.choose (function DataPathSegment.Name name -> Some name | DataPathSegment.Index _ -> None)
        | None -> path.Split('.') |> Array.toList

    let private sourceSpellings (sources: IConfig) names =
        sources.Sources |> List.map (fun (_, spell) -> spell names)

    let private pathNames (path: SchemaPath) =
        SchemaPath.fold (fun names key -> names @ [ key ]) (fun names _ -> names) [] path

    let private omittedLine (sources: IConfig) path names =
        let spellings = sourceSpellings sources names

        if spellings.IsEmpty then
            path + " was omitted"
        else
            path + " was omitted (set one of: " + String.concat ", " spellings + ")"

    let private issueLine (sources: IConfig) issue =
        let path = SchemaPath.format issue.Path

        match issue.Error with
        | SchemaError.Omitted when not (String.IsNullOrEmpty path) ->
            omittedLine sources path (pathNames issue.Path)
        | _ when String.IsNullOrEmpty path -> issue.Error.ToString()
        | _ -> path + ": " + issue.Error.ToString()

    /// <summary>Renders a failure as invariant, sorted lines. Secret values never reach this text.</summary>
    let describe (sources: IConfig) (error: ConfigError) : string =
        match error with
        | ConfigError.SourceFailed(source, message) -> source + ": " + message
        | ConfigError.Invalid errors ->
            errors
            |> SchemaErrors.toList
            |> List.sortBy (fun issue -> SchemaPath.format issue.Path, issue.Error.ToString())
            |> List.map (issueLine sources)
            |> String.concat Environment.NewLine
        | ConfigError.Missing path ->
            omittedLine sources path (segments path)
        | ConfigError.NotText path -> path + " is not a text value"
        | ConfigError.InvalidPath path -> path + " is not a valid configuration path"

/// <summary>One named configuration layer. Reading is the effect; the result is plain <see cref="T:Reified.Data" />.</summary>
type ConfigSource<'env> = {
    /// <summary>The layer name shown in messages.</summary>
    Name: string
    /// <summary>Writes a model path in this layer's own notation, for omission messages.</summary>
    Spell: string list -> string
    /// <summary>Reads the layer; source failures are typed and accumulated by <c>Config.load</c>.</summary>
    Read: Flow<'env, ConfigError, Data>
    /// <summary>Files this layer reads, watched by <c>Config.watch</c>; empty for static layers.</summary>
    Paths: string list
}

module ConfigSource =
    let private comparer =
        { new IEqualityComparer<string> with
            member _.Equals(left, right) = String.Equals(left, right, StringComparison.OrdinalIgnoreCase)
            member _.GetHashCode(text) = text.ToUpperInvariant().GetHashCode() }

    let private segments (path: string list) = path |> List.map string

    let private normalisePrefix prefix =
        match prefix with
        | Some value when not (String.IsNullOrWhiteSpace value) -> Some(value.TrimEnd('_') + "__")
        | _ -> None

    let private environmentSpell prefix names =
        let joined = String.concat "__" names

        match normalisePrefix prefix with
        | Some value -> value + joined
        | None -> joined

    let private environmentData prefix (pairs: (string * string) seq) =
        let options (current: DataEnvironmentOptions) =
            { current with Prefix = normalisePrefix prefix }

        Data.ofEnvironmentWith options pairs

    let private commandLineSpell names = "--" + String.concat ":" names

    let private dataSpell name names =
        name + " " + String.concat "." names

    let private ofJson name text =
        match Json.tryParseData (if isNull text then "" else text) with
        | Ok data -> Flow.succeed data
        | Error message -> Flow.fail (ConfigError.SourceFailed(name, message))

    /// <summary>Reads the live process environment once, split by the <c>__</c> convention, optionally under a prefix.</summary>
    /// <remarks><paramref name="prefix" /> is the application prefix without the separator, for example <c>Some "MYAPP"</c>.</remarks>
    let environment (prefix: string option) : ConfigSource<'env> =
        { Name = "environment"
          Spell = environmentSpell prefix
          Read =
            Flow.attemptBlocking (fun _ -> Platform.environmentPairs ())
            |> Flow.mapError (fun error -> ConfigError.SourceFailed("environment", error.Message))
            |> Flow.map (fun pairs -> environmentData prefix pairs)
          Paths = [] }

    /// <summary>Reads a fixed set of environment-style pairs; use it in tests and for host-supplied environments.</summary>
    let environmentOf (prefix: string option) (pairs: (string * string) seq) : ConfigSource<'env> =
        let materialised = Seq.toArray pairs
        { Name = "environment"
          Spell = environmentSpell prefix
          Read = Flow.succeed (environmentData prefix materialised)
          Paths = [] }

    /// <summary>Reads command-line arguments, including nested <c>--Db:Port</c>, repeated values and positionals.</summary>
    let commandLine (arguments: string list) : ConfigSource<'env> =
        { Name = "arguments"
          Spell = commandLineSpell
          Read = Flow.succeed (Data.ofCliArgs arguments)
          Paths = [] }

    /// <summary>Reads one JSON file through <see cref="T:Axial.FileSystem.IHasFileSystem" />; a missing file is a source failure.</summary>
    let jsonFile (path: string) : ConfigSource<'env> when 'env :> IHasFileSystem =
        { Name = path
          Spell = dataSpell path
          Read =
            FileSystem.readAllText path
            |> Flow.mapError (fun error -> ConfigError.SourceFailed(path, FileSystemError.describe error))
            |> Flow.bind (ofJson path)
          Paths = [ path ] }

    /// <summary>Reads one JSON file when it exists and an empty object when it does not.</summary>
    let optionalJsonFile (path: string) : ConfigSource<'env> when 'env :> IHasFileSystem =
        { Name = path
          Spell = dataSpell path
          Read =
            FileSystem.fileExists path
            |> Flow.mapError (fun error -> ConfigError.SourceFailed(path, FileSystemError.describe error))
            |> Flow.bind (fun exists ->
                if exists then
                    FileSystem.readAllText path
                    |> Flow.mapError (fun error -> ConfigError.SourceFailed(path, FileSystemError.describe error))
                    |> Flow.bind (ofJson path)
                else
                    Flow.succeed (Data.Object []))
          Paths = [ path ] }

    /// <summary>Reads JSON text already in memory; a parse failure is a source failure.</summary>
    let json (name: string) (text: string) : ConfigSource<'env> =
        { Name = name
          Spell = dataSpell name
          Read = ofJson name text
          Paths = [] }

    let private decodeQueryComponent (text: string) =
        Platform.decodeComponent (text.Replace("+", " "))

    let private insertPath (path: DataPathSegment list) (value: Data) (root: Data) =
        let findName name fields =
            fields |> List.tryFind (fun (key, _) -> String.Equals(key, name, StringComparison.OrdinalIgnoreCase))

        let rec insert path data =
            match path with
            | [] -> value
            | DataPathSegment.Name name :: rest ->
                let fields =
                    match data with
                    | Data.Object current -> current
                    | _ -> []

                let child =
                    match findName name fields with
                    | Some(_, existing) -> existing
                    | None -> Data.Object []

                let updated = insert rest child
                Data.Object((fields |> List.filter (fun (key, _) -> not (String.Equals(key, name, StringComparison.OrdinalIgnoreCase)))) @ [ name, updated ])
            | DataPathSegment.Index index :: rest ->
                let items =
                    match data with
                    | Data.List current -> current
                    | _ -> []

                let padded = items @ List.replicate (max 0 (index + 1 - items.Length)) Data.Null
                let updated = insert rest padded[index]
                Data.List(padded |> List.mapi (fun position item -> if position = index then updated else item))

        insert path root

    let private queryData (search: string) =
        let query = if isNull search then "" else search.TrimStart('?')

        query.Split('&')
        |> Array.filter (fun part -> part <> "")
        |> Array.fold
            (fun data part ->
                let split = part.IndexOf('=')
                let rawKey, rawValue =
                    if split < 0 then part, ""
                    else part.Substring(0, split), part.Substring(split + 1)

                let key = decodeQueryComponent rawKey
                let value = Data.Text(decodeQueryComponent rawValue)

                if String.IsNullOrEmpty key then
                    data
                else
                    match DataPath.tryParse key with
                    | Some path -> insertPath path value data
                    | None -> insertPath [ DataPathSegment.Name key ] value data)
            (Data.Object [])
        |> fun data -> Data.mergeAllWith comparer [ data ]

    let private queryDataResult (search: string) =
        try
            Ok(queryData search)
        with error ->
            Error(ConfigError.SourceFailed("query", error.Message))

    /// <summary>Reads a browser query string such as <c>?Db.Host=localhost&amp;Db.Port=5432</c>.</summary>
    let queryString (search: string) : ConfigSource<'env> =
        { Name = "query"
          Spell = dataSpell "query"
          Read =
            Flow.delay (fun () ->
                match queryDataResult search with
                | Ok data -> Flow.succeed data
                | Error error -> Flow.fail error)
          Paths = [] }

    /// <summary>Reads structured data provided by the caller; use it for defaults, fixtures and fakes.</summary>
    let ofData (name: string) (data: Data) : ConfigSource<'env> =
        { Name = name
          Spell = dataSpell name
          Read = Flow.succeed data
          Paths = [] }

/// <summary>The latest valid configuration and the stream of decode and source failures.</summary>
type ConfigWatch<'env, 'model> = {
    /// <summary>Always the last successfully decoded value.</summary>
    Current: SubscriptionRef<'model>
    /// <summary>Each failed reload; the current value is unchanged when one arrives.</summary>
    Errors: FlowStream<'env, Never, ConfigError list>
}

/// <summary>Loads, decodes and watches layered configuration.</summary>
/// <remarks>Axial 0.12 requires Reified 0.12.1 or later; its Fable schema builder supports typed schema construction and <c>Config.load</c> decoding on Fable.</remarks>
[<RequireQualifiedAccess>]
module Config =
    let private comparer =
        { new IEqualityComparer<string> with
            member _.Equals(left, right) = String.Equals(left, right, StringComparison.OrdinalIgnoreCase)
            member _.GetHashCode(text) = text.ToUpperInvariant().GetHashCode() }

    let private merge (layers: Data list) =
        match layers with
        | [] -> Data.Object []
        | _ -> Data.mergeAllWith comparer layers

    let private sourceList (sources: ConfigSource<'env> list) =
        sources |> List.map (fun source -> source.Name, source.Spell)

    let private readAll (sources: ConfigSource<'env> list) : Flow<'env, ConfigError list, Data list * ConfigError list> =
        let rec loop data failures remaining =
            match remaining with
            | [] -> Flow.succeed (List.rev data, List.rev failures)
            | source :: rest ->
                source.Read
                |> Flow.map Ok
                |> Flow.orElseWith (fun error -> Flow.succeed (Error error))
                |> Flow.bind (function
                    | Ok layer -> loop (layer :: data) failures rest
                    | Error failure -> loop data (failure :: failures) rest)

        loop [] [] sources |> Flow.mapError List.singleton

    let private currentConfig<'env, 'error when 'env :> IHasConfig> : Flow<'env, 'error, IConfig> =
        Flow.envWith (fun environment -> environment.Config)

    let private loadDataFrom (sources: ConfigSource<'env> list) =
        readAll sources
        |> Flow.bind (fun (layers, failures) ->
            if not failures.IsEmpty then
                Flow.fail failures
            else
                Flow.succeed
                    { new IConfig with
                        member _.Data = merge layers
                        member _.Sources = sourceList sources })

    let private pathSegments (path: string) = path.Split('.') |> Array.toList

    let private lookupData (path: string) (data: Data) =
        match DataPath.tryParse path with
        | None -> Error(ConfigError.InvalidPath path)
        | Some segments ->
            let rec lookup segments data =
                match segments with
                | [] -> Some data
                | DataPathSegment.Name name :: rest ->
                    match data with
                    | Data.Object fields ->
                        fields
                        |> List.tryFind (fun (key, _) -> String.Equals(key, name, StringComparison.OrdinalIgnoreCase))
                        |> Option.bind (fun (_, value) -> lookup rest value)
                    | _ -> None
                | DataPathSegment.Index index :: rest ->
                    match data with
                    | Data.List items when index >= 0 && index < items.Length -> lookup rest items[index]
                    | _ -> None

            lookup segments data |> Ok

    let private textAt path data =
        match data with
        | Data.Null -> Ok None
        | Data.Text value -> Ok(Some value)
        | Data.Number token -> Ok(Some token)
        | Data.Bool value -> Ok(Some(if value then "true" else "false"))
        | _ -> Error(ConfigError.NotText path)

    let private publish (errors: Queue<ConfigError list>) (sources: (string * (string list -> string)) list) (failures: ConfigError list) =
        let described =
            failures
            |> List.map (ConfigError.describe
                { new IConfig with
                    member _.Data = Data.Object []
                    member _.Sources = sources })
            |> String.concat "; "

        flow {
            do! Log.warning described
            let! _ = Queue.offer failures errors
            return ()
        }

    /// <summary>Reads every source, merges them with later-wins precedence and decodes the result with <paramref name="schema" />.</summary>
    /// <remarks>Every source failure is reported together; the schema is not run when any source failed.</remarks>
    let load (schema: Schema<'model>) (sources: ConfigSource<'env> list) : Flow<'env, ConfigError list, 'model> =
        loadDataFrom sources
        |> Flow.bind (fun config ->
            match Schema.parseWith Schema.ignoreKeyCase schema config.Data with
            | Ok value -> Flow.succeed value
            | Error errors -> Flow.fail [ ConfigError.Invalid errors ])

    /// <summary>Reads and merges every source without decoding a model.</summary>
    let loadData (sources: ConfigSource<'env> list) : Flow<'env, ConfigError list, IConfig> = loadDataFrom sources

    /// <summary>Decodes an already merged snapshot; this is pure and safe to call in tests.</summary>
    let parse (schema: Schema<'model>) (config: IConfig) : Result<'model, ConfigError list> =
        Schema.parseWith Schema.ignoreKeyCase schema config.Data
        |> Result.mapError (fun errors -> [ ConfigError.Invalid errors ])

    /// <summary>Decodes one section of an already loaded snapshot.</summary>
    let section (path: string) (schema: Schema<'model>) : Flow<'env, ConfigError list, 'model> when 'env :> IHasConfig =
        currentConfig
        |> Flow.bind (fun config ->
            match lookupData path config.Data with
            | Error error -> Flow.fail [ error ]
            | Ok found ->
                let data = Option.defaultValue (Data.Object []) found

                match Schema.parseWith Schema.ignoreKeyCase schema data with
                | Ok value -> Flow.succeed value
                | Error errors -> Flow.fail [ ConfigError.Invalid errors ])

    /// <summary>Creates a configuration snapshot from structured data; the fake used by tests.</summary>
    let ofData (data: Data) : IConfig =
        { new IConfig with
            member _.Data = data
            member _.Sources = [] }

    /// <summary>Reads a required text value at a Reified data path, comparing keys ordinal-ignore-case.</summary>
    let required (path: string) : Flow<'env, ConfigError, string> when 'env :> IHasConfig =
        currentConfig
        |> Flow.bind (fun config ->
            match lookupData path config.Data |> Result.bind (fun found -> textAt path (Option.defaultValue Data.Null found)) with
            | Ok(Some value) -> Flow.succeed value
            | Ok None -> Flow.fail (ConfigError.Missing path)
            | Error error -> Flow.fail error)

    /// <summary>Reads an optional text value at a Reified data path; absent is <c>None</c>, not an error.</summary>
    let optional (path: string) : Flow<'env, ConfigError, string option> when 'env :> IHasConfig =
        currentConfig
        |> Flow.bind (fun config ->
            match lookupData path config.Data |> Result.bind (fun found -> textAt path (Option.defaultValue Data.Null found)) with
            | Ok value -> Flow.succeed value
            | Error error -> Flow.fail error)

    /// <summary>Reads a text value from an already loaded snapshot without starting a flow.</summary>
    let tryFind (path: string) (config: IConfig) : Result<string option, ConfigError> =
        lookupData path config.Data
        |> Result.bind (fun found -> textAt path (Option.defaultValue Data.Null found))

    /// <summary>Bridges a schema-declared secret into a redacted process argument without exposing its payload.</summary>
    /// <remarks>The real value reaches the child process; plans, transcripts and failures show <c>***</c>.</remarks>
    /// <example><code>let deploy (token: Reified.Refinements.Secret&lt;string&gt;) =
    ///     Axial.Process.DSL.cmd $"deploy --token {Axial.Configuration.Config.processArg token}"</code></example>
    let processArg (secret: Secret<'value>) : SecretArgument = DSL.secret (Secret.reveal secret)

    /// <summary>Watches file sources and publishes each successfully decoded configuration.</summary>
    /// <remarks>
    /// The initial configuration is loaded before this returns, so an invalid initial configuration fails the flow.
    /// File changes are debounced by 200 ms. A failed reload keeps the last valid value, logs the failure and emits it
    /// on <c>Errors</c>. Environment and argument sources are read once per reload and are otherwise static.
    /// </remarks>
    let watch (sources: ConfigSource<'env> list) (schema: Schema<'model>) : Flow<'env, ConfigError list, ConfigWatch<'env, 'model>> when 'env :> IHasFileSystem =
        flow {
            let! initial = load schema sources
            let! current = SubscriptionRef.make initial
            let! (errors: Queue<ConfigError list>) = Queue.unbounded ()
            let spells = sourceList sources
            let paths = sources |> List.collect _.Paths |> List.distinct

            let reload () =
                readAll sources
                |> Flow.bind (fun (layers, failures) ->
                    if not failures.IsEmpty then
                        publish errors spells failures
                    else
                        let config =
                            { new IConfig with
                                member _.Data = merge layers
                                member _.Sources = spells }

                        match Schema.parseWith Schema.ignoreKeyCase schema config.Data with
                        | Ok value -> SubscriptionRef.set value current
                        | Error schemaErrors -> publish errors spells [ ConfigError.Invalid schemaErrors ])

            if not paths.IsEmpty then
                let! fileSystem = FileSystem.service
                let! (changes: Queue<unit>) = Queue.unbounded ()

                for path in paths do
                    let subscription =
                        try
                            Ok(
                                fileSystem.Watch(
                                    path,
                                    false,
                                    (fun _ -> Queue.tryOffer () changes |> ignore),
                                    (fun error ->
                                        Queue.tryOffer [ ConfigError.SourceFailed("watch " + path, error.Message) ] errors
                                        |> ignore)
                                )
                            )
                        with error ->
                            Error error

                    match subscription with
                    | Ok subscription ->
                        do!
                            Flow.scopeFinalizer (
                                Flow.delay (fun () ->
                                    subscription.Dispose()
                                    Flow.succeed ())
                            )
                    | Error error ->
                        do! publish errors spells [ ConfigError.SourceFailed("watch " + path, error.Message) ]

                let! _ =
                    Flow.fork (
                        Flow.delay (fun () ->
                            changes
                            |> FlowStream.fromDequeue
                            |> FlowStream.debounce (TimeSpan.FromMilliseconds 200.0)
                            |> FlowStream.mapFlow (fun () -> reload ())
                            |> FlowStream.runDrain
                            |> Flow.orElseWith (fun failures -> publish errors spells failures)))

                ()

            return { Current = current; Errors = FlowStream.fromDequeue errors }
        }
