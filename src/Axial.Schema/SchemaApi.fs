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

    /// <summary>Describes a list whose items use <paramref name="item" />.</summary>
    let list item = SchemaCore.list item
    /// <summary>Describes an optional value.</summary>
    let option item = SchemaCore.option item
    /// <summary>Describes a text-keyed map.</summary>
    let map item = SchemaCore.map item
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

    /// <summary>Creates a progressive record-schema builder.</summary>
    let record constructor = SchemaCore.record constructor
    /// <summary>Creates a progressive record-schema builder anchored to an explicit model type.</summary>
    let recordFor<'model, 'constructor> constructor = SchemaCore.recordFor<'model, 'constructor> constructor
    /// <summary>Attaches a completed field schema to a record builder.</summary>
    let field name getter schema builder = SchemaCore.field name getter schema builder
    /// <summary>Completes a record schema whose constructor is total.</summary>
    let build builder = SchemaCore.build builder
    /// <summary>Completes a record schema whose constructor returns <c>Result&lt;_, string&gt;</c>.</summary>
    let buildResult builder = SchemaCore.buildResult builder
    /// <summary>Completes a record schema whose constructor returns a domain-typed error.</summary>
    let buildResultWith render builder = SchemaCore.buildResultWith render builder
    /// <summary>Compiles a record schema with a typed interpreter factory.</summary>
    let specialize factory schema = SchemaCore.specialize factory schema

    /// <summary>The default raw-input parsing options.</summary>
    let defaults = SchemaParsing.defaults
    /// <summary>Places a record-constructor failure at a field path.</summary>
    let constructorErrorAt path options = SchemaParsing.constructorErrorAt path options
    /// <summary>Parses raw input after configuring parser options.</summary>
    let parseWith configure schema input = SchemaParsing.parseWith configure schema input
    /// <summary>Parses source-neutral raw input, runs constraints and refinements, and invokes record constructors.</summary>
    let parse schema input = SchemaParsing.parse schema input
    /// <summary>Parses raw input with a C#-friendly options delegate.</summary>
    let parseWithOptions options schema input = SchemaParsing.parseWithOptions options schema input
    /// <summary>Checks an already assembled value and re-invokes its record constructor when present.</summary>
    let check schema value = SchemaParsing.check schema value
