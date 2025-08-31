using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RW.Library.Mathematics.Polynomials
{
    public static class Monomial
    {
        public static string Signature(Term t) =>
            string.Join(",", t.Variables
                .OrderBy(kv => kv.Key, StringComparer.Ordinal)
                .Select(kv => $"{kv.Key}^{kv.Value}"));
    }
}
