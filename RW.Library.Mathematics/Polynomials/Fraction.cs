using System.Numerics;

namespace RW.Library.Mathematics.Polynomials
{


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
    
}
