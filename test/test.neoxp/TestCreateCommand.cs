using System;
using System.IO;
using System.IO.Abstractions.TestingHelpers;
using FluentAssertions;
using McMaster.Extensions.CommandLineUtils;
using Moq;
using NeoExpress;
using NeoExpress.Commands;
using NeoExpress.Models;
using Xunit;

namespace test.neo_express
{
    public class TestCreateCommand
    {
        [Fact]
        public void can_invoke_default_create()
        {
            var fileSystem = new MockFileSystem();
            var path = fileSystem.Path.Join(fileSystem.Directory.GetCurrentDirectory(), "default.neo-express");

            var chain = new ExpressChain();
            var chainManager = new Mock<IBlockchainOperations>();
            chainManager.Setup(o => o.ResolveChainFileName("")).Returns(path);
            chainManager.Setup(o => o.CreateChain(1)).Returns(chain);

            var cmd = new CreateCommand(fileSystem, chainManager.Object);
            var result = cmd.Execute();
            
            result.Should().Be(path);
            chainManager.Verify(o => o.SaveChain(chain, path));
        }

        [Fact]
        public void cant_overwrite_without_force_flag()
        {
            var fileSystem = new MockFileSystem();
            var path = fileSystem.Path.Join(fileSystem.Directory.GetCurrentDirectory(), "default.neo-express");
            fileSystem.AddFile(path, MockFileData.NullObject);

            var chain = new ExpressChain();
            var chainManager = new Mock<IBlockchainOperations>();
            chainManager.Setup(o => o.ResolveChainFileName("")).Returns(path);

            var cmd = new CreateCommand(fileSystem, chainManager.Object);
            Assert.Throws<Exception>(() => cmd.Execute());
        }

        [Fact]
        public void can_overwrite_with_force_flag()
        {
            var fileSystem = new MockFileSystem();
            var path = fileSystem.Path.Join(fileSystem.Directory.GetCurrentDirectory(), "default.neo-express");
            fileSystem.AddFile(path, MockFileData.NullObject);

            var chain = new ExpressChain();
            var chainManager = new Mock<IBlockchainOperations>();
            chainManager.Setup(o => o.ResolveChainFileName("")).Returns(path);
            chainManager.Setup(o => o.CreateChain(1)).Returns(chain);

            var cmd = new CreateCommand(fileSystem, chainManager.Object) { Force = true };
            var result = cmd.Execute();
            
            result.Should().Be(path);
            chainManager.Verify(o => o.SaveChain(chain, path));
        }
    }
}
