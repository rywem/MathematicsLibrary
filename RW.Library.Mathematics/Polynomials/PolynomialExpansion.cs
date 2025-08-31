using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace RW.Library.Mathematics.Polynomials
{
    public static class PolynomialExpansion
    {
        /// <summary>
        /// Expand a list of factor expressions (each factor is its own mini-polynomial).
        /// Examples:
        ///   (a+h)(a+h)(a+h) -> a^3 + 3a^2h + 3ah^2 + h^3
        ///   (2x+1)(x-4)     -> -2x^2 - 7x - 4
        ///   (x+y)(x-y)      -> x^2 - y^2
        /// </summary>
        public static Polynomial ExpandFactors(IEnumerable<Factor> factors)
        {
            if (factors is null) throw new ArgumentNullException(nameof(factors));

            var result = new Polynomial(new Term(1)); // multiplicative identity

            foreach (var f in factors)
            {
                var poly = ParseFactorToPolynomial(f.Expression);
                result = Multiply(result, poly);
            }

            return result;
        }

        /// <summary>
        /// Parses a factor like "(a+h)", "(x-y+2)", "(2xy^2 - 3y + 4)", "(-1)" into a Polynomial.
        /// Supports multiple variable terms per factor and exponents like x^2, y^3, etc.
        /// </summary>
        private static Polynomial ParseFactorToPolynomial(string expression)
        {
            if (string.IsNullOrWhiteSpace(expression))
                throw new ArgumentException("Empty factor expression.", nameof(expression));

            // Strip outer parens and spaces
            var s = expression.Trim();
            if (s.StartsWith("(") && s.EndsWith(")") && s.Length >= 2)
                s = s.Substring(1, s.Length - 2);
            s = s.Trim();

            // Special case: whole integer constant factor like "-1", "3"
            if (int.TryParse(s, out int constValue))
                return new Polynomial(new Term(constValue));

            // Tokenize into signed terms by splitting at +/-, keeping signs.
            // This creates chunks like "+a", "-h", "+2xy^2", "-3", etc.
            var chunks = SplitIntoSignedTerms(s);
            if (chunks.Count == 0)
                throw new FormatException($"Cannot parse factor: {expression}");

            var terms = new List<Term>();

            foreach (var chunk in chunks)
            {
                var term = ParseSignedMonomial(chunk);
                if (term != null)
                    terms.Add(term);
            }

            // if all terms canceled (shouldn't normally happen), return 0-poly
            if (terms.Count == 0)
                return new Polynomial(new Term(0));

            return new Polynomial(terms.ToArray());
        }

        /// <summary>
        /// Splits a polynomial string "a+h-2xy" into signed chunks: ["+a", "+h", "-2xy"]
        /// </summary>
        private static List<string> SplitIntoSignedTerms(string s)
        {
            // Normalize unary +/-
            s = s.Replace(" ", "");
            if (s.Length == 0) return new List<string>();

            // Ensure first term has an explicit sign for simpler parsing
            if (s[0] != '+' && s[0] != '-')
                s = "+" + s;

            // Split at + or - that indicate a new term
            var pieces = Regex.Matches(s, @"[+\-][^+\-]+")
                              .Cast<Match>()
                              .Select(m => m.Value)
                              .ToList();
            return pieces;
        }

        /// <summary>
        /// Parses a single signed monomial like "+2xy^2", "-x", "+3", "+ab^2c".
        /// Returns a Term or null if the chunk is empty/invalid.
        /// </summary>
        private static Term? ParseSignedMonomial(string chunk)
        {
            if (string.IsNullOrWhiteSpace(chunk)) return null;

            // Sign
            int sign = chunk[0] == '-' ? -1 : 1;
            var body = chunk.Substring(1); // drop leading sign
            if (string.IsNullOrWhiteSpace(body)) return null;

            // If purely integer => constant term
            if (int.TryParse(body, out int constVal))
                return new Term(sign * constVal);

            // Extract optional leading coefficient
            int coeff = 1;
            var mCoeff = Regex.Match(body, @"^\d+");
            if (mCoeff.Success)
            {
                coeff = int.Parse(mCoeff.Value);
                body = body.Substring(mCoeff.Length);
            }

            // Now body should be a product of variables with optional exponents (e.g., x, y^2, ab^3c)
            // We'll match each variable token and sum exponents if repeated.
            var varDict = new Dictionary<string, int>(StringComparer.Ordinal);

            foreach (Match m in Regex.Matches(body, @"([A-Za-z_]\w*)(?:\^(\d+))?"))
            {
                string v = m.Groups[1].Value;
                int pow = m.Groups[2].Success ? int.Parse(m.Groups[2].Value) : 1;

                if (pow == 0) continue; // x^0 -> 1, skip
                varDict[v] = varDict.TryGetValue(v, out int e) ? e + pow : pow;
            }

            // If no variables found and body wasn't numeric => invalid
            if (varDict.Count == 0)
                throw new FormatException($"Invalid monomial '{chunk}'.");

            return new Term(sign * coeff, varDict);
        }

        /// <summary>
        /// Multiply p1 * p2 (cartesian product of terms). Your Polynomial ctor will combine like terms.
        /// </summary>
        private static Polynomial Multiply(Polynomial p1, Polynomial p2)
        {
            var resultTerms = new List<Term>(p1.Terms.Count * p2.Terms.Count);

            foreach (var t1 in p1.Terms)
            {
                foreach (var t2 in p2.Terms)
                {
                    resultTerms.Add(MultiplyTerms(t1, t2));
                }
            }

            return new Polynomial(resultTerms.ToArray());
        }

        /// <summary>
        /// Multiply two terms: multiply coefficients and add exponents of like variables.
        /// </summary>
        private static Term MultiplyTerms(Term a, Term b)
        {
            int newCoeff = a.Coefficient * b.Coefficient;

            var vars = new Dictionary<string, int>(a.Variables, StringComparer.Ordinal);
            foreach (var kv in b.Variables)
            {
                vars[kv.Key] = vars.TryGetValue(kv.Key, out var e) ? e + kv.Value : kv.Value;
            }

            return new Term(newCoeff, vars);
        }
    }
}
