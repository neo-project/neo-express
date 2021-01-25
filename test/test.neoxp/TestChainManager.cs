using System.IO;
using System.IO.Abstractions.TestingHelpers;
using FluentAssertions;
using NeoExpress;
using Xunit;

namespace test.neo_express
{
    public class TestChainManager 
    {
        [Fact]
        public void can_resolve_empty_filename()
        {
            var fileSystem = new MockFileSystem();
            var expected = Path.Join(fileSystem.Directory.GetCurrentDirectory(), ChainManager.DEFAULT_EXPRESS_FILENAME);

            var chainManager = new ChainManager(fileSystem);
            var actual = chainManager.ResolveFileName(string.Empty);

            actual.Should().Be(expected);
        }

        [Fact]
        public void can_resolve_provided_filename_without_directory_or_extension()
        {
            var fileSystem = new MockFileSystem();
            var expected = Path.Join(fileSystem.Directory.GetCurrentDirectory(), "test" + ChainManager.EXPRESS_EXTENSION);

            var chainManager = new ChainManager(fileSystem);
            var actual = chainManager.ResolveFileName("test");

            actual.Should().Be(expected);
        }

        [Fact]
        public void can_resolve_provided_filename_without_directory()
        {
            var fileSystem = new MockFileSystem();
            var expected = Path.Join(fileSystem.Directory.GetCurrentDirectory(), "test" + ChainManager.EXPRESS_EXTENSION);

            var chainManager = new ChainManager(fileSystem);
            var actual = chainManager.ResolveFileName("test" + ChainManager.EXPRESS_EXTENSION);

            actual.Should().Be(expected);
        }

        
        [Fact]
        public void can_resolve_provided_filename_without_extension()
        {
            var fileSystem = new MockFileSystem();
            var expected = "x:\\test" + ChainManager.EXPRESS_EXTENSION;

            var chainManager = new ChainManager(fileSystem);
            var actual = chainManager.ResolveFileName("x:\\test");

            actual.Should().Be(expected);
        }

        [Fact]
        public void can_resolve_provided_filename_wrong_extension()
        {
            var fileSystem = new MockFileSystem();
            var expected = "x:\\test.foo" + ChainManager.EXPRESS_EXTENSION;

            var chainManager = new ChainManager(fileSystem);
            var actual = chainManager.ResolveFileName("x:\\test.foo");

            actual.Should().Be(expected);
        }
    }
}
