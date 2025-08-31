using System;
using System.Collections.Generic;
using System.Linq;

namespace RW.Library.Mathematics.Polynomials
{
    // ===========================
    // Expansion (expands a factor tree)
    // ===========================

    public static class PolynomialExpansion
    {
        /// <summary>Expands a (possibly nested) factor tree into a single polynomial.</summary>
        public static Polynomial Expand(Factor factor)
        {
            if (factor == null) throw new ArgumentNullException(nameof(factor));

            if (factor.IsLeaf)
                return factor.Polynomial!;

            Polynomial result = new Polynomial(new Term(1));
            foreach (var child in factor.Children)
                result = result * Expand(child);

            return result;
        }
    }

}
