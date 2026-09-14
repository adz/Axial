/// The `ReflectionFormatting` analyzer: flags formatting that relies on F#'s reflection-based structured printer,
/// which fails at run time under NativeAOT and full trimming.
///
/// F# unions and records get a compiler-generated `ToString()` that calls `sprintf "%+A"`. FSharp.Core's own option,
/// voption, list, Result, Choice, Map and Set override `ToString()` but print their contents the same way. `%A`/`%O`
/// walk the value with `FSharp.Reflection`. NativeAOT strips the metadata this needs, so the call throws — for
/// example `KeyNotFoundException` from `FSharpValue.GetUnionFields` — and the build gives no warning, because the
/// reflection happens inside FSharp.Core, whose trim warnings are reported once for the whole assembly.
///
/// Formatting a type parameter is flagged too: it is only as safe as whatever type is substituted, and a generic
/// helper like `Cause.prettyPrint string` is exactly where a union slips in unnoticed.
///
/// Render such values explicitly instead (a `describe`/`render` function or a hand-written `ToString` override), or
/// mark a deliberate use with `// axial-allow-reflection-format` on the line or the line above.
module Axial.Guardrails.ReflectionFormattingAnalyzer

open System.Text.RegularExpressions
open FSharp.Analyzers.SDK
open FSharp.Compiler.Symbols
open FSharp.Compiler.Symbols.FSharpExprPatterns
open FSharp.Compiler.Text

let private allowDirective =
    Regex(@"axial-allow-reflection-format\b", RegexOptions.Compiled)

let private isAllowed (sourceText: ISourceText) (line1: int) : bool =
    let textOfLine1 (n: int) =
        if n >= 1 && n <= sourceText.GetLineCount() then
            sourceText.GetLineString(n - 1)
        else
            ""

    allowDirective.IsMatch(textOfLine1 line1) || allowDirective.IsMatch(textOfLine1 (line1 - 1))

/// FSharp.Core types whose ToString overrides still print their contents with the structured printer.
let private structuredCoreTypes =
    set
        [ "Microsoft.FSharp.Core.FSharpOption`1"
          "Microsoft.FSharp.Core.FSharpValueOption`1"
          "Microsoft.FSharp.Core.FSharpResult`2"
          "Microsoft.FSharp.Collections.FSharpList`1"
          "Microsoft.FSharp.Collections.FSharpMap`2"
          "Microsoft.FSharp.Collections.FSharpSet`1"
          "Microsoft.FSharp.Core.FSharpChoice`2"
          "Microsoft.FSharp.Core.FSharpChoice`3"
          "Microsoft.FSharp.Core.FSharpChoice`4"
          "Microsoft.FSharp.Core.FSharpChoice`5"
          "Microsoft.FSharp.Core.FSharpChoice`6"
          "Microsoft.FSharp.Core.FSharpChoice`7" ]

let private hasOwnToString (entity: FSharpEntity) =
    entity.MembersFunctionsAndValues
    |> Seq.exists (fun m -> m.CompiledName = "ToString" && m.IsOverrideOrExplicitInterfaceImplementation && m.CurriedParameterGroups.Count <= 1)

let rec private stripAbbreviations (t: FSharpType) =
    if t.IsAbbreviation then stripAbbreviations t.AbbreviatedType else t

/// Why formatting a value of this type needs reflection, or None when its ToString is safe.
let private reflectionFormatted (t: FSharpType) : string option =
    let t = stripAbbreviations t

    if t.IsGenericParameter then
        Some $"'{t.GenericParameter.Name} is a type parameter, so it may be an F# union or record"
    elif t.IsAnonRecordType then
        Some "anonymous records print with the structured printer"
    elif t.HasTypeDefinition then
        let entity = t.TypeDefinition

        match entity.TryFullName with
        | Some name when structuredCoreTypes.Contains(entity.QualifiedName.Split(',').[0]) || structuredCoreTypes.Contains name ->
            Some $"{entity.DisplayName} prints its contents with the structured printer"
        | _ when (entity.IsFSharpUnion || entity.IsFSharpRecord || entity.IsFSharpExceptionDeclaration) && not (hasOwnToString entity) ->
            let kind = if entity.IsFSharpUnion then "union" elif entity.IsFSharpRecord then "record" else "exception"
            Some $"{entity.DisplayName} is an F# {kind} whose generated ToString uses sprintf \"%%+A\""
        | _ -> None
    else
        None

