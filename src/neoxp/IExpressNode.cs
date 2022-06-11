using System;
using System.Collections.Generic;
using System.Numerics;
using System.Threading.Tasks;
using Neo;
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
        ProtocolSettings ProtocolSettings { get; }

        enum CheckpointMode { Online, Offline }

        ValueTask<CheckpointMode> CreateCheckpointAsync(string checkPointPath);

        ValueTask<RpcInvokeResult> InvokeAsync(Script script, Signer? signer = null);
        Task<UInt256> ExecuteAsync(Wallet wallet, UInt160 accountHash, WitnessScope witnessScope, Script script, decimal additionalGas = 0);
        Task<UInt256> SubmitOracleResponseAsync(OracleResponse response);
        Task FastForwardAsync(uint blockCount, TimeSpan timestampDelta);

        ValueTask<Block> GetBlockAsync(UInt256 blockHash);
        ValueTask<Block> GetBlockAsync(uint blockIndex);
        ValueTask<ContractManifest> GetContractAsync(UInt160 scriptHash);
        ValueTask<Block> GetLatestBlockAsync();
        ValueTask<(Transaction tx, RpcApplicationLog? appLog)> GetTransactionAsync(UInt256 txHash);
        ValueTask<uint> GetTransactionHeightAsync(UInt256 txHash);

        ValueTask<IReadOnlyList<(TokenContract contract, BigInteger balance)>> ListBalancesAsync(UInt160 address);
        ValueTask<IReadOnlyList<(UInt160 hash, ContractManifest manifest)>> ListContractsAsync();
        ValueTask<IReadOnlyList<(ulong requestId, OracleRequest request)>> ListOracleRequestsAsync();
        ValueTask<IReadOnlyList<(ReadOnlyMemory<byte> key, ReadOnlyMemory<byte> value)>> ListStoragesAsync(UInt160 scriptHash);
        ValueTask<IReadOnlyList<TokenContract>> ListTokenContractsAsync();

        ValueTask<int> PersistContractAsync(ContractState state, IReadOnlyList<(string key, string value)> storagePairs, ContractCommand.OverwriteForce force);
        IAsyncEnumerable<(uint blockIndex, NotificationRecord notification)> EnumerateNotificationsAsync(IReadOnlySet<UInt160>? contractFilter, IReadOnlySet<string>? eventFilter);
    }
}
