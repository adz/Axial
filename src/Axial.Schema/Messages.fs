// Schema's own message catalogue. The identities stay here rather than moving into Axial.Constraint: Schema
// depends on Constraint, never the reverse, and a MessageFormatSpec carries everything the generic renderer
// needs — descriptor, neutral fallback, plural operand — so no Schema key is ever known to that package.
namespace Axial.Schema

open Axial.Constraint

/// <summary>The message keys Schema's own failures render through.</summary>
/// <remarks>
/// <para>
/// Parse, boundary-supply, and structural failures are closed identities with <c>schema.*</c> keys and neutral
/// English fallbacks. Constructor failures and custom errors carrying authored prose stay verbatim: Schema has no
/// catalogue entry for text an application wrote.
/// </para>
/// <para>
/// Entries are bare predicates like the constraint catalogue's, so <c>SchemaErrors.messages</c> and
/// <c>SchemaErrors.fullMessages</c> compose the attribute noun exactly once in either case.
/// </para>
/// </remarks>
[<RequireQualifiedAccess>]
module SchemaMessages =
    type private Entry =
        { Segments: string list
          Arguments: string list
          English: string
          Plural: string option }

    let private entries =
        let entry segments arguments english =
            { Segments = "schema" :: segments
              Arguments = arguments
              English = english
              Plural = None }

        [ entry [ "omitted" ] [] "must be supplied"
          entry [ "blank" ] [] "must be present"
          entry [ "expectedScalar" ] [] "must be a single value"
          entry [ "expectedObject" ] [] "must be an object"
          entry [ "expectedMany" ] [] "must be a collection"
          entry [ "invalidFormat" ] [ "expected" ] "must be a valid {expected}"
          entry [ "parseOutOfRange" ] [ "target" ] "must be within the range of {target}"
          entry [ "unknownTag" ] [ "choices" ] "must be one of {choices}" ]

    let private byKey =
        entries
        |> List.map (fun entry -> String.concat "." entry.Segments, entry)
        |> Map.ofList

    let private specOf key arguments =
        let entry = byKey |> Map.find key

        MessageDescriptor.Advanced.ofSegments entry.Segments arguments
        |> MessageFormatSpec.Advanced.create entry.English entry.Plural

    /// <summary>Every Schema message key, with the arguments its template may interpolate.</summary>
    /// <remarks>Use it the way <c>Catalogue.keys</c> is used: to test that a translation covers Schema too.</remarks>
    /// <example><code>SchemaMessages.keys |> List.filter (translations.ContainsKey >> not)</code></example>
    let keys = entries |> List.map (fun entry -> String.concat "." entry.Segments)

    /// <summary>The argument names each Schema entry interpolates.</summary>
    /// <example><code>SchemaMessages.arguments.["schema.invalidFormat"] // [ "expected" ]</code></example>
    let arguments =
        entries
        |> List.map (fun entry -> String.concat "." entry.Segments, entry.Arguments)
        |> Map.ofList

    /// <summary>The neutral English template for each Schema entry.</summary>
    /// <example><code>SchemaMessages.english.["schema.omitted"] // "must be supplied"</code></example>
    let english =
        entries
        |> List.map (fun entry -> String.concat "." entry.Segments, entry.English)
        |> Map.ofList

    /// <summary>The specification a schema error renders through, or the prose it reports verbatim.</summary>
    /// <remarks>
    /// A constraint violation returns neither: it renders through <c>Violation.message</c>, which owns group and
    /// actual-value composition.
    /// </remarks>
    let internal trySpec (error: SchemaError) : Choice<MessageFormatSpec, string> option =
        let text value = ConstraintValue.Text value

        match error with
        | SchemaError.Omitted -> Some(Choice1Of2(specOf "schema.omitted" Map.empty))
        | SchemaError.Blank -> Some(Choice1Of2(specOf "schema.blank" Map.empty))
        | SchemaError.ExpectedScalar -> Some(Choice1Of2(specOf "schema.expectedScalar" Map.empty))
        | SchemaError.ExpectedObject -> Some(Choice1Of2(specOf "schema.expectedObject" Map.empty))
        | SchemaError.ExpectedMany -> Some(Choice1Of2(specOf "schema.expectedMany" Map.empty))
        | SchemaError.InvalidFormat expected ->
            Some(Choice1Of2(specOf "schema.invalidFormat" (Map [ "expected", text expected ])))
        | SchemaError.ParseOutOfRange target ->
            Some(Choice1Of2(specOf "schema.parseOutOfRange" (Map [ "target", text target ])))
        | SchemaError.UnknownTag choices ->
            Some(Choice1Of2(specOf "schema.unknownTag" (Map [ "choices", text choices ])))
        | SchemaError.ConstructorFailed message -> Some(Choice2Of2 message)
        // A custom code is an application-relative key under the ordinary descriptor grammar; its own message is
        // the fallback. A code that is not a valid key names nothing a catalogue could hold, so the prose stands.
        | SchemaError.Custom(code, message) ->
            let fallback = message |> Option.defaultValue code

            match MessageDescriptor.Advanced.tryCreate code Map.empty with
            | Ok descriptor -> Some(Choice1Of2(MessageFormatSpec.Advanced.create fallback None descriptor))
            | Error _ -> Some(Choice2Of2 fallback)
        | SchemaError.Violation _ -> None
