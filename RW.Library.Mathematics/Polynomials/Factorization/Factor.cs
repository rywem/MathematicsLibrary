using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RW.Library.Mathematics.Polynomials.Factorization
{
    public class Factor
    {
        public string Expression { get; set; }
        public Factor(string expr) => Expression = expr;
        public override string ToString() => Expression;
    }

}
