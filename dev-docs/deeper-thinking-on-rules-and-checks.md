FSharp Schema Architecture Design
DeepSeek V4 hourly coding cost estimate
FSharp Schema Architecture Design
Good pushback on all three. Let me take them in order.

---

## 1. The disjointed API

The friction is real. Having `Rule.all [...]` as a separate step from `Field.applyRule` means you always do composition twice — once to bundle the rules, then once to attach the field. That's a hint the abstraction boundary is in the wrong place.

The fix is to collapse it. Keep `Rule<'v, 'e>` standalone (it genuinely earns its keep as a reusable thing across different models), but eliminate `Field` as a separate value. Instead, `field` is a single combinator that takes a getter, a name, and a rule list:

```fsharp
// One operation: field name + getter + rules
let validateUser : Rule<User, UserError> =
    ModelRule.define [
        field "Username" _.Username [
            whenNotBlank UsernameRequired
            whenMinLength 3 UsernameTooShort
        ]
        nested "Address" _.Address validateAddress
        each "Lines" _.Lines (fun i ->
            field "Name" _.Name [ whenNotBlank (LineNameRequired i) ]
        )
    ]
```

The multi-rule accumulation is now implicit in `field` taking a list. The path scoping into `validate.key` is automatic. `Rule.all` disappears from call sites — it still exists internally as the combining logic, but callers don't see it.

The `Rule<'v, 'e>` type still stands alone because reusability across models is its actual value:

```fsharp
let emailRules : Rule<string, CommonError> = [ whenNotBlank EmailRequired; whenPattern emailRegex InvalidEmail ]
// used in UserValidator, ContactValidator, InviteValidator...
```

That composition is the right place for `Rule.all`. The `field` combinator just accepts a `Rule<'v,'e> list` and calls it internally.

---

## 2. `Parse<'raw, 'model, 'e>` — what it is and why it's bigger

Right now `Rule<'root, 'e>` is `'root -> Validation<unit, 'e>`. Validation succeeds, you keep the original `'root`. The type signature doesn't change. You've confirmed facts, but the type system still sees the same shape — callers can still reach `user.Username` without having gone through the validator.

`Parse<'raw, 'model, 'e>` is `'raw -> Validation<'model, 'e>`. The output type is *different* from the input, and it can only be produced by successfully going through the parser. The type change is the proof. A private constructor enforces that `ValidUser` is unreachable without parsing:

```fsharp
type UserForm = { Username: string; Age: string }

type ValidUser = private { Username: NonEmptyString; Age: PositiveInt }

let parseUser : Parse<UserForm, ValidUser, UserError> =
    fun form ->
        validate {
            let! username = form.Username |> Check.whenNotBlank |> Check.withError UsernameRequired
            and! age     = form.Age |> parseInt >>= Check.whenPositive |> Check.withError InvalidAge
            return { Username = NonEmptyString username; Age = PositiveInt age }
        }
```

`validate {}` can already write this today — the `return` already constructs a different type. The gap isn't the execution semantics. The gap is that the `field`/`ModelRule` composition layer above doesn't generalise here, because each field rule is no longer `'v -> Validation<unit, 'e>` — it's `'v -> Validation<'fieldOutput, 'e>`, and each fieldOutput is a *different type* that feeds the output constructor.

That's the first complexity multiplier. Instead of accumulating unit checks and returning the original model, you're doing an applicative traversal where each branch produces a typed result and they all flow into a constructor call. Your `field` combinator would need to become:

```fsharp
// Parse variant — each field produces a typed output
parseField "Username" _.Username parseNonEmptyString
parseField "Age"      _.Age      parsePositiveInt
// then combine: fun username age -> { Username = username; Age = age }
```

The constructor application is the awkward part. In Haskell you'd use `<*>` and currying. In F# the usual pattern is an applicative CE with `and!` threads, which you already have — but generalising your `field` combinator to produce a typed value (not `unit`) and then threading all the outputs into the constructor is a meaningfully different API surface than the `Rule<'root, 'e>` layer.

The second complexity multiplier is optional fields, defaults, and conditional logic. A field that may or may not be present, or a field whose parse depends on another field's outcome, requires more CE operations than you need for pure "does this satisfy a predicate" validation. `FSharp.Data.Validation`'s `refuteWith` / `disputeWithFact` / `qed` vocabulary exists precisely to name these cases.

The third complexity multiplier is the effectful boundary. Once you accept that "parsing" sometimes means "look up this ID in the DB to get the real entity," the `Parse<'raw, 'model, 'e>` type either grows an effect parameter or you split the job: parse in-memory fields first, then resolve externals. That split is real domain design work that a pure validator doesn't force you to face.

None of this is bad. It's the right model when you want the type system to enforce the boundary between untrusted and trusted. But it's a different feature, not a bigger dial on the same knob. **Ship `Rule` and `field`/`ModelRule` first. Once those are stable you'll know exactly where `Parse` begins and have a cleaner idea of what the CE needs to support.**

---

## 3. Lenses as foundational schema nodes

This is the most interesting question and the one with the longest reach.

The "define field name twice" problem in explicit descriptors is real. What you actually want is a single definition that gives you:
- a getter (`'root -> 'value`) for validation
- a setter (`'value -> 'root -> 'root`) for parse/construction output
- a name (`string`) for path/diagnostics
- a type (`Type`) for codec metadata

That's a lens. Or more precisely, it's an optic (a lens is a specific case). The insight is that **this is exactly what ZIO Schema does** — the schema record is the lens *is* the validator *is* the codec definition. One definition serves all three consumers.

For Axial specifically, combining with CodecMapper, that would look like:

