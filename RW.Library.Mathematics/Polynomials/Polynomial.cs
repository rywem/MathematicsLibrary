using System.Numerics;

namespace RW.Library.Mathematics.Polynomials
{
    // ===============
    // Polynomial core
    // ===============

    public class Polynomial
    {
        public List<Term> Terms { get; private set; } = new();

        public Polynomial(params Term[] terms) : this((IEnumerable<Term>)terms) { }

        public Polynomial(IEnumerable<Term> terms)
        {
            Terms.AddRange(terms);
            CombineLikeTerms();
        }

        private void CombineLikeTerms()
        {
            Terms = Terms
                .GroupBy(Monomial.Signature)
                .Select(group => new Term(
                    group.Sum(t => t.Coefficient),
                    new Dictionary<string, int>(group.First().Variables, StringComparer.Ordinal)))
                .Where(t => t.Coefficient != 0)
                .ToList();
        }

        public override string ToString()
        {
            if (Terms.Count == 0) return "0";

            // A simple, stable ordering for display
            var ordered = Terms
                .OrderByDescending(t => t.Variables.Count)
                .ThenBy(Monomial.Signature, StringComparer.Ordinal);

            var parts = new List<string>();
            foreach (var term in ordered)
            {
                string termString = term.ToString();
                if (parts.Count == 0)
                    parts.Add(termString);
                else
                    parts.Add(term.Coefficient >= 0 ? $"+{termString}" : termString);
            }
            return string.Concat(parts);
        }

        public Polynomial Multiply(Polynomial other)
        {
            var multipliedTerms = new List<Term>(Terms.Count * other.Terms.Count);
            foreach (var left in Terms)
                foreach (var right in other.Terms)
                    multipliedTerms.Add(Term.Multiply(left, right));

            return new Polynomial(multipliedTerms);
        }

        public static Polynomial operator *(Polynomial left, Polynomial right) => left.Multiply(right);

        /// <summary>
        /// Parses a polynomial like "2xy^2 - 3y + 4 + x" (no outer parentheses required).
        /// Supports implicit multiplication inside monomials ("2ab^2c").
        /// </summary>
        public static Polynomial Parse(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
                throw new ArgumentException("Empty polynomial string.", nameof(input));

            var chunks = Term.SplitIntoSignedTerms(input.Trim());
            if (chunks.Count == 0) return new Polynomial(new Term(0));

            var terms = new List<Term>();
            foreach (var chunk in chunks)
                terms.Add(Term.ParseSignedMonomial(chunk));

            return new Polynomial(terms);
        }

        public HashSet<string> GetVariables()
        {
            var set = new HashSet<string>(StringComparer.Ordinal);
            foreach (var term in Terms)
                foreach (var variable in term.Variables.Keys)
                    set.Add(variable);
            return set;
        }

        internal Dictionary<int, BigInteger> ToExponentMap(string variableName)
        {
            var exponentToCoefficient = new Dictionary<int, BigInteger>();
            foreach (var term in Terms)
            {
                bool isConstantOrUnivariate =
                    term.Variables.Count == 0 ||
                    (term.Variables.Count == 1 && term.Variables.ContainsKey(variableName));
                if (!isConstantOrUnivariate) continue;

                int exponent = term.Variables.TryGetValue(variableName, out int e) ? e : 0;
                exponentToCoefficient[exponent] = exponentToCoefficient.TryGetValue(exponent, out var acc)
                    ? acc + term.Coefficient
                    : term.Coefficient;
            }

            foreach (var key in exponentToCoefficient.Keys.ToList())
                if (exponentToCoefficient[key] == 0) exponentToCoefficient.Remove(key);

            return exponentToCoefficient;
        }
    }

}
