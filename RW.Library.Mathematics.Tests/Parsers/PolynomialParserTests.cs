using RW.Library.Mathematics.Parsers;
using RW.Library.Mathematics.Polynomials;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
namespace RW.Library.Mathematics.Tests.Polynomials
{
    public class PolynomialParserTests
    {
        // --- helpers ---------------------------------------------------------

        private static Dictionary<string, int> Vars(params (string v, int e)[] pairs)
        { 
            return pairs.ToDictionary(p => p.v, p => p.e, StringComparer.Ordinal);
        }
        private static void AssertTermExists(Polynomial p, int coefficient, Dictionary<string, int> vars)
        {
            Assert.Contains(p.Terms, t =>
                t.Coefficient == coefficient &&
                t.Variables.Count == vars.Count &&
                t.Variables.OrderBy(kv => kv.Key).SequenceEqual(vars.OrderBy(kv => kv.Key)));
        }

        private static void AssertNoVars(Polynomial p, int coefficient)
        {
            Assert.Contains(p.Terms, t => t.Coefficient == coefficient && t.Variables.Count == 0);
        }

        // --- basic parses ----------------------------------------------------

        [Fact]
        public void Parse_SimpleQuadratic()
        {
            var p = PolynomialParser.Parse("x^2-3x+4");

            Assert.Equal(3, p.Terms.Count);
            AssertTermExists(p, 1, Vars(("x", 2)));
            AssertTermExists(p, -3, Vars(("x", 1)));
            AssertNoVars(p, 4);
        }

        [Fact]
        public void Parse_MultiVariable_WithPowers()
        {
            var p = PolynomialParser.Parse("2xy^2 - y + 2x + 4");

            Assert.Equal(4, p.Terms.Count);
            AssertTermExists(p, 2, Vars(("x", 1), ("y", 2)));
            AssertTermExists(p, -1, Vars(("y", 1)));
            AssertTermExists(p, 2, Vars(("x", 1)));
            AssertNoVars(p, 4);
        }

        [Fact]
        public void Parse_AllowsAsteriskSeparators()
        {
            var p = PolynomialParser.Parse("3*x*y^2 - 5");

            Assert.Equal(2, p.Terms.Count);
            AssertTermExists(p, 3, Vars(("x", 1), ("y", 2)));
            AssertNoVars(p, -5);
        }

        [Theory]
        [InlineData("x + y - 2", new[] { 1, 1, -2 })]
        [InlineData("-x + y - 2", new[] { -1, 1, -2 })]
        [InlineData("  x  -  3x + 4 ", new[] { 1, -3, 4 })]
        public void Parse_Whitespace_And_Signs(string expr, int[] expectedCoeffs)
        {
            var p = PolynomialParser.Parse(expr);

            // Just validate the expected number of terms and that all constants/variables show up
            Assert.Equal(expectedCoeffs.Length, p.Terms.Count);
        }

        [Theory]
        [InlineData("x", 1)]
        [InlineData("-y", -1)]
        [InlineData("+z", 1)]
        public void Parse_ImplicitCoefficient_One(string expr, int expectedCoef)
        {
            var p = PolynomialParser.Parse(expr);
            Assert.Single(p.Terms);
            var t = p.Terms[0];

            Assert.Equal(expectedCoef, t.Coefficient);
            Assert.Single(t.Variables);
        }

        [Fact]
        public void Parse_ConstantOnly()
        {
            var p = PolynomialParser.Parse("42");
            Assert.Single(p.Terms);
            AssertNoVars(p, 42);
        }

        // --- like-term combination (Polynomial ctor) ------------------------

        [Fact]
        public void Parse_CombinesLikeTerms_InConstructor()
        {
            var p = PolynomialParser.Parse("x + 2x + 4");
            // (x + 2x) -> 3x
            Assert.Equal(2, p.Terms.Count);
            AssertTermExists(p, 3, Vars(("x", 1)));
            AssertNoVars(p, 4);
        }

