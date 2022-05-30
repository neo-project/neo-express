using System.IO.Abstractions.TestingHelpers;
using Xunit;
using FluentAssertions;
using NeoExpress;
using System.Linq;
using System;

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

    [Fact]
    public void test_import_nep6()
    {
        var expectedScript = Convert.FromBase64String("DCEDbgIGarz2YSK4Lg3ibqYoP2aC3G/Ar0RrWFHM06p87vtBVuezJw==");

        var fileSystem = new MockFileSystem();
        var path = @"c:\test-wallet.json";
        fileSystem.AddResource(path, "test-wallet.json");
        if (fileSystem.TryImportNEP6(path, "password", Neo.ProtocolSettings.Default.AddressVersion, out var wallet))
        {
            var account = wallet.GetAccounts().Single();
            account.Address.Should().Be("NLdwJoSk6rEJyahgLLjNiE3oKE15vCDeb9");
            account.Contract.Script.Should().BeEquivalentTo(expectedScript);
        }
        else
        {
            throw new System.Exception("TryImportNEP6 returned false");
        }
    }

    [Fact]
    public void test_export_nep6()
    {
        var chain = Utility.GetResourceChain("4.neo-express");

        var fileSystem = new MockFileSystem();
        var path = @"c:\test-wallet.json";
        var password = "G0Kr@k3n";
        var settings = Neo.ProtocolSettings.Default;

        fileSystem.ExportNEP6(chain.ConsensusNodes[0].Wallet, path, password, settings.AddressVersion);
        var file = fileSystem.GetFile(path);
        var json = Neo.IO.Json.JObject.Parse(file.TextContents);

        var wallet = new Neo.Wallets.NEP6.NEP6Wallet(string.Empty, settings, json);
        using var _ = wallet.Unlock(password);

        var defaultAccount = chain.ConsensusNodes[0].Wallet.DefaultAccount ?? throw new Exception("No default account");
        var nep6DefaultAccount = wallet.GetDefaultAccount() ?? throw new Exception("No default account");
        var nep6KeyPair = nep6DefaultAccount.GetKey() ?? throw new Exception("No Key");

        defaultAccount.ScriptHash.Should().Be(nep6DefaultAccount.Address);
        nep6KeyPair.PrivateKey.Should().BeEquivalentTo(Convert.FromHexString(defaultAccount.PrivateKey));
    }
}