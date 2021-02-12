using Neo;
using Neo.BlockchainToolkit.Models;
using Neo.Cryptography.ECC;
using Neo.Network.P2P.Payloads;
using Neo.Network.RPC.Models;
using Neo.SmartContract.Manifest;
using Neo.SmartContract.Native;
using Neo.VM;
using NeoExpress.Models;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace NeoExpress
{
    internal interface IExpressNode : IDisposable
    {
        Task<bool> CreateCheckpointAsync(string checkPointPath);
        Task<UInt256> ExecuteAsync(ExpressWalletAccount account, Script script, decimal additionalGas = 0);
        Task<UInt256> SubmitTransactionAsync(Transaction tx);
        Task<RpcInvokeResult> InvokeAsync(Script script);
        Task<(RpcNep17Balance balance, Nep17Contract contract)[]> GetBalancesAsync(UInt160 address);
        Task<(Transaction tx, RpcApplicationLog? appLog)> GetTransactionAsync(UInt256 txHash);
        Task<Block> GetBlockAsync(UInt256 blockHash);
        Task<Block> GetBlockAsync(uint blockIndex);
        Task<Block> GetLatestBlockAsync();
        Task<uint> GetTransactionHeightAsync(UInt256 txHash);
        Task<IReadOnlyList<ExpressStorage>> GetStoragesAsync(UInt160 scriptHash);
        Task<ContractManifest> GetContractAsync(UInt160 scriptHash);
        Task<IReadOnlyList<(UInt160 hash, ContractManifest manifest)>> ListContractsAsync();
        Task<IReadOnlyList<Nep17Contract>> ListNep17ContractsAsync();
        Task<IReadOnlyList<(ulong requestId, OracleRequest request)>> ListOracleRequestsAsync();
        Task<UInt256> SubmitOracleResponseAsync(OracleResponse response, ECPoint[] oracleNodes);
    }
}