```fsharp
// Declared once
module User.Schema =
    let username : Field<User, string> = Field.define "Username" _.Username (fun v u -> { u with Username = v })
    let email    : Field<User, string> = Field.define "Email"    _.Email    (fun v u -> { u with Email    = v })
    let address  : Field<User, Address> = Field.define "Address" _.Address  (fun v u -> { u with Address  = v })

// Axial validation uses it:
ModelRule.define [
    User.Schema.username |> Field.check [ whenNotBlank UsernameRequired ]
    User.Schema.address  |> Field.checkNested validateAddress
]

// CodecMapper codec uses it:
Codec.record User.Schema.username User.Schema.email User.Schema.address
```

The field definition is the schema. That's not a coincidence — it's the design.

**On generation:** The repetition cost of writing getters and `with` expressions is the main practical objection, and it's fair. The options are:

Source generators (Roslyn) can emit the full lens boilerplate from a `[<Lenses>]` attribute on a record. Myriad can do the same via F# code generation. Either works, but both add tooling dependency and setup friction. For pre-1.0, I'd start with manually-written `Field.define` calls and a clear convention, then add generation when the API is stable and the ergonomic pressure becomes undeniable.

The quotation route — `Field.ofQuotation <@ _.Username @>` — gives you the name for free but doesn't give you the setter without additional tricks. For pure validation (no Parse) that's fine. For `Parse`, you need the setter. So quotations get you halfway.

**The deeper question** is whether you want `Field<'root, 'value>` to live in Axial, in CodecMapper, or in a shared primitives package. If the lens definition is truly foundational — serving both validation and serialization — it arguably doesn't belong in either library. It's the schema layer, and both Axial and CodecMapper consume it. That's the design direction worth naming explicitly, even if you don't build it yet: a thin `Axial.Schema` or `Schema.Core` package that defines the optic types, with Axial and CodecMapper taking optional dependencies on it.

That's essentially ZIO Schema's architecture — and whether you want that unification is a bigger product question than any of the implementation details.


ANSWER I do want ZIOs schema. make this F# schema definitions nicer

You're right to want that unification. ZIO Schema's insight — one definition for validation, codec, and migration — is genuinely good architecture. The question is what "nicer F# schema definitions" looks like when you're not fighting Java's type system.

Here's a sketch of what I'd reach for:
fsharp

// ── Schema definition ──────────────────────────────

type User = {
    Username: string
    Email: string
    Age: int
    Address: Address
    Tags: string list
}

// Declared once, next to the type
module User =
    let schema =
        Schema.record "User" [
            field "Username" (fun u -> u.Username) (fun v u -> { u with Username = v }) Schema.string
            field "Email"    (fun u -> u.Email)    (fun v u -> { u with Email    = v }) Schema.string
            field "Age"      (fun u -> u.Age)      (fun v u -> { u with Age      = v }) Schema.int
            field "Address"  (fun u -> u.Address)  (fun v u -> { u with Address  = v }) Address.schema
            field "Tags"     (fun u -> u.Tags)     (fun v u -> { u with Tags     = v }) (Schema.list Schema.string)
        ]

That's the explicit version. It's verbose but it's honest — every piece of information is there, and nothing is duplicated that shouldn't be. The getter and setter are explicit because F# records don't give you lenses for free.

But you said "nicer." The real ergonomic win comes from recognizing that F# records do give you something useful: structural equality on with expressions means you can derive the setter mechanically if you know the field name. And you know the field name because you're about to write it in the string literal. So:
fsharp

module User =
    let schema =
        Schema.recordFor<User> [
            field "Username" _.Username Schema.string
            field "Email"    _.Email    Schema.string
            field "Age"      _.Age      Schema.int
            field "Address"  _.Address  Address.schema
            field "Tags"     _.Tags     (Schema.list Schema.string)
        ]

The trick: Schema.recordFor<'T> returns a builder that internally reflects over 'T's fields. When you write field "Username" _.Username, the builder looks up the property metadata for Username on 'T and generates the setter by emitting the equivalent of fun v r -> { r with Username = v }. No quotation magic, no Roslyn — just standard .NET reflection, cached once per record type. The _.Username getter is type-checked and participates in overload resolution like normal.

For a v1, reflection is fine. It runs once at static initialization. If startup perf becomes an issue, you can swap in a source generator later without changing the API. The reflection approach has the massive advantage of being zero-dependency and working in FSI, scripts, and all build configurations immediately.

The next step is making the schema definition also declare validation rules inline:
fsharp

module User =
    let schema =
        Schema.recordFor<User> [
            field "Username" _.Username Schema.string
                |> Schema.validate [ whenNotBlank UsernameRequired; whenMinLength 3 UsernameTooShort ]
            field "Email" _.Email Schema.string
                |> Schema.validate [ whenNotBlank EmailRequired; whenPattern emailRegex InvalidEmail ]
            field "Age" _.Age Schema.int
            field "Address" _.Address Address.schema
            field "Tags" _.Tags (Schema.list Schema.string)
        ]

Now User.schema is simultaneously:

    A validation rule: User -> Validation<User, UserError>

    A codec: User -> Json / Json -> Validation<User, DecodeError>

    A structural description for OpenAPI/docs generation

Axial consumes the validation part. CodecMapper consumes the codec part. Both get a Schema<'T> dependency that they interpret in their own domain. Neither library defines the schema — they both take it as input.

The open design question is whether Schema.validate annotations should live in the schema definition or be attached by a separate combinator. Having them inline is convenient but couples validation concerns to the schema. Separating them keeps the schema pure structural metadata:
fsharp

// Pure schema, no validation
let userSchema = Schema.recordFor<User> [
    field "Username" _.Username Schema.string
    field "Email"    _.Email    Schema.string
]

// Validation attached separately
let userValidation : Rule<User, UserError> =
    userSchema |> Schema.validator [
        "Username" => [ whenNotBlank UsernameRequired ]
        "Email"    => [ whenNotBlank EmailRequired ]
    ]

