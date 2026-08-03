// Message identity: the parsed relative keys, resource-segment encoding, and the two opaque values a
// renderer needs — MessageDescriptor (runtime identity plus arguments) and MessageFormatSpec (the owning
// catalogue's fallback and plural metadata). Nothing here knows about Violation, Schema, or cultures.
namespace Axial.Constraint

open System

/// <summary>Why a relative message key could not be parsed.</summary>
/// <remarks>
/// A key is <c>segment ("." segment)*</c>. Dots separate segments and are never literal segment data; every other
/// character, including <c>%</c>, brackets, whitespace, and non-ASCII text, is exact input that the resource-segment
/// encoder handles later. Callers never pre-encode a key.
/// </remarks>
[<RequireQualifiedAccess>]
type MessageKeyError =
    /// <summary>The key was empty.</summary>
    | EmptyKey
    /// <summary>The zero-based segment at this position was empty, as in <c>books..isbn</c>.</summary>
    | EmptySegment of index: int

/// <summary>The reserved-character encoding that keeps independently supplied segments from colliding.</summary>
/// <remarks>
/// <c>%</c> is encoded first, so a literal <c>%2E</c> in an attribute name stays distinct from a literal dot.
/// Emitted hex digits are uppercase. No trimming, case folding, or Unicode normalization happens anywhere.
/// </remarks>
module internal MessageSegment =
    let encode (segment: string) =
        let builder = Text.StringBuilder(segment.Length)

        for character in segment do
            match character with
            | '%' -> builder.Append "%25" |> ignore
            | '.' -> builder.Append "%2E" |> ignore
            | '[' -> builder.Append "%5B" |> ignore
            | ']' -> builder.Append "%5D" |> ignore
            | character -> builder.Append character |> ignore

        builder.ToString()

    let join (segments: string list) = segments |> List.map encode |> String.concat "."

    let tryParse (key: string) : Result<string list, MessageKeyError> =
        if isNull key || key = "" then
            Error MessageKeyError.EmptyKey
        else
            let segments = key.Split '.' |> List.ofArray

            match segments |> List.tryFindIndex (fun segment -> segment = "") with
            | Some index -> Error(MessageKeyError.EmptySegment index)
            | None -> Ok segments

    let describe (error: MessageKeyError) =
        match error with
        | MessageKeyError.EmptyKey -> "A message key must contain at least one segment."
        | MessageKeyError.EmptySegment index -> $"The message key segment at index {index} is empty."

/// <summary>A message identity and the operands its template may interpolate.</summary>
/// <remarks>
/// <para>
/// The identity is a parsed relative key such as <c>constraint.cardinality.between</c> or an application's own
/// <c>books.isbn.invalid</c>. A descriptor never carries a document context, an attribute, an encoded resource key,
/// or a plural category: those are rendering-edge facts, and a violation that captured them would stop being
/// path-free comparable data.
/// </para>
/// <para>
/// The representation is private and validated, so rendering has no malformed-descriptor branch. Independently
/// constructed descriptors with the same key and arguments compare equal, as do violations containing them.
/// </para>
/// </remarks>
[<CustomEquality; NoComparison>]
type MessageDescriptor =
    private
        { Segments: string list
          Values: Map<string, ConstraintValue> }

    override this.Equals(other) =
        match other with
        | :? MessageDescriptor as other -> this.Segments = other.Segments && this.Values = other.Values
        | _ -> false

    override this.GetHashCode() = hash (this.Segments, this.Values)

/// <summary>Why a message format specification was rejected.</summary>
[<RequireQualifiedAccess>]
type MessageFormatSpecError =
    /// <summary>The declared plural operand names no argument the descriptor carries.</summary>
    | UnknownPluralArgument of string

/// <summary>
/// A descriptor plus the rendering metadata its owning catalogue holds: the neutral fallback template and the
/// optional plural operand.
/// </summary>
/// <remarks>
/// This separation is what lets Schema push its own <c>schema.*</c> entries through the same renderer mechanics —
/// contextual fallback, <c>.one</c>/<c>.other</c> selection, interpolation, value formatting — without
/// <c>Axial.Constraint</c> learning a single Schema identity, and without a reverse package dependency.
/// </remarks>
[<CustomEquality; NoComparison>]
type MessageFormatSpec =
    private
        { Descriptor: MessageDescriptor
          Fallback: string
          Plural: string option }

    override this.Equals(other) =
        match other with
        | :? MessageFormatSpec as other ->
            this.Descriptor.Equals other.Descriptor
            && this.Fallback = other.Fallback
            && this.Plural = other.Plural
        | _ -> false

    override this.GetHashCode() = hash (this.Descriptor, this.Fallback, this.Plural)

