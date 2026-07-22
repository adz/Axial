namespace Axial.Refined

[<AutoOpen>]
module Builders =
    /// <summary>
    /// The fail-fast <c>refine { }</c> computation expression. A raw value can be parsed or refined according to the
    /// type annotation on the left side of <c>let!</c>; explicit <c>Parse</c> and <c>Refine</c> results also bind directly.
    /// </summary>
    /// <example>
    /// <code>
    /// let create rawId rawName rawQuantity =
    ///     refine {
    ///         let! (id: int) = rawId
    ///         let! (name: NonBlankString) = rawName
    ///         let! (quantity: PositiveInt) = rawQuantity
    ///         return id, name, quantity
    ///     }
    /// </code>
    /// </example>
    let refine = RefineBuilder()