let private isMember (entityFullName: string) (names: string list) (mfv: FSharpMemberOrFunctionOrValue) =
    mfv.DeclaringEntity
    |> Option.exists (fun e -> e.TryFullName = Some entityFullName && List.contains mfv.CompiledName names)

let private isPrintfFunction (mfv: FSharpMemberOrFunctionOrValue) =
    mfv.DeclaringEntity
    |> Option.exists (fun e ->
        match e.TryFullName with
        | Some "Microsoft.FSharp.Core.ExtraTopLevelOperators"
        | Some "Microsoft.FSharp.Core.PrintfModule" -> true
        | _ -> false)
    && (mfv.CompiledName.StartsWith "Print" || mfv.CompiledName.Contains "Format")

/// BCL methods that call ToString on boxed arguments.
let private formatsBoxedArguments (mfv: FSharpMemberOrFunctionOrValue) =
    isMember "System.String" [ "Format"; "Concat"; "Join" ] mfv
    || isMember "System.Text.StringBuilder" [ "Append"; "AppendFormat"; "AppendLine"; "Insert" ] mfv
    || isMember "System.IO.TextWriter" [ "Write"; "WriteLine" ] mfv
    || isMember "System.Console" [ "Write"; "WriteLine" ] mfv
    || isMember "System.Diagnostics.Trace" [ "Write"; "WriteLine"; "TraceInformation"; "TraceWarning"; "TraceError" ] mfv
    || isMember "System.Diagnostics.Debug" [ "Write"; "WriteLine"; "Print" ] mfv

let private isObject (t: FSharpType) =
    let t = stripAbbreviations t
    t.HasTypeDefinition && t.TypeDefinition.TryFullName = Some "System.Object"

/// The value inside `box x` or a coercion to obj: what a formatting method will call ToString on. Coercions to other
/// types (such as a list passed as IEnumerable<string> to String.Join) format elements, not the value itself.
let rec private unboxed (e: FSharpExpr) =
    match e with
    | Call(None, mfv, _, _, [ inner ]) when isMember "Microsoft.FSharp.Core.Operators" [ "Box" ] mfv -> unboxed inner
    | Coerce(target, inner) when isObject target -> unboxed inner
    | _ -> e

/// The types of the holes of a PrintfFormat<'Printer, _, _, _, 'Tuple>: curried printf carries them as the 'Printer
/// function chain; interpolated strings as 'Tuple (unit, one type, or a tuple).
let private holeTypes (formatType: FSharpType) : FSharpType list =
    let formatType = stripAbbreviations formatType

    // PrintfFormat`4 (sprintf, printfn) has only the 'Printer; PrintfFormat`5 (interpolation) adds 'Tuple.
    if formatType.HasTypeDefinition && formatType.GenericArguments.Count >= 4 then
        let holes = stripAbbreviations formatType.GenericArguments.[formatType.GenericArguments.Count - 1]

        // Interpolated strings pass holes as a tuple; curried printf (`sprintf "%A %d"`) as a function chain.
        let rec domains (t: FSharpType) =
            let t = stripAbbreviations t
            if t.IsFunctionType then stripAbbreviations t.GenericArguments.[0] :: domains t.GenericArguments.[1] else []

        let printer = stripAbbreviations formatType.GenericArguments.[0]

        if printer.IsFunctionType then domains printer
        elif formatType.GenericArguments.Count < 5 then []
        elif holes.IsTupleType then List.ofSeq holes.GenericArguments
        elif holes.HasTypeDefinition && holes.TypeDefinition.TryFullName = Some "Microsoft.FSharp.Core.Unit" then []
        else [ holes ]
    else
        []

/// Types the structured printer renders without reflection: primitives, strings and common BCL scalars.
let private primitiveTypes =
    set
        [ "System.String"; "System.Boolean"; "System.Char"; "System.Byte"; "System.SByte"; "System.Int16"
          "System.UInt16"; "System.Int32"; "System.UInt32"; "System.Int64"; "System.UInt64"; "System.Single"
          "System.Double"; "System.Decimal"; "System.IntPtr"; "System.UIntPtr"; "System.DateTime"
          "System.DateTimeOffset"; "System.TimeSpan"; "System.Guid"; "System.Numerics.BigInteger" ]

