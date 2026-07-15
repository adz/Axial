namespace Axial.Schema.Http

open System.IO
open System.Text.Json
open Axial.Validation
open Axial.Schema

/// <summary>Renders diagnostics paths as RFC 6901 JSON pointers such as <c>/address/street</c> or <c>/tags/0</c>.</summary>
[<RequireQualifiedAccess>]
module JsonPointer =
    let private escape (token: string) =
        token.Replace("~", "~0").Replace("/", "~1")

    /// <summary>Renders a diagnostics path as a JSON pointer. The empty path renders as <c>""</c> (the whole document).</summary>
    let ofPath (path: Path) : string =
        path
        |> List.map (function
            | PathSegment.Index index -> "/" + string index
            | PathSegment.Key key -> "/" + escape key
            | PathSegment.Name name -> "/" + escape name)
        |> String.concat ""

/// <summary>One boundary error: a JSON pointer into the request body plus a rendered message.</summary>
type ProblemError =
    {
        /// <summary>RFC 6901 JSON pointer to the offending value; <c>""</c> points at the whole document.</summary>
        Pointer: string
        /// <summary>The rendered error message.</summary>
        Message: string
    }

/// <summary>An RFC 9457 problem-details value carrying path-aware parse errors.</summary>
/// <remarks>
/// This is the shared error contract for schema-driven endpoints: every host adapter renders the same JSON body with
/// media type <c>application/problem+json</c>, so clients handle one error shape regardless of the server behind it.
/// </remarks>
type ProblemDetails =
    { Type: string
      Title: string
      Status: int
      Detail: string option
      Errors: ProblemError list }

[<RequireQualifiedAccess>]
module ProblemDetails =
    /// <summary>The media type problem-details responses must be served with.</summary>
    [<Literal>]
    let ContentType = "application/problem+json"

    /// <summary>Builds a 400 problem-details value from parse diagnostics, rendering each error with <paramref name="render" />.</summary>
    let ofDiagnosticsWith (render: 'error -> string) (diagnostics: Diagnostics<'error>) : ProblemDetails =
        { Type = "https://datatracker.ietf.org/doc/html/rfc9457"
          Title = "The request input could not be parsed."
          Status = 400
          Detail = None
          Errors =
            Diagnostics.flatten diagnostics
            |> List.map (fun diagnostic ->
                { Pointer = JsonPointer.ofPath diagnostic.Path
                  Message = render diagnostic.Error }) }

    /// <summary>Builds a 400 problem-details value from failed schema parse diagnostics.</summary>
    let ofDiagnostics (diagnostics: Diagnostics<SchemaError>) : ProblemDetails =
        ofDiagnosticsWith SchemaError.render diagnostics

    /// <summary>Builds a 400 problem-details value from a failed parse, or <c>None</c> when parsing succeeded.</summary>
    let ofParsed (parsed: ParsedInput<'model, SchemaError>) : ProblemDetails option =
        match parsed.Result with
        | Ok _ -> None
        | Error diagnostics -> Some(ofDiagnostics diagnostics)

    /// <summary>Builds a 400 problem-details value for a syntactically invalid JSON request body.</summary>
    let malformedJson : ProblemDetails =
        { Type = "https://datatracker.ietf.org/doc/html/rfc9457"
          Title = "The request input could not be parsed."
          Status = 400
          Detail = None
          Errors =
            [ { Pointer = ""
                Message = "The request body is not valid JSON." } ] }

    /// <summary>Writes the problem-details JSON body to a stream.</summary>
    let writeTo (stream: Stream) (problem: ProblemDetails) : unit =
        use writer = new Utf8JsonWriter(stream)
        writer.WriteStartObject()
        writer.WriteString("type", problem.Type)
        writer.WriteString("title", problem.Title)
        writer.WriteNumber("status", problem.Status)

        match problem.Detail with
        | Some detail -> writer.WriteString("detail", detail)
        | None -> ()

        writer.WriteStartArray "errors"

        for error in problem.Errors do
            writer.WriteStartObject()
            writer.WriteString("pointer", error.Pointer)
            writer.WriteString("message", error.Message)
            writer.WriteEndObject()

        writer.WriteEndArray()
        writer.WriteEndObject()

    /// <summary>Renders the problem-details JSON body as a string.</summary>
    let toJson (problem: ProblemDetails) : string =
        use stream = new MemoryStream()
        writeTo stream problem
        System.Text.Encoding.UTF8.GetString(stream.ToArray())

    /// <summary>The JSON Schema for problem-details bodies, for embedding in OpenAPI error responses.</summary>
    let jsonSchema: string =
        """{"type":"object","properties":{"type":{"type":"string"},"title":{"type":"string"},"status":{"type":"integer"},"detail":{"type":"string"},"errors":{"type":"array","items":{"type":"object","properties":{"pointer":{"type":"string"},"message":{"type":"string"}},"required":["pointer","message"]}}},"required":["type","title","status","errors"]}"""
