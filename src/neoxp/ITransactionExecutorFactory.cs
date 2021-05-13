namespace NeoExpress
{
    interface ITransactionExecutorFactory
    {
        ITransactionExecutor Create(IExpressChainManager chainManager, bool trace, bool json);
    }
}