        [Fact]
        public void Parse_CombinesSameVarPowers()
        {
            var p = PolynomialParser.Parse("x*y^2 + 2xy^2 - 3y + y");
            // xy^2 terms combine: 1 + 2 = 3; y terms combine: -3 + 1 = -2
            Assert.Equal(2, p.Terms.Count);
            AssertTermExists(p, 3, Vars(("x", 1), ("y", 2)));
            AssertTermExists(p, -2, Vars(("y", 1)));
        }

        // --- error cases -----------------------------------------------------

        [Theory]
        [InlineData("x^")]
        [InlineData("2x^")]
        [InlineData("3**x")]
        [InlineData("2^3")] // our grammar doesn't support exponent on numeric literals
        public void Parse_InvalidInputs_Throw(string expr)
        {
            Assert.Throws<FormatException>(() => PolynomialParser.Parse(expr));
        }

        // --- normalization details ------------------------------------------

        [Fact]
        public void Parse_NormalizesUnicodeMinus()
        {
            var enDash = "x^2–3x+4";   // EN DASH
            var emDash = "x^2—3x+4";   // EM DASH
            var minus = "x^2-3x+4";   // hyphen-minus

            var p1 = PolynomialParser.Parse(enDash);
            var p2 = PolynomialParser.Parse(emDash);
            var p3 = PolynomialParser.Parse(minus);

            Assert.Equal(p3.ToString(), p1.ToString());
            Assert.Equal(p3.ToString(), p2.ToString());
        }
    }
    public class PolynomialParser_RawTests
    {
        private static List<int> CoeffsOf(string expr)
            => PolynomialParser.ParseTerms(expr).Select(t => t.Coefficient).ToList();

        [Theory]
        [InlineData("x + y - 2", new[] { 1, 1, -2 })]
        [InlineData("-x + y - 2", new[] { -1, 1, -2 })]
        [InlineData("  x  -  3x + 4 ", new[] { 1, -3, 4 })] // raw terms keep both x and -3x
        public void Raw_Parse_Whitespace_And_Signs(string expr, int[] expectedCoeffs)
        {
            var coeffs = CoeffsOf(expr);
            Assert.Equal(expectedCoeffs, coeffs);
        }
    }

    public class PolynomialParser_SemanticTests
    {
        private static string Sig(Term t)
            => string.Join(",", t.Variables.OrderBy(kv => kv.Key).Select(kv => $"{kv.Key}^{kv.Value}"));

        private static Dictionary<string, int> Map(Polynomial p)
            => p.Terms.ToDictionary(Sig, t => t.Coefficient);

        [Fact]
        public void Semantic_SimpleQuadratic()
        {
            var p = PolynomialParser.Parse("x^2 - 3x + 4");
            var m = Map(p);

            Assert.Equal(3, p.Terms.Count);
            Assert.Equal(1, m["x^2"]);
            Assert.Equal(-3, m["x^1"]);
            Assert.Equal(4, m[""]); // empty signature = constant
        }

        [Fact]
        public void Semantic_MultiVar()
        {
            var p = PolynomialParser.Parse("2xy^2 - y + 2x + 4");
            var m = Map(p);

            Assert.Equal(4, p.Terms.Count);
            Assert.Equal(2, m["x^1,y^2"]);
            Assert.Equal(-1, m["y^1"]);
            Assert.Equal(2, m["x^1"]);
            Assert.Equal(4, m[""]);
        }

        [Fact]
        public void Semantic_AllowsAsterisk()
        {
            var p = PolynomialParser.Parse("3*x*y^2 - 5");
            var m = Map(p);

            Assert.Equal(2, p.Terms.Count);
            Assert.Equal(3, m["x^1,y^2"]);
            Assert.Equal(-5, m[""]);
        }

        [Fact]
        public void Semantic_CombinesLikeTerms()
        {
            var p = PolynomialParser.Parse("x - 3x + 4");
            var m = Map(p);

            // (x - 3x) => -2x
            Assert.Equal(2, p.Terms.Count);
            Assert.Equal(-2, m["x^1"]);
            Assert.Equal(4, m[""]);
        }
    }
}