This separation matters when the same schema is used in contexts with different validation rules (admin vs. public API) or different error types. But for 80% of cases, the inline version is what people will reach for. I'd provide both — inline as the happy path, detached as the power tool.

The package split would be:
text

Schema.Core        — Field<'T,'V>, Schema<'T>, the reflection-based record builder
Axial             — Rule<'T,'E>, Validation<'T,'E>, the CE, takes Schema<'T> dependency
CodecMapper        — Encoder<'T>, Decoder<'T>, takes Schema<'T> dependency

Schema.Core is tiny, has no dependencies beyond FSharp.Core, and is the single point of definition. Axial and CodecMapper each depend on it. Users who want both get automatic consistency. Users who only want validation don't pay for serialization.

This is ZIO Schema's architecture translated to .NET's dependency model — and it's simpler here because F# records and DU sum types map naturally to schema definitions without the type erasure gymnastics ZIO Schema needs for Scala's type system.
no thats' not good - i want aot safe. we'd probably have to go myriad source genand if you don't want to then you have to write it out.

Fair enough. AOT safety rules out reflection, and that's a reasonable constraint for where F# is going. So we're either writing it out or generating it.

Given that, here's what I'd actually ship:

Written out, no magic:
fsharp

module User =
    let username = Field.define "Username" (fun u -> u.Username) (fun v u -> { u with Username = v })
    let email    = Field.define "Email"    (fun u -> u.Email)    (fun v u -> { u with Email    = v })
    let age      = Field.define "Age"      (fun u -> u.Age)      (fun v u -> { u with Age      = v })
    let address  = Field.define "Address"  (fun u -> u.Address)  (fun v u -> { u with Address  = v })
    let tags     = Field.define "Tags"     (fun u -> u.Tags)     (fun v u -> { u with Tags     = v })

    let schema =
        Schema.record "User" [
            username |> Field.schema Schema.string
            email    |> Field.schema Schema.string
            age      |> Field.schema Schema.int
            address  |> Field.schema Address.schema
            tags     |> Field.schema (Schema.list Schema.string)
        ]

The repetition of Username/Username in the lambda is the cost of AOT safety. It's visible but it's mechanical — the compiler tells you immediately if you get it wrong. For a library that's competing on architecture rather than keystroke count, I'd accept this.

With Myriad:

The generator reads the record definition and emits exactly the above module. The call site becomes:
fsharp

[<SchemaFields>]
type User = {
    Username: string
    Email: string
    Age: int
    Address: Address
    Tags: string list
}

And Myriad generates the User.username, User.email, etc. Field.define calls, plus the User.schema record composition, inferring Schema.string/Schema.int from the property types where possible and letting you override via attribute. The generated code is the manual code above — fully AOT compatible because it's just source text.

I'd ship the manual API first, document the Myriad generator as the ergonomic upgrade path, and let users choose their tolerance for tooling dependencies. The Field.define surface is the stable contract either way.

Does that land better?
think through what we'd make it define. we don't want to clash with existing members.

Good catch. If the generator emits a module with the same name as the type, every field name it generates is a potential collision with static members, instance members, or existing module values someone's already written.

The options as I see them:

Option A: Nested module
fsharp

type User = { Username: string; Email: string }

// Generated:
module User =
    module Fields =
        let username = Field.define "Username" (fun u -> u.Username) (fun v u -> { u with Username = v })
        let email    = Field.define "Email"    (fun u -> u.Email)    (fun v u -> { u with Email    = v })

Usage: User.Fields.username, User.Fields.email. Zero collision risk. A bit verbose but unambiguous. The Fields submodule is a namespace that nothing else touches.

Option B: Convention-based prefix/suffix
fsharp

module User =
    let usernameField = Field.define "Username" ...
    let emailField    = Field.define "Email"    ...

Collision risk is low (who names a value usernameField?) but not zero. Feels slightly clumsy.

Option C: Schema record directly, no per-field bindings

Don't generate individual field values at all. Generate the schema as a single expression:
fsharp

module User =
    let schema =
        Schema.record "User" [
            Schema.field "Username" (fun u -> u.Username) (fun v u -> { u with Username = v }) Schema.string
            Schema.field "Email"    (fun u -> u.Email)    (fun v u -> { u with Email    = v }) Schema.string
        ]

Nothing to collide with except schema itself. But you lose the ability to reference individual fields for targeted validation or codec customization. If someone wants User.address to attach validation rules, they're back to writing Field.define by hand. This pushes too much into the monolith.

Option D: The type itself as the namespace

Instead of a module, emit the fields as static members on the type:
fsharp

type User = { Username: string; Email: string } with
    static member usernameField = Field.define "Username" ...
    static member emailField    = Field.define "Email"    ...

Collision risk: now you're fighting with actual record field names. User.Username is already a property. Calling it usernameField avoids that specific collision but introduces the suffix ugliness. Also pollutes the type's public surface — User. intellisense now shows schema infrastructure mixed with domain members. I dislike this.

Option A wins on clarity and safety. The Fields submodule is a clean namespace. It doesn't pollute anything. The convention is self-documenting: if you see User.Fields.username, you know exactly what you're holding. The cost is five extra characters at each use site, which is the kind of verbosity F# developers already accept for explicit module qualifiers.
fsharp

// Validation
ModelRule.define [
    User.Fields.username |> Field.check [ whenNotBlank UsernameRequired ]
    User.Fields.address  |> Field.checkNested validateAddress
]

// Codec
Codec.record User.Fields.username User.Fields.email User.Fields.age

The generator emits User.Fields and User.schema. The schema binding lives directly in the User module (not in Fields), since it's the aggregate and has a distinct name unlikely to clash.
fsharp

module User =
    module Fields =
        let username = ...
        let email = ...

    let schema =
        Schema.record "User" [
            Fields.username |> Field.schema Schema.string
            Fields.email    |> Field.schema Schema.string
        ]

