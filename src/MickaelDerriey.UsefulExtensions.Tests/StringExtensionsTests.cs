using System;
using Xunit;

namespace MickaelDerriey.UsefulExtensions.Tests
{
    public class StringExtensionsTests
    {
        public class TheFormatWithMethod
        {
            [Fact]
            public void ThrowsArgumentNullExceptionWhenSourceIsNull()
            {
                string source = null;

                var exception = Record.Exception(() => source.FormatWith("BOOM!"));

                Assert.NotNull(exception);
                Assert.IsType<ArgumentNullException>(exception);
            }

            [Fact]
            public void FormatsTheSourceValueWithSpecifiedArguments()
            {
                var source = "Hey my name is {0}";

                var result = source.FormatWith("Mickaël");

                Assert.Equal("Hey my name is Mickaël", result);
            }

            [Fact]
            public void FormatsTheSourceValueWithSpecifiedMultipleArguments()
            {
                var source = "Hey my name is {0}, I'm {1}";

                var result = source.FormatWith("Mickaël", 19);

                Assert.Equal("Hey my name is Mickaël, I'm 19", result);
            }
        }
    }
}
