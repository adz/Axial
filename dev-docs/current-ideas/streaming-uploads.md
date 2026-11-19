# Streaming Uploads in the HTTP Adapters

Status: sketch. Builds on [`flow-stream-proving.md`](flow-stream-proving.md) and the streaming HTTP section of
[`flow-transport-packages.md`](flow-transport-packages.md); it does not propose a separate streaming model.

## Problem

`Request.form` and `Request.parseForm` buffer the whole body, bounded by `EndpointOptions.MaxRequestBodySize`. That
suits pages and small forms. It does not suit large file uploads. FleetOps takes component builds of hundreds of
megabytes, so `ComponentStorage.receive` drops to `HttpContext`: it raises `IHttpMaxRequestBodySizeFeature` per
request, runs a `MultipartReader` with `BodyLengthLimit`, counts sections, checks field and file names, and copies each
file section to disk in a hand-written loop. None of that is Axial-shaped. Cancellation, cleanup of a half-written file
and the 413 response are the application's job, and the same code cannot run on GenHTTP.

## Proposed shape

A multipart body is a `FlowStream` of parts. A file part's content is a `FlowStream` of owned `byte array` chunks,
following the byte-ownership rule in `flow-stream-proving.md` (no pooled memory whose validity ends on the next pull).

```fsharp
type UploadPart<'env> =
    | Field of name: string * value: string
    | File of name: string * fileName: string * contentType: string * content: FlowStream<'env, UploadError, byte array>

Request.parts : FlowStream<HttpEndpointEnv<'app>, EndpointError<'error>, UploadPart<HttpEndpointEnv<'app>>>
```

Both adapters expose the same `Request.parts`; only the reader differs. ASP.NET Core wraps `MultipartReader` over
`HttpRequest.Body`. GenHTTP parses the boundary over `IRequest.Content` with the same reader code, since
`Microsoft.AspNetCore.WebUtilities` would be a new dependency for the GenHTTP package (see the open questions).

A handler writes each file with an ordinary stream consumer:

```fsharp
Request.parts
|> FlowStream.runForEachFlow (function
    | UploadPart.Field (name, value) -> remember name value
    | UploadPart.File (name, fileName, _, content) -> content |> FileSystem.writeStreamAtomic (incoming name fileName))
```

Rules, from the FlowStream contract:

- **Single pass, in order.** Parts arrive in body order. A file's `content` is valid only until the next part is pulled.
  Pulling the next part drains or skips the rest of the current one. This is the only temporal rule; it should be
  enforced with a typed failure (`UploadError.PartAlreadyPassed`), not left as undefined behavior.
- **Backpressure by pulling.** Nothing reads the socket ahead of the consumer. A slow disk slows the client.
- **Resource safety.** The parts stream is built with `FlowStream.acquireRelease` (today `FlowStream.using`). Early
  termination, a typed failure or interruption releases the reader. A consumer that writes a file owns its cleanup.
  `FileSystem` has `writeAtomic` for a byte array; a streaming `writeStreamAtomic` (write to a temporary file, move
  on success, delete on failure or interruption) is the missing piece, so a half-written file never appears under its
  final name.
- **Limits are enforced as the stream is pulled.** `EndpointOptions.MaxRequestBodySize` caps the whole body for
  `Request.parts` too. On ASP.NET Core it is the per-request server limit plus the reader's `BodyLengthLimit`. On
  GenHTTP the reader counts bytes. Past the limit the pull fails, and the endpoint answers with the same 413 problem
  response as `Request.json` and `Request.form`. An app that takes large uploads raises the limit on that endpoint
  only.
- **Bounded metadata.** A field value is buffered as a string, so it needs its own cap. A part count cap replaces
  FleetOps' `count > 32` check. Both caps belong in `EndpointOptions` or in `Request.parts` arguments; see below.

## Overlap with the other sketches

- `flow-transport-packages.md` streams HTTP *client* response bodies as `FlowStream<unit, HttpError, byte array>`
  (`Http.stream`, `Response.body`). An upload body is the server-side mirror. Use the same chunk type and the same
  single-consumer rule so code that copies a download to disk also copies an upload.
- `flow-stream-proving.md` lists Process, TCP and Serial as proving slices. A multipart upload is a good additional
  slice: one resource (the request body), nested single-consumer streams, early termination, and a hard byte limit.
- The `acquireRelease` naming decision in `flow-stream-proving.md` applies. This sketch uses whatever name it settles
  on and adds no constructor of its own.

## Open questions

- Is a nested stream (`File` carrying `content`) the right shape, or should the adapter hand out
  `FlowStream<..., UploadEvent>` with `FileStart`/`Chunk`/`FileEnd` events? Nesting reads better; flat events make the
  single-pass rule structural and avoid `PartAlreadyPassed`.
- Where do the field-value and part-count caps live: `EndpointOptions`, or arguments to `Request.parts`?
- GenHTTP: reuse `Microsoft.AspNetCore.WebUtilities` (a large dependency for a GenHTTP package) or write a small
  boundary parser in `Axial.Hosting`? A shared parser would also give both adapters identical limit behavior.
- Should `Request.form` and `Request.parseForm` accept `multipart/form-data` on GenHTTP by collecting the non-file
  fields of `Request.parts`? ASP.NET Core's `ReadFormAsync` already does.
- Error typing: does an over-limit or malformed body stay `EndpointError.InvalidRequest` with a problem response, or
  does `UploadError` reach the handler so it can delete partial files before the endpoint ends?
