using System;
using System.Threading.Tasks;
using Neo.Network.P2P.Payloads;

namespace NeoExpress
{
    interface ITransactionExecutor : IDisposable
    {
        IExpressNode ExpressNode { get; }
        Task ContractDeployAsync(string contract, string account, string password, WitnessScope witnessScope, bool force);
        Task ContractInvokeAsync(string invocationFile, string account, string password, WitnessScope witnessScope);
        Task InvokeForResultsAsync(string invocationFile);
        Task TransferAsync(string quantity, string asset, string sender, string password, string receiver);
        Task OracleEnableAsync(string account, string password);
        Task OracleResponseAsync(string url, string responsePath, ulong? requestId = null);
    }
}