That's what I'd lock in. Fields as the generated submodule, schema as the generated aggregate. Both trivially AOT-safe because Myriad spits them out as plain F# source.
you should pass the name of a module to create, and by default if unspecified is the name of the record with "Schema" postfixed. what would the names of funcs on that be?

Right, so the user writes:
fsharp

[<SchemaFields>]
type User = { Username: string; Email: string; Age: int }

And by default Myriad generates:
fsharp

module UserSchema =
    let username = Field.define "Username" (fun (u: User) -> u.Username) (fun v u -> { u with Username = v })
    let email    = Field.define "Email"    (fun (u: User) -> u.Email)    (fun v u -> { u with Email    = v })
    let age      = Field.define "Age"      (fun (u: User) -> u.Age)      (fun v u -> { u with Age      = v })

    let schema =
        Schema.record "User" [
            username |> Field.schema Schema.string
            email    |> Field.schema Schema.string
            age      |> Field.schema Schema.int
        ]

Or they can override:
fsharp

[<SchemaFields(ModuleName = "User.Fields")>]

Which generates module User.Fields = ....

Now, the naming of the functions inside that module. They're currently just username, email, etc. — the field name verbatim, lowercased to F# convention. That works because they're now in their own namespace (UserSchema or User.Fields), so the collision problem is solved at the module level. You don't need suffixes or prefixes on the bindings themselves.

The generated module has exactly two things the consumer cares about:

    Per-field values: UserSchema.username, UserSchema.email, UserSchema.age — each of type Field<User, string> or Field<User, int>. These are the composable pieces.

    The aggregate schema: UserSchema.schema — of type Schema<User>, the record-level composition.

That's it. No helper functions, no extra ceremony. The module exports field definitions and the schema that ties them together. Callers pick their level: grab individual fields for targeted validation/codec work, or grab schema for the whole thing at once.

The schema binding name is slightly unfortunate if the user sets ModuleName = "User.Schema" because you'd get User.Schema.schema. I can live with that. The alternative — calling it something like record or full or all — doesn't read any better. schema is the honest name, and the stutter is a minor cosmetic issue the user can avoid by not naming their module Schema.
design the names better

You're right, schema inside a module that might be called Schema is lazy. Let me think about what each thing actually is.

The per-field bindings are lenses: they know how to get and set a named field. The aggregate is the record schema that composes them.

Per-field bindings:

They're not just the field name — they're the lens for that field. But calling them usernameLens is ugly and redundant inside a module that exists solely to hold these things. The module itself provides the context. So UserSchema.username already reads as "the username lens in the User schema." The bare name works.

The aggregate:

This is the tricky one. What is it? It's the schema for the whole record. It's not a lens — it's the composed thing. Names I'd consider:

    all — too vague, doesn't say what it is

    record — accurate but UserSchema.record reads redundantly

    fields — conflicts if someone uses Fields as the module name

    shape — unusual but precise: it describes the shape of the type

    descriptor — accurate but bureaucratic

    ofType — UserSchema.ofType reads like it's a constructor for something else

    _ — UserSchema._ is the module's default export... but F# doesn't do that cleanly

    No binding at all — just compose the record inline as the module body's return value, accessible as UserSchema the module? Can't do that in F#.

Actually, the cleanest answer might be to make the aggregate binding have the same name as the module, or rather, make the aggregate be the module's primary export under a predictable name that reads well with the module qualifier.

If the module is UserSchema, then UserSchema.definition reads well — "the definition for User." Same for UserSchema.descriptor, UserSchema.structure, UserSchema.shape. But the one that I keep coming back to as reading most naturally:

UserSchema.record — despite the superficial redundancy, it actually tells you what it is: a record schema. UserSchema.record parses as "the record schema for User." The word record is carrying its weight as a schema term, not as the type name.

But if someone names their module User.Record (unlikely but possible), the stutter is bad. More robust:

UserSchema.full — "the full schema." UserSchema.complete. Both meh.

UserSchema.entity — implies it's the whole thing. Has domain-driven connotations.

I think the answer is: the aggregate is called record by default, and the module naming convention steers people away from calling their module Record. If someone sets ModuleName = "UserRecord" they get UserRecord.record and that's on them. The common case — default naming, or ModuleName = "User.Fields" — reads cleanly:
fsharp

UserSchema.record          // fine
User.Fields.record         // fine

And the generated code is:
fsharp

module UserSchema =
    let username = Field.define "Username" (fun (u: User) -> u.Username) (fun v u -> { u with Username = v })
    let email    = Field.define "Email"    (fun (u: User) -> u.Email)    (fun v u -> { u with Email    = v })
    let age      = Field.define "Age"      (fun (u: User) -> u.Age)      (fun v u -> { u with Age      = v })

    let record =
        Schema.record "User" [
            username |> Field.schema Schema.string
            email    |> Field.schema Schema.string
            age      |> Field.schema Schema.int
        ]

Per-field: bare names. Aggregate: record. Two concepts, two naming rules, no stutter in the common case.
ok write the myriad plugin that would enable this, and how we'd use it in Axial to make Validation types and CodecMapper

Here's the Myriad generator, then the consumption side.
Myriad Generator
fsharp

// SchemaFieldsGenerator.fs

module SchemaFieldsGenerator

open Myriad.Core
open FSharp.Compiler.SyntaxTree
open FSharp.Compiler.SyntaxTrivia
open FSharp.Compiler.Xml
open FSharp.Compiler.Text

