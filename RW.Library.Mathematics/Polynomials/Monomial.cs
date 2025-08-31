namespace RW.Library.Mathematics.Polynomials
{
    public static class Monomial
    {
        /// <summary>Deterministic signature for grouping like terms (same variables/ exponents).</summary>
        public static string Signature(Term term) =>
            string.Join(",", term.Variables
                .OrderBy(kv => kv.Key, StringComparer.Ordinal)
                .Select(kv => $"{kv.Key}^{kv.Value}"));
    }

}
