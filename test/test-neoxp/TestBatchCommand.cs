using System;
using Xunit;
using NeoExpress.Commands;
using Moq;
using NeoExpress;
using System.IO.Abstractions.TestingHelpers;
using System.Threading.Tasks;

namespace test_neoxp
{
    public class TestBatchCommand
    {
        [Fact]
        public async Task Test1Async()
        {
            var mockNode = new Mock<IExpressNode>();
            var mockManager = new Mock<IExpressChainManager>();
            mockManager.Setup(m => m.GetExpressNode(false)).Returns(mockNode.Object);
            var mockFactory = new Mock<IExpressChainManagerFactory>();
            mockFactory.Setup(f => f.LoadChain(It.IsAny<string>())).Returns((mockManager.Object, string.Empty));
            var mockWriter = new Mock<System.IO.TextWriter>();
            var mockFileSystem = new MockFileSystem();
            var cmd = new BatchCommand(mockFactory.Object, mockFileSystem)
            {
                Reset = true,
            };

            var commands = new[] {
                "transfer 1000 neo genesis alice"
            };

            await cmd.ExecuteAsync(commands, mockWriter.Object).ConfigureAwait(false);
        }
    }
}
