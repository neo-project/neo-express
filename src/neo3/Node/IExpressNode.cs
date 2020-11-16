using Neo;
using Neo.Network.P2P.Payloads;
using Neo.Network.RPC.Models;
using Neo.SmartContract.Manifest;
using Neo.SmartContract.Native.Oracle;
using Neo.VM;
using NeoExpress.Abstractions.Models;
using NeoExpress.Neo3.Models;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace NeoExpress.Neo3.Node
{
    internal interface IExpressNode : IDisposable
    {
        Task<UInt256> ExecuteAsync(ExpressChain chain, ExpressWalletAccount account, Script script, decimal additionalGas = 0);
        Task<InvokeResult> InvokeAsync(Script script);
        Task<(RpcNep5Balance balance, Nep5Contract contract)[]> GetBalancesAsync(UInt160 address);
        Task<(Transaction tx, RpcApplicationLog? appLog)> GetTransactionAsync(UInt256 txHash);
        Task<Block> GetBlockAsync(UInt256 blockHash);
        Task<Block> GetBlockAsync(uint blockIndex);
        Task<Block> GetLatestBlockAsync();
        Task<IReadOnlyList<ExpressStorage>> GetStoragesAsync(UInt160 scriptHash);
        Task<ContractManifest> GetContractAsync(UInt160 scriptHash);
        Task<IReadOnlyList<ContractManifest>> ListContractsAsync();
        Task<IReadOnlyList<Nep5Contract>> ListNep5ContractsAsync();
        Task<IReadOnlyList<(ulong requestId, OracleRequest request)>> ListOracleRequestsAsync();
    }
}
