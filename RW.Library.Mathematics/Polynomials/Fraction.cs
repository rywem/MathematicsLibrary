using System.Numerics;

namespace RW.Library.Mathematics.Polynomials
{
    /// <summary>Immutable rational number with reduced numerator/denominator and positive denominator.</summary>
    public sealed class Fraction
    {
        public BigInteger Numerator { get; }
        public BigInteger Denominator { get; } // always > 0

        public static Fraction Zero => new Fraction(0, 1);
        public static Fraction One => new Fraction(1, 1);

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
            new Fraction(a.Numerator * b.Denominator + b.Numerator * a.Denominator,
                         a.Denominator * b.Denominator);

        public static Fraction operator -(Fraction a, Fraction b) =>
            new Fraction(a.Numerator * b.Denominator - b.Numerator * a.Denominator,
                         a.Denominator * b.Denominator);

        public static Fraction operator *(Fraction a, Fraction b) =>
            new Fraction(a.Numerator * b.Numerator, a.Denominator * b.Denominator);

        public static Fraction operator /(Fraction a, Fraction b)
        {
            if (b.IsZero) throw new DivideByZeroException();
            return new Fraction(a.Numerator * b.Denominator, a.Denominator * b.Numerator);
        }

        public override string ToString() =>
            Denominator == 1 ? Numerator.ToString() : $"{Numerator}/{Denominator}";
    }
}
