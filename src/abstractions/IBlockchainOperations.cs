using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NeoExpress.Abstractions.Models;
using Newtonsoft.Json.Linq;

namespace NeoExpress.Abstractions
{
    public interface IBlockchainOperations
    {
        ExpressChain CreateBlockchain(int count);
        void ExportBlockchain(ExpressChain chain, string folder, string password, Action<string> writeConsole);
        ExpressWallet CreateWallet(string name);
        void ExportWallet(ExpressWallet wallet, string filename, string password);
        void CreateCheckpoint(ExpressChain chain, string blockChainStoreDirectory, string checkPointFileName);
        Task<JToken?> CreateCheckpointOnline(ExpressChain chain, string checkPointFileName);
        void RestoreCheckpoint(ExpressChain chain, string chainDirectory, string checkPointDirectory);
        Task RunBlockchainAsync(string directory, ExpressChain chain, int index, uint secondsPerBlock, TextWriter writer, CancellationToken cancellationToken);
        Task RunCheckpointAsync(string directory, ExpressChain chain, uint secondsPerBlock, TextWriter writer, CancellationToken cancellationToken);
        Task<JArray> Transfer(ExpressChain chain, string asset, string quantity, ExpressWalletAccount sender, ExpressWalletAccount receiver);
        Task<JArray> Claim(ExpressChain chain, string asset, ExpressWalletAccount address);
        Task<JArray> InvokeContract(ExpressChain chain, ExpressContract contract, IEnumerable<JObject> @params, ExpressWalletAccount? account);
        ExpressContract LoadContract(string filepath, Func<string, bool, bool> promptYesNo);
        Task<JArray> DeployContract(ExpressChain chain, ExpressContract contract, ExpressWalletAccount account);
    }
}
