namespace Axial.Codec

open System
open System.Globalization
open System.Text
open Axial.Schema
open Axial.Codec.JsonRuntime

/// <summary>A compiled JSON codec for one schema-described model.</summary>
/// <remarks>
/// <para>
/// Compile once with <see cref="M:Axial.Codec.Json.compile``1" /> and reuse the codec for every value. Compilation
/// compiles the schema's retained typed shape into a direct record plan — ordered field descriptors, cached wire-name
/// bytes, and typed field decoders applied to the original curried constructor — so per-value encoding and decoding
/// use no reflection and no boxed <c>obj array</c> dispatch for record fields.
/// </para>
/// <para>
/// The codec is the trusted hot path: it enforces JSON structure and required fields, but does not run schema
/// constraint metadata such as <c>maxLength</c>. Parse untrusted boundary input with schema input parsing
/// (<c>Schema.parse</c>) when complete path-aware diagnostics are needed, and use the codec where the payload producer
/// is trusted, such as internal services, storage, caches, and message queues.
/// </para>
/// </remarks>
[<Sealed>]
type JsonCodec<'model> internal (encoder: Encoder<'model>, decoder: Decoder<'model>) =
    member internal _.Encoder = encoder
    member internal _.Decoder = decoder

/// <summary>Functions for compiling and running JSON codecs over built model schemas.</summary>
[<RequireQualifiedAccess>]
module rec Json =

    // ---------------------------------------------------------------------
    // Shared compile-time pieces
    // ---------------------------------------------------------------------

    /// A per-decode mutable cell that one compiled field decoder writes into.
    type private ISlot =
        abstract member Decode: ByteSource -> ByteSource
        abstract member Seen: bool

    and private Slot<'field>(decoder: Decoder<'field>) =
        member val Value = Unchecked.defaultof<'field> with get, set
        member val HasValue = false with get, set

        interface ISlot with
            member x.Decode src =
                let struct (value, next) = decoder src
                x.Value <- value
                x.HasValue <- true
                next

            member x.Seen = x.HasValue

    type private FieldMatcher =
        { NameText: string
          NameUtf8: byte[]
          CreateSlot: unit -> ISlot }

    let private utf8 (text: string) = Encoding.UTF8.GetBytes text

    /// Decodes one object with a fixed field plan: unknown fields are skipped, known fields fill their slot.
    let private objectDecoder (matchers: FieldMatcher[]) (apply: ISlot[] -> 'result) : Decoder<'result> =
        fun src ->
            let slots = Array.init matchers.Length (fun index -> matchers[index].CreateSlot())
            let mutable current = expectByte (byte '{') "{" src
            let data = current.Data
            let mutable continueLoop = true

            if current.Offset < data.Length && data[current.Offset] = byte '}' then
                current <- current.Advance 1
                continueLoop <- false

            while continueLoop do
                let struct (keyStart, keyLength, keyHadEscapes, afterKey) = stringRaw current
                let afterColon = advancePastColon afterKey

                let mutable matched = -1
                let mutable index = 0

                while matched < 0 && index < matchers.Length do
                    let matcher = matchers[index]

                    let equal =
                        if keyHadEscapes then
                            materializeString data keyStart keyLength true = matcher.NameText
                        else
                            bytesEqual matcher.NameUtf8 data keyStart keyLength

                    if equal then matched <- index else index <- index + 1

                let afterValue =
                    if matched >= 0 then
                        try
                            slots[matched].Decode afterColon
                        with
                        | :? JsonCodecException as ex ->
                            raise (
                                JsonCodecException(
                                    "." + matchers[matched].NameText
                                    + (if ex.Path = "$" then "" else ex.Path.Substring 1),
                                    ex.Detail,
                                    ex
                                )
                            )
                    else
                        skipValue afterColon

                let struct (next, hasMore) = readSeparatorOrClose (byte '}') "}" afterValue
                current <- next
                continueLoop <- hasMore

            let mutable missing = 0

            while missing < slots.Length do
                if not slots[missing].Seen then
                    raise (
                        JsonCodecException("." + matchers[missing].NameText, "missing required field")
                    )

                missing <- missing + 1

            struct (apply slots, current)

    let private listDecoder (decodeItem: Decoder<'item>) : Decoder<'item list> =
        fun src ->
            let mutable current = expectByte (byte '[') "[" src
            let data = current.Data
            let mutable items = []
            let mutable count = 0
            let mutable continueLoop = true

            if current.Offset < data.Length && data[current.Offset] = byte ']' then
                current <- current.Advance 1
                continueLoop <- false

            while continueLoop do
                let struct (item, afterItem) =
                    try
                        decodeItem current
                    with
                    | :? JsonCodecException as ex ->
                        raise (
                            JsonCodecException(
                                "[" + string count + "]" + (if ex.Path = "$" then "" else ex.Path.Substring 1),
                                ex.Detail,
                                ex
                            )
                        )

                items <- item :: items
                count <- count + 1
                let struct (next, hasMore) = readSeparatorOrClose (byte ']') "]" afterItem
                current <- next
                continueLoop <- hasMore

            struct (List.rev items, current)

    let private listEncoder (encodeItem: Encoder<'item>) : Encoder<'item list> =
        fun writer items ->
            writer.WriteByte(byte '[')

            items
            |> List.iteri (fun index item ->
                if index > 0 then
                    writer.WriteByte(byte ',')

                encodeItem writer item)

            writer.WriteByte(byte ']')

    /// Decodes one object with arbitrary text keys, each value decoded by `decodeItem`.
    let private mapDecoder (decodeItem: Decoder<'item>) : Decoder<(string * 'item) list> =
        fun src ->
            let mutable current = expectByte (byte '{') "{" src
            let data = current.Data
            let mutable entries = []
            let mutable continueLoop = true

            if current.Offset < data.Length && data[current.Offset] = byte '}' then
                current <- current.Advance 1
                continueLoop <- false

            while continueLoop do
                let struct (keyStart, keyLength, keyHadEscapes, afterKey) = stringRaw current
                let key = materializeString data keyStart keyLength keyHadEscapes
                let afterColon = advancePastColon afterKey

                let struct (item, afterValue) =
                    try
                        decodeItem afterColon
                    with
                    | :? JsonCodecException as ex ->
                        raise (
                            JsonCodecException(
                                "." + key + (if ex.Path = "$" then "" else ex.Path.Substring 1),
                                ex.Detail,
                                ex
                            )
                        )

                entries <- (key, item) :: entries
                let struct (next, hasMore) = readSeparatorOrClose (byte '}') "}" afterValue
                current <- next
                continueLoop <- hasMore

            struct (List.rev entries, current)

    let private mapEncoder (encodeItem: Encoder<'item>) : Encoder<(string * 'item) list> =
        fun writer entries ->
            writer.WriteByte(byte '{')

            entries
            |> List.iteri (fun index (key, item) ->
                if index > 0 then
                    writer.WriteByte(byte ',')

                writeQuoted writer key
                writer.WriteByte(byte ':')
                encodeItem writer item)

            writer.WriteByte(byte '}')

    // ---------------------------------------------------------------------
    // Primitive value codecs
    // ---------------------------------------------------------------------

    let private dateTimeOffsetDecoder: Decoder<DateTimeOffset> =
        fun src ->
            let struct (text, next) = stringDecoder src

            match DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind) with
            | true, value -> struct (value, next)
            | false, _ -> decodeFailure (sprintf "invalid date-time value: %s" text)

    let private guidDecoder: Decoder<Guid> =
        fun src ->
            let struct (text, next) = stringDecoder src

            match Guid.TryParse text with
            | true, value -> struct (value, next)
            | false, _ -> decodeFailure (sprintf "invalid uuid value: %s" text)

#if NET8_0_OR_GREATER
    let private dateDecoder: Decoder<DateOnly> =
        fun src ->
            let struct (text, next) = stringDecoder src

            match DateOnly.TryParseExact(text, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None) with
            | true, value -> struct (value, next)
            | false, _ -> decodeFailure (sprintf "invalid date value: %s" text)
#endif

    /// Boxed `Decoder<'concrete>` per primitive kind, unboxed at typed compile sites.
    let private primitiveTypedDecoder (kind: PrimitiveValueKind) : obj =
        match kind with
        | PrimitiveValueKind.Text -> box stringDecoder
        | PrimitiveValueKind.Int -> box intDecoder
        | PrimitiveValueKind.Decimal -> box decimalDecoder
        | PrimitiveValueKind.Bool -> box boolDecoder
#if NET8_0_OR_GREATER
        | PrimitiveValueKind.Date -> box dateDecoder
#else
        | PrimitiveValueKind.Date ->
            invalidOp "Calendar date value schemas are not available on this target framework."
#endif
        | PrimitiveValueKind.DateTime -> box dateTimeOffsetDecoder
        | PrimitiveValueKind.Guid -> box guidDecoder

    let private primitiveObjDecoder (kind: PrimitiveValueKind) : Decoder<obj> =
        match kind with
        | PrimitiveValueKind.Text ->
            fun src ->
                let struct (value, next) = stringDecoder src
                struct (box value, next)
        | PrimitiveValueKind.Int ->
            fun src ->
                let struct (value, next) = intDecoder src
                struct (box value, next)
        | PrimitiveValueKind.Decimal ->
            fun src ->
                let struct (value, next) = decimalDecoder src
                struct (box value, next)
        | PrimitiveValueKind.Bool ->
            fun src ->
                let struct (value, next) = boolDecoder src
                struct (box value, next)
#if NET8_0_OR_GREATER
        | PrimitiveValueKind.Date ->
            fun src ->
                let struct (value, next) = dateDecoder src
                struct (box value, next)
#else
        | PrimitiveValueKind.Date ->
            invalidOp "Calendar date value schemas are not available on this target framework."
#endif
        | PrimitiveValueKind.DateTime ->
            fun src ->
                let struct (value, next) = dateTimeOffsetDecoder src
                struct (box value, next)
        | PrimitiveValueKind.Guid ->
            fun src ->
                let struct (value, next) = guidDecoder src
                struct (box value, next)

    let private writeQuoted (writer: IByteWriter) (text: string) =
        writer.WriteByte(byte '"')
        writer.WriteString text
        writer.WriteByte(byte '"')

    let private encodeBool: Encoder<bool> =
        fun writer value -> writer.WriteString(if value then "true" else "false")

    let private encodeDateTimeOffset: Encoder<DateTimeOffset> =
        fun writer value -> writeQuoted writer (value.ToString("O", CultureInfo.InvariantCulture))

    let private encodeGuid: Encoder<Guid> =
        fun writer value -> writeQuoted writer (value.ToString("D"))

#if NET8_0_OR_GREATER
    let private encodeDate: Encoder<DateOnly> =
        fun writer value -> writeQuoted writer (value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture))
#endif

    /// Boxed `Encoder<'concrete>` per primitive kind, unboxed at typed compile sites.
    let private primitiveTypedEncoder (kind: PrimitiveValueKind) : obj =
        match kind with
        | PrimitiveValueKind.Text -> box (fun (writer: IByteWriter) (value: string) -> writeEscapedString writer value)
        | PrimitiveValueKind.Int -> box (fun (writer: IByteWriter) (value: int) -> writer.WriteInt value)
        | PrimitiveValueKind.Decimal -> box (fun (writer: IByteWriter) (value: decimal) -> writer.WriteDecimal value)
        | PrimitiveValueKind.Bool -> box encodeBool
#if NET8_0_OR_GREATER
        | PrimitiveValueKind.Date -> box encodeDate
#else
        | PrimitiveValueKind.Date ->
            invalidOp "Calendar date value schemas are not available on this target framework."
#endif
        | PrimitiveValueKind.DateTime -> box encodeDateTimeOffset
        | PrimitiveValueKind.Guid -> box encodeGuid

    let private primitiveObjEncoder (kind: PrimitiveValueKind) : Encoder<obj> =
        match kind with
        | PrimitiveValueKind.Text -> fun writer value -> writeEscapedString writer (unbox<string> value)
        | PrimitiveValueKind.Int -> fun writer value -> writer.WriteInt(unbox<int> value)
        | PrimitiveValueKind.Decimal -> fun writer value -> writer.WriteDecimal(unbox<decimal> value)
        | PrimitiveValueKind.Bool -> fun writer value -> encodeBool writer (unbox<bool> value)
#if NET8_0_OR_GREATER
        | PrimitiveValueKind.Date -> fun writer value -> encodeDate writer (unbox<DateOnly> value)
#else
        | PrimitiveValueKind.Date ->
            invalidOp "Calendar date value schemas are not available on this target framework."
#endif
        | PrimitiveValueKind.DateTime -> fun writer value -> encodeDateTimeOffset writer (unbox<DateTimeOffset> value)
        | PrimitiveValueKind.Guid -> fun writer value -> encodeGuid writer (unbox<Guid> value)

    // ---------------------------------------------------------------------
    // Type-erased value codecs (refined raw layers, union payloads, and
    // schemas without a retained compiled record plan)
    // ---------------------------------------------------------------------

    let private compileValueDecoderObj (definition: ValueSchemaDefinition) : Decoder<obj> =
        match definition.Shape with
        | PrimitiveValueDefinition kind -> primitiveObjDecoder kind
        | RefinedValueDefinition(raw, ops) ->
            let rawDecoder = compileValueDecoderObj raw

            fun src ->
                let struct (rawValue, next) = rawDecoder src
                match ops.Construct rawValue with
                | Ok value -> struct (value, next)
                | Error errors ->
                    let detail = errors |> List.map string |> String.concat "; "
                    raise (JsonCodecException("", detail))
        | NestedValueDefinition(model, _) -> compileErasedModelDecoder model
        | ManyValueDefinition collection ->
            let itemDecoder = compileValueDecoderObj collection.Item
            let decodeItems = listDecoder itemDecoder

            fun src ->
                let struct (items, next) = decodeItems src
                struct (collection.BoxItems items, next)
        | MapValueDefinition collection ->
            let itemDecoder = compileValueDecoderObj collection.Item
            let decodeEntries = mapDecoder itemDecoder

            fun src ->
                let struct (entries, next) = decodeEntries src
                struct (collection.BoxEntries entries, next)
        | UnionValueDefinition union -> compileUnionDecoderObj union
        | UnionInlineValueDefinition union -> compileUnionInlineDecoderObj union
        | EnumValueDefinition enum -> compileEnumDecoderObj enum
        | OptionValueDefinition optional ->
            let payloadDecoder = compileValueDecoderObj optional.Payload

            fun src ->
                let src = skipWhitespace src

                if src.Offset < src.Data.Length && src.Data[src.Offset] = byte 'n' then
                    struct (optional.NoneValue, skipValue src)
                else
                    let struct (payload, next) = payloadDecoder src
                    struct (optional.WrapSome payload, next)
        | LazyValueDefinition deferred ->
            let mutable compiled: Decoder<obj> option = None
            fun src ->
                let decoder =
                    match compiled with
                    | Some decoder -> decoder
                    | None ->
                        let decoder = compileValueDecoderObj (deferred.Force())
                        compiled <- Some decoder
                        decoder
                decoder src

    let private compileErasedModelDecoder (model: ModelSchemaDefinition<obj>) : Decoder<obj> =
        let matchers =
            model.Fields
            |> List.map (fun field ->
                let name = ExternalFieldName.value field.ExternalName
                let fieldDecoder = compileValueDecoderObj field.ValueSchema

                let createSlot =
                    match field.ValueSchema.Shape with
                    | OptionValueDefinition optional ->
                        // An absent optional field is a legal None, so its slot starts filled instead of
                        // failing the missing-required-field check.
                        fun () ->
                            let slot = Slot<obj>(fieldDecoder)
                            slot.Value <- optional.NoneValue
                            slot.HasValue <- true
                            slot :> ISlot
                    | _ -> fun () -> Slot<obj>(fieldDecoder) :> ISlot

                { NameText = name
                  NameUtf8 = utf8 name
                  CreateSlot = createSlot })
            |> Array.ofList

        let apply (slots: ISlot[]) =
            let arguments = Array.init slots.Length (fun index -> (slots[index] :?> Slot<obj>).Value)

            match model.Constructor.TryApplyTrusted arguments with
            | Ok value -> value
            | Error message -> decodeFailure message

        objectDecoder matchers apply

    let private compileUnionDecoderObj (union: TaggedUnionValueDefinition) : Decoder<obj> =
        let discriminatorName = ExternalFieldName.value union.DiscriminatorField
        let discriminatorUtf8 = utf8 discriminatorName
        let payloadName = ExternalFieldName.value union.PayloadField
        let payloadUtf8 = utf8 payloadName

        let cases =
            union.Cases
            |> List.map (fun case -> case.Tag, compileValueDecoderObj case.Payload, case.Construct)
            |> Array.ofList

        fun src ->
            let mutable current = expectByte (byte '{') "{" src
            let data = current.Data
            let mutable continueLoop = true
            let mutable tag: string = null
            let mutable payloadOffset = -1

            if current.Offset < data.Length && data[current.Offset] = byte '}' then
                current <- current.Advance 1
                continueLoop <- false

            while continueLoop do
                let struct (keyStart, keyLength, keyHadEscapes, afterKey) = stringRaw current
                let afterColon = advancePastColon afterKey

                let matches (expected: byte[]) (expectedText: string) =
                    if keyHadEscapes then
                        materializeString data keyStart keyLength true = expectedText
                    else
                        bytesEqual expected data keyStart keyLength

                let afterValue =
                    if matches discriminatorUtf8 discriminatorName then
                        let struct (value, next) =
                            withFieldPath discriminatorName (fun () -> stringDecoder afterColon)

                        tag <- value
                        next
                    elif matches payloadUtf8 payloadName then
                        payloadOffset <- (skipWhitespace afterColon).Offset
                        skipValue afterColon
                    else
                        skipValue afterColon

                let struct (next, hasMore) = readSeparatorOrClose (byte '}') "}" afterValue
                current <- next
                continueLoop <- hasMore

            if isNull tag then
                raise (JsonCodecException("." + discriminatorName, "missing union discriminator field"))

            let mutable caseIndex = -1
            let mutable scan = 0

            while caseIndex < 0 && scan < cases.Length do
                let (caseTag, _, _) = cases[scan]

                if caseTag = tag then caseIndex <- scan else scan <- scan + 1

            if caseIndex < 0 then
                raise (JsonCodecException("." + discriminatorName, sprintf "unknown union case tag: %s" tag))

            if payloadOffset < 0 then
                raise (JsonCodecException("." + payloadName, "missing union payload field"))

            let (_, decodePayload, construct) = cases[caseIndex]

            let struct (payload, _) =
                withFieldPath payloadName (fun () -> decodePayload (ByteSource(data, payloadOffset)))

            struct (construct payload, current)

    let private compileValueEncoderObj (definition: ValueSchemaDefinition) : Encoder<obj> =
        match definition.Shape with
        | PrimitiveValueDefinition kind -> primitiveObjEncoder kind
        | RefinedValueDefinition(raw, ops) ->
            let rawEncoder = compileValueEncoderObj raw
            fun writer value -> rawEncoder writer (ops.Inspect value)
        | NestedValueDefinition(model, _) -> compileErasedModelEncoder model
        | ManyValueDefinition collection ->
            let itemEncoder = compileValueEncoderObj collection.Item

            fun writer value ->
                writer.WriteByte(byte '[')
                let mutable first = true

                for item in value :?> System.Collections.IEnumerable do
                    if not first then
                        writer.WriteByte(byte ',')

                    first <- false
                    itemEncoder writer item

                writer.WriteByte(byte ']')
        | MapValueDefinition collection ->
            let itemEncoder = compileValueEncoderObj collection.Item
            let encodeEntries = mapEncoder itemEncoder

            fun writer value -> encodeEntries writer (collection.Entries value)
        | UnionValueDefinition union -> compileUnionEncoderObj union
        | UnionInlineValueDefinition union -> compileUnionInlineEncoderObj union
        | EnumValueDefinition enum -> compileEnumEncoderObj enum
        | OptionValueDefinition optional ->
            // Options in non-field positions (collection items, union payloads, refined raw layers) have no
            // "absent" representation, so None encodes as JSON null there; field-level None omission is handled
            // by the model encoders.
            let payloadEncoder = compileValueEncoderObj optional.Payload

            fun writer value ->
                match optional.TryUnwrap value with
                | Some payload -> payloadEncoder writer payload
                | None -> writer.WriteString "null"
        | LazyValueDefinition deferred ->
            let mutable compiled: Encoder<obj> option = None
            fun writer value ->
                let encoder =
                    match compiled with
                    | Some encoder -> encoder
                    | None ->
                        let encoder = compileValueEncoderObj (deferred.Force())
                        compiled <- Some encoder
                        encoder
                encoder writer value

    /// Writes one model field, choosing the leading-comma prefix from whether an earlier field was written,
    /// and reports whether it wrote anything so None-valued optional fields can be omitted.
    type private FieldWriter<'model> = IByteWriter -> bool -> 'model -> bool

    let private fieldPrefixes name =
        utf8 ("\"" + name + "\":"), utf8 (",\"" + name + "\":")

    let private compileModelFieldWriters (model: ModelSchemaDefinition<obj>) : FieldWriter<obj>[] =
        model.Fields
        |> List.map (fun field ->
            let firstPrefix, restPrefix = fieldPrefixes (ExternalFieldName.value field.ExternalName)
            let getter = field.Getter

            match field.ValueSchema.Shape with
            | OptionValueDefinition optional ->
                let payloadEncoder = compileValueEncoderObj optional.Payload

                (fun (writer: IByteWriter) needsComma value ->
                    match optional.TryUnwrap (getter value) with
                    | Some payload ->
                        writer.WriteBytes(if needsComma then restPrefix else firstPrefix)
                        payloadEncoder writer payload
                        true
                    | None -> false)
                : FieldWriter<obj>
            | _ ->
                let encodeField = compileValueEncoderObj field.ValueSchema

                fun writer needsComma value ->
                    writer.WriteBytes(if needsComma then restPrefix else firstPrefix)
                    encodeField writer (getter value)
                    true)
        |> Array.ofList

    let private compileErasedModelEncoder (model: ModelSchemaDefinition<obj>) : Encoder<obj> =
        let fields = compileModelFieldWriters model

        fun writer value ->
            writer.WriteByte(byte '{')
            let mutable needsComma = false

            for writeField in fields do
                if writeField writer needsComma value then
                    needsComma <- true

            writer.WriteByte(byte '}')

    let private compileUnionEncoderObj (union: TaggedUnionValueDefinition) : Encoder<obj> =
        let discriminatorPrefix = utf8 ("\"" + ExternalFieldName.value union.DiscriminatorField + "\":")
        let payloadPrefix = utf8 (",\"" + ExternalFieldName.value union.PayloadField + "\":")

        let cases =
            union.Cases
            |> List.map (fun case -> case.Tag, case.TryInspect, compileValueEncoderObj case.Payload)
            |> Array.ofList

        fun writer value ->
            let mutable written = false
            let mutable index = 0

            while not written && index < cases.Length do
                let (tag, tryInspect, encodePayload) = cases[index]

                match tryInspect value with
                | Some payload ->
                    writer.WriteByte(byte '{')
                    writer.WriteBytes discriminatorPrefix
                    writeEscapedString writer tag
                    writer.WriteBytes payloadPrefix
                    encodePayload writer payload
                    writer.WriteByte(byte '}')
                    written <- true
                | None -> index <- index + 1

            if not written then
                invalidOp "No union case matched the value being encoded."

    let private compileEnumDecoderObj (enum: TaggedEnumValueDefinition) : Decoder<obj> =
        let cases = enum.Cases |> List.map (fun case -> case.Tag, case.Value) |> Array.ofList

        fun src ->
            let struct (tag, next) = stringDecoder src

            match cases |> Array.tryFind (fun (caseTag, _) -> caseTag = tag) with
            | Some(_, value) -> struct (value, next)
            | None -> raise (JsonCodecException("$", sprintf "unknown enum case tag: %s" tag))

    let private compileEnumEncoderObj (enum: TaggedEnumValueDefinition) : Encoder<obj> =
        let cases = enum.Cases |> List.map (fun case -> case.Value, case.Tag) |> Array.ofList

        fun writer value ->
            match cases |> Array.tryFind (fun (caseValue, _) -> caseValue.Equals value) with
            | Some(_, tag) -> writeEscapedString writer tag
            | None -> invalidOp "No enum case matched the value being encoded."

    /// Union-inline payloads are nested model schemas whose fields are spliced beside the discriminator field, so
    /// decoding independently rescans the object once to find the tag and once through the matched case's own model
    /// decoder (which tolerates the discriminator key as just another unrecognized, skipped field).
    let private compileUnionInlineDecoderObj (union: InlineTaggedUnionValueDefinition) : Decoder<obj> =
        let discriminatorName = ExternalFieldName.value union.DiscriminatorField
        let discriminatorUtf8 = utf8 discriminatorName

        let cases =
            union.Cases
            |> List.map (fun case ->
                match case.Payload.Shape with
                | NestedValueDefinition(model, _) -> case.Tag, compileErasedModelDecoder model, case.Construct
                | _ -> invalidOp "Union-inline case payloads must be nested model schemas.")
            |> Array.ofList

        fun src ->
            let mutable current = expectByte (byte '{') "{" src
            let data = current.Data
            let mutable continueLoop = true
            let mutable tag: string = null

            if current.Offset < data.Length && data[current.Offset] = byte '}' then
                current <- current.Advance 1
                continueLoop <- false

            while continueLoop do
                let struct (keyStart, keyLength, keyHadEscapes, afterKey) = stringRaw current
                let afterColon = advancePastColon afterKey

                let matches (expected: byte[]) (expectedText: string) =
                    if keyHadEscapes then
                        materializeString data keyStart keyLength true = expectedText
                    else
                        bytesEqual expected data keyStart keyLength

                let afterValue =
                    if isNull tag && matches discriminatorUtf8 discriminatorName then
                        let struct (value, next) =
                            withFieldPath discriminatorName (fun () -> stringDecoder afterColon)

                        tag <- value
                        next
                    else
                        skipValue afterColon

                let struct (next, hasMore) = readSeparatorOrClose (byte '}') "}" afterValue
                current <- next
                continueLoop <- hasMore

            if isNull tag then
                raise (JsonCodecException("." + discriminatorName, "missing union discriminator field"))

            match cases |> Array.tryFind (fun (caseTag, _, _) -> caseTag = tag) with
            | None -> raise (JsonCodecException("." + discriminatorName, sprintf "unknown union case tag: %s" tag))
            | Some(_, decodePayload, construct) ->
                let struct (value, _) = decodePayload src
                struct (construct value, current)

    let private compileUnionInlineEncoderObj (union: InlineTaggedUnionValueDefinition) : Encoder<obj> =
        let discriminatorPrefix = utf8 ("\"" + ExternalFieldName.value union.DiscriminatorField + "\":")

        let cases =
            union.Cases
            |> List.map (fun case ->
                match case.Payload.Shape with
                | NestedValueDefinition(model, _) -> case.Tag, case.TryInspect, compileModelFieldWriters model
                | _ -> invalidOp "Union-inline case payloads must be nested model schemas.")
            |> Array.ofList

        fun writer value ->
            let mutable written = false
            let mutable index = 0

            while not written && index < cases.Length do
                let (tag, tryInspect, fields) = cases[index]

                match tryInspect value with
                | Some payload ->
                    writer.WriteByte(byte '{')
                    writer.WriteBytes discriminatorPrefix
                    writeEscapedString writer tag

                    for writeField in fields do
                        writeField writer true payload |> ignore

                    writer.WriteByte(byte '}')
                    written <- true
                | None -> index <- index + 1

            if not written then
                invalidOp "No union-inline case matched the value being encoded."

    // ---------------------------------------------------------------------
    // Typed value codecs over the retained field chain
    // ---------------------------------------------------------------------

    let private compileValueDecoder<'field> (definition: ValueSchemaDefinition) : Decoder<'field> =
        match definition.Shape with
        | PrimitiveValueDefinition kind -> unbox<Decoder<'field>> (primitiveTypedDecoder kind)
        | RefinedValueDefinition _ ->
            let objDecoder = compileValueDecoderObj definition

            fun src ->
                let struct (value, next) = objDecoder src
                struct (unbox<'field> value, next)
        | NestedValueDefinition(model, source) ->
            match source with
            | :? Schema<'field> as nestedSchema when Option.isSome nestedSchema.RecordPlan ->
                compileTypedModelDecoder<'field> nestedSchema
            | _ ->
                let objDecoder = compileErasedModelDecoder model

                fun src ->
                    let struct (value, next) = objDecoder src
                    struct (unbox<'field> value, next)
        | ManyValueDefinition collection ->
            collection.AcceptItem
                { new ICollectionItemInterpreter with
                    member _.Item<'item>(item: ValueSchemaDefinition) =
                        box (listDecoder (compileValueDecoder<'item> item)) }
            |> unbox<Decoder<'field>>
        | MapValueDefinition collection ->
            collection.AcceptItem
                { new ICollectionItemInterpreter with
                    member _.Item<'item>(item: ValueSchemaDefinition) =
                        let decodeEntries = mapDecoder (compileValueDecoder<'item> item)

                        box (fun src ->
                            let struct (entries, next) = decodeEntries src
                            struct (Map.ofList entries, next)) }
            |> unbox<Decoder<'field>>
        | UnionValueDefinition _
        | UnionInlineValueDefinition _
        | EnumValueDefinition _
        | OptionValueDefinition _ ->
            let objDecoder = compileValueDecoderObj definition

            fun src ->
                let struct (value, next) = objDecoder src
                struct (unbox<'field> value, next)
        | LazyValueDefinition _ ->
            let objDecoder = compileValueDecoderObj definition
            fun src ->
                let struct (value, next) = objDecoder src
                struct (unbox<'field> value, next)

    let private compileValueEncoder<'field> (definition: ValueSchemaDefinition) : Encoder<'field> =
        match definition.Shape with
        | PrimitiveValueDefinition kind -> unbox<Encoder<'field>> (primitiveTypedEncoder kind)
        | NestedValueDefinition(model, source) ->
            match source with
            | :? Schema<'field> as nestedSchema when Option.isSome nestedSchema.RecordPlan ->
                compileTypedModelEncoder<'field> nestedSchema
            | _ ->
                let objEncoder = compileErasedModelEncoder model
                fun writer value -> objEncoder writer (box value)
        | ManyValueDefinition collection ->
            collection.AcceptItem
                { new ICollectionItemInterpreter with
                    member _.Item<'item>(item: ValueSchemaDefinition) =
                        box (listEncoder (compileValueEncoder<'item> item)) }
            |> unbox<Encoder<'field>>
        | MapValueDefinition collection ->
            collection.AcceptItem
                { new ICollectionItemInterpreter with
                    member _.Item<'item>(item: ValueSchemaDefinition) =
                        let encodeEntries = mapEncoder (compileValueEncoder<'item> item)
                        box (fun (writer: IByteWriter) (value: Map<string, 'item>) -> encodeEntries writer (Map.toList value)) }
            |> unbox<Encoder<'field>>
        | RefinedValueDefinition _
        | UnionValueDefinition _
        | UnionInlineValueDefinition _
        | EnumValueDefinition _
        | OptionValueDefinition _ ->
            let objEncoder = compileValueEncoderObj definition
            fun writer value -> objEncoder writer (box value)
        | LazyValueDefinition _ ->
            let objEncoder = compileValueEncoderObj definition
            fun writer value -> objEncoder writer (box value)

    // The typed decode chain: each field contributes a matcher plus a typed
    // application step that reads the field's slot without boxing.
    type private DecodeChainLink<'constructorIn, 'constructorOut> =
        { Matchers: FieldMatcher list
          Apply: 'constructorIn -> ISlot[] -> 'constructorOut }

    type private DecodeChainResult<'model, 'constructorIn, 'constructorOut>
        (link: DecodeChainLink<'constructorIn, 'constructorOut>) =
        interface IRecordPlanState<'model, 'constructorIn, 'constructorOut> with
            member _.Value = box link

    type private DecodeFactory<'model>() =
        interface IRecordPlanCompiler<'model, Decoder<'model>> with
            member _.OnEnd<'constructor>() =
                DecodeChainResult<'model, 'constructor, 'constructor>(
                    { Matchers = []
                      Apply = fun constructor' _ -> constructor' }
                )
                :> IRecordPlanState<'model, 'constructor, 'constructor>

            member _.OnField<'constructorIn, 'field, 'next>
                (
                    order: int,
                    field: Field<'model, 'field>,
                    head: IRecordPlanState<'model, 'constructorIn, 'field -> 'next>
                ) : IRecordPlanState<'model, 'constructorIn, 'next> =
                let headLink = unbox<DecodeChainLink<'constructorIn, 'field -> 'next>> head.Value
                let name = field |> Field.externalName |> ExternalFieldName.value
                let fieldDecoder = compileValueDecoder<'field> field.Definition.ValueSchema

                let createSlot =
                    match field.Definition.ValueSchema.Shape with
                    | OptionValueDefinition _ ->
                        // An absent optional field is a legal None ('field is an option type, whose default
                        // representation is None), so its slot starts filled.
                        fun () ->
                            let slot = Slot<'field>(fieldDecoder)
                            slot.HasValue <- true
                            slot :> ISlot
                    | _ -> fun () -> Slot<'field>(fieldDecoder) :> ISlot

                let matcher =
                    { NameText = name
                      NameUtf8 = utf8 name
                      CreateSlot = createSlot }

                DecodeChainResult<'model, 'constructorIn, 'next>(
                    { Matchers = headLink.Matchers @ [ matcher ]
                      Apply =
                        fun constructor' slots ->
                            (headLink.Apply constructor' slots) (slots[order] :?> Slot<'field>).Value }
                )
                :> IRecordPlanState<'model, 'constructorIn, 'next>

            member _.OnComplete<'constructor, 'constructed>(constructor: 'constructor, chain, finish) =
                let link = unbox<DecodeChainLink<'constructor, 'constructed>> (chain :> IRecordPlanState<_, _, _>).Value
                let matchers = Array.ofList link.Matchers
                objectDecoder matchers (fun slots ->
                    match finish (link.Apply constructor slots) with
                    | Ok model -> model
                    | Error message -> decodeFailure message)

    let private compileTypedModelDecoder<'model> (schema: Schema<'model>) : Decoder<'model> =
        SchemaCore.compilePlan (DecodeFactory<'model>()) schema

    // The typed encode chain: each field contributes cached wire-name bytes
    // plus a writer over the typed getter.
    type private EncodeChainResult<'model, 'constructorIn, 'constructorOut>(fields: FieldWriter<'model> list) =
        interface IRecordPlanState<'model, 'constructorIn, 'constructorOut> with
            member _.Value = box fields

    type private EncodeFactory<'model>() =
        interface IRecordPlanCompiler<'model, Encoder<'model>> with
            member _.OnEnd<'constructor>() =
                EncodeChainResult<'model, 'constructor, 'constructor>([])
                :> IRecordPlanState<'model, 'constructor, 'constructor>

            member _.OnField<'constructorIn, 'field, 'next>
                (
                    order: int,
                    field: Field<'model, 'field>,
                    head: IRecordPlanState<'model, 'constructorIn, 'field -> 'next>
                ) : IRecordPlanState<'model, 'constructorIn, 'next> =
                ignore order
                let headFields = unbox<FieldWriter<'model> list> head.Value
                let name = field |> Field.externalName |> ExternalFieldName.value
                let firstPrefix, restPrefix = fieldPrefixes name
                let getter = field.Definition.Getter

                let writeField: FieldWriter<'model> =
                    match field.Definition.ValueSchema.Shape with
                    | OptionValueDefinition optional ->
                        let payloadEncoder = compileValueEncoderObj optional.Payload

                        fun writer needsComma model ->
                            match optional.TryUnwrap(box (getter model)) with
                            | Some payload ->
                                writer.WriteBytes(if needsComma then restPrefix else firstPrefix)
                                payloadEncoder writer payload
                                true
                            | None -> false
                    | _ ->
                        let encodeValue = compileValueEncoder<'field> field.Definition.ValueSchema

                        fun writer needsComma model ->
                            writer.WriteBytes(if needsComma then restPrefix else firstPrefix)
                            encodeValue writer (getter model)
                            true

                EncodeChainResult<'model, 'constructorIn, 'next>(headFields @ [ writeField ])
                :> IRecordPlanState<'model, 'constructorIn, 'next>

            member _.OnComplete<'constructor, 'constructed>
                (
                    _: 'constructor,
                    chain: IRecordPlanState<'model, 'constructor, 'constructed>,
                    _: 'constructed -> Result<'model, string>
                ) =
                let fields =
                    unbox<FieldWriter<'model> list> (chain :> IRecordPlanState<_, _, _>).Value
                    |> Array.ofList

                fun writer model ->
                    writer.WriteByte(byte '{')
                    let mutable needsComma = false

                    for writeField in fields do
                        if writeField writer needsComma model then
                            needsComma <- true

                    writer.WriteByte(byte '}')

    let private compileTypedModelEncoder<'model> (schema: Schema<'model>) : Encoder<'model> =
        SchemaCore.compilePlan (EncodeFactory<'model>()) schema

    // ---------------------------------------------------------------------
    // Public API
    // ---------------------------------------------------------------------

    /// <summary>Compiles a completed schema into a reusable JSON codec.</summary>
    /// <remarks>
    /// <para>
    /// Compile once per schema, typically at startup, and reuse the codec for every value. Constructor-last object
    /// schemas retain a typed record plan, including checked constructors. Constructor failures surface as
    /// <see cref="T:Axial.Codec.JsonCodecException" /> during decoding.
    /// </para>
    /// </remarks>
    /// <exception cref="T:System.ArgumentNullException">Thrown when <paramref name="schema" /> is null.</exception>
    /// <exception cref="T:System.ArgumentException">Thrown when <paramref name="schema" /> is incomplete.</exception>
    /// <example>
    /// <code>
    /// let codec = Json.compile customerSchema
    /// let json = Json.serialize codec customer
    /// let roundTripped = Json.deserialize codec json
    /// </code>
    /// </example>
    let compile (schema: Schema<'model>) : JsonCodec<'model> =
        if isNull (box schema) then
            nullArg (nameof schema)

        match schema.Definition with
        | PendingDefinition -> invalidArg (nameof schema) "Expected a completed schema."
        | ValueDefinition definition ->
            JsonCodec(compileValueEncoder<'model> definition, compileValueDecoder<'model> definition)
        | ModelDefinition definition ->
            match schema.RecordPlan with
            | Some _ -> JsonCodec(compileTypedModelEncoder schema, compileTypedModelDecoder schema)
            | None ->
                let erased = ModelSchemaErasure.erase definition
                let objEncoder = compileErasedModelEncoder erased
                let objDecoder = compileErasedModelDecoder erased
                let encoder: Encoder<'model> = fun writer model -> objEncoder writer (box model)
                let decoder: Decoder<'model> =
                    fun src ->
                        let struct (value, next) = objDecoder src
                        struct (unbox<'model> value, next)
                JsonCodec(encoder, decoder)

    /// <summary>Serializes a trusted model to a JSON string through a compiled codec.</summary>
    /// <exception cref="T:System.ArgumentNullException">Thrown when <paramref name="codec" /> is null.</exception>
    let serialize (codec: JsonCodec<'model>) (value: 'model) : string =
        if isNull (box codec) then
            nullArg (nameof codec)

        let buffer = ResizableBuffer.Create(4096)

        try
            codec.Encoder (buffer :> IByteWriter) value
            Encoding.UTF8.GetString(buffer.InternalData, 0, buffer.InternalCount)
        finally
            buffer.Release()

    /// <summary>Serializes a trusted model to UTF-8 JSON bytes through a compiled codec.</summary>
    /// <exception cref="T:System.ArgumentNullException">Thrown when <paramref name="codec" /> is null.</exception>
    let serializeBytes (codec: JsonCodec<'model>) (value: 'model) : byte[] =
        if isNull (box codec) then
            nullArg (nameof codec)

        let buffer = ResizableBuffer.Create(4096)

        try
            codec.Encoder (buffer :> IByteWriter) value
            let result = Array.zeroCreate buffer.InternalCount
            Array.blit buffer.InternalData 0 result 0 buffer.InternalCount
            result
        finally
            buffer.Release()

    let private decodeRoot (codec: JsonCodec<'model>) (data: byte[]) : 'model =
        let struct (value, next) = codec.Decoder (ByteSource(data, 0))
        let next = skipWhitespace next

        if next.Offset <> data.Length then
            decodeFailure "unexpected trailing content"

        value

    /// <summary>Deserializes UTF-8 JSON bytes to a trusted model through a compiled codec.</summary>
    /// <exception cref="T:System.ArgumentNullException">Thrown when <paramref name="codec" /> or <paramref name="bytes" /> is null.</exception>
    /// <exception cref="T:Axial.Codec.JsonCodecException">Thrown when the JSON does not match the schema's wire shape.</exception>
    let deserializeBytes (codec: JsonCodec<'model>) (bytes: byte[]) : 'model =
        if isNull (box codec) then
            nullArg (nameof codec)

        if isNull bytes then
            nullArg (nameof bytes)

        decodeRoot codec bytes

    /// <summary>Deserializes a JSON string to a trusted model through a compiled codec.</summary>
    /// <exception cref="T:System.ArgumentNullException">Thrown when <paramref name="codec" /> or <paramref name="json" /> is null.</exception>
    /// <exception cref="T:Axial.Codec.JsonCodecException">Thrown when the JSON does not match the schema's wire shape.</exception>
    let deserialize (codec: JsonCodec<'model>) (json: string) : 'model =
        if isNull (box codec) then
            nullArg (nameof codec)

        if isNull json then
            nullArg (nameof json)

        decodeRoot codec (Encoding.UTF8.GetBytes json)

    /// <summary>Deserializes a JSON string, returning decode failures as a rendered message instead of raising.</summary>
    /// <exception cref="T:System.ArgumentNullException">Thrown when <paramref name="codec" /> or <paramref name="json" /> is null.</exception>
    let tryDeserialize (codec: JsonCodec<'model>) (json: string) : Result<'model, string> =
        try
            Ok(deserialize codec json)
        with :? JsonCodecException as ex ->
            Error ex.Message

#if !FABLE_COMPILER
    /// <summary>Serializes a trusted model as UTF-8 JSON directly to a stream through a compiled codec, flushing once when complete.</summary>
    /// <remarks>
    /// Encodes into a pooled buffer and writes it to <paramref name="stream" /> in one call, so the response path never
    /// materializes an intermediate string. Not available on Fable.
    /// </remarks>
    /// <exception cref="T:System.ArgumentNullException">Thrown when <paramref name="codec" /> or <paramref name="stream" /> is null.</exception>
    let serializeToStream (codec: JsonCodec<'model>) (stream: System.IO.Stream) (value: 'model) : unit =
        if isNull (box codec) then
            nullArg (nameof codec)

        if isNull stream then
            nullArg (nameof stream)

        let buffer = ResizableBuffer.Create(4096)

        try
            codec.Encoder (buffer :> IByteWriter) value
            stream.Write(buffer.InternalData, 0, buffer.InternalCount)
            stream.Flush()
        finally
            buffer.Release()

    /// <summary>Reads a stream to end into a pooled buffer, then deserializes it as UTF-8 JSON through a compiled codec.</summary>
    /// <remarks>
    /// This reads the whole stream before decoding; there is no incremental/streaming JSON parser pre-1.0. Not available
    /// on Fable.
    /// </remarks>
    /// <exception cref="T:System.ArgumentNullException">Thrown when <paramref name="codec" /> or <paramref name="stream" /> is null.</exception>
    /// <exception cref="T:Axial.Codec.JsonCodecException">Thrown when the JSON does not match the schema's wire shape.</exception>
    let deserializeStreamAsync
        (codec: JsonCodec<'model>)
        (stream: System.IO.Stream)
        : System.Threading.Tasks.Task<'model> =
        task {
            if isNull (box codec) then
                nullArg (nameof codec)

            if isNull stream then
                nullArg (nameof stream)

            let buffer = ResizableBuffer.Create(4096)

            try
                let mutable bytesRead = -1

                while bytesRead <> 0 do
                    (buffer :> IByteWriter).Ensure(4096)

                    let! read =
                        stream.ReadAsync(
                            buffer.InternalData,
                            buffer.InternalCount,
                            buffer.InternalData.Length - buffer.InternalCount
                        )

                    bytesRead <- read
                    buffer.InternalCount <- buffer.InternalCount + read

                let data = Array.zeroCreate buffer.InternalCount
                Array.blit buffer.InternalData 0 data 0 buffer.InternalCount
                return decodeRoot codec data
            finally
                buffer.Release()
        }
#endif
