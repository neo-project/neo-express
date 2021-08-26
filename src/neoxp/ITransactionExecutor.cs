using System;
using System.Collections.Generic;
using System.Numerics;
using System.Threading.Tasks;
using Neo.Network.P2P.Payloads;
using Neo.VM;
using NeoExpress.Models;

namespace NeoExpress
{
    interface ITransactionExecutor : IDisposable
    {
        IExpressNode ExpressNode { get; }
        Task ContractDeployAsync(string contract, string account, string password, WitnessScope witnessScope, bool force);
        Task<Script> LoadInvocationScriptAsync(string invocationFile);
        Task<Script> BuildInvocationScriptAsync(string contract, string operation, IReadOnlyList<string>? arguments = null);
        Task ContractInvokeAsync(Script script, string account, string password, WitnessScope witnessScope, decimal additionalGas = 0m);
        Task InvokeForResultsAsync(Script script, string account, WitnessScope witnessScope);
        Task TransferAsync(string quantity, string asset, string sender, string password, string receiver);
        Task OracleEnableAsync(string account, string password);
        Task OracleResponseAsync(string url, string responsePath, ulong? requestId = null);
        Task SetPolicyAsync(PolicyName policy, BigInteger value, string account, string password);
        Task BlockAsync(string scriptHash, string account, string password);
        Task UnblockAsync(string scriptHash, string account, string password);
    }
}
