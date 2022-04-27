using System.IO.Abstractions.TestingHelpers;
using NeoExpress.Commands;
using Xunit;
using FluentAssertions;
using System;
using Neo.Wallets;
using Neo.SmartContract;
using Neo.Cryptography.ECC;
using Neo.BlockchainToolkit.Models;
using static Neo.BlockchainToolkit.Constants;

namespace NeoExpressTest;

public class CreateCommandTests
{
    [Theory]
    [InlineData(1)]
    [InlineData(4)]
    [InlineData(7)]
    public void test_create_chain(int count)
    {
        var fileSystem = new MockFileSystem();
        var cmd = new CreateCommand(fileSystem) { Count = count };
        var chain = cmd.CreateChain();
        chain.ConsensusNodes.Count.Should().Be(count);
        chain.AddressVersion.Should().Be(Neo.ProtocolSettings.Default.AddressVersion);

        var publicKeys = new ECPoint[count];
        for (int i = 0; i < chain.ConsensusNodes.Count; i++)
        {
            var node = chain.ConsensusNodes[i];
            node.Wallet.Accounts.Count.Should().Be(2);
            node.Wallet.Accounts[0].PrivateKey.Should().Be(node.Wallet.Accounts[1].PrivateKey);
            var privateKey = Convert.FromHexString(node.Wallet.Accounts[0].PrivateKey);
            var keyPair = new KeyPair(privateKey);
            publicKeys[i] = keyPair.PublicKey;
            var contract = Contract.CreateSignatureContract(keyPair.PublicKey);
            var scriptHash = node.Wallet.Accounts[0].ScriptHash.ToScriptHash(chain.AddressVersion);
            scriptHash.Should().Be(contract.ScriptHash);
        }

        var multiSigContract = Contract.CreateMultiSigContract((count * 2 / 3) + 1, publicKeys);
        for (int i = 0; i < chain.ConsensusNodes.Count; i++)
        {
            var node = chain.ConsensusNodes[i];
            var scriptHash = node.Wallet.Accounts[1].ScriptHash.ToScriptHash(chain.AddressVersion);
            scriptHash.Should().Be(multiSigContract.ScriptHash);
        }
    }

    [Fact]
    public void test_create_chain_invalid_count()
    {
        var fileSystem = new MockFileSystem();
        var cmd = new CreateCommand(fileSystem)
        {
            Count = 2
        };
        Assert.Throws<ArgumentException>(() => cmd.CreateChain());
    }

    [Fact]
    public void test_create_chain_custom_address_version()
    {
        byte addressVersion = 42;
        var fileSystem = new MockFileSystem();
        var cmd = new CreateCommand(fileSystem) { AddressVersion = addressVersion };
        var chain = cmd.CreateChain();

        chain.AddressVersion.Should().Be(addressVersion);
    }

    [Fact]
    public void test_save_chain()
    {
        var fileSystem = new MockFileSystem();
        var cmd = new CreateCommand(fileSystem);

        var chain = new ExpressChain();
        var outputPath = cmd.SaveChain(chain);

        var cwd = fileSystem.Directory.GetCurrentDirectory();
        var path = fileSystem.Path.Combine(cwd, DEFAULT_EXPRESS_FILENAME);
        outputPath.Should().Be(path);
    }

    [Fact]
    public void test_save_chain_cant_overwrite()
    {
        var fileSystem = new MockFileSystem();
        var cwd = fileSystem.Directory.GetCurrentDirectory();
        var path = fileSystem.Path.Combine(cwd, DEFAULT_EXPRESS_FILENAME);
        fileSystem.AddFile(path, new MockFileData(string.Empty));

        var cmd = new CreateCommand(fileSystem);

        var chain = new ExpressChain();
        Assert.Throws<Exception>(() => cmd.SaveChain(chain));
    }

    [Fact]
    public void test_save_chain_can_overwrite()
    {
        var fileSystem = new MockFileSystem();
        var cwd = fileSystem.Directory.GetCurrentDirectory();
        var path = fileSystem.Path.Combine(cwd, DEFAULT_EXPRESS_FILENAME);
        fileSystem.AddFile(path, new MockFileData(string.Empty));

        fileSystem.GetFile(path).Contents.Should().BeEmpty();

        var cmd = new CreateCommand(fileSystem) { Force = true };

        var chain = new ExpressChain();
        var outputPath = cmd.SaveChain(chain);

        outputPath.Should().Be(path);
        fileSystem.GetFile(path).Contents.Should().NotBeEmpty();
    }

    [Theory]
    [InlineData("foo", @"C:\foo.neo-express")]
    [InlineData("foo.bar", @"C:\foo.bar.neo-express")]
    [InlineData("foo.neo-express", @"C:\foo.neo-express")]
    [InlineData(@"c:\foo.neo-express", @"c:\foo.neo-express")]
    [InlineData(@"c:\bar\foo", @"c:\bar\foo.neo-express")]
    [InlineData(@"c:\bar\foo.baz", @"c:\bar\foo.baz.neo-express")]
    [InlineData(@"c:\bar\foo.neo-express", @"c:\bar\foo.neo-express")]
    public void test_save_chain_output(string option, string expected)
    {
        var fileSystem = new MockFileSystem();
        fileSystem.AddDirectory(@"c:\bar");
        var cmd = new CreateCommand(fileSystem) { Output = option };
        var chain = new ExpressChain();
        var outputPath = cmd.SaveChain(chain);

        outputPath.Should().Be(expected);
    }
}