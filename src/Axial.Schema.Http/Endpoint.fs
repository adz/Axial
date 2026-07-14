namespace Axial.Schema.Http

open System.IO
open System.Text.Json
open Axial.Schema

/// <summary>One documented response of an endpoint.</summary>
type ResponseSpec =
    {
        /// <summary>The HTTP status code.</summary>
        Status: int
        /// <summary>Human-readable description required by OpenAPI.</summary>
        Description: string
        /// <summary>JSON Schema text for the response body, or <c>None</c> for a body-less response.</summary>
        JsonSchema: string option
        /// <summary>Media type of the response body; ignored when <see cref="F:Axial.Schema.Http.ResponseSpec.JsonSchema" /> is <c>None</c>.</summary>
        MediaType: string
    }

/// <summary>A host-neutral description of one schema-driven endpoint, used to assemble OpenAPI documents.</summary>
/// <remarks>
/// The spec deliberately does not describe routing or handlers: hosts keep their own idioms for those. It records
/// the boundary contract — method, path, request schema, and responses — which is the part every host shares.
/// </remarks>
type EndpointSpec =
    { Method: string
      Path: string
      Summary: string option
      OperationId: string option
      Tags: string list
      /// <summary>JSON Schema text for the request body, produced by <see cref="M:Axial.Schema.JsonSchemaModule.generate" />.</summary>
      RequestSchema: string option
      Responses: ResponseSpec list }

