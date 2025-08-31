using System.Text.RegularExpressions;

namespace RW.Library.Mathematics.Polynomials
{
    // =========================
    // Term / Monomial / Factor
    // =========================

    public class Term
    {
        public int Coefficient { get; set; }

        /// <summary>Dictionary of variable name -> exponent (nonzero exponents only).</summary>
        public Dictionary<string, int> Variables { get; set; }

        public Term(int coefficient, Dictionary<string, int>? variables = null)
        {
            Coefficient = coefficient;
            Variables = NormalizeVariables(variables);
        }

        private static Dictionary<string, int> NormalizeVariables(Dictionary<string, int>? variables)
        {
            if (variables == null || variables.Count == 0)
                return new Dictionary<string, int>(StringComparer.Ordinal);

            var normalized = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (var pair in variables)
            {
                if (pair.Value == 0) continue;
                normalized[pair.Key] = normalized.TryGetValue(pair.Key, out var existing)
                    ? existing + pair.Value
                    : pair.Value;
            }
            // remove any that cancel to zero
            foreach (var key in normalized.Where(p => p.Value == 0).Select(p => p.Key).ToList())
                normalized.Remove(key);

            return normalized;
        }

        public override string ToString()
        {
            var variablePart = Variables
                .OrderBy(p => p.Key, StringComparer.Ordinal)
                .Select(p => p.Value == 1 ? p.Key : $"{p.Key}^{p.Value}");
            var joined = string.Concat(variablePart);
            return $"{Coefficient}{joined}";
        }

        public static Term Multiply(Term first, Term second)
        {
            var resultCoefficient = first.Coefficient * second.Coefficient;
            var mergedVariables = new Dictionary<string, int>(first.Variables, StringComparer.Ordinal);

            foreach (var kvp in second.Variables)
                mergedVariables[kvp.Key] = mergedVariables.TryGetValue(kvp.Key, out var existing)
                    ? existing + kvp.Value
                    : kvp.Value;

            return new Term(resultCoefficient, mergedVariables);
        }

        // ---- Parsing helpers for Polynomial.Parse ----

        internal static List<string> SplitIntoSignedTerms(string expression)
        {
            var compact = expression.Replace(" ", "");
            if (compact.Length == 0) return new List<string>();
            if (compact[0] != '+' && compact[0] != '-') compact = "+" + compact;
            return Regex.Matches(compact, @"[+\-][^+\-]+")
                        .Cast<Match>()
                        .Select(m => m.Value)
                        .ToList();
        }

        internal static Term ParseSignedMonomial(string signedChunk)
        {
            if (string.IsNullOrWhiteSpace(signedChunk))
                throw new FormatException("Empty monomial.");

            int sign = signedChunk[0] == '-' ? -1 : 1;
            string body = signedChunk.Substring(1);
            if (string.IsNullOrWhiteSpace(body))
                throw new FormatException($"Invalid monomial '{signedChunk}'.");

            // Pure integer constant?
            if (int.TryParse(body, out int constantValue))
                return new Term(sign * constantValue);

            // Optional leading integer coefficient
            int coefficient = 1;
            var leadingCoeffMatch = Regex.Match(body, @"^\d+");
            if (leadingCoeffMatch.Success)
            {
                coefficient = int.Parse(leadingCoeffMatch.Value);
                body = body.Substring(leadingCoeffMatch.Length);
            }

            // Variables with optional integer exponents
            var variableDict = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (Match m in Regex.Matches(body, @"([A-Za-z_]\w*)(?:\^(\d+))?"))
            {
                string variableName = m.Groups[1].Value;
                int exponent = m.Groups[2].Success ? int.Parse(m.Groups[2].Value) : 1;
                if (exponent == 0) continue;
                variableDict[variableName] = variableDict.TryGetValue(variableName, out var existing)
                    ? existing + exponent
                    : exponent;
            }

            if (variableDict.Count == 0)
                throw new FormatException($"Invalid monomial '{signedChunk}'.");

            return new Term(sign * coefficient, variableDict);
        }
    }

}
