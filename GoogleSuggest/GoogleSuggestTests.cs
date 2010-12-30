using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NUnit.Framework;

namespace GoogleSuggest
{
    [TestFixture]
    class GoogleSuggestTests
    {
        private string _testString = "miccrosoft";
        private string _hopedForResult = "microsoft";

        [Test]
        public void GetSuggestion_Works()
        {
            Assert.AreEqual(_hopedForResult, GoogleSuggest.GetSuggestion(_testString));
        }

        [Test]
        public void GetSuggestions_Works()
        {
            List<CompleteSuggestion> sugs = GoogleSuggest.GetSuggestions(_testString);

            Assert.Greater(sugs.Count, 0);
            foreach (var s in sugs.Take(5))
            {
                Assert.AreNotEqual("", s.suggestion);
            }

        }
    }
}