/// <summary>Builder functions for <see cref="T:Axial.Schema.Http.EndpointSpec" />.</summary>
/// <example>
/// <code>
/// let createSignup =
///     Endpoint.post "/signups"
///     |> Endpoint.summary "Create a signup"
///     |> Endpoint.accepts Signup.schema
///     |> Endpoint.returnsJson 201 "The trusted signup that was parsed." Signup.schema
///     |> Endpoint.returnsProblemDetails
/// </code>
/// </example>
[<RequireQualifiedAccess>]
module Endpoint =
    let private create method path =
        { Method = method
          Path = path
          Summary = None
          OperationId = None
          Tags = []
          RequestSchema = None
          Responses = [] }

    /// <summary>Starts a GET endpoint spec at the supplied path.</summary>
    let get (path: string) = create "get" path

    /// <summary>Starts a POST endpoint spec at the supplied path.</summary>
    let post (path: string) = create "post" path

    /// <summary>Starts a PUT endpoint spec at the supplied path.</summary>
    let put (path: string) = create "put" path

    /// <summary>Starts a PATCH endpoint spec at the supplied path.</summary>
    let patch (path: string) = create "patch" path

    /// <summary>Starts a DELETE endpoint spec at the supplied path.</summary>
    let delete (path: string) = create "delete" path

    /// <summary>Sets the operation summary shown in generated documents.</summary>
    let summary (text: string) (spec: EndpointSpec) = { spec with Summary = Some text }

    /// <summary>Sets the OpenAPI operation id.</summary>
    let operationId (id: string) (spec: EndpointSpec) = { spec with OperationId = Some id }

    /// <summary>Appends an OpenAPI tag used to group operations.</summary>
    let tag (name: string) (spec: EndpointSpec) = { spec with Tags = spec.Tags @ [ name ] }

    /// <summary>Declares the request body: JSON described by the schema's generated JSON Schema.</summary>
    let accepts (schema: Schema<'model>) (spec: EndpointSpec) =
        { spec with RequestSchema = Some(JsonSchema.generate schema) }

    /// <summary>Adds a JSON response whose body is described by the schema's generated JSON Schema.</summary>
    let returnsJson (status: int) (description: string) (schema: Schema<'model>) (spec: EndpointSpec) =
        { spec with
            Responses =
                spec.Responses
                @ [ { Status = status
                      Description = description
                      JsonSchema = Some(JsonSchema.generate schema)
                      MediaType = "application/json" } ] }

    /// <summary>Adds a body-less response.</summary>
    let returns (status: int) (description: string) (spec: EndpointSpec) =
        { spec with
            Responses =
                spec.Responses
                @ [ { Status = status
                      Description = description
                      JsonSchema = None
                      MediaType = "application/json" } ] }

    /// <summary>Adds the standard 400 problem-details response every schema-parsing endpoint produces.</summary>
    let returnsProblemDetails (spec: EndpointSpec) =
        { spec with
            Responses =
                spec.Responses
                @ [ { Status = 400
                      Description = "Path-aware parse diagnostics."
                      JsonSchema = Some ProblemDetails.jsonSchema
                      MediaType = ProblemDetails.ContentType } ] }

/// <summary>Document-level OpenAPI metadata.</summary>
type OpenApiInfo =
    { Title: string
      Version: string
      Description: string option }

/// <summary>Assembles an OpenAPI 3.1 document from endpoint specs.</summary>
[<RequireQualifiedAccess>]
module OpenApi =
    /// <summary>Builds document metadata with no description.</summary>
    let info (title: string) (version: string) : OpenApiInfo =
        { Title = title
          Version = version
          Description = None }

    /// <summary>Writes an OpenAPI 3.1 JSON document covering the supplied endpoints to a stream.</summary>
    /// <remarks>
    /// Request and response schemas are embedded verbatim from the generated JSON Schema text, so the published
    /// contract cannot drift from what the parser accepts.
    /// </remarks>
    let writeTo (stream: Stream) (documentInfo: OpenApiInfo) (endpoints: EndpointSpec list) : unit =
        use writer = new Utf8JsonWriter(stream)
        writer.WriteStartObject()
        writer.WriteString("openapi", "3.1.0")

        writer.WriteStartObject "info"
        writer.WriteString("title", documentInfo.Title)
        writer.WriteString("version", documentInfo.Version)

        match documentInfo.Description with
        | Some description -> writer.WriteString("description", description)
        | None -> ()

        writer.WriteEndObject()

        writer.WriteStartObject "paths"

        for path, specs in endpoints |> List.groupBy _.Path do
            writer.WriteStartObject path

            for spec in specs do
                writer.WriteStartObject spec.Method

                match spec.Summary with
                | Some summary -> writer.WriteString("summary", summary)
                | None -> ()

                match spec.OperationId with
                | Some id -> writer.WriteString("operationId", id)
                | None -> ()

                match spec.Tags with
                | [] -> ()
                | tags ->
                    writer.WriteStartArray "tags"

                    for tag in tags do
                        writer.WriteStringValue tag

                    writer.WriteEndArray()

                match spec.RequestSchema with
                | Some schema ->
                    writer.WriteStartObject "requestBody"
                    writer.WriteBoolean("required", true)
                    writer.WriteStartObject "content"
                    writer.WriteStartObject "application/json"
                    writer.WritePropertyName "schema"
                    writer.WriteRawValue schema
                    writer.WriteEndObject()
                    writer.WriteEndObject()
                    writer.WriteEndObject()
                | None -> ()

                writer.WriteStartObject "responses"

                for response in spec.Responses do
                    writer.WriteStartObject(string response.Status)
                    writer.WriteString("description", response.Description)

                    match response.JsonSchema with
                    | Some schema ->
                        writer.WriteStartObject "content"
                        writer.WriteStartObject response.MediaType
                        writer.WritePropertyName "schema"
                        writer.WriteRawValue schema
                        writer.WriteEndObject()
                        writer.WriteEndObject()
                    | None -> ()

                    writer.WriteEndObject()

                writer.WriteEndObject()
                writer.WriteEndObject()

            writer.WriteEndObject()

        writer.WriteEndObject()
        writer.WriteEndObject()

    /// <summary>Renders an OpenAPI 3.1 JSON document covering the supplied endpoints.</summary>
    let document (documentInfo: OpenApiInfo) (endpoints: EndpointSpec list) : string =
        use stream = new MemoryStream()
        writeTo stream documentInfo endpoints
        System.Text.Encoding.UTF8.GetString(stream.ToArray())
