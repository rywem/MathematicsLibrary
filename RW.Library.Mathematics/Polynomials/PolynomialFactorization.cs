using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
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

    public static class Monomial
    {
        /// <summary>Deterministic signature for grouping like terms (same variables/ exponents).</summary>
        public static string Signature(Term term) =>
            string.Join(",", term.Variables
                .OrderBy(kv => kv.Key, StringComparer.Ordinal)
                .Select(kv => $"{kv.Key}^{kv.Value}"));
    }

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

        /// <summary>
        /// Convenience: parse a single factor (power defaults to 1). If the input is "(a+b)^3",
        /// this returns a product node with three identical children.
        /// </summary>
        public static Factor ProductFromString(string factorText)
        {
            var parsedChildren = ParseMany(factorText).ToArray();
            return parsedChildren.Length == 1 ? parsedChildren[0] : Product(parsedChildren);
        }

        /// <summary>
        /// Parses a factor string and returns one or more Factors.
        /// Supports integer powers: "(a+h)^3" => three identical factor leaves of (a+h).
        /// Disallows fractional, negative, or non-integer exponents.
        /// </summary>
        public static IEnumerable<Factor> ParseMany(string factorText)
        {
            if (string.IsNullOrWhiteSpace(factorText))
                throw new ArgumentException("Empty factor string.", nameof(factorText));

            string trimmed = factorText.Trim();

            // Detect a caret outside of parentheses (overall factor exponent).
            int caretIndex = FindTopLevelCaret(trimmed);
            string basePart = trimmed;
            string? exponentPart = null;

            if (caretIndex >= 0)
            {
                basePart = trimmed.Substring(0, caretIndex).Trim();
                exponentPart = trimmed.Substring(caretIndex + 1).Trim();
            }

            // Strip one layer of outer parentheses from the base if present and balanced.
            basePart = StripSingleOuterParensIfBalanced(basePart);

            int repeatCount = 1;
            if (!string.IsNullOrEmpty(exponentPart))
            {
                // Disallow fractional or negative exponents (outside integer polynomials).
                if (exponentPart.Contains('/') || exponentPart.Contains('.'))
                    throw new NotSupportedException($"Non-integer exponent '{exponentPart}' is not supported. Use an integer >= 0.");

                if (!int.TryParse(exponentPart, out repeatCount))
                    throw new FormatException($"Invalid exponent '{exponentPart}'.");

                if (repeatCount < 0)
                    throw new NotSupportedException("Negative exponents are not supported for polynomial factors.");

                if (repeatCount == 0)
                    return new[] { Constant(1) }; // (anything)^0 = 1
            }

            // If the basePart is an integer constant like "-1" or "3"
            if (int.TryParse(basePart, out int asInt))
            {
                return Enumerable.Repeat(Constant(asInt), repeatCount);
            }

            // Otherwise parse as a polynomial
            var polynomial = Polynomial.Parse(basePart);
            var factorLeaf = new Factor(polynomial);
            return Enumerable.Repeat(factorLeaf, repeatCount);
        }

        /// <summary>
        /// Back-compat: parse a factor (without returning multiples). If a power is supplied, returns a product node.
        /// </summary>
        public static Factor Parse(string factorText)
        {
            var factors = ParseMany(factorText).ToArray();
            return factors.Length == 1 ? factors[0] : Product(factors);
        }

        public override string ToString()
        {
            if (IsLeaf) return $"({Polynomial})";
            if (Children.Count == 0) return "(1)";
            return string.Join("", Children.Select(c => c.ToString()));
        }

        // ---- Helpers for ^-parsing ----

        private static int FindTopLevelCaret(string text)
        {
            int depth = 0;
            for (int i = 0; i < text.Length; i++)
            {
                char ch = text[i];
                if (ch == '(') depth++;
                else if (ch == ')') depth = Math.Max(0, depth - 1);
                else if (ch == '^' && depth == 0)
                    return i;
            }
            return -1;
        }

        private static string StripSingleOuterParensIfBalanced(string text)
        {
            if (text.Length < 2 || text[0] != '(' || text[^1] != ')') return text;

            int depth = 0;
            for (int i = 0; i < text.Length; i++)
            {
                char ch = text[i];
                if (ch == '(') depth++;
                else if (ch == ')') depth--;
                if (depth == 0 && i < text.Length - 1)
                    return text; // outer parens close before end => not a single wrapping pair
            }
            // Balanced single outer pair
            return text.Substring(1, text.Length - 2).Trim();
        }
    }

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

    public static class PolynomialFactorization
    {
        /// <summary>
        /// Factor a polynomial over the rationals into a nested factor tree with **integer-coefficient leaf polynomials only**.
        /// - Univariate only; throws if multiple variables are detected.
        /// - Uses the Rational Root Theorem and synthetic division (over rationals internally).
        /// - Every linear factor for a root r = p/q is emitted as (q x - p) with integer coefficients.
        /// - A single integer constant factor is added so that Expand(Factorize(P)) == P.
        /// - If no more rational roots remain, an irreducible remainder (integer coefficients) is appended as a leaf.
        /// </summary>
        public static Factor Factorize(Polynomial polynomial)
        {
            if (polynomial == null) throw new ArgumentNullException(nameof(polynomial));

            var variableNames = polynomial.GetVariables();
            if (variableNames.Count > 1)
                throw new InvalidOperationException($"Only univariate factorization is supported. Variables: {string.Join(", ", variableNames)}");

            // If constant-only polynomial, return that as a single leaf factor (possibly 0 or ±k).
            if (variableNames.Count == 0)
                return new Factor(polynomial);

            string variableName = variableNames.First();

            // Build exponent->coefficient map and normalize sign
            var exponentCoefficientMap = polynomial.ToExponentMap(variableName);
            if (exponentCoefficientMap.Count == 0)
                return new Factor(new Polynomial(new Term(0)));

            int degree = exponentCoefficientMap.Keys.Max();
            if (!exponentCoefficientMap.TryGetValue(degree, out BigInteger leadingCoefficient) || leadingCoefficient == 0)
                return new Factor(new Polynomial(new Term(0)));

            var productChildren = new List<Factor>();

            // Track ∏(1/q) from rational roots so we can combine with remainder content at the end.
            Fraction overallScale = new Fraction(1, 1);

            // If leading coefficient is negative, factor out -1 as an integer factor.
            if (leadingCoefficient < 0)
            {
                productChildren.Add(Factor.Constant(-1));
                foreach (var key in exponentCoefficientMap.Keys.ToList())
                    exponentCoefficientMap[key] = -exponentCoefficientMap[key];
            }

            // Build dense list of Fraction coefficients in descending powers.
            int normalizedDegree = exponentCoefficientMap.Keys.Max();
            var coefficientsDescending = new List<Fraction>(Enumerable.Range(0, normalizedDegree + 1)
                .Select(i => new Fraction(
                    exponentCoefficientMap.TryGetValue(normalizedDegree - i, out var c) ? c : 0, 1)));

            // Peel off rational roots; loop terminates when no new root is found or degree < 2.
            while (true)
            {
                int currentDegree = coefficientsDescending.Count - 1;
                if (currentDegree <= 1) break;

                Fraction leading = coefficientsDescending[0];
                Fraction constant = coefficientsDescending[^1];

                var possibleNumerators = GetDivisorsWithSigns(BigInteger.Abs(constant.Numerator)).ToArray();
                var possibleDenominators = GetDivisorsWithSigns(BigInteger.Abs(leading.Numerator)).ToArray();

                bool foundRootThisPass = false;
                var testedPairs = new HashSet<(BigInteger, BigInteger)>();

                foreach (var numeratorCandidate in possibleNumerators)
                {
                    foreach (var denominatorCandidate in possibleDenominators)
                    {
                        if (denominatorCandidate.IsZero) continue;

                        var (reducedNumerator, reducedDenominator) = ReduceToLowestTerms(numeratorCandidate, denominatorCandidate);
                        if (reducedDenominator.Sign < 0)
                        {
                            reducedNumerator = -reducedNumerator;
                            reducedDenominator = -reducedDenominator;
                        }
                        if (!testedPairs.Add((reducedNumerator, reducedDenominator))) continue;

                        var candidateRoot = new Fraction(reducedNumerator, reducedDenominator);

                        if (EvaluateAt(coefficientsDescending, candidateRoot).IsZero)
                        {
                            // Emit INTEGER linear factor (q x - p)
                            var integerLinearFactor = new Polynomial(
                                new Term((int)reducedDenominator, new Dictionary<string, int> { { variableName, 1 } }),
                                new Term(-(int)reducedNumerator));
                            productChildren.Add(new Factor(integerLinearFactor));

                            // Synthetic division by (x - r) (not by qx - p)
                            coefficientsDescending = SyntheticDivide(coefficientsDescending, candidateRoot);

                            // Track the 1/q scale because (qx - p) = q*(x - r)
                            overallScale = overallScale * new Fraction(1, reducedDenominator);

                            foundRootThisPass = true;
                            break;
                        }
                    }
                    if (foundRootThisPass) break;
                }

                if (!foundRootThisPass)
                    break; // no more rational roots -> stop peeling
            }

            // Build remainder leaf as an integer-coefficient polynomial plus its integer "content".
            Factor remainderFactor = BuildIntegerLeafFromFractions(coefficientsDescending, variableName, out int remainderContent);

            // Combine remainder content with the overall 1/∏q scale into a single INTEGER constant factor.
            int combinedConstant = CombineScaleAndContentToInteger(overallScale, remainderContent);

            // Add constant if not 1
            if (combinedConstant != 1)
                productChildren.Insert(0, Factor.Constant(combinedConstant));

            // Add remainder leaf only if it is not redundant (i.e., not exactly 1 or 0)
            if (remainderFactor.Polynomial != null && remainderFactor.Polynomial.Terms.Count > 0)
            {
                bool isPureConstant = remainderFactor.Polynomial.Terms.All(t => t.Variables.Count == 0);
                int constantValue = isPureConstant ? remainderFactor.Polynomial.Terms.Sum(t => t.Coefficient) : 0;

                // Skip adding "(1)"
                if (!(isPureConstant && constantValue == 1))
                    productChildren.Add(remainderFactor);
            }

            // If we ended up only with a constant, just return that constant leaf
            if (productChildren.Count == 1 && productChildren[0].IsLeaf && productChildren[0].Polynomial!.Terms.All(t => t.Variables.Count == 0))
                return productChildren[0];

            return Factor.Product(productChildren.ToArray());
        }

        // ======= Fraction & helpers (internal) =======

        public class Fraction
        {
            public readonly BigInteger Numerator;
            public readonly BigInteger Denominator; // always > 0

            public Fraction(BigInteger numerator, BigInteger denominator)
            {
                if (denominator == 0) throw new DivideByZeroException();
                if (numerator.IsZero)
                {
                    Numerator = 0;
                    Denominator = 1;
                    return;
                }

                var gcd = BigInteger.GreatestCommonDivisor(BigInteger.Abs(numerator), BigInteger.Abs(denominator));
                numerator /= gcd;
                denominator /= gcd;

                if (denominator.Sign < 0)
                {
                    numerator = -numerator;
                    denominator = -denominator;
                }

                Numerator = numerator;
                Denominator = denominator;
            }

            public bool IsZero => Numerator.IsZero;

            public static Fraction operator +(Fraction a, Fraction b) =>
                new Fraction(a.Numerator * b.Denominator + b.Numerator * a.Denominator, a.Denominator * b.Denominator);

            public static Fraction operator *(Fraction a, Fraction b) =>
                new Fraction(a.Numerator * b.Numerator, a.Denominator * b.Denominator);
        }

        private static IEnumerable<BigInteger> GetDivisorsWithSigns(BigInteger value)
        {
            if (value.IsZero) { yield return BigInteger.Zero; yield break; }
            var abs = BigInteger.Abs(value);
            for (BigInteger i = 1; i * i <= abs; i++)
            {
                if (abs % i == 0)
                {
                    yield return i; yield return -i;
                    var j = abs / i;
                    if (j != i) { yield return j; yield return -j; }
                }
            }
        }

        private static (BigInteger, BigInteger) ReduceToLowestTerms(BigInteger numerator, BigInteger denominator)
        {
            if (numerator.IsZero) return (0, 1);
            var g = BigInteger.GreatestCommonDivisor(BigInteger.Abs(numerator), BigInteger.Abs(denominator));
            return (numerator / g, denominator / g);
        }

        private static Fraction EvaluateAt(List<Fraction> coefficientsDescending, Fraction x)
        {
            // Horner scheme with coefficients in descending powers
            var acc = new Fraction(0, 1);
            foreach (var c in coefficientsDescending)
                acc = acc * x + c;
            return acc;
        }

        private static List<Fraction> SyntheticDivide(List<Fraction> coefficientsDescending, Fraction root)
        {
            int degree = coefficientsDescending.Count - 1;
            var quotient = new List<Fraction>(degree);

            var accumulator = coefficientsDescending[0];
            quotient.Add(accumulator);

            for (int i = 1; i < coefficientsDescending.Count - 1; i++)
            {
                accumulator = accumulator * root + coefficientsDescending[i];
                quotient.Add(accumulator);
            }
            // Remainder would be accumulator * root + a0, which must be 0 for a true root.
            return quotient;
        }

        /// <summary>
        /// Builds an integer-coefficient polynomial from a list of Fraction coefficients (descending powers).
        /// Returns a leaf Factor and outputs an integer content factor (the gcd of all integerized coefficients).
        /// </summary>
        private static Factor BuildIntegerLeafFromFractions(List<Fraction> coefficientsDescending, string variableName, out int integerContent)
        {
            int remainingDegree = coefficientsDescending.Count - 1;
            if (remainingDegree < 0)
            {
                integerContent = 1;
                return new Factor(new Polynomial(new Term(0)));
            }

            // Clear denominators
            BigInteger lcm = 1;
            foreach (var f in coefficientsDescending)
                lcm = LeastCommonMultiple(lcm, f.Denominator);

            var integerCoefficients = new List<BigInteger>(coefficientsDescending.Count);
            foreach (var f in coefficientsDescending)
                integerCoefficients.Add(f.Numerator * (lcm / f.Denominator));

            // Extract content = gcd of all integer coefficients
            BigInteger content = 0;
            foreach (var c in integerCoefficients)
                content = content == 0 ? BigInteger.Abs(c) : BigInteger.GreatestCommonDivisor(content, BigInteger.Abs(c));
            if (content == 0) content = 1; // zero polynomial -> treat content as 1

            // Build terms with coefficients divided by content
            var terms = new List<Term>();
            for (int i = 0; i < integerCoefficients.Count; i++)
            {
                BigInteger coeff = integerCoefficients[i] / content;
                int power = remainingDegree - i;
                if (coeff == 0) continue;

                if (power == 0)
                    terms.Add(new Term((int)coeff));
                else
                    terms.Add(new Term((int)coeff, new Dictionary<string, int> { { variableName, power } }));
            }

            integerContent = (int)content;
            return new Factor(new Polynomial(terms));
        }

        /// <summary>
        /// Combine the accumulated 1/∏q scale with an integer remainder-content into a single INTEGER constant.
        /// Computes (remainderContent * scale.Numerator) / scale.Denominator and requires exact divisibility.
        /// </summary>
        private static int CombineScaleAndContentToInteger(Fraction scale, int remainderContent)
        {
            if (scale.Denominator == 1)
                return checked((int)(remainderContent * scale.Numerator));

            // Ensure exact divisibility (expected for integer-input polynomials)
            if (remainderContent % (int)scale.Denominator != 0)
                throw new InvalidOperationException("Internal scale/content mismatch produced a non-integer constant.");

            int divided = checked(remainderContent / (int)scale.Denominator);
            return checked(divided * (int)scale.Numerator);
        }

        private static BigInteger LeastCommonMultiple(BigInteger a, BigInteger b)
        {
            if (a == 0 || b == 0) return 0;
            return BigInteger.Abs(a / BigInteger.GreatestCommonDivisor(a, b) * b);
        }
    }
}
