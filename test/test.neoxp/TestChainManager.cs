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
            var expected = Path.Join(fileSystem.Directory.GetCurrentDirectory(), BlockchainOperations.DEFAULT_EXPRESS_FILENAME);

            var chainManager = new BlockchainOperations(fileSystem);
            var actual = chainManager.ResolveChainFileName(string.Empty);

            actual.Should().Be(expected);
        }

        [Fact]
        public void can_resolve_provided_filename_without_directory_or_extension()
        {
            var fileSystem = new MockFileSystem();
            var expected = Path.Join(fileSystem.Directory.GetCurrentDirectory(), "test" + BlockchainOperations.EXPRESS_EXTENSION);

            var chainManager = new BlockchainOperations(fileSystem);
            var actual = chainManager.ResolveChainFileName("test");

            actual.Should().Be(expected);
        }

        [Fact]
        public void can_resolve_provided_filename_without_directory()
        {
            var fileSystem = new MockFileSystem();
            var expected = Path.Join(fileSystem.Directory.GetCurrentDirectory(), "test" + BlockchainOperations.EXPRESS_EXTENSION);

            var chainManager = new BlockchainOperations(fileSystem);
            var actual = chainManager.ResolveChainFileName("test" + BlockchainOperations.EXPRESS_EXTENSION);

            actual.Should().Be(expected);
        }

        
        [Fact]
        public void can_resolve_provided_filename_without_extension()
        {
            var fileSystem = new MockFileSystem();
            var expected = "x:\\test" + BlockchainOperations.EXPRESS_EXTENSION;

            var chainManager = new BlockchainOperations(fileSystem);
            var actual = chainManager.ResolveChainFileName("x:\\test");

            actual.Should().Be(expected);
        }

        [Fact]
        public void can_resolve_provided_filename_wrong_extension()
        {
            var fileSystem = new MockFileSystem();
            var expected = "x:\\test.foo" + BlockchainOperations.EXPRESS_EXTENSION;

            var chainManager = new BlockchainOperations(fileSystem);
            var actual = chainManager.ResolveChainFileName("x:\\test.foo");

            actual.Should().Be(expected);
        }
    }
}
