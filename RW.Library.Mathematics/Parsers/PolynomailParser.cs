using RW.Library.Mathematics.Polynomials;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RW.Library.Mathematics.Parsers
{
    public static class PolynomialParser
    {

        public static bool TryParse(string expression, out Polynomial? polynomial, out string? error)
        {
            try
            {
                polynomial = Parse(expression);
                error = null;
                return true;
            }
            catch (Exception ex) when (ex is FormatException || ex is ArgumentNullException)
            {
                polynomial = null;
                error = ex.Message;
                return false;
            }
        }
        /// <summary>
        /// Parse a polynomial expression (sum/difference of monomials) into a Polynomial.
        /// Examples: "x^2-3x+4", "2xy^2 - y + 2x + 4", "3*x*y^2 - 5"
        /// </summary>
        public static Polynomial Parse(string expression)
        {
            return new Polynomial(ParseTerms(expression).ToArray());
        }

        /// <summary>
        /// Parse into raw Term list (before like-term combination).
        /// </summary>
        public static List<Term> ParseTerms(string expression)
        {
            if (expression is null) throw new ArgumentNullException(nameof(expression));

            var s = Normalize(expression);
            var pieces = SplitIntoSignedTerms(s);

            var terms = new List<Term>(pieces.Count);

            foreach (var piece in pieces)
            {
                // piece is like "+2xy^2", "-x", "+4", "-3*x*y^2", etc.
                int sign = piece[0] == '-' ? -1 : 1;
                string body = piece.Substring(1); // drop the explicit sign we inserted

                var (coef, vars) = ParseMonomial(body);
                terms.Add(new Term(sign * coef, vars));
            }

            return terms;
        }

        // ---------- internals ----------

        private static string Normalize(string expr)
        {
            // unify minus sign, remove spaces, allow uppercase/lowercase variables
            var s = expr.Replace("−", "-")
                        .Replace("—", "-")
                        .Replace("–", "-")
                        .Replace(" ", string.Empty);

            // Insert a leading '+' if the string starts with neither '+' nor '-'
            if (s.Length > 0 && s[0] != '+' && s[0] != '-')
                s = "+" + s;

            return s;
        }

        private static List<string> SplitIntoSignedTerms(string s)
        {
            // We assume '+' and '-' delimit terms. We’ve already forced a leading sign.
            var pieces = new List<string>();
            int i = 0;
            while (i < s.Length)
            {
                // s[i] is '+' or '-'
                char sign = s[i];
                int start = i;
                i++; // skip sign

                // read until next '+' or '-' (or end)
                while (i < s.Length && s[i] != '+' && s[i] != '-')
                    i++;

                pieces.Add(s.Substring(start, i - start));
            }
            return pieces;
        }

        private static (int Coefficient, Dictionary<string, int> Vars) ParseMonomial(string body)
        {
            // Grammar (simple):
            // monomial := coefficient? factor*
            // coefficient := INT
            // factor := '*'? VAR ('^' INT)?
            // VAR := single letter [a-zA-Z]
            // Examples: "2xy^2", "x^2", "4", "3*x*y^2", "x", "y^5"
            int i = 0;
            int coef = 0;
            bool coefSet = false;
            var vars = new Dictionary<string, int>(StringComparer.Ordinal);

            // Try to read an initial integer coefficient
            int start = i;
            while (i < body.Length && char.IsDigit(body[i]))
                i++;
            if (i > start)
            {
                coef = int.Parse(body.AsSpan(start, i - start), CultureInfo.InvariantCulture);
                coefSet = true;
            }

            // Now zero or more variable factors (possibly with '*' separators)
            while (i < body.Length)
            {
                if (body[i] == '*') { i++; continue; }

                if (!IsVarChar(body[i]))
                {
                    throw new FormatException($"Unexpected character '{body[i]}' at position {i} in term '{body}'.");
                }

                string varName = body[i].ToString();
                i++;

                int exp = 1;
                if (i < body.Length && body[i] == '^')
                {
                    i++; // skip '^'
                    if (i >= body.Length || !char.IsDigit(body[i]))
                        throw new FormatException($"Missing exponent after '^' in term '{body}'.");

                    int expStart = i;
                    while (i < body.Length && char.IsDigit(body[i]))
                        i++;

                    exp = int.Parse(body.AsSpan(expStart, i - expStart), CultureInfo.InvariantCulture);
                }

                if (vars.TryGetValue(varName, out var existing))
                    vars[varName] = existing + exp;
                else
                    vars[varName] = exp;
            }

            // If no coefficient digits were present but we have variables, coefficient is 1
            // If nothing but digits existed, that's the constant term (variables empty)
            if (!coefSet)
                coef = vars.Count > 0 ? 1 : 0;

            return (coef, vars);
        }

        private static bool IsVarChar(char c) => char.IsLetter(c);
    }

}