let private isPrimitive (t: FSharpType) =
    let t = stripAbbreviations t

    t.HasTypeDefinition
    && (t.TypeDefinition.IsEnum
        || t.TypeDefinition.TryFullName |> Option.exists primitiveTypes.Contains)

/// The literal format text of a printf or interpolated-string call, when it is a constant.
let rec private formatLiteral (e: FSharpExpr) : string option =
    match e with
    | Const(:? string as text, _) -> Some text
    | _ -> e.ImmediateSubExpressions |> List.tryPick formatLiteral

/// `%A` walks any non-primitive value with reflection, even one with its own ToString.
let private usesStructuredSpecifier (format: string) =
    Regex.IsMatch(format, @"%[-+0 ]*[0-9]*(\.[0-9]+)?A")

/// Every reflection-formatting site in an expression tree.
let rec private findings (e: FSharpExpr) : (range * string) list =
    let here =
        match e with
        | Call(Some receiver, mfv, _, _, []) when mfv.CompiledName = "ToString" ->
            reflectionFormatted receiver.Type |> Option.map (fun why -> e.Range, $"ToString() on {why}") |> Option.toList
        | Call(None, mfv, _, _, [ argument ]) when isMember "Microsoft.FSharp.Core.Operators" [ "ToString" ] mfv ->
            reflectionFormatted argument.Type |> Option.map (fun why -> e.Range, $"`string` on {why}") |> Option.toList
        | Call(None, mfv, _, _, arguments) when isPrintfFunction mfv ->
            let structured = arguments |> List.exists (fun argument -> formatLiteral argument |> Option.exists usesStructuredSpecifier)

            arguments
            |> List.collect (fun argument -> holeTypes argument.Type)
            |> List.choose (fun hole ->
                match reflectionFormatted hole with
                | Some why -> Some $"formatting a hole where {why}"
                | None when structured && not (isPrimitive hole) ->
                    Some $"%%A on {(stripAbbreviations hole).Format FSharpDisplayContext.Empty}, which walks the value with reflection even when it has its own ToString"
                | None -> None)
            |> List.map (fun why -> e.Range, why)
        | Call(_, mfv, _, _, arguments) when formatsBoxedArguments mfv ->
            arguments
            |> List.choose (fun argument ->
                let inner = unboxed argument
                if obj.ReferenceEquals(inner, argument) then None
                else reflectionFormatted inner.Type |> Option.map (fun why -> e.Range, $"{mfv.DisplayName} on {why}"))
        | _ -> []

    here @ (e.ImmediateSubExpressions |> List.collect findings)

let rec private declarationFindings (declaration: FSharpImplementationFileDeclaration) =
    match declaration with
    | FSharpImplementationFileDeclaration.Entity(_, declarations) -> declarations |> List.collect declarationFindings
    | FSharpImplementationFileDeclaration.MemberOrFunctionOrValue(_, _, body) -> findings body
    | FSharpImplementationFileDeclaration.InitAction body -> findings body

let private toMessage (range: range, why: string) : Message =
    { Type = "Axial Reflection Formatting"
      Message =
        $"This formats with F#'s reflection-based structured printer ({why}), which throws under NativeAOT and "
        + "trimming. Render the value explicitly (a describe/render function or a hand-written ToString override). "
        + "If this code never runs trimmed, mark it: `// axial-allow-reflection-format` on this line or the line above."
      Code = "AXG006"
      Severity = Severity.Warning
      Range = range
      Fixes = [] }

let analyze (ctx: CliContext) : Message list =
    match ctx.TypedTree with
    | None -> []
    | Some tree ->
        tree.Declarations
        |> List.collect declarationFindings
        |> List.distinctBy (fun (range, _) -> range.StartLine, range.StartColumn, range.EndLine, range.EndColumn)
        |> List.filter (fun (range, _) -> not (isAllowed ctx.SourceText range.StartLine))
        |> List.map toMessage

[<CliAnalyzer("ReflectionFormatting",
              "Flags ToString, string, interpolation and %A formatting of F# unions, records and type parameters, "
              + "which relies on reflection that NativeAOT and trimming remove.",
              "https://github.com/adz/Axial/blob/main/docs/15-notes/03-guardrails.md")>]
let reflectionFormattingAnalyzer: Analyzer<CliContext> =
    fun ctx -> async { return analyze ctx }
