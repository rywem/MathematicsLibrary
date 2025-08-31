using System.Numerics;

namespace RW.Library.Mathematics.Polynomials
{
    // ===========================
    // Factorization (univariate; integer-only leaf factors)
    // ===========================

    public static partial class PolynomialFactorization
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
        /// If not divisible, throws (should not happen for integer-coefficient inputs).
        /// </summary>
        private static int CombineScaleAndContentToInteger(Fraction scale, int remainderContent)
        {
            // We need an integer K such that:
            //   K * (Π(q_i x - p_i)) * remainderPrimitive == original
            // From algebra:
            //   original = (Π(q_i x - p_i)) * (remainderContent * remainderPrimitive) * (scale.Numerator / scale.Denominator)
            // where remainderPrimitive is the primitive (content=1) remainder polynomial.
            // Therefore the single integer constant we should emit is:
            //   K = (remainderContent * scale.Numerator) / scale.Denominator
            // For integer-coefficient inputs and our synthetic division, scale.Denominator should divide remainderContent.
            if (scale.Denominator == 1)
                return checked((int)(remainderContent * scale.Numerator));

            // Ensure exact divisibility
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
