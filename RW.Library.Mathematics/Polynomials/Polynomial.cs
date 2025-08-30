using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RW.Library.Mathematics.Polynomials
{
    public class Polynomial
    {
        public List<Term> Terms { get; private set; } = new();

        public Polynomial(params Term[] terms)
        {
            Terms.AddRange(terms);

            // Combine like terms (same variable dictionary)
            Terms = Terms
                .GroupBy(t => string.Join(",", t.Variables.OrderBy(v => v.Key)
                                                         .Select(v => $"{v.Key}^{v.Value}")))
                .Select(g => new Term(g.Sum(t => t.Coefficient), g.First().Variables))
                .Where(t => t.Coefficient != 0)
                .ToList();
        }

        public override string ToString()
        {
            return string.Join(" + ", Terms.Select(t => t.ToString()));
        }
    }
}
