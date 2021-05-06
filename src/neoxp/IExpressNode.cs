using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Neo;
using Neo.Cryptography.ECC;
using Neo.Network.P2P.Payloads;
using Neo.Network.RPC.Models;
using Neo.SmartContract.Manifest;
using Neo.SmartContract.Native;
using Neo.VM;
using Neo.Wallets;
using NeoExpress.Models;

namespace NeoExpress
{
    internal interface IExpressNode : IDisposable
    {
        ProtocolSettings ProtocolSettings { get; }

        enum CheckpointMode { Online, Offline }

        Task<CheckpointMode> CreateCheckpointAsync(string checkPointPath);

        Task<RpcInvokeResult> InvokeAsync(Script script);
        Task<UInt256> ExecuteAsync(Wallet wallet, UInt160 accountHash, WitnessScope witnessScope, Script script, decimal additionalGas = 0);
        Task<UInt256> SubmitOracleResponseAsync(OracleResponse response, IReadOnlyList<ECPoint> oracleNodes);

        Task<Block> GetBlockAsync(UInt256 blockHash);
        Task<Block> GetBlockAsync(uint blockIndex);
        Task<ContractManifest> GetContractAsync(UInt160 scriptHash);
        Task<Block> GetLatestBlockAsync();
        Task<(Transaction tx, RpcApplicationLog? appLog)> GetTransactionAsync(UInt256 txHash);
        Task<uint> GetTransactionHeightAsync(UInt256 txHash);

        Task<IReadOnlyList<(RpcNep17Balance balance, Nep17Contract contract)>> ListBalancesAsync(UInt160 address);
        Task<IReadOnlyList<(UInt160 hash, ContractManifest manifest)>> ListContractsAsync();
        Task<IReadOnlyList<Nep17Contract>> ListNep17ContractsAsync();
        Task<IReadOnlyList<(ulong requestId, OracleRequest request)>> ListOracleRequestsAsync();
        Task<IReadOnlyList<ExpressStorage>> ListStoragesAsync(UInt160 scriptHash);
    }
}
