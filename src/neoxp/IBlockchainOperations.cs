// using System;
// using System.IO;
// using System.Threading;
// using System.Threading.Tasks;
// using Neo.Persistence;
// using Neo.SmartContract;
// using Neo.SmartContract.Manifest;
// using NeoExpress.Models;

// namespace NeoExpress
// {
//     internal interface IBlockchainOperations
//     {
//         string CreateChain(int nodeCount, string output, bool force);
//         (ExpressChain chain, string filename) LoadChain(string filename);
//         void SaveChain(ExpressChain chain, string fileName);
//         void ResetNode(ExpressConsensusNode node, bool force);
//         ExpressWallet CreateWallet(ExpressChain chain, string name);
//         void ExportChain(ExpressChain chain, string password);

//         IExpressNode GetExpressNode(ExpressChain chain, bool offlineTrace = false);
//         Func<IStore, ExpressConsensusNode, bool, TextWriter, CancellationToken, Task> GetNodeRunner(ExpressChain chain, uint secondsPerBlock);
//         IStore GetNodeStore(ExpressConsensusNode node, bool discard);
//         IStore GetCheckpointStore(ExpressChain chain, string name);

//         Task<(string path, bool online)> CreateCheckpointAsync(ExpressChain chain, string checkPointFileName, bool force);
//         void RestoreCheckpoint(ExpressChain chain, string checkPointArchive, bool force);
//         Task<(NefFile nefFile, ContractManifest manifest)> LoadContractAsync(string contractPath);
//     }
// }
