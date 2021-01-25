using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO.Abstractions;
using System.Linq;
using System.Threading;
using Microsoft.Extensions.Configuration;
using NeoExpress.Models;
using Newtonsoft.Json;

namespace NeoExpress
{
    static class Extensions
    {
        // public static string GetDefaultFilename(this IFileSystem @this, string filename) => string.IsNullOrEmpty(filename)
        //    ? @this.Path.Combine(@this.Directory.GetCurrentDirectory(), "default.neo-express")
        //    : filename;

        // public static ExpressChain LoadChain(this IFileSystem @this, string filename)
        // {
        //     var serializer = new JsonSerializer();
        //     using var stream = @this.File.OpenRead(filename);
        //     using var reader = new JsonTextReader(new System.IO.StreamReader(stream));
        //     return serializer.Deserialize<ExpressChain>(reader)
        //         ?? throw new Exception($"Cannot load Neo-Express instance information from {filename}");
        // }

        // public static void SaveChain(this IFileSystem @this, ExpressChain chain, string fileName)
        // {
        //     var serializer = new JsonSerializer();
        //     using (var stream = @this.File.Open(fileName, System.IO.FileMode.Create, System.IO.FileAccess.Write))
        //     using (var writer = new JsonTextWriter(new System.IO.StreamWriter(stream)) { Formatting = Formatting.Indented })
        //     {
        //         serializer.Serialize(writer, chain);
        //     }
        // }

        // static string GetNodePath(this IFileSystem fileSystem, ExpressWalletAccount account)
        // {
        //     if (fileSystem == null) throw new ArgumentNullException(nameof(fileSystem));
        //     if (account == null) throw new ArgumentNullException(nameof(account));

        //     var rootPath = fileSystem.Path.Combine(
        //         Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData, Environment.SpecialFolderOption.DoNotVerify),
        //         "Neo-Express", 
        //         "blockchain-nodes");
        //     return fileSystem.Path.Combine(rootPath, account.ScriptHash);
        // }

        // static string GetNodePath(this IFileSystem fileSystem, ExpressWallet wallet)
        // {
        //     if (wallet == null) throw new ArgumentNullException(nameof(wallet));

        //     var defaultAccount = wallet.Accounts.Single(a => a.IsDefault);
        //     return fileSystem.GetNodePath(defaultAccount);
        // }

        // public static string GetNodePath(this IFileSystem fileSystem, ExpressConsensusNode node)
        // {
        //     if (node == null) throw new ArgumentNullException(nameof(node));

        //     return fileSystem.GetNodePath(node.Wallet);
        // }

        // const string GLOBAL_PREFIX = "Global\\";

        // public static Mutex CreateRunningMutex(this ExpressConsensusNode node)
        // {
        //     var account = node.Wallet.Accounts.Single(a => a.IsDefault);
        //     return new Mutex(true, GLOBAL_PREFIX + account.ScriptHash);
        // }

        // public static bool IsRunning(this ExpressChain chain, [MaybeNullWhen(false)] out ExpressConsensusNode node)
        // {
        //     // Check to see if there's a neo-express blockchain currently running by
        //     // attempting to open a mutex with the multisig account address for a name

        //     foreach (var consensusNode in chain.ConsensusNodes)
        //     {
        //         if (consensusNode.IsRunning())
        //         {
        //             node = consensusNode;
        //             return true;
        //         }
        //     }

        //     node = default;
        //     return false;
        // }

        // public static bool IsRunning(this ExpressConsensusNode node)
        // {
        //     // Check to see if there's a neo-express blockchain currently running by
        //     // attempting to open a mutex with the multisig account address for a name

        //     var account = node.Wallet.Accounts.Single(a => a.IsDefault);
        //     return Mutex.TryOpenExisting(GLOBAL_PREFIX + account.ScriptHash, out var _);
        // }
    }
}