/// <summary>Reads and builds message identities.</summary>
[<RequireQualifiedAccess>]
[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module MessageDescriptor =
    /// <summary>The canonical unencoded key, exactly as authored.</summary>
    /// <remarks>
    /// This is identity, not a lookup key. The encoded contextual resource keys a lookup receives come from
    /// <c>Renderer.Advanced.lookupCandidates</c>.
    /// </remarks>
    /// <example><code>MessageDescriptor.key descriptor // "books.isbn.invalid"</code></example>
    let key (descriptor: MessageDescriptor) = descriptor.Segments |> String.concat "."

    /// <summary>The operands the message interpolates, named for the template.</summary>
    /// <example><code>MessageDescriptor.arguments descriptor |> Map.tryFind "expectedLength"</code></example>
    let arguments (descriptor: MessageDescriptor) = descriptor.Values

    /// <summary>The parsed, unencoded key segments.</summary>
    /// <remarks>
    /// Segments exist for safe encoding and canonical reconstruction, not namespace fallback. Lookup for
    /// <c>books.isbn.invalid</c> never tries <c>books.isbn</c>.
    /// </remarks>
    let segments (descriptor: MessageDescriptor) = descriptor.Segments

    let internal ofSegments segments arguments =
        { Segments = segments; Values = arguments }

    let internal encodedKey (descriptor: MessageDescriptor) = MessageSegment.join descriptor.Segments

    /// <summary>Building descriptors from keys that were not written by the calling programmer.</summary>
    [<RequireQualifiedAccess>]
    module Advanced =
        /// <summary>Parses a relative key, returning the parse failure rather than raising.</summary>
        /// <remarks>Use this for externally supplied configuration; it is total.</remarks>
        /// <example><code>MessageDescriptor.Advanced.tryCreate "books.isbn.invalid" Map.empty</code></example>
        let tryCreate (key: string) (arguments: Map<string, ConstraintValue>) : Result<MessageDescriptor, MessageKeyError> =
            MessageSegment.tryParse key
            |> Result.map (fun segments -> { Segments = segments; Values = arguments })

        /// <summary>Parses a relative key, raising for a malformed programmer-authored key.</summary>
        /// <remarks>
        /// A key written in source is either right or a defect. Failing at construction is what keeps rendering
        /// free of a malformed-descriptor branch.
        /// </remarks>
        /// <example><code>MessageDescriptor.Advanced.create "books.isbn.invalid" Map.empty</code></example>
        let create (key: string) (arguments: Map<string, ConstraintValue>) : MessageDescriptor =
            match tryCreate key arguments with
            | Ok descriptor -> descriptor
            | Error error -> invalidArg (nameof key) (MessageSegment.describe error)

        /// <summary>Builds a descriptor from already-parsed segments, skipping the parse.</summary>
        /// <remarks>
        /// For a generated catalogue whose segments are known at build time, so a render does not reparse a key
        /// it has already validated. Segments are unencoded and may contain any character except that an empty
        /// segment, or no segments at all, is rejected.
        /// </remarks>
        /// <example><code>MessageDescriptor.Advanced.ofSegments [ "schema"; "omitted" ] Map.empty</code></example>
        let ofSegments (segments: string list) (arguments: Map<string, ConstraintValue>) : MessageDescriptor =
            if isNull (box segments) || segments.IsEmpty then
                invalidArg (nameof segments) (MessageSegment.describe MessageKeyError.EmptyKey)

            match segments |> List.tryFindIndex (fun segment -> isNull segment || segment = "") with
            | Some index ->
                invalidArg (nameof segments) (MessageSegment.describe (MessageKeyError.EmptySegment index))
            | None -> { Segments = segments; Values = arguments }

/// <summary>Reads and builds catalogue-owned rendering metadata.</summary>
[<RequireQualifiedAccess>]
[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module MessageFormatSpec =
    /// <summary>The message identity and its arguments.</summary>
    let descriptor (spec: MessageFormatSpec) = spec.Descriptor

    /// <summary>The owning catalogue's neutral template, used when no resource resolves.</summary>
    let fallback (spec: MessageFormatSpec) = spec.Fallback

    /// <summary>The argument a translator may pluralize on, when the catalogue declares one.</summary>
    /// <remarks>
    /// At most one per entry. Ordinary lookup supports <c>.one</c> for an operand exactly equal to one and
    /// <c>.other</c> otherwise; full CLDR selection belongs to an advanced resolver.
    /// </remarks>
    let pluralArgument (spec: MessageFormatSpec) = spec.Plural

    let internal ofParts fallback plural descriptor =
        { Descriptor = descriptor
          Fallback = fallback
          Plural = plural }

    let internal validate (plural: string option) (descriptor: MessageDescriptor) =
        match plural with
        | Some name when not (MessageDescriptor.arguments descriptor |> Map.containsKey name) ->
            Error(MessageFormatSpecError.UnknownPluralArgument name)
        | _ -> Ok()

    /// <summary>Building specifications for a catalogue of your own.</summary>
    [<RequireQualifiedAccess>]
    module Advanced =
        /// <summary>Builds a specification, returning the validation failure rather than raising.</summary>
        /// <example><code>MessageFormatSpec.Advanced.tryCreate "must be present" None descriptor</code></example>
        let tryCreate
            (fallback: string)
            (pluralArgument: string option)
            (descriptor: MessageDescriptor)
            : Result<MessageFormatSpec, MessageFormatSpecError> =
            validate pluralArgument descriptor
            |> Result.map (fun () -> ofParts fallback pluralArgument descriptor)

        /// <summary>Builds a specification, raising when the plural operand names no argument.</summary>
        /// <example><code>MessageFormatSpec.Advanced.create "must be at least {expected}" None descriptor</code></example>
        let create (fallback: string) (pluralArgument: string option) (descriptor: MessageDescriptor) : MessageFormatSpec =
            match tryCreate fallback pluralArgument descriptor with
            | Ok spec -> spec
            | Error(MessageFormatSpecError.UnknownPluralArgument name) ->
                invalidArg
                    (nameof pluralArgument)
                    $"The plural argument '{name}' is not an argument of '{MessageDescriptor.key descriptor}'."
