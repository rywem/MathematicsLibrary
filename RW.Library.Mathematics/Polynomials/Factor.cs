namespace RW.Library.Mathematics.Polynomials
{
    /// <summary>
    /// A factor can be a leaf (a single Polynomial with integer coefficients),
    /// or a product node with nested child factors. All leaves are integer-coefficient polynomials.
    /// </summary>
    public class Factor
    {
        public Polynomial? Polynomial { get; private set; }
        public List<Factor> Children { get; } = new List<Factor>();

        public bool IsLeaf => Polynomial != null;

        private Factor() { }

        public Factor(Polynomial leafPolynomial)
        {
            Polynomial = leafPolynomial ?? throw new ArgumentNullException(nameof(leafPolynomial));
        }

        public static Factor Product(params Factor[] factors)
        {
            var composite = new Factor();
            composite.Children.AddRange(factors ?? Array.Empty<Factor>());
            return composite;
        }

        public static Factor Constant(int constant)
        {
            return new Factor(new Polynomial(new Term(constant)));
        }

        public static Factor Parse(string factorString)
        {
            if (string.IsNullOrWhiteSpace(factorString))
                throw new ArgumentException("Empty factor string.", nameof(factorString));

            string trimmed = factorString.Trim();
            if (trimmed.StartsWith("(") && trimmed.EndsWith(")") && trimmed.Length >= 2)
                trimmed = trimmed.Substring(1, trimmed.Length - 2).Trim();

            if (int.TryParse(trimmed, out int asInt))
                return Constant(asInt);

            var poly = Polynomial.Parse(trimmed);
            return new Factor(poly);
        }

        public override string ToString()
        {
            if (IsLeaf) return $"({Polynomial})";
            if (Children.Count == 0) return "(1)";
            return string.Join("", Children.Select(c => c.ToString()));
        }
    }

}
