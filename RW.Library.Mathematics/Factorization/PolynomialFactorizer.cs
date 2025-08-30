using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RW.Library.Mathematics.Factorization
{
    using RW.Library.Mathematics.Polynomials;
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using System.Numerics;

    public static class PolynomialFactorizer
    {
        public static List<Factor> Factorize(Polynomial poly, string variable)
        {
            var factors = new List<Factor>();

            // Build exponent -> coefficient (univariate in 'variable' + constants)
            var byExp = new Dictionary<int, BigInteger>();
            foreach (var t in poly.Terms)
            {
                if (t.Variables.Count == 0 ||
                    t.Variables.Count == 1 && t.Variables.ContainsKey(variable))
                {
                    int exp = t.Variables.TryGetValue(variable, out var e) ? e : 0;
                    byExp[exp] = byExp.TryGetValue(exp, out var sum) ? sum + t.Coefficient : t.Coefficient;
                }
            }
            if (byExp.Count == 0) return factors;

            // prune cancellations
            foreach (var k in byExp.Keys.ToList())
                if (byExp[k] == 0) byExp.Remove(k);
            if (byExp.Count == 0) return factors;

            int degree = byExp.Keys.Max();
            if (!byExp.TryGetValue(degree, out var leading) || leading == 0) return factors;

            // Optional: pull out a -1 so leading is positive (nicer factors)
            if (leading < 0)
            {
                factors.Add(new Factor("(-1)"));
                var keys = byExp.Keys.ToList();
                foreach (var k in keys) byExp[k] = -byExp[k];
                leading = -leading;
            }

            var constant = byExp.TryGetValue(0, out var c0) ? c0 : BigInteger.Zero;

            // Special case root at 0
            if (constant == 0)
                factors.Add(new Factor($"({variable})"));

            // Candidates r = p/q with p | constant, q | leading (±, reduced)
            var ps = DivisorsWithSign(BigInteger.Abs(constant)).ToArray();
            var qs = DivisorsWithSign(BigInteger.Abs(leading)).ToArray();
            var seen = new HashSet<(BigInteger p, BigInteger q)>();

            foreach (var p in ps)
            {
                foreach (var q in qs)
                {
                    if (q.IsZero) continue;
                    var (rp, rq) = Reduce(p, q);
                    if (rq.Sign < 0) { rp = -rp; rq = -rq; } // keep q > 0
                    if (!seen.Add((rp, rq))) continue;

                    if (EvaluateAt(byExp, rp, rq) == 0)
                    {
                        // Emit (q x - p) with integer coeffs
                        var sign = rp.Sign < 0 ? "+" : "-";
                        factors.Add(new Factor($"({rq}{variable} {sign} {BigInteger.Abs(rp)})"));
                    }
                }
            }

            return factors;
        }

        // Evaluate P(p/q) exactly by scaling by q^deg: sum a_k * p^k * q^(deg-k)
        private static BigInteger EvaluateAt(Dictionary<int, BigInteger> byExp, BigInteger p, BigInteger q)
        {
            int deg = byExp.Keys.Max();
            BigInteger sum = 0;
            foreach (var kv in byExp)
            {
                int k = kv.Key;
                var ak = kv.Value;
                sum += ak * IPow(p, k) * IPow(q, deg - k);
            }
            return sum; // == 0 iff r = p/q is a root
        }

        private static IEnumerable<BigInteger> DivisorsWithSign(BigInteger n)
        {
            if (n.IsZero)
            {
                yield return 0; // handled elsewhere; typically not used
                yield break;
            }
            n = BigInteger.Abs(n);
            for (BigInteger i = 1; i * i <= n; i++)
            {
                if (n % i == 0)
                {
                    yield return i; yield return -i;
                    var j = n / i;
                    if (j != i) { yield return j; yield return -j; }
                }
            }
        }

        private static (BigInteger p, BigInteger q) Reduce(BigInteger p, BigInteger q)
        {
            if (p.IsZero) return (0, 1);
            var g = BigInteger.GreatestCommonDivisor(BigInteger.Abs(p), BigInteger.Abs(q));
            return (p / g, q / g);
        }

        private static BigInteger IPow(BigInteger b, int e)
        {
            if (e < 0) throw new ArgumentOutOfRangeException(nameof(e));
            BigInteger r = 1;
            while (e > 0)
            {
                if ((e & 1) != 0) r *= b;
                b *= b;
                e >>= 1;
            }
            return r;
        }
    }

}
