using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RW.Library.Mathematics.Polynomials.Factorization
{
    public static class PolynomialFactorizer
    {
        public static List<Factor> Factorize(Polynomial poly, string variable)
        {
            var factors = new List<Factor>();

            // Accept only UNIVARIATE terms in "variable" + pure constants.
            // If any term has other variables, skip it for this univariate factorization attempt.
            var univariateTerms = poly.Terms
                .Where(t =>
                       t.Variables.Count == 0 ||                                   // constant
                       (t.Variables.Count == 1 && t.Variables.ContainsKey(variable))) // single var term
                .ToList();

            if (univariateTerms.Count == 0)
                return factors; // nothing we can factor in this variable

            // Build exponent -> coefficient map (include constant term at exp=0)
            var byExp = new Dictionary<int, int>();
            foreach (var t in univariateTerms)
            {
                int exp = t.Variables.TryGetValue(variable, out var e) ? e : 0;
                if (!byExp.TryGetValue(exp, out var sum)) sum = 0;
                byExp[exp] = sum + t.Coefficient;
            }

            // Remove zeros (in case of cancellation), and bail if empty
            foreach (var key in byExp.Keys.ToList())
                if (byExp[key] == 0) byExp.Remove(key);
            if (byExp.Count == 0)
                return factors;

            int degree = byExp.Keys.Max();
            if (!byExp.TryGetValue(degree, out var leading) || leading == 0)
                return factors;

            int constant = byExp.TryGetValue(0, out var c0) ? c0 : 0;

            // Special-case: constant == 0 ⇒ root at 0
            if (constant == 0)
                factors.Add(new Factor($"({variable})"));

            // Rational Root Theorem candidates: ±(divisors of |constant|)/(divisors of |leading|)
            var numerators = GetFactors(Math.Abs(constant)).ToHashSet();
            var denominators = GetFactors(Math.Abs(leading)).ToHashSet();
            var candidates = numerators.SelectMany(n => denominators.Select(d => (double)n / d))
                                       .Concat(numerators.SelectMany(n => denominators.Select(d => -(double)n / d)))
                                       .Distinct();

            foreach (var r in candidates)
            {
                if (Evaluate(poly, r, variable) == 0)
                    factors.Add(new Factor($"({variable} - {r})"));
            }

            return factors;
        }

        private static int Evaluate(Polynomial poly, double x, string variable)
        {
            // Treat all OTHER variables as symbolic constants (ignored for numeric evaluation).
            // For univariate inputs like x^2 - 5x + 6, this works exactly.
            double sum = 0;
            foreach (var t in poly.Terms)
            {
                int exp = t.Variables.TryGetValue(variable, out var e) ? e : 0;
                sum += t.Coefficient * Math.Pow(x, exp);
            }
            return (int)Math.Round(sum);
        }

        private static IEnumerable<int> GetFactors(int n)
        {
            if (n == 0) yield break; // handled separately by constant==0 case
            for (int i = 1; i <= n; i++)
                if (n % i == 0)
                    yield return i;
        }
    }

}
