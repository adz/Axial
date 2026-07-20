// The public Schema module: the facade every consumer opens. Each function delegates to an internal
// implementation (ValueSchema, SchemaCore, Parsing, ShapeOps) — no logic lives here, so the public
// surface can be read top to bottom as a catalog.
namespace Axial.Schema

/// <summary>Construction, composition, parsing, and checking for universal schemas.</summary>
[<RequireQualifiedAccess>]
module Schema =
    /// <summary>Describes text input.</summary>
    let text = SchemaCore.text
    /// <summary>Describes a 32-bit integer.</summary>
    let ``int`` = SchemaCore.``int``
    /// <summary>Describes a decimal number.</summary>
    let ``decimal`` = SchemaCore.``decimal``
    /// <summary>Describes a Boolean value.</summary>
    let ``bool`` = SchemaCore.``bool``
#if NET8_0_OR_GREATER
    /// <summary>Describes a calendar date.</summary>
    let date = SchemaCore.date
#endif
    /// <summary>Describes a date and time with an offset.</summary>
    let dateTime = SchemaCore.dateTime
    /// <summary>Describes a GUID.</summary>
    let guid = SchemaCore.guid

    /// <summary>Describes a list using an explicit item schema.</summary>
    let listWith item = SchemaCore.listWith item
    /// <summary>Describes a list by resolving its item schema from <typeparamref name="'item" />.</summary>
    let inline list () : Schema< ^item list> = listWith (SchemaDefaults.Resolve())
    /// <summary>Describes an optional value.</summary>
    let option item = SchemaCore.option item
    /// <summary>Describes a string-keyed map using an explicit value schema.</summary>
    let mapWith item = SchemaCore.mapWith item
    /// <summary>Describes a string-keyed map by resolving its value schema from <typeparamref name="'item" />.</summary>
    let inline map () : Schema<Map<string, ^item>> = mapWith (SchemaDefaults.Resolve())
    /// <summary>Defers a recursive schema reference until an interpreter needs it.</summary>
    let defer schema = SchemaCore.defer schema
    /// <summary>Maps a schema through a total, reversible domain conversion.</summary>
    let convert construct inspect schema = SchemaCore.convert construct inspect schema
    /// <summary>Maps a schema through a fallible smart constructor and lowers its failures to schema errors.</summary>
    /// <remarks>Use this for intrinsic domain constraints. <paramref name="inspect" /> supplies the raw representation to checking, encoding, and metadata interpreters.</remarks>
    let refine construct mapError inspect schema = SchemaCore.refine construct mapError inspect schema
    /// <summary>Describes an externally tagged union.</summary>
    let union discriminator payload cases = SchemaCore.union discriminator payload cases
    /// <summary>Describes an internally tagged union.</summary>
    let inlineUnion discriminator cases = SchemaCore.inlineUnion discriminator cases
    /// <summary>Describes a scalar enum.</summary>
    let enum cases = SchemaCore.enum cases

    /// <summary>Adds one portable constraint to a schema.</summary>
    let constrain constraint' schema = SchemaCore.constrain constraint' schema
    /// <summary>Adds portable constraints to a schema in declaration order.</summary>
    let constrainAll constraints schema = SchemaCore.constrainAll constraints schema
    /// <summary>Adds format metadata.</summary>
    let withFormat format schema = SchemaCore.withFormat format schema
    /// <summary>Adds human-readable description metadata.</summary>
    let describe text schema = SchemaCore.describe text schema
    /// <summary>Adds default-value metadata.</summary>
    let withDefault value schema = SchemaCore.withDefault value schema

    let format schema = SchemaCore.format schema
    let description schema = SchemaCore.description schema
    let defaultValue schema = SchemaCore.defaultValue schema
    let constraints schema = SchemaCore.constraints schema
    let isRefined schema = SchemaCore.isRefined schema
    let primitiveKind schema = SchemaCore.primitiveKind schema
    let underlyingPrimitiveKind schema = SchemaCore.underlyingPrimitiveKind schema
    let rawConstraints schema = SchemaCore.rawConstraints schema
    let inspectUnderlying schema = SchemaCore.inspectUnderlying schema
    let allConstraints schema = SchemaCore.allConstraints schema

    /// <summary>Compiles a record schema with a typed interpreter factory.</summary>
    let compilePlan factory schema = SchemaCore.compilePlan factory schema

    /// <summary>The default raw-input parsing options.</summary>
    let defaults = SchemaParsing.defaults
    /// <summary>Places a record-constructor failure at a field path.</summary>
    let constructorErrorAt path options = SchemaParsing.constructorErrorAt path options
    /// <summary>Parses structured data after configuring parser options.</summary>
    let parseWith configure schema input = SchemaParsing.parseWith configure schema input
    /// <summary>Parses source-neutral structured data, runs constraints and refinements, and invokes record constructors.</summary>
    let parse schema input = SchemaParsing.parse schema input
    /// <summary>Parses source-neutral structured data while retaining it for redisplay and error lookup.</summary>
    let parseRetainingInput schema input = SchemaParsing.parseRetainingInput schema input
    /// <summary>Parses structured data with a C#-friendly options delegate.</summary>
    let parseWithOptions options schema input = SchemaParsing.parseWithOptions options schema input
    /// <summary>Checks an existing typed value, such as a freely constructed draft, through the schema's constraints, refinements, and record constructor.</summary>
    let check schema value = SchemaParsing.check schema value

    /// <summary>Starts a constructor-last structural shape for a model: add fields with <c>Syntax.field</c>,
    /// then close the shape with <c>construct</c> or <c>constructResult</c>.</summary>
    let define<'model> : DefineShape<'model> = ShapeOps.define<'model>

    /// <summary>Admits a permissive draft model schema into a trusted domain schema through an admission
    /// function and a projection, preserving fields, wire names, constraints, and metadata.</summary>
    let admit (create: 'draft -> Result<'domain, string>) (project: 'domain -> 'draft) (draft: Schema<'draft>) : Schema<'domain> =
        ShapeOps.admit create project draft
