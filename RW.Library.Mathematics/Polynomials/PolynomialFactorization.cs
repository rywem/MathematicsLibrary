using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Numerics;

namespace RW.Library.Mathematics.Polynomials
{
    public static class PolynomialFactorization
    {
        /// <summary>
        /// Factors a multivariate polynomial with respect to a single chosen variable using the Rational Root Theorem.
        /// Returns linear factors of the form "(a x ± b)" and optional "(-1)" if the leading coefficient is negative.
        /// </summary>
        public static List<Factor> Factorize(Polynomial polynomial, string mainVariableName)
        {
            var factorList = new List<Factor>();

            // 1) Collapse the polynomial to a univariate map: exponent -> coefficient (only terms in mainVariableName or constants).
            // (A univariate polynomial is simply a polynomial that has only one variable.)
            var exponentToCoefficient = new Dictionary<int, BigInteger>();
            foreach (var term in polynomial.Terms)
            {
                // Keep constants or terms that only involve the selected variable.
                var isConstant = term.Variables.Count == 0;
                var isUnivariateInSelected = term.Variables.Count == 1 && term.Variables.ContainsKey(mainVariableName);
                if (!isConstant && !isUnivariateInSelected)
                    continue;

                // Determine exponent on the selected variable (0 for constants).
                int exponentOnSelected = term.Variables.TryGetValue(mainVariableName, out int exponentFound) ? exponentFound : 0;

                // Sum coefficients per exponent.
                exponentToCoefficient[exponentOnSelected] =
                    exponentToCoefficient.TryGetValue(exponentOnSelected, out BigInteger coefficientSum)
                        ? coefficientSum + term.Coefficient
                        : term.Coefficient;
            }

            if (exponentToCoefficient.Count == 0)
                return factorList;

            // 2) Remove any exponents whose net coefficient canceled to zero.
            foreach (var exponent in exponentToCoefficient.Keys.ToList())
            {
                if (exponentToCoefficient[exponent] == 0)
                    exponentToCoefficient.Remove(exponent);
            }

            if (exponentToCoefficient.Count == 0)
                return factorList;

            // 3) Identify degree and leading coefficient.
            int polynomialDegree = exponentToCoefficient.Keys.Max();
            if (!exponentToCoefficient.TryGetValue(polynomialDegree, out BigInteger leadingCoefficient) || leadingCoefficient == 0)
                return factorList;

            // 4) Normalize sign so the leading coefficient is positive (emits a factor of -1 if needed).
            if (leadingCoefficient < 0)
            {
                factorList.Add(new Factor("(-1)"));
                foreach (var exponent in exponentToCoefficient.Keys.ToList())
                    exponentToCoefficient[exponent] = -exponentToCoefficient[exponent];

                leadingCoefficient = -leadingCoefficient;
            }

            // Constant term (coefficient for exponent 0). Used to build rational root candidates.
            BigInteger constantTerm = exponentToCoefficient.TryGetValue(0, out BigInteger constantCoeff)
                ? constantCoeff
                : BigInteger.Zero;

            // 5) Special-case root at zero: if constant term is zero, (x) is a factor.
            if (constantTerm == 0)
                factorList.Add(new Factor($"({mainVariableName})"));

            // 6) Generate rational root candidates r = p/q where p | constant, q | leading (both ±, reduced to lowest terms).
            var candidateNumerators = GetDivisorsWithSigns(BigInteger.Abs(constantTerm)).ToArray();
            var candidateDenominators = GetDivisorsWithSigns(BigInteger.Abs(leadingCoefficient)).ToArray();

            var testedReducedPairs = new HashSet<(BigInteger Numerator, BigInteger Denominator)>();

            foreach (var numeratorCandidate in candidateNumerators)
            {
                foreach (var denominatorCandidate in candidateDenominators)
                {
                    if (denominatorCandidate.IsZero)
                        continue;

                    // Reduce fraction and keep denominator positive for canonical form.
                    var (reducedNumerator, reducedDenominator) = ReduceToLowestTerms(numeratorCandidate, denominatorCandidate);
                    if (reducedDenominator.Sign < 0)
                    {
                        reducedNumerator = -reducedNumerator;
                        reducedDenominator = -reducedDenominator;
                    }

                    // Skip duplicates.
                    if (!testedReducedPairs.Add((reducedNumerator, reducedDenominator)))
                        continue;

                    // 7) Evaluate P(reducedNumerator / reducedDenominator) exactly (scaled to avoid fractions).
                    if (EvaluateAtRationalRoot(exponentToCoefficient, reducedNumerator, reducedDenominator) == 0)
                    {
                        // Root r = p/q implies factor (q * x - p). Choose sign formatting for readability.
                        string signText = reducedNumerator.Sign < 0 ? "+" : "-";
                        factorList.Add(new Factor($"({reducedDenominator}{mainVariableName} {signText} {BigInteger.Abs(reducedNumerator)})"));
                    }
                }
            }

            return factorList;
        }