[<MyriadGenerator("schema-fields")>]
type SchemaFieldsGenerator() =
    inherit MyriadGenerator()

    let generateField (parentType: string) (field: SynField) =
        let fieldName = field.Id.Value
        let fieldNameLower = fieldName.Substring(0, 1).ToLower() + fieldName.Substring(1)
        let fieldType = field.Type

        // Field.define "FieldName" (fun (x: Parent) -> x.Field) (fun v x -> { x with Field = v })
        SynModuleDecl.Let(
            false,
            [SynBinding.create(
                SynPat.Named(fieldNameLower),
                SynExpr.App(
                    SynExpr.App(
                        SynExpr.App(
                            SynExpr.LongIdent("Field.define"),
                            SynExpr.Const(SynConst.String fieldName)
                        ),
                        SynExpr.Lambda(
                            SynPat.Paren(SynPat.Typed(SynPat.Named("x"), fieldType)),
                            SynExpr.DotGet(SynExpr.Ident("x"), fieldName)
                        )
                    ),
                    SynExpr.Lambda(
                        SynPat.Paren(SynPat.Typed(SynPat.Named("v"), fieldType)),
                        SynExpr.Lambda(
                            SynPat.Named("x"),
                            SynExpr.RecordUpdate(SynExpr.Ident("x"), [(fieldName, SynExpr.Ident("v"))])
                        )
                    )
                )
            )]
        )

    let generateSchemaRecord (recordName: string) (fields: SynField list) =
        let recordNameLower = recordName.Substring(0, 1).ToLower() + recordName.Substring(1)
        let fieldNamesLower = fields |> List.map (fun f -> f.Id.Value.Substring(0, 1).ToLower() + f.Id.Value.Substring(1))

        let schemaElements =
            fieldNamesLower
            |> List.map (fun fn ->
                SynExpr.App(
                    SynExpr.App(
                        SynExpr.LongIdent("Field.schema"),
                        SynExpr.LongIdent($"Schema.{inferSchemaType fields fn}")
                    ),
                    SynExpr.Ident(fn)
                )
            )
            |> List.foldBack (fun expr acc ->
                SynExpr.App(
                    SynExpr.App(SynExpr.LongIdent("List.cons"), expr),
                    acc
                )
            ) (SynExpr.LongIdent("List.empty"))

        // Schema.record "RecordName" [...]
        SynModuleDecl.Let(
            false,
            [SynBinding.create(
                SynPat.Named("record"),
                SynExpr.App(
                    SynExpr.App(
                        SynExpr.LongIdent("Schema.record"),
                        SynExpr.Const(SynConst.String recordName)
                    ),
                    schemaElements
                )
            )]
        )

    static member InferSchemaType(fieldType: SynType) =
        match fieldType with
        | SynType.LongIdent("string") -> "Schema.string"
        | SynType.LongIdent("int") -> "Schema.int"
        | SynType.LongIdent("int64") -> "Schema.int64"
        | SynType.LongIdent("bool") -> "Schema.bool"
        | SynType.LongIdent("float") -> "Schema.float"
        | SynType.LongIdent("decimal") -> "Schema.decimal"
        | SynType.LongIdent("Guid") -> "Schema.guid"
        | SynType.LongIdent("DateTime") -> "Schema.dateTime"
        | SynType.LongIdent("DateTimeOffset") -> "Schema.dateTimeOffset"
        | SynType.App(SynType.LongIdent("Option"), [inner]) -> $"Schema.option {InferSchemaType inner}"
        | SynType.App(SynType.LongIdent("List"), [inner]) -> $"Schema.list {InferSchemaType inner}"
        | SynType.App(SynType.LongIdent("array"), [inner]) -> $"Schema.array {InferSchemaType inner}"
        | _ -> $"Schema.unknown<{fieldType}>"  // fallback: user provides explicit override

    override _.Generate(context: GeneratorContext) =
        let moduleName = 
            context.AdditionalArguments 
            |> Option.ofObj 
            |> Option.bind (fun args -> args.TryFind "module") 
            |> Option.defaultValue (context.InputFilename.Replace(".fs", "Schema"))

        let records = 
            Ast.getRecords context.Ast
            |> List.filter (fun (_, attrs) -> 
                attrs |> List.exists (fun a -> a.TypeName = "SchemaFields"))

        let generatedModules =
            records |> List.map (fun (recordDef, _) ->
                let recordName = recordDef.Name
                let fields = recordDef.Fields

                let fieldDecls = fields |> List.map (generateField recordName)
                let schemaDecl = generateSchemaRecord recordName fields

                SynModuleDecl.NestedModule(
                    SynModuleDecl.NestedModule(
                        [ yield! fieldDecls; schemaDecl ],
                        SynModuleDecl.Module(
                            moduleName,
                            [],
                            [],
                            [],
                            [],
                            []
                        )
                    )
                )
            )

        Output.Ast generatedModules

Usage in Myriad config:
fsharp

// myriad.toml
[generators]
"schema-fields" = {}

fsharp

// Domain.fs
[<SchemaFields>]
type User = {
    Username: string
    Email: string
    Age: int
}

// optionally override module name:
// [<SchemaFields(Module = "User.Fields")>]

Generates:
fsharp

module UserSchema =
    let username = Field.define "Username" (fun (x: User) -> x.Username) (fun v x -> { x with Username = v })
    let email    = Field.define "Email"    (fun (x: User) -> x.Email)    (fun v x -> { x with Email    = v })
    let age      = Field.define "Age"      (fun (x: User) -> x.Age)      (fun v x -> { x with Age      = v })

    let record =
        Schema.record "User" [
            username |> Field.schema Schema.string
            email    |> Field.schema Schema.string
            age      |> Field.schema Schema.int
        ]

Consumption in Axial

Axial takes a Schema<'T> and produces validation:
fsharp

