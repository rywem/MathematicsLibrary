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

        // Dictionary of variable -> exponent
        public Dictionary<string, int> Variables { get; set; } = new();

        public Term(int coefficient, Dictionary<string, int>? variables = null)
        {
            Coefficient = coefficient;
            Variables = variables ?? new Dictionary<string, int>();
        }

        public override string ToString()
        {
            var parts = Variables
                .OrderBy(v => v.Key)
                .Select(v => v.Value == 1 ? v.Key : $"{v.Key}^{v.Value}");

            return $"{Coefficient}{(parts.Any() ? string.Concat(parts) : "")}";
        }
    }
}