        /// <summary>
        /// Evaluates q^deg * P(p/q) as an integer to avoid fractional arithmetic.
        /// Returns 0 if and only if p/q is an exact root of P.
        /// </summary>
        private static BigInteger EvaluateAtRationalRoot(
            Dictionary<int, BigInteger> exponentToCoefficient,
            BigInteger rootNumerator,
            BigInteger rootDenominator)
        {
            int degree = exponentToCoefficient.Keys.Max();
            BigInteger scaledSum = BigInteger.Zero;

            foreach (var exponentAndCoefficient in exponentToCoefficient)
            {
                int exponent = exponentAndCoefficient.Key;
                BigInteger coefficient = exponentAndCoefficient.Value;

                // a_k * p^k * q^(deg - k)
                scaledSum += coefficient
                           * IntegerPower(rootNumerator, exponent)
                           * IntegerPower(rootDenominator, degree - exponent);
            }

            return scaledSum;
        }

        /// <summary>
        /// Returns all positive and negative divisors of |n|.
        /// For n = 0, yields a single 0 (typically not used by callers).
        /// </summary>
        private static IEnumerable<BigInteger> GetDivisorsWithSigns(BigInteger value)
        {
            if (value.IsZero)
            {
                yield return BigInteger.Zero;
                yield break;
            }

            BigInteger absolute = BigInteger.Abs(value);
            for (BigInteger divisorCandidate = 1; divisorCandidate * divisorCandidate <= absolute; divisorCandidate++)
            {
                if (absolute % divisorCandidate == 0)
                {
                    // divisorCandidate and its complementaryDivisor are both divisors.
                    yield return divisorCandidate;
                    yield return -divisorCandidate;

                    BigInteger complementaryDivisor = absolute / divisorCandidate;
                    if (complementaryDivisor != divisorCandidate)
                    {
                        yield return complementaryDivisor;
                        yield return -complementaryDivisor;
                    }
                }
            }
        }

        /// <summary>
        /// Reduces a rational pair (numerator, denominator) to lowest terms.
        /// Ensures (0, anything) -> (0, 1).
        /// </summary>
        private static (BigInteger Numerator, BigInteger Denominator) ReduceToLowestTerms(BigInteger numerator, BigInteger denominator)
        {
            if (numerator.IsZero)
                return (BigInteger.Zero, BigInteger.One);

            BigInteger gcd = BigInteger.GreatestCommonDivisor(BigInteger.Abs(numerator), BigInteger.Abs(denominator));
            return (numerator / gcd, denominator / gcd);
        }

        /// <summary>
        /// Fast integer exponentiation: computes baseValue^exponent (exponent must be non-negative).
        /// </summary>
        private static BigInteger IntegerPower(BigInteger baseValue, int exponent)
        {
            if (exponent < 0)
                throw new ArgumentOutOfRangeException(nameof(exponent), "Exponent must be non-negative.");

            BigInteger result = BigInteger.One;
            BigInteger currentPower = baseValue;
            int remainingExponent = exponent;

            // Exponentiation by squaring.
            while (remainingExponent > 0)
            {
                bool isOddBit = (remainingExponent & 1) != 0;
                if (isOddBit)
                    result *= currentPower;

                currentPower *= currentPower;
                remainingExponent >>= 1;
            }

            return result;
        }
    }

}