module Axial =
    // Schema<'T> comes from Schema.Core
    type Rule<'T, 'E> = 'T -> Validation<'T, 'E>

    module SchemaValidation =
        /// Attach validation rules to specific fields in a schema
        let validate (schema: Schema<'T>) (rules: (Schema<'T> -> Field<'T, obj> * Rule<obj, 'E> list) list) : Rule<'T, 'E> =
            fun value ->
                rules
                |> List.fold (fun acc getRule ->
                    let field, fieldRules = getRule schema
                    let fieldValue = Field.get field value
                    let results = fieldRules |> List.map (fun r -> r fieldValue)
                    // combine with acc
                    Validation.combine acc (Validation.sequence results)
                ) (Validation.success value)

    // Usage
    let userValidation : Rule<User, UserError> =
        SchemaValidation.validate UserSchema.record [
            (fun s -> s |> Schema.field "Username", [ whenNotBlank UsernameRequired; whenMinLength 3 UsernameTooShort ])
            (fun s -> s |> Schema.field "Email",    [ whenNotBlank EmailRequired ])
        ]

But with the generated fields, it's cleaner because you can reference them directly:
fsharp

    let userValidation : Rule<User, UserError> =
        SchemaValidation.validate UserSchema.record [
            UserSchema.username, [ whenNotBlank UsernameRequired; whenMinLength 3 UsernameTooShort ]
            UserSchema.email,    [ whenNotBlank EmailRequired ]
        ]

