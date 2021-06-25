using System;
using System.Threading.Tasks;
using Neo.Network.P2P.Payloads;
using Neo.VM;

namespace NeoExpress
{
    interface ITransactionExecutor : IDisposable
    {
        IExpressNode ExpressNode { get; }
        Task ContractDeployAsync(string contract, string account, string password, WitnessScope witnessScope, bool force);
        Task<Script> LoadInvocationScriptAsync(string invocationFile);
        Task ContractInvokeAsync(Script script, string account, string password, WitnessScope witnessScope);
        Task InvokeForResultsAsync(Script script, string account, WitnessScope witnessScope);
        Task TransferAsync(string quantity, string asset, string sender, string password, string receiver);
        Task OracleEnableAsync(string account, string password);
        Task OracleResponseAsync(string url, string responsePath, ulong? requestId = null);
    }
}
