using System.IO.Abstractions.TestingHelpers;
using NeoExpress.Commands;
using Xunit;
using FluentAssertions;
using System;
using Neo.Wallets;
using Neo.SmartContract;
using Neo.Cryptography.ECC;
using Neo.BlockchainToolkit.Models;

namespace NeoExpressTest;

public class CreateCommandTests
{
    [Theory]
    [InlineData(1)]
    [InlineData(4)]
    [InlineData(7)]
    public void test_create_chain(int count)
    {
        var cmd = new CreateCommand() { Count = count };
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
        var cmd = new CreateCommand() { Count = 2 };
        Assert.Throws<ArgumentException>(() => cmd.CreateChain());
    }

    [Fact]
    public void test_create_chain_custom_address_version()
    {
        byte addressVersion = 42;
        var cmd = new CreateCommand() { AddressVersion = addressVersion };
        var chain = cmd.CreateChain();

        chain.AddressVersion.Should().Be(addressVersion);
    }

    [Fact]
    public void test_save_chain()
    {
        var cmd = new CreateCommand();
        var path = @"c:\test.express";

        var chain = Utility.GetResourceChain("1.neo-express");
        var fileSystem = new MockFileSystem();

        cmd.SaveChain(chain, path, fileSystem);

        var file = fileSystem.GetFile(path);
        file.Contents.Should().NotBeEmpty();
        var json = Newtonsoft.Json.Linq.JObject.Parse(file.TextContents);
        json.Value<uint>("magic").Should().Be(chain.Network);
        var key = json["consensus-nodes"]?[0]?["wallet"]?["accounts"]?[0]?.Value<string>("private-key");
        key.Should().NotBeNull();
        key.Should().Be(chain.ConsensusNodes[0].Wallet.DefaultAccount!.PrivateKey);
    }

    [Fact]
    public void test_save_chain_cant_overwrite()
    {
        var fileSystem = new MockFileSystem();
        var cwd = fileSystem.Directory.GetCurrentDirectory();
        var path = @"c:\test.express";
        fileSystem.AddFile(path, new MockFileData(string.Empty));

        var cmd = new CreateCommand();

        var chain = new ExpressChain();
        Assert.Throws<Exception>(() => cmd.SaveChain(chain, path, fileSystem));
    }

    [Fact]
    public void test_save_chain_can_overwrite()
    {
        var fileSystem = new MockFileSystem();
        var cwd = fileSystem.Directory.GetCurrentDirectory();
        var path = @"c:\test.express";
        fileSystem.AddFile(path, new MockFileData(string.Empty));

        fileSystem.GetFile(path).Contents.Should().BeEmpty();

        var cmd = new CreateCommand() { Force = true };

        var chain = Utility.GetResourceChain("1.neo-express");
        var expectedNetwork = 3800502614u;
        cmd.SaveChain(chain, path, fileSystem);

        var file = fileSystem.GetFile(path);
        file.Contents.Should().NotBeEmpty();
        var json = Newtonsoft.Json.Linq.JObject.Parse(file.TextContents);
        json.Value<uint>("magic").Should().Be(expectedNetwork);
    }
}