using EnterpriseOMS.Application.Common.Querying;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EnterpriseOMS.Application.UnitTests.Querying
{
    public class LikeSearchTests
    {
        [Theory]
        [InlineData("plain", "plain")]
        [InlineData("50%", "50\\%")]
        [InlineData("a_b", "a\\_b")]
        [InlineData("[x]", "\\[x]")]
        [InlineData("a\\b", "a\\\\b")]
        public void Escape_MakesSpecialCharactersLiteral(string typed, string escaped)
        {
            Assert.Equal(escaped, LikeSearch.Escape(typed));
        }

        [Fact]
        public void Escape_EscapesTheEscapeCharacterFirst_SoItNeverDoubleEscapes()
        {
            // "\%" typed by a user means a backslash followed by a percent sign: BOTH must be literal.
            Assert.Equal("\\\\\\%", LikeSearch.Escape("\\%"));
        }

        [Fact]
        public void ContainsPattern_TrimsTheTextAndWrapsItInWildcards()
        {
            Assert.Equal("%wire%", LikeSearch.ContainsPattern("  wire "));
        }

        [Fact]
        public void ContainsPattern_EscapesWhatTheUserTyped_ButNotItsOwnWildcards()
        {
            Assert.Equal("%50\\%%", LikeSearch.ContainsPattern("50%"));
        }

        [Fact]
        public void TheEscapeCharacter_IsABackslash()
        {
            Assert.Equal("\\", LikeSearch.EscapeCharacter);
        }
    }
}