Axial only cares about the validation aspect. It accesses Field.get to extract values, applies rules, and returns the original model on success (since it's pure validation, not parsing — the output type equals the input type).
Consumption in CodecMapper

CodecMapper takes the same schema and produces encoders/decoders:
fsharp

module CodecMapper =
    type Encoder<'T> = 'T -> JsonValue
    type Decoder<'T> = JsonValue -> Validation<'T, DecodeError>

    module SchemaCodec =
        let encoder (schema: Schema<'T>) : Encoder<'T> =
            fun value ->
                match schema with
                | Schema.Record(name, fields) ->
                    let props = 
                        fields 
                        |> List.map (fun (Field(name, get, _), fieldSchema) ->
                            name, encodeField fieldSchema (get value))
                    JsonValue.Object(props |> Map.ofList)
                // other schema cases...

        let decoder (schema: Schema<'T>) : Decoder<'T> =
            fun json ->
                match schema with
                | Schema.Record(name, fields) ->
                    fields
                    |> List.fold (fun acc (Field(name, _, set), fieldSchema) ->
                        Validation.bind acc (fun obj ->
                            match json.TryGetProperty name with
                            | Some fieldJson ->
                                decodeField fieldSchema fieldJson
                                |> Validation.map (fun fieldValue -> set fieldValue obj)
                            | None -> Validation.error [MissingField name]))
                    (Validation.success (createEmptyRecord schema))

Usage:
fsharp

let userEncoder = SchemaCodec.encoder UserSchema.record
let userDecoder = SchemaCodec.decoder UserSchema.record

CodecMapper uses Field.get for encoding and Field.set for decoding. The same field definitions serve both directions. No duplication.
What Schema.Core provides

The thin shared package both depend on:
fsharp

module Schema.Core

type Field<'T, 'V> = 
    { Name: string
      Get: 'T -> 'V
      Set: 'V -> 'T -> 'T }

type Schema<'T> =
    | Record of string * (Field<'T, obj> * Schema<obj>) list
    | Primitive of string * Type
    | Option of Schema<'T>
    | List of Schema<'T>
    | Array of Schema<'T>
    // ... other cases

module Field =
    let define name get set = { Name = name; Get = get; Set = set }
    let get (field: Field<'T, 'V>) (value: 'T) = field.Get value
    let set (field: Field<'T, 'V>) (v: 'V) (t: 'T) = field.Set v t
    let schema (typeSchema: Schema<'V>) (field: Field<'T, 'V>) : Field<'T, obj> * Schema<obj> =
        // box the field and schema for heterogeneous list storage
        { Name = field.Name; Get = field.Get >> box; Set = fun v t -> field.Set (unbox v) t }, 
        typeSchema |> Schema.box

module Schema =
    let record name fields = Record(name, fields)
    let string = Primitive("string", typeof<string>)
    let int = Primitive("int", typeof<int>)
    // ... etc

    let field name (schema: Schema<'T>) = 
        // lookup helper for validation attachment
        fun (s: Schema<'T>) ->
            match s with
            | Record(_, fields) ->
                fields |> List.find (fun (f, _) -> f.Name = name)

The key: Field<'T, 'V> is the lens. Schema<'T> is the structural description. Axial uses Field.Get for validation. CodecMapper uses Field.Get for encoding and Field.Set for decoding. One definition, two consumers, zero reflection at runtime.
now review the validation using it in fsflow - optimise for DX and LLM guessability

You're right to call that out. The validation API I sketched has friction points that would make an LLM — or a developer — stumble. Let me go back to the usage site and work outward from there.

The goal: someone (human or LLM) sees a type with [<SchemaFields>], knows the generated module name, and can write validation for it without consulting docs. The API should be guessable from one example.
Target DX
fsharp

// Given this:
[<SchemaFields>]
type User = {
    Username: string
    Email: string
    Age: int
    Address: Address
}

// The generated module:
// module UserSchema =
//     let username = Field.define "Username" ...
//     let email    = Field.define "Email"    ...
//     let age      = Field.define "Age"      ...
//     let address  = Field.define "Address"  ...
//     let record   = Schema.record "User" [ ... ]

Validation should look like:
fsharp

let userValidation =
    UserSchema.record
    |> Rule.define [
        UserSchema.username |> Rule.check [
            whenNotBlank UsernameRequired
            whenMinLength 3 UsernameTooShort
        ]
        UserSchema.email |> Rule.check [
            whenNotBlank EmailRequired
            whenPattern emailRegex InvalidEmail
        ]
        UserSchema.age |> Rule.check [
            whenAtLeast 18 MustBeAdult
        ]
        UserSchema.address |> Rule.checkNested addressValidation
    ]

That reads in order: take the schema, define rules for it, each field gets checks. The shape is Schema |> Rule.define [ field |> Rule.check [ ... ] ]. An LLM seeing one example of UserSchema.username |> Rule.check [...] will pattern-match to UserSchema.email |> Rule.check [...] without variation.
What Rule.define returns

It produces a Rule<'T, 'E> — a function 'T -> Validation<'T, 'E>. Pure validation, output type equals input type. This is the common case and should be the default.
fsharp

let result : Validation<User, UserError> = userValidation someUser

Nested and collections

Same pattern, same combinators:
fsharp

let addressValidation =
    AddressSchema.record
    |> Rule.define [
        AddressSchema.street |> Rule.check [ whenNotBlank StreetRequired ]
        AddressSchema.city   |> Rule.check [ whenNotBlank CityRequired ]
    ]

let userValidation =
    UserSchema.record
    |> Rule.define [
        UserSchema.address |> Rule.checkNested addressValidation
        UserSchema.tags    |> Rule.checkEach (fun tag -> 
            whenNotBlank (TagRequired)  // or a shorthand when the error needs index
        )
    ]

checkNested takes another Rule<'V, 'E> and runs it against the field value, lifting errors into the parent path. checkEach does the same per element. Both are guessable extensions of check.
No separate ModelRule vs Rule distinction

Earlier I had ModelRule.define vs Rule.all. That's gone. There's one entry point: Rule.define takes a schema and a list of field rules and returns a Rule<'T, 'E>. The schema carries the structural knowledge (field names for error paths). The Rule.check combinator attaches predicates.

If someone wants a standalone rule without a schema (e.g., a reusable email rule), they still use Rule<'v, 'e> directly:
fsharp

let emailRule : Rule<string, CommonError> =
    Rule.all [
        whenNotBlank EmailRequired
        whenPattern emailRegex InvalidEmail
    ]

Rule.all still exists for composing rules on bare values. The schema path is for composing rules on records. Both return the same Rule<_, _> type. Consistency.
Error path generation

The schema provides field names, so errors automatically carry paths:
fsharp

// Error type
type ValidationError<'E> = {
    Path: string list
    Error: 'E
}

// Generated: Path = ["Username"], Error = UsernameTooShort
// Nested:   Path = ["Address"; "Street"], Error = StreetRequired

The user never manually specifies paths. The schema knows the field names. Rule.check wraps the predicate with path information from the Field.Name. That's the payoff for defining the schema once.
Summary of the surface
Combinator	Purpose	Signature
Rule.define	Entry point: schema → rule list → Rule	Schema<'T> -> (Schema<'T> -> FieldRule<'T,'E>) list -> Rule<'T,'E>
Rule.check	Attach predicates to a field	Field<'T,'V> -> Rule<'V,'E> list -> FieldRule<'T,'E>
Rule.checkNested	Attach nested rule to a field	Field<'T,'V> -> Rule<'V,'E> -> FieldRule<'T,'E>
Rule.checkEach	Attach element rule to a collection field	Field<'T,'V list> -> Rule<'V,'E> -> FieldRule<'T,'E>
Rule.all	Combine rules on bare values	Rule<'V,'E> list -> Rule<'V,'E>

Three check variants on fields, one all for bare values, one define to wire it to a schema. That's the whole API surface for pure validation. An LLM can memorize it in one shot and never hallucinate a combinator that doesn't exist.
how do we layer up from Check -> Validatoins -> Rule -> This??

Let me trace the layers from bottom to top so the stack is clear. Each layer builds on the one below it.
Layer 1: Check<'v, 'e>

The atomic unit. A predicate with an error.
fsharp

type Check<'v, 'e> = 'v -> Validation<unit, 'e>

These are the leaf nodes. They know nothing about paths, nothing about composition. They just look at a value and say pass or fail.
fsharp

// Built-in checks (in Axial)
Check.whenNotBlank : 'e -> Check<string, 'e>
Check.whenMinLength : int -> 'e -> Check<string, 'e>
Check.whenPattern : Regex -> 'e -> Check<string, 'e>
Check.whenAtLeast : 'v -> 'e -> Check<'v, 'e> when 'v : comparison
Check.whenNotEmpty : 'e -> Check<'v list, 'e>

// Or write your own — it's just a function
let whenYoungerThan (max: int) error : Check<int, 'e> =
    fun age -> if age < max then Validation.pass else Validation.fail error

No dependencies. Pure functions. The building blocks.
Layer 2: Validation<'v, 'e>

The result type that Check produces. Accumulates errors applicatively.
fsharp

type Validation<'v, 'e> =
    | Pass of 'v
    | Fail of 'e list

module Validation =
    let pass v = Pass v
    let fail e = Fail [e]
    let map f = function Pass v -> Pass (f v) | Fail es -> Fail es
    let apply (f: Validation<'a -> 'b, 'e>) (v: Validation<'a, 'e>) = ...
    let combine (v1: Validation<'a, 'e>) (v2: Validation<'b, 'e>) = ...
    let sequence (vs: Validation<'v, 'e> list) = ...

This is the applicative core. Checks return Validation<unit, 'e>. Multiple checks on the same field get combined via Validation.combine, accumulating all failures. This exists in Axial already.
Layer 3: Rule<'v, 'e>

A named type alias for a check function. The term "Rule" signals intent: this is a composable, reusable validation rule, not just a raw function.
fsharp

type Rule<'v, 'e> = 'v -> Validation<'v, 'e>

A Rule succeeds with the original value (unlike Check which returns unit). This preserves the value for chaining.
fsharp

module Rule =
    let fromCheck (check: Check<'v, 'e>) : Rule<'v, 'e> =
        fun v -> check v |> Validation.map (fun () -> v)

    let all (rules: Rule<'v, 'e> list) : Rule<'v, 'e> =
        fun v ->
            rules
            |> List.fold (fun acc rule ->
                Validation.combine acc (rule v)
            ) (Validation.pass v)

Rule.all is the composition operator for bare values. Each rule receives the same original value, all failures accumulate, success returns the value untouched.

At this point you can already do useful work:
fsharp

let emailRule : Rule<string, CommonError> =
    Rule.all [
        Rule.fromCheck (Check.whenNotBlank EmailRequired)
        Rule.fromCheck (Check.whenPattern emailRegex InvalidEmail)
    ]

emailRule "test@example.com"  // Pass "test@example.com"
emailRule ""                  // Fail [EmailRequired]

Layer 4: Schema-aware rules

This is where Rule.define and Rule.check come in. They take the same Rule<'v, 'e> type but wrap it with path information from the schema's Field definitions.
fsharp

type FieldRule<'root, 'e> = 'root -> Validation<'root, 'e>

module Rule =
    let check (field: Field<'root, 'v>) (rules: Rule<'v, 'e> list) : FieldRule<'root, 'e> =
        fun root ->
            let value = Field.get field root
            rules
            |> Rule.all
            |> fun rule -> rule value
            |> Validation.mapError (fun es -> 
                es |> List.map (fun e -> { Path = [field.Name]; Error = e }))
            |> Validation.map (fun _ -> root)

    let checkNested (field: Field<'root, 'v>) (rule: Rule<'v, 'e>) : FieldRule<'root, 'e> =
        fun root ->
            let value = Field.get field root
            rule value
            |> Validation.mapError (fun es ->
                es |> List.map (fun e -> { Path = field.Name :: e.Path; Error = e.Error }))
            |> Validation.map (fun _ -> root)

    let checkEach (field: Field<'root, 'v list>) (rule: Rule<'v, 'e>) : FieldRule<'root, 'e> =
        fun root ->
            let values = Field.get field root
            values
            |> List.mapi (fun i v ->
                rule v
                |> Validation.mapError (fun es ->
                    es |> List.map (fun e -> { Path = field.Name :: string i :: e.Path; Error = e.Error }))
            )
            |> Validation.sequence
            |> Validation.map (fun _ -> root)

    let define (schema: Schema<'root>) (fieldRules: FieldRule<'root, 'e> list) : Rule<'root, 'e> =
        fun root ->
            fieldRules
            |> List.fold (fun acc fieldRule ->
                Validation.combine acc (fieldRule root)
            ) (Validation.pass root)

The key insight: check, checkNested, and checkEach all return FieldRule<'root, 'e> — which is just 'root -> Validation<'root, 'e> — exactly the same shape as Rule<'root, 'e>. They're rules that happen to know how to extract a field and attach paths. Rule.define combines them just like Rule.all combines bare rules.

The layers stack cleanly:
text

Check<'v, 'e>              — predicate + error
    ↓ produces
Validation<'v, 'e>         — result with error accumulation
    ↓ aliased as
Rule<'v, 'e>               — composable validation function
    ↓ wrapped by
Rule.check / Rule.define   — path-aware composition via schema fields

An LLM only needs to know: checks are built with Check.when*, combined with Rule.all for bare values or Rule.check for schema fields, and wired together with Rule.define. The type signatures are consistent across all layers — everything flows into Rule<'v, 'e>.


No — and that's the problem with what I just wrote. Look at `Rule.check`:

```fsharp
let check (field: Field<'root, 'v>) (rules: Rule<'v, 'e> list) : FieldRule<'root, 'e> =
    fun root ->
        let value = Field.get field root
        rules
        |> Rule.all    // <-- takes Rule<'v, 'e> list
        |> fun rule -> rule value
```

It takes `Rule<'v, 'e> list`, not `Check<'v, 'e> list`. So the user has to write:

```fsharp
UserSchema.username |> Rule.check [
    Rule.fromCheck (Check.whenNotBlank UsernameRequired)
    Rule.fromCheck (Check.whenMinLength 3 UsernameTooShort)
]
```

That `Rule.fromCheck` wrapping is boilerplate that shouldn't exist at the call site. The user has checks. They want to attach checks to a field. They shouldn't need to know that `Rule` is the intermediate type.

The fix: `Rule.check` should accept `Check<'v, 'e> list` directly. The conversion from check to rule is an implementation detail.

```fsharp
module Rule =
    let check (field: Field<'root, 'v>) (checks: Check<'v, 'e> list) : FieldRule<'root, 'e> =
        fun root ->
            let value = Field.get field root
            let results = checks |> List.map (fun c -> c value)
            match Validation.sequence results with
            | Pass _ -> Validation.pass root
            | Fail es ->
                es |> List.map (fun e -> { Path = [field.Name]; Error = e })
                |> Validation.Fail

    // For the case where someone has an existing Rule they want to reuse:
    let checkWith (field: Field<'root, 'v>) (rule: Rule<'v, 'e>) : FieldRule<'root, 'e> =
        fun root ->
            let value = Field.get field root
            rule value
            |> Validation.mapError (fun es ->
                es |> List.map (fun e -> { Path = field.Name :: e.Path; Error = e.Error }))
            |> Validation.map (fun _ -> root)
```

Now the call site:

```fsharp
UserSchema.username |> Rule.check [
    Check.whenNotBlank UsernameRequired
    Check.whenMinLength 3 UsernameTooShort
]
```

That's direct. The user writes `Check.when*` at the call site, not `Rule.fromCheck`. The `Rule` module does the conversion internally.

For the case where someone has already composed a reusable `Rule` (like `emailRule` from earlier), `checkWith` accepts it:

```fsharp
UserSchema.email |> Rule.checkWith emailRule
```

Two entry points: `check` for checks (the common case), `checkWith` for pre-composed rules (the reuse case). Both guessable. An LLM sees `check` takes checks, `checkWith` takes a rule.
