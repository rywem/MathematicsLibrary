using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RW.Library.Mathematics.Polynomials
{
    public class Term
    {
        public int Coefficient { get; set; }
        public Dictionary<string, int> Variables { get; set; }

        public Term(int coefficient, Dictionary<string, int>? variables = null)
        {
            Coefficient = coefficient;
            Variables = NormalizeVars(variables);
        }

        private static Dictionary<string, int> NormalizeVars(Dictionary<string, int>? vars)
        {
            if (vars == null || vars.Count == 0)
                return new Dictionary<string, int>(StringComparer.Ordinal);

            var result = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (var kv in vars)
            {
                if (kv.Value == 0) continue;
                result[kv.Key] = result.TryGetValue(kv.Key, out var e) ? e + kv.Value : kv.Value;
            }
            // drop any that became 0 after merging
            foreach (var k in result.Where(p => p.Value == 0).Select(p => p.Key).ToList())
                result.Remove(k);

            return result;
        }

        public override string ToString()
        {
            var parts = Variables
                .OrderBy(v => v.Key, StringComparer.Ordinal)
                .Select(v => v.Value == 1 ? v.Key : $"{v.Key}^{v.Value}");
            return $"{Coefficient}{(parts.Any() ? string.Concat(parts) : "")}";
        }
    }
}
