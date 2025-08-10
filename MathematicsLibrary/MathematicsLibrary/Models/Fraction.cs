using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Library.Mathematics.Models
{
    public class Fraction
    {
        public int Numerator { get; set; }
        public int Denominator { get; set; }
        public Fraction(int numerator, int denominator)
        {
            if (denominator == 0)
            {
                throw new ArgumentException("Denominator cannot be zero.");
            }
            Numerator = numerator;
            Denominator = denominator;
        }
        public override string ToString()
        {
            return $"{Numerator}/{Denominator}";
        }
        public double ToDecimal()
        {
            return (double)Numerator / Denominator;
        }
    }
}
