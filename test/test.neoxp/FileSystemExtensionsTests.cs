using System.IO.Abstractions.TestingHelpers;
using Xunit;
using FluentAssertions;
using NeoExpress;

namespace NeoExpressTest;

public class FileSystemExtensionsTests
{
    [Theory]
    [InlineData("", @"C:\default-filename.test-ext")]
    [InlineData("foo", @"C:\foo.test-ext")]
    [InlineData("foo.bar", @"C:\foo.bar.test-ext")]
    [InlineData("foo.test-ext", @"C:\foo.test-ext")]
    [InlineData(@"c:\foo.test-ext", @"c:\foo.test-ext")]
    [InlineData(@"c:\bar\foo", @"c:\bar\foo.test-ext")]
    [InlineData(@"c:\bar\foo.baz", @"c:\bar\foo.baz.test-ext")]
    [InlineData(@"c:\bar\foo.test-ext", @"c:\bar\foo.test-ext")]
    public void test_ResolveFileName(string option, string expected)
    {
        var fileSystem = new MockFileSystem();
        var actual = fileSystem.ResolveFileName(option, ".test-ext", () => "default-filename");
        actual.Should().Be(expected);
    }
}