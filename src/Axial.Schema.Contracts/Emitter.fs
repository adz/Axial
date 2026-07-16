namespace Axial.Schema.Contracts

open System
open System.Text

/// <summary>Emits the checked-in F# for a resolved contract file: one public record, schema, validate,
/// parse, and Fields module per contract, in declaration order.</summary>
[<RequireQualifiedAccess>]
module Emitter =

    let private fsharpKeywords =
        set
            [ "abstract"; "and"; "as"; "assert"; "base"; "begin"; "class"; "default"; "delegate"; "do"; "done"
              "downcast"; "downto"; "elif"; "else"; "end"; "exception"; "extern"; "false"; "finally"; "fixed"
              "for"; "fun"; "function"; "global"; "if"; "in"; "inherit"; "inline"; "interface"; "internal"
              "lazy"; "let"; "match"; "member"; "module"; "mutable"; "namespace"; "new"; "not"; "null"; "of"
              "open"; "or"; "override"; "private"; "public"; "rec"; "return"; "select"; "static"; "struct"
              "then"; "to"; "true"; "try"; "type"; "upcast"; "use"; "val"; "void"; "when"; "while"; "with"
              "yield"; "const"; "atomic"; "break"; "checked"; "component"; "constraint"; "constructor"
              "continue"; "eager"; "event"; "external"; "functor"; "include"; "method"; "mixin"; "object"
              "parallel"; "process"; "protected"; "pure"; "sealed"; "tailcall"; "trait"; "virtual" ]

    let private escapeIdent (name: string) =
        if fsharpKeywords.Contains name then $"``{name}``" else name

    let private pascal (name: string) =
        if name.Length = 0 then name
        else string (Char.ToUpperInvariant name.[0]) + name.Substring 1

    let private camel (name: string) =
        if name.Length = 0 then name
        else string (Char.ToLowerInvariant name.[0]) + name.Substring 1

    let private duCaseName (text: string) =
        text.Split([| '-'; '_'; ' ' |], StringSplitOptions.RemoveEmptyEntries)
        |> Array.map pascal
        |> String.Concat

    let private escapeString (value: string) =
        value.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n").Replace("\t", "\\t")

    /// The generated DU name for a literal-union or union-block field. The prefix is the contract's
    /// generated type name, so superseded versions keep distinct case types.
    let private caseTypeName contractTypeName fieldName = contractTypeName + pascal fieldName

    let private renderNumericLiteral (kind: PrimitiveType) literal =
        let suffix =
            match kind with
            | PDecimal -> "m"
            | _ -> ""

        match literal with
        | LInt value -> string value + suffix
        | LDecimal value -> value.ToString(Globalization.CultureInfo.InvariantCulture) + "m"
        | LString value -> $"\"{escapeString value}\""
        | LBool value -> if value then "true" else "false"

    let rec private numericKind fieldType =
        match fieldType with
        | Primitive PDecimal -> PDecimal
        | _ -> PInt

    /// Renders one Constraint expression for the constraint list.
    let private renderConstraint fieldType constraint' =
        let sized minName maxName size =
            match constraint' with
            | MinSize n -> $"Constraint.{minName} ({n})"
            | MaxSize n -> $"Constraint.{maxName} ({n})"
            | _ -> size

        match constraint' with
        | AtLeast literal -> $"Constraint.atLeast ({renderNumericLiteral (numericKind fieldType) literal})"
        | GreaterThan literal -> $"Constraint.greaterThan ({renderNumericLiteral (numericKind fieldType) literal})"
        | AtMost literal -> $"Constraint.atMost ({renderNumericLiteral (numericKind fieldType) literal})"
        | LessThan literal -> $"Constraint.lessThan ({renderNumericLiteral (numericKind fieldType) literal})"
        | MultipleOf literal -> $"Constraint.multipleOf ({renderNumericLiteral (numericKind fieldType) literal})"
        | MinSize _
        | MaxSize _ ->
            match fieldType with
            | ListOf _
            | MapOf _ -> sized "minCount" "maxCount" ""
            | _ -> sized "minLength" "maxLength" ""
        | Pattern value -> $"Constraint.pattern (\"{escapeString value}\")"
        | Distinct -> "Constraint.distinct"
        | CheckRef name -> failwith $"check reference '{name}' should have been rejected by the resolver"

    /// The F# type of a field as written in the record and FieldRef declarations. `refTypeName` maps a
    /// pinned contract reference to its generated type name.
    let rec private fsType (refTypeName: ContractRef -> string) contractTypeName fieldName fieldType =
        match fieldType with
        | Primitive PText
        | Primitive PEmail -> "string"
        | Primitive PInt -> "int"
        | Primitive PDecimal -> "decimal"
        | Primitive PBool -> "bool"
        | Primitive PDate -> "System.DateOnly"
        | Primitive PDateTime -> "System.DateTimeOffset"
        | Primitive PGuid -> "System.Guid"
        | Reference reference -> refTypeName reference
        | ListOf element -> $"{fsType refTypeName contractTypeName fieldName element} list"
        | MapOf element -> $"Map<string, {fsType refTypeName contractTypeName fieldName element}>"
        | LiteralUnion _
        | UnionBlock _ -> caseTypeName contractTypeName fieldName
        | ExternalEnum(typeName, _)
        | ExternalUnion(typeName, _, _) -> typeName

    /// The base Schema.* expression for a field's type, before decorations. Self-references (same
    /// contract, same version) lower to Schema.defer over the module's own schema binding.
    let rec private baseValueExpr (refTypeName: ContractRef -> string) (contractName, contractVersion) fieldName fieldType =
        match fieldType with
        | Primitive PText
        | Primitive PEmail -> "Schema.text"
        | Primitive PInt -> "Schema.int"
        | Primitive PDecimal -> "Schema.decimal"
        | Primitive PBool -> "Schema.bool"
        | Primitive PDate -> "Schema.date"
        | Primitive PDateTime -> "Schema.dateTime"
        | Primitive PGuid -> "Schema.guid"
        | Reference reference when reference.RefName = contractName && reference.RefVersion = contractVersion ->
            "Schema.defer (fun () -> schema)"
        | Reference reference -> $"{refTypeName reference}.schema"
        | ListOf element -> $"Schema.list {parenthesize (baseValueExpr refTypeName (contractName, contractVersion) fieldName element)}"
        | MapOf element -> $"Schema.map {parenthesize (baseValueExpr refTypeName (contractName, contractVersion) fieldName element)}"
        | LiteralUnion _
        | ExternalEnum _ -> $"Schema.enum {camel fieldName}Cases"
        | UnionBlock(discriminator, _)
        | ExternalUnion(_, discriminator, _) -> $"Schema.inlineUnion \"{escapeString discriminator}\" {camel fieldName}Cases"

    and private parenthesize (expression: string) =
        if expression.Contains " " then $"({expression})" else expression

    let private joinedDoc (doc: string list) = String.Join(" ", doc)

    let private renderDefault fieldType literal =
        match fieldType, literal with
        | LiteralUnion _, LString value -> failwith $"literal union defaults are rendered by the caller, got \"{value}\""
        | _, LString value -> $"\"{escapeString value}\""
        | _, LBool value -> (if value then "true" else "false")
        | _, (LInt _ | LDecimal _) -> renderNumericLiteral (numericKind fieldType) literal

    /// Renders the complete value expression for one field's schema pipe (excluding field-level constraints).
    let private valueExpr refTypeName (contractName, contractVersion, contractTypeName) (field: FieldDecl) =
        let mutable expression = baseValueExpr refTypeName (contractName, contractVersion) field.FieldName field.FieldType

        if not (List.isEmpty field.Constraints) then
            let emailPrefix =
                match field.FieldType with
                | Primitive PEmail -> [ "Constraint.email" ]
                | _ -> []

            let rendered =
                emailPrefix @ (field.Constraints |> List.map (fun (constraint', _) -> renderConstraint field.FieldType constraint'))

            let joined = String.Join("; ", rendered)
            expression <- $"{expression} |> Schema.constrainAll [ {joined} ]"
        else
            match field.FieldType with
            | Primitive PEmail -> expression <- $"{expression} |> Schema.constrainAll [ Constraint.email ]"
            | _ -> ()

        match field.Doc with
        | [] -> ()
        | doc -> expression <- $"{expression} |> Schema.describe \"{escapeString (joinedDoc doc)}\""

        match field.Default with
        | None -> ()
        | Some literal ->
            let renderedDefault =
                match field.FieldType, literal with
                | LiteralUnion _, LString value -> $"{caseTypeName contractTypeName field.FieldName}.{duCaseName value}"
                | ExternalEnum(typeName, cases), LString value ->
                    let fsCase =
                        cases
                        |> List.tryFind (fun case -> case.EnumTag = value)
                        |> Option.map _.EnumFsCase
                        |> Option.defaultValue (duCaseName value)

                    $"{typeName}.{fsCase}"
                | _ -> renderDefault field.FieldType literal

            expression <- $"{expression} |> Schema.withDefault {renderedDefault}"

        if field.Optional then
            $"Schema.option {parenthesize expression}"
        else
            expression

    let private fieldLevelConstraints (field: FieldDecl) =
        if field.Optional then
            []
        else
            let emailPrefix =
                match field.FieldType with
                | Primitive PEmail -> [ "Constraint.email" ]
                | _ -> []

            emailPrefix
            @ (field.Constraints |> List.map (fun (constraint', _) -> renderConstraint field.FieldType constraint'))

    let private fieldTypeOf (field: FieldDecl) =
        let inner = field.FieldType
        inner

    /// Emits one contract file as F# source text. `fileSet` is the whole resolved generation set;
    /// it decides generated type names for references (the latest version of a name keeps the bare
    /// name, superseded versions are suffixed, like ConfigV1).
    let emit (namespaceName: string) (fileSet: ContractFile list) (file: ContractFile) : string =
        let declared = fileSet |> List.collect _.Contracts

        let latestVersions =
            declared
            |> List.groupBy _.ContractName
            |> List.map (fun (name, contracts) -> name, contracts |> List.map _.Version |> List.max)
            |> Map.ofList

        // User-owned types keep their actual F# names even when a chain override means the conventional
        // generated name would differ.
        let externalNames =
            declared
            |> List.choose (fun contract ->
                contract.ExternalTypeName
                |> Option.map (fun name -> (contract.ContractName, contract.Version), name))
            |> Map.ofList

        let typeNameOf name version =
            match Map.tryFind (name, version) externalNames with
            | Some externalName -> externalName
            | None ->
                match Map.tryFind name latestVersions with
                | Some latest when latest <> version -> $"{name}V{version}"
                | _ -> name

        let refTypeName (reference: ContractRef) = typeNameOf reference.RefName reference.RefVersion
        let namespaceName = file.Namespace |> Option.defaultValue namespaceName

        let builder = StringBuilder()
        let line (text: string) = builder.AppendLine text |> ignore
        let fileName = IO.Path.GetFileName file.FilePath

        let versionList =
            file.Contracts
            |> List.map (fun contract -> $"{contract.ContractName}.v{contract.Version}")
            |> fun names -> String.Join(", ", names)

        line "// <auto-generated>"
        line $"//   Generated by axial schemagen from {fileName} ({versionList})."
        line "//   Do not edit directly; edit the contract source and regenerate."
        line "// </auto-generated>"
        line $"namespace {namespaceName}"
        line ""
        line "open Axial.Validation"
        line "open Axial.Schema"

        for contract in file.Contracts do
            let contractTypeName = typeNameOf contract.ContractName contract.Version
            let contractRef = $"{fileName}, {contract.ContractName}.v{contract.Version}"

            let rec hasSelfReference fieldType =
                match fieldType with
                | Reference reference -> reference.RefName = contract.ContractName && reference.RefVersion = contract.Version
                | ListOf element
                | MapOf element -> hasSelfReference element
                | UnionBlock(_, cases) ->
                    cases |> List.exists (fun case -> case.CaseRef.RefName = contract.ContractName && case.CaseRef.RefVersion = contract.Version)
                | ExternalUnion(_, _, cases) ->
                    cases |> List.exists (fun case -> case.ExtRef.RefName = contract.ContractName && case.ExtRef.RefVersion = contract.Version)
                | Primitive _
                | LiteralUnion _
                | ExternalEnum _ -> false

            // User-owned record fields are referenced verbatim; generated records normalize to PascalCase.
            let fsFieldName (field: FieldDecl) =
                if contract.OwnsType then pascal field.FieldName else field.FieldName

            let caseFields =
                contract.Fields
                |> List.filter (fun field ->
                    match field.FieldType with
                    | LiteralUnion _
                    | UnionBlock _
                    | ExternalEnum _
                    | ExternalUnion _ -> true
                    | _ -> false)

            if contract.OwnsType then
                // Case DUs come before the record that uses them; user-owned union types already exist.
                for field in caseFields do
                    match field.FieldType with
                    | LiteralUnion _
                    | UnionBlock _ ->
                        line ""
                        line $"/// The \"{field.FieldName}\" cases of {contract.ContractName} ({contractRef})."
                        line "[<RequireQualifiedAccess>]"
                        line $"type {caseTypeName contractTypeName field.FieldName} ="

                        match field.FieldType with
                        | LiteralUnion cases ->
                            for case in cases do
                                line $"    | {duCaseName case}"
                        | UnionBlock(_, cases) ->
                            for case in cases do
                                line $"    | {duCaseName case.CaseTag} of {refTypeName case.CaseRef}"
                        | _ -> ()
                    | _ -> ()

                line ""

                for doc in contract.Doc do
                    line $"/// {doc}"

                line $"type {contractTypeName} ="
                line "    {"

                for field in contract.Fields do
                    for doc in field.Doc do
                        line $"        /// {doc}"

                    let optionSuffix = if field.Optional then " option" else ""
                    line $"        {escapeIdent (fsFieldName field)}: {fsType refTypeName contractTypeName field.FieldName (fieldTypeOf field)}{optionSuffix}"

                line "    }"

            line ""
            line $"/// Schema and boundary functions for {contractTypeName} ({contractRef})."
            line "[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]"
            line "[<RequireQualifiedAccess>]"
            line $"module {contractTypeName} ="

            for field in caseFields do
                line ""
                line $"    let private {camel field.FieldName}Cases ="

                match field.FieldType with
                | LiteralUnion cases ->
                    let du = caseTypeName contractTypeName field.FieldName

                    cases
                    |> List.iteri (fun index case ->
                        let opener = if index = 0 then "[ " else "  "
                        let closer = if index = List.length cases - 1 then " ]" else ""
                        line $"        {opener}EnumCase.create \"{escapeString case}\" {du}.{duCaseName case}{closer}")
                | UnionBlock(_, cases) ->
                    let du = caseTypeName contractTypeName field.FieldName

                    cases
                    |> List.iteri (fun index case ->
                        let opener = if index = 0 then "[ " else "  "
                        let closer = if index = List.length cases - 1 then " ]" else ""

                        let extractor =
                            if List.length cases = 1 then
                                $"(function {du}.{duCaseName case.CaseTag} payload -> Some payload)"
                            else
                                $"(function {du}.{duCaseName case.CaseTag} payload -> Some payload | _ -> None)"

                        line
                            $"        {opener}UnionCase.create \"{escapeString case.CaseTag}\" {du}.{duCaseName case.CaseTag} {extractor} {refTypeName case.CaseRef}.schema{closer}")
                | ExternalEnum(typeName, cases) ->
                    cases
                    |> List.iteri (fun index case ->
                        let opener = if index = 0 then "[ " else "  "
                        let closer = if index = List.length cases - 1 then " ]" else ""
                        line $"        {opener}EnumCase.create \"{escapeString case.EnumTag}\" {typeName}.{case.EnumFsCase}{closer}")
                | ExternalUnion(typeName, _, cases) ->
                    cases
                    |> List.iteri (fun index case ->
                        let opener = if index = 0 then "[ " else "  "
                        let closer = if index = List.length cases - 1 then " ]" else ""

                        let extractor =
                            if List.length cases = 1 then
                                $"(function {typeName}.{case.ExtFsCase} payload -> Some payload)"
                            else
                                $"(function {typeName}.{case.ExtFsCase} payload -> Some payload | _ -> None)"

                        line
                            $"        {opener}UnionCase.create \"{escapeString case.ExtTag}\" {typeName}.{case.ExtFsCase} {extractor} {refTypeName case.ExtRef}.schema{closer}")
                | _ -> ()

            line ""
            line $"    /// The schema declared by {fileName} ({contract.ContractName}.v{contract.Version})."
            let recursion = if contract.Fields |> List.exists (fun field -> hasSelfReference field.FieldType) then " rec" else ""
            line $"    let{recursion} schema : Schema<{contractTypeName}> ="

            let parameters =
                contract.Fields
                |> List.map (fun field -> escapeIdent (camel field.FieldName))
                |> fun names -> String.Join(" ", names)

            line $"        Schema.recordFor<{contractTypeName}, _> (fun {parameters} ->"

            contract.Fields
            |> List.iteri (fun index field ->
                let opener = if index = 0 then "{ " else "  "
                let closer = if index = List.length contract.Fields - 1 then " })" else ""
                line $"            {opener}{escapeIdent (fsFieldName field)} = {escapeIdent (camel field.FieldName)}{closer}")

            for field in contract.Fields do
                let wire = FieldDecl.wireName field
                let getter = $"_.{escapeIdent (fsFieldName field)}"
                let value = valueExpr refTypeName (contract.ContractName, contract.Version, contractTypeName) field

                line $"        |> Schema.field \"{escapeString wire}\" {getter} {parenthesize value}"

            line "        |> Schema.build"

            match contract.Doc with
            | [] -> ()
            | doc -> line $"        |> Schema.describe \"{escapeString (joinedDoc doc)}\""

            line ""
            line "    /// Checks a draft built with an ordinary record literal."
            line $"    let validate (draft: {contractTypeName}) : Result<{contractTypeName}, Diagnostics<SchemaError>> ="
            line "        Schema.check schema draft"
            line ""
            line "    /// Parses raw boundary input through the schema."
            line $"    let parse (input: RawInput) : ParsedInput<{contractTypeName}, SchemaError> ="
            line "        Schema.parse schema input"

            // The latest version of a multi-version chain gets the Contract wiring. Migrations stay
            // hand-written typed F#: the builder takes each n-1 -> n migration as a parameter, so the
            // grammar never names F# symbols and the compiler enforces the chain.
            let oldestVersion =
                fileSet
                |> List.collect _.Contracts
                |> List.filter (fun candidate -> candidate.ContractName = contract.ContractName)
                |> List.map _.Version
                |> List.min

            if contract.Version = Map.find contract.ContractName latestVersions && oldestVersion < contract.Version then
                line ""
                line "    /// Builds the versioned wire contract; supply each n-1 -> n migration and the version-detection source."
                line "    let contract"

                for step in oldestVersion .. contract.Version - 1 do
                    let fromType = typeNameOf contract.ContractName step
                    let toType = typeNameOf contract.ContractName (step + 1)
                    line $"        (migrateV{step}ToV{step + 1}: {fromType} -> Result<{toType}, MigrationError>)"

                line "        (source: VersionSource)"
                line $"        : Contract<{contractTypeName}> ="
                line $"        Contract.create \"{escapeString contract.ContractName}\" {contract.Version} schema"

                for step in contract.Version - 1 .. -1 .. oldestVersion do
                    line $"        |> Contract.supersedes {step} {typeNameOf contract.ContractName step}.schema migrateV{step}ToV{step + 1}"

                line "        |> Contract.build source"

            line ""
            line "    /// Typed field references for rules, redisplay, and UI binding."
            line "    [<RequireQualifiedAccess>]"
            line "    module Fields ="

            for field in contract.Fields do
                let wire = FieldDecl.wireName field
                let optionSuffix = if field.Optional then " option" else ""
                let fieldType = $"{fsType refTypeName contractTypeName field.FieldName (fieldTypeOf field)}{optionSuffix}"

                line
                    $"        let {escapeIdent (camel field.FieldName)} : FieldRef<{contractTypeName}, {fieldType}> = {{ Name = \"{escapeString wire}\"; Get = _.{escapeIdent (fsFieldName field)}; Set = fun draft value -> {{ draft with {escapeIdent (fsFieldName field)} = value }} }}"

        builder.ToString().Replace("\r\n", "\n")
