namespace FsFlow

[<AutoOpen>]
module Builders =
    /// <summary>
    /// The fail-fast <c>result { }</c> computation expression.
    /// </summary>
    /// <returns>A <see cref="T:FsFlow.ResultBuilder"/> instance.</returns>
    /// <remarks>
    /// <para>
    /// Use this builder when the happy path should short-circuit on the first error
    /// and you want to keep the workflow in <c>Result</c> shape all the way through.
    /// </para>
    /// <para>
    /// It works well for parsing, validation, and other boundaries where failure is expected
    /// to stop the flow immediately instead of accumulating diagnostics.
    /// </para>
    /// <para>
    /// Use <c>Check.withError</c> when a pure check needs a domain error, and <c>Result.mapError</c>
    /// when you need to remap an existing error before entering the CE.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// let parsedUser =
    ///     result {
    ///         let! age = parseAge input
    ///         let! name = parseName input
    ///         return { Age = age; Name = name }
    ///     }
    /// </code>
    /// </example>
    let result = ResultBuilder()

    /// <summary>
    /// The universal <c>flow { }</c> computation expression.
    /// </summary>
    /// <returns>A <see cref="T:FsFlow.FlowBuilder"/> instance.</returns>
    /// <remarks>
    /// <para>
    /// Use this builder when the boundary can mix synchronous values, <c>Async</c>, <c>Task</c>,
    /// <c>Result</c>, and environment requests while keeping typed failures and explicit
    /// dependency access.
    /// </para>
    /// <para>
    /// It preserves the current environment model while allowing the workflow to compose
    /// task-oriented inputs directly, so callers do not need to switch builders just to cross
    /// an async boundary.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// let greeting =
    ///     flow {
    ///         let! name = Flow.env
    ///         let! suffix = async { return "!" }
    ///         return $"Hello, {name}{suffix}"
    ///     }
    /// </code>
    /// </example>
    let flow = FlowBuilder()

    /// <summary>
    /// The accumulating <c>validate { }</c> computation expression.
    /// </summary>
    /// <returns>A <see cref="T:FsFlow.ValidateBuilder"/> instance.</returns>
    /// <remarks>
    /// <para>
    /// Use this builder when you want to collect all validation failures instead of stopping
    /// at the first one.
    /// </para>
    /// <para>
    /// Use <c>and!</c> when sibling validations should accumulate into the same diagnostics graph.
    /// Plain <c>let!</c> and <c>do!</c> are sequential: if the left side fails, the later step is
    /// not evaluated.
    /// </para>
    /// <para>
    /// <c>Check&lt;'value&gt;</c> covers unit-error predicate results. Use <c>Check.when*</c>
    /// when a predicate should preserve the original value, <c>Check.take*</c> when it should
    /// extract an inner value, <c>Check.withError</c> to attach an application error, and
    /// <c>Validation.mapError</c> when you need to remap accumulated diagnostics.
    /// </para>
    /// <para>
    /// When nested API response fields need to keep their place in the diagnostics graph, use
    /// the scoped helpers <c>validate.key</c>, <c>validate.index</c>, and <c>validate.name</c>
    /// inside the computation expression. If you already have a <c>Validation</c> value, use
    /// <c>Validation.key</c>, <c>Validation.index</c>, or <c>Validation.name</c> to prefix it
    /// after the fact.
    /// </para>
    /// <para>
    /// It is intended for forms, configuration checks, and other input-heavy boundaries where
    /// the user benefits from seeing every problem at once.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// let validatedUser =
    ///     validate {
    ///         let! name = input.Name |> Check.whenNotBlank |> Check.withError "Name required"
    ///         do! input.Age > 0 |> Check.isTrue |> Check.withError "Age must be positive"
    ///         return { Name = name; Age = input.Age }
    ///     }
    /// </code>
    ///
    /// <code>
    /// let validatedCustomer =
    ///     validate.key "customer" {
    ///         let! name =
    ///             validate.name "Name" {
    ///                 return! input.Name |> Check.whenNotBlank |> Check.withError "Name required"
    ///             }
    ///
    ///         return name
    ///     }
    /// </code>
    /// </example>
    let validate = ValidateBuilder()

    /// <summary>
    /// The <c>layer { }</c> computation expression for provisioning explicit service environments.
    /// </summary>
    /// <returns>A <see cref="T:FsFlow.LayerBuilder"/> instance.</returns>
    /// <remarks>
    /// Use plain <c>let!</c> when a later provisioning step depends on an earlier value.
    /// Use sibling <c>and!</c> bindings when services are independent and can be provisioned
    /// through <c>Layer.merge</c> / <c>Layer.zipPar</c>.
    /// </remarks>
    /// <example>
    /// <code>
    /// let appLayer =
    ///     layer {
    ///         let! config = configLayer
    ///
    ///         let! runtime = BaseRuntime.live
    ///         and! orders = Orders.layer config
    ///
    ///         return { Runtime = runtime; Orders = orders }
    ///     }
    /// </code>
    /// </example>
    let layer = LayerBuilder()
