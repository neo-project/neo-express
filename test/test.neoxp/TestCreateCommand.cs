using System;
using System.IO;
using System.IO.Abstractions.TestingHelpers;
using FluentAssertions;
using McMaster.Extensions.CommandLineUtils;
using Moq;
using NeoExpress.Commands;
using NeoExpress.Models;
using Xunit;

namespace test.neo_express
{
    public class TestCreateCommand
    {
        Mock<IConsole> MockConsole 
        {
            get
            {
                var outWriter = new StringWriter();
                var errWriter = new StringWriter();
                var console = new Mock<IConsole>();
                console.SetupGet(o => o.Out).Returns(outWriter);
                console.SetupGet(o => o.Error).Returns(errWriter);
                return console;
            }
        }

        [Fact]
        public void can_invoke_default_create()
        {
            var fileSystem = new MockFileSystem();
            var path = fileSystem.Path.Combine(
                fileSystem.Directory.GetCurrentDirectory(),
                "default.neo-express");

            fileSystem.FileExists(path).Should().BeFalse();

            var cmd = new CreateCommand();
            var result = cmd.OnExecute(fileSystem, MockConsole.Object);
            
            result.Should().Be(0);
            fileSystem.FileExists(path).Should().BeTrue();
            var chain = NeoExpress.Extensions2.LoadChain(fileSystem, path);
            chain.Should().NotBeNull();
        }

        [Fact]
        public void cant_overwrite_without_force_flag()
        {
            const string testContent = "test content";
            var fileSystem = new MockFileSystem();
            var path = fileSystem.Path.Combine(
                fileSystem.Directory.GetCurrentDirectory(),
                "default.neo-express");
            
            fileSystem.AddFile(path, new MockFileData(testContent));

            var cmd = new CreateCommand();
            var result = cmd.OnExecute(fileSystem, MockConsole.Object);
            
            result.Should().Be(1);
            fileSystem.FileExists(path).Should().BeTrue();
            fileSystem.GetFile(path).TextContents.Should().Be(testContent);
        }

        
        [Fact]
        public void can_overwrite_with_force_flag()
        {
            const string testContent = "test content";
            var fileSystem = new MockFileSystem();
            var path = fileSystem.Path.Combine(
                fileSystem.Directory.GetCurrentDirectory(),
                "default.neo-express");
            
            fileSystem.AddFile(path, new MockFileData(testContent));

            var cmd = new CreateCommand()
            {
                Force = true
            };
            var result = cmd.OnExecute(fileSystem, MockConsole.Object);
            
            result.Should().Be(0);
            fileSystem.FileExists(path).Should().BeTrue();
            fileSystem.GetFile(path).TextContents.Should().NotBe(testContent);
        }

        [Theory]
        [InlineData(1)]
        [InlineData(4)]
        [InlineData(7)]
        public void can_specify_valid_node_count(int count)
        {
            var fileSystem = new MockFileSystem();
            var path = fileSystem.Path.Combine(
                fileSystem.Directory.GetCurrentDirectory(),
                "default.neo-express");

            fileSystem.FileExists(path).Should().BeFalse();

            var cmd = new CreateCommand() { Count = count };
            var result = cmd.OnExecute(fileSystem, MockConsole.Object);
            
            result.Should().Be(0);
            fileSystem.FileExists(path).Should().BeTrue();
            var chain = NeoExpress.Extensions2.LoadChain(fileSystem, path);
            chain.Should().NotBeNull();
            chain.ConsensusNodes.Count.Should().Be(count);
        }

        [Theory]
        [InlineData(-1)]
        [InlineData(0)]
        [InlineData(2)]
        [InlineData(3)]
        [InlineData(5)]
        [InlineData(6)]
        [InlineData(8)]
        public void cant_specify_invalid_node_count(int count)
        {
            var fileSystem = new MockFileSystem();
            var path = fileSystem.Path.Combine(
                fileSystem.Directory.GetCurrentDirectory(),
                "default.neo-express");

            var cmd = new CreateCommand() { Count = count };
            var result = cmd.OnExecute(fileSystem, MockConsole.Object);
            
            result.Should().Be(1);
            fileSystem.FileExists(path).Should().BeFalse();
        }
    }
}
