# Typed Relational Layer

The strongest direction is an immutable, generated, typed relational AST interpreted through Flow. It should not
resemble EF's tracked object graph, and it should not copy Ecto's Changeset wholesale because Axial already has Schema,
path-aware diagnostics, and typed Flow errors.

  The result could be genuinely compelling: database-first correctness like SqlHydra, query composition closer to Ecto, immutable F# values throughout, and
  first-class Flow integration.

  ## Where Axial is currently up to

  The existing architecture gives SQL a solid foundation:

  - `Schema<'model>` describes domain shape, constraints, construction, field metadata, parsing, and checking.
  - `Schema.parse` and `Schema.check` return `Result<'model, Diagnostics<SchemaError>>`; success contains the ordinary
    admitted value rather than a universal trust wrapper.
  - FieldRef<'model,'value> provides stable typed paths for attaching diagnostics.
  - Flow<'env,'error,'value> models database access naturally:
      - database capabilities live explicitly in 'env;
      - expected database failures enter 'error;
      - cancellation and scoped cleanup remain runtime mechanics.

  - BindError and Flow.mapError already provide the application-level error mapping lane.
  - Schema retains typed construction information and avoids reflection in hot paths. SQL should follow the same pattern.

  The package dependency direction matters:

  Axial.Flow         Axial.Schema
       \                 /
            Axial.Sql
            /       \
   Postgres         Sqlite

  Neither Flow nor Schema should learn about SQL.

  ## Three possible interface designs

  ### 1. Minimal/deep interface

  Expose only three conceptual values:

  type Table<'row, 'key>
  type Expr<'scope, 'value>
  type Statement<'result>

  Generated table modules produce immutable statements, and one operation executes them:

  module Db =
      val run :
          Statement<'value> ->
          Flow<#IHas<IDatabase>, DbError, 'value>

  Usage:

  let activeAdults name =
      Users.query
      |> Query.where (fun u -> u.Active &&. u.Age >=. Expr.value 18)
      |> Query.whereOption name (fun name u ->
          u.Name |> Expr.ilike (Expr.value $"%{name}%"))
      |> Query.orderByDescending (fun u -> u.CreatedAt)
      |> Query.select (fun u ->
          select {
              field u.Id
              field u.Name
          })
      |> Query.toList

  flow {
      let! users = Db.run (activeAdults filter)
      let! created = Db.run (Users.insert signup)
      return created
  }

  This design hides virtually everything: query versus command execution, cardinality, row decoding, parameter binding, dialect rendering, and statement
  caching.

  Its strength is conceptual depth. Its weakness is that `Statement<'result>` can conceal meaningful distinctions
  between query cardinality, streaming, and mutation.

  ### 2. Full relational AST

  Expose the underlying distinctions: tables, typed columns, scoped expressions, projections, queries, and mutation
  commands. This surface can represent joins and subqueries, `EXISTS`, grouping and aggregates, set operations,
  conflict clauses, and provider-specific extensions.

  A complex query might read:

  ```fsharp
  query {
      for customer in Customers.table do
      join order in Orders.table on (customer.Id =. order.CustomerId)
      where (
          customer.TenantId =. Expr.value tenant
          &&. order.Total >=. Expr.value minimum
      )
      groupBy (customer.Id, customer.Email)
      having (Expr.count order.Id >. Expr.value 2L)
      sortByDescending (Expr.sum order.Total)
      select
          {| CustomerId = customer.Id
             Email = customer.Email
             Spend = Expr.sum order.Total |}
  }
  ```

  This is the most complete design, but it risks exposing too many types and making straightforward CRUD feel like constructing a compiler AST.

  ### 3. Ecto-inspired design

  Organize the library around generated schemas, queries, mutations and a database service:

  type Query<'row>
  type Insert<'result>
  type Update<'result>
  type Delete<'result>
  type ConstraintRef<'input>

  Generated table modules provide the everyday operations:

  module Customer =
      val schema : Schema<Customer>
      val createSchema : Schema<NewCustomer>

      val table : Table<Customer, CustomerId>
      val id : Column<Customer, CustomerId>
      val email : Column<Customer, Email>

      val emailUnique : ConstraintRef<NewCustomer>

      val query : Query<Customer>
      val insert : NewCustomer -> Insert<Customer>
      val update : CustomerId -> Update<Customer>
      val delete : CustomerId -> Delete<bool>

  The analogue of an Ecto changeset is not one monolithic object. It is the composition of:

  1. an input value admitted by `Schema.parse` or `Schema.check`;
  2. an immutable Insert or Update;
  3. explicit constraint translations attached to the mutation.

  That separation fits Axial much better than copying Changeset.

  ## Recommended synthesis

  Use the Ecto-shaped generated surface over the full relational AST, with the minimal Db.run idea selectively retained.

  The public layers would be:

  type Query<'value>
  type Insert<'value>
  type Update<'value>
  type Delete<'value>

  module Database =
      val all :
          Query<'value> ->
          Flow<#IHas<IDatabase>, DbError, 'value list>

      val tryExactlyOne :
          Query<'value> ->
          Flow<#IHas<IDatabase>, DbError, 'value option>

      val exactlyOne :
          Query<'value> ->
          Flow<#IHas<IDatabase>, DbError, 'value>

      val stream :
          Query<'value> ->
          Flow<#IHas<IDatabase>, DbError, FlowStream<_, _, 'value>>

      val insert :
          Insert<'value> ->
          Flow<#IHas<IDatabase>, DbError, 'value>

      val update :
          Update<'value> ->
          Flow<#IHas<IDatabase>, DbError, 'value>

      val delete :
          Delete<'value> ->
          Flow<#IHas<IDatabase>, DbError, 'value>

  I would not overload everything into Db.run. Separate verbs make cardinality and intent clearer, while all executable values can still share an internal
  Statement<'result> representation.

  ## Immutable writes

  Writes should never involve tracking or mutable entity state.

  let rename customerId name expectedVersion =
      Customer.update customerId
      |> Update.set Customer.name name
      |> Update.where (Customer.version =. Expr.value expectedVersion)
      |> Update.increment Customer.version
      |> Update.returningRow

  Nullable updates must distinguish “unchanged” from “set to null”:

  type Assignment<'value> =
      | Unchanged
      | Set of 'value
      | SetNull

  Generated patch records could use that representation:

  type CustomerPatch =
      {
          Name: Assignment<string>
          Phone: Assignment<string>
      }

  Bulk inserts, upserts, deletes and conflict handling should all produce immutable command values.

  ## Query ergonomics

  The expression system should be explicit rather than translating arbitrary F# quotations:

  customer.Active =. Expr.value true
  customer.Email |> Expr.ilike pattern
  Expr.exists openOrders
  customer.DeletedAt |> Expr.isNull
  Expr.coalesce customer.Nickname customer.Name

  That means:

  - unsupported operations fail at compile time;
  - nothing silently becomes client-side evaluation;
  - the AST is AOT-safe and trimming-safe;
  - parameterization is guaranteed;
  - query values are reusable and branchable.

  Both a pipeline API and a query computation expression should lower into the same AST. The CE is syntax, not a second query implementation.

  Complex queries should compose as ordinary values:

  let forTenant tenant query =
      query
      |> Query.where (fun row ->
          row.TenantId =. Expr.value tenant)

  let active =
      Customers.query
      |> Query.where (fun row ->
          row.Active =. Expr.value true)

  let activeForTenant tenant =
      active |> forTenant tenant

  ## Generated database modules

  Generation should emit physical database metadata separately from Schema metadata:

  module Customer =
      type Row =
          {
              Id: CustomerId
              Email: Email
              Name: string
              Version: int64
          }

      type New =
          {
              Email: Email
              Name: string
          }

      val schema : Schema<Row>
      val newSchema : Schema<New>

      val table : Table<Row, CustomerId>

      val id : Column<Row, CustomerId>
      val email : Column<Row, Email>
      val name : Column<Row, string>
      val version : Column<Row, int64>

      val primaryKey : Key<Row, CustomerId>
      val emailUnique : ConstraintRef<New>

      val query : Query<Row>
      val insert : Model<New> -> Insert<Row>

  Not every table row should automatically become a domain model. The generator should distinguish:

  - database row records;
  - insert/input records;
  - explicit mappings to hand-authored domain types.

  Schema can describe row and input shapes, but SQL metadata must remain responsible for:

  - keys;
  - foreign keys;
  - indexes;
  - defaults;
  - identities and generated columns;
  - database nullability;
  - database types;
  - named constraints;
  - provider annotations.

  A Schema<'t> is not automatically a relational mapping: nested objects, unions, collections and maps require explicit JSON, array or related-table
  mappings.

  ## Constraint errors and Schema diagnostics

  This is where Axial can be unusually good.

  type DbError =
      | Constraint of ConstraintViolation
      | Cardinality of CardinalityError
      | Decode of DecodeError
      | Unsupported of feature: string * dialect: string
      | Connection of DbFault
      | SerializationFailure of DbFault
      | Deadlock of DbFault
      | Provider of DbFault

  type ConstraintViolation =
      {
          Kind: ConstraintKind
          Name: string option
          Table: string option
          Columns: string list
          Diagnostics: Diagnostics<SchemaError> option
          Detail: string option
      }

  Generated ConstraintRefs connect database constraints to Schema FieldRefs:

  Customer.emailUnique
  // DB constraint name + affected columns + CustomerNew.email FieldRef

  However, conversion to Schema diagnostics must be explicit and semantic. Not every persistence failure is invalid input:

  - unique email: normally applicable to the email field;
  - not-null: applicable when the input schema owns that field;
  - check constraint: applicable only when it corresponds to an understood Schema constraint;
  - foreign key: sometimes a field error, sometimes a domain conflict;
  - serialization/deadlock/connection failures: never Schema errors.

  An Ecto-style mapping could look like:

  let command =
      Customer.insert customer
      |> Insert.mapConstraint
          Customer.emailUnique
          CustomerNew.email
          (SchemaError.custom
              "email.taken"
              "This email is already registered.")

  Execution could then return:

  type WriteError =
      | Invalid of Diagnostics<SchemaError>
      | Database of DbError

  Or the lower-level API can always return DbError, leaving the application to map it:

  let! customer =
      Customer.insert model
      |> Database.insert
      |> Bind.mapError (function
          | DbError.Constraint violation
              when violation.Diagnostics.IsSome ->
              RegistrationError.Invalid violation.Diagnostics.Value

          | error ->
              RegistrationError.Storage error)

  The database remains authoritative, which handles race conditions correctly.

  ## Dump, interrogation and generation

  Your proposed pipeline is the right one:

  schema.sql / migrations
            │
            ▼
   disposable real database
            │
            ▼
   catalog interrogation
            │
            ▼
   versioned .axialdb.json snapshot
            │
            ▼
   deterministic F# source generation

  I would not initially skip database initialization and introspection.

  Parsing full PostgreSQL DDL correctly means understanding:

  - search paths;
  - domains and enums;
  - extensions;
  - casts;
  - generated expressions;
  - quoted identifiers;
  - provider type resolution;
  - version-specific grammar.

  The database engine is already the correct parser and semantic analyzer. SQLite is simpler, but its original DDL still contains information that PRAGMAs
  do not always expose cleanly.

  Commands could be:

  axial sql import --provider postgres schema.sql --output db.axial.json
  axial sql snapshot --provider sqlite app.db --output db.axial.json
  axial sql generate db.axial.json
  axial sql verify db.axial.json --connection ...

  Normal builds consume the checked-in snapshot and never need a live database. This gives:

  - hermetic builds;
  - reviewable schema changes;
  - deterministic generation;
  - CI drift checking;
  - a stable provider-neutral catalog format.

  A direct SQL parser can later become an optimization, not an architectural dependency.

  ## Provider expansion

  Each provider implements internal service-provider interfaces roughly equivalent to:

  - catalog reader;
  - type registry and codecs;
  - AST compiler/dialect renderer;
  - parameter binder;
  - exception classifier;
  - migration renderer;
  - feature capabilities;
  - optional bulk-copy implementation.

  The public AST represents relational semantics. Provider-specific features remain typed extensions:

  Postgres.Json.pathText Customer.metadata [ "address"; "city" ]
  Postgres.Array.contains Customer.tags tag
  Sqlite.Fts5.matches Search.content query

  Running a query against an unsupported dialect should fail during compilation, before database I/O.

  ## Transactions

  Transactions must be lexical and Flow-scoped:

  Database.transaction {
      let! customer = Database.insert createCustomer
      let! invoice = Database.insert (Invoice.forCustomer customer.Id)
      return customer, invoice
  }

  The hard architectural issue is ensuring nested repository calls use the transaction-bound connection without placing it in ambient runtime state.

  The preferred model is for the transaction body to receive or locally provide a transaction-bound database service:

  Database.transaction options (fun transaction ->
      flow {
          let! customer =
              createCustomer
              |> Database.insertWith transaction

          return customer
      })

  A more ergonomic environment-lens mechanism could later locally replace IDatabase so existing repository functions automatically participate. That likely
  exposes a small missing capability in Flow worth designing independently. The connection should not be hidden in runtime-local ambient state because that
  conflicts with Axial’s explicit-environment direction.

  Transactions must guarantee rollback and disposal on typed failure, defect, or interruption. Nested transactions become savepoints where supported.

  ## Performance model

  Internally:

  1. Normalize the immutable relational AST.
  2. Validate dialect capabilities.
  3. Compile it into parameterized SQL and an ordered parameter plan.
  4. Attach a generated typed row decoder.
  5. Cache by AST shape and dialect, never parameter values.

  Generated readers should construct records directly from ordinals, following Schema/Codec’s retained typed-chain approach:

  - no per-row reflection;
  - no obj array constructor dispatch;
  - no expression compilation required at runtime;
  - no mutable tracking;
  - sequential reader support;
  - cancellation throughout;
  - streaming through FlowStream;
  - optional prepared statements.

  ## Package layout

  Axial.Sql
  Axial.Sql.Postgres
  Axial.Sql.Sqlite
  Axial.Sql.Tooling
  Axial.Sql.Generator

  Potentially split execution from pure query construction later:

  Axial.Sql             // metadata and immutable AST
  Axial.Flow.Sql        // IDatabase and Flow execution

  That split is architecturally cleaner if users might want query generation without Flow, though it creates another package to explain. I would begin with
  Axial.Sql as the integrated add-on unless a non-Flow consumer appears.

  ## What makes it preferable

  The differentiating combination is:

  - immutable, reusable query and mutation values;
  - no DbContext, unit of work, proxies or tracking;
  - generated, refactor-safe columns and constraints;
  - full relational query composition;
  - explicit SQL semantics rather than quotation magic;
  - provider-neutral core with typed provider extensions;
  - real-catalog-driven generation;
  - direct generated codecs;
  - Flow-native cancellation, resource management and errors;
  - Schema-native, path-aware persistence diagnostics;
  - explicit race-safe constraint translation.

  ## Proposed sequencing

  1. Prove generated Table/Column/RowCodec metadata from a SQLite catalog snapshot.
  2. Implement select/where/order/limit plus typed projections and direct decoding.
  3. Add inserts, updates, deletes and RETURNING.
  4. Add PostgreSQL rendering, codecs and constraint classification.
  5. Add explicit constraint-to-diagnostics mappings.
  6. Add joins, subqueries, grouping, aggregates and CTEs.
  7. Solve transaction-bound environment substitution.
  8. Add streaming, compiled query caching and bulk operations.
  9. Add provider extensions, drift verification and migration support.

  The biggest prototype risks are typed anonymous-record projections, alias/scope typing across complex joins, and transaction-local service replacement.
  Those should be proven before committing the final public API.
