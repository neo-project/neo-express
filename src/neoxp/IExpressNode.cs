using System;
using System.Collections.Generic;
using System.Numerics;
using System.Threading.Tasks;
using Neo;
using Neo.BlockchainToolkit.Models;
using Neo.Cryptography.ECC;
using Neo.Network.P2P.Payloads;
using Neo.Network.RPC.Models;
using Neo.SmartContract;
using Neo.SmartContract.Manifest;
using Neo.SmartContract.Native;
using Neo.VM;
using Neo.Wallets;
using NeoExpress.Commands;
using NeoExpress.Models;

namespace NeoExpress
{
    interface IExpressNode : IDisposable
    {
        ExpressChain Chain { get; }
        ProtocolSettings ProtocolSettings { get; }

        enum CheckpointMode { Online, Offline }

        Task<CheckpointMode> CreateCheckpointAsync(string checkPointPath);

        Task<RpcInvokeResult> InvokeAsync(Script script, Signer? signer = null);
        Task<UInt256> ExecuteAsync(Wallet wallet, UInt160 accountHash, WitnessScope witnessScope, Script script, decimal additionalGas = 0);
        Task<UInt256> SubmitOracleResponseAsync(OracleResponse response, IReadOnlyList<ECPoint> oracleNodes);
        Task FastForwardAsync(uint blockCount, TimeSpan timestampDelta);

        Task<Block> GetBlockAsync(UInt256 blockHash);
        Task<Block> GetBlockAsync(uint blockIndex);
        Task<ContractManifest> GetContractAsync(UInt160 scriptHash);
        Task<Block> GetLatestBlockAsync();
        Task<(Transaction tx, RpcApplicationLog? appLog)> GetTransactionAsync(UInt256 txHash);
        Task<uint> GetTransactionHeightAsync(UInt256 txHash);

        Task<IReadOnlyList<(TokenContract contract, BigInteger balance)>> ListBalancesAsync(UInt160 address);
        Task<IReadOnlyList<(UInt160 hash, ContractManifest manifest)>> ListContractsAsync();
        Task<IReadOnlyList<(ulong requestId, OracleRequest request)>> ListOracleRequestsAsync();
        Task<IReadOnlyList<ExpressStorage>> ListStoragesAsync(UInt160 scriptHash);
        Task<IReadOnlyList<TokenContract>> ListTokenContractsAsync();

        Task<int> PersistContractAsync(ContractState state, IReadOnlyList<(string key, string value)> storagePairs, ContractCommand.OverwriteForce force);
        IAsyncEnumerable<(uint blockIndex, NotificationRecord notification)> EnumerateNotificationsAsync(IReadOnlySet<UInt160>? contractFilter, IReadOnlySet<string>? eventFilter);
    }
}
