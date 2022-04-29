using System;
using Neo;

namespace NeoExpress
{
    static class ExpressFileExtensions
    {
        public static (Neo.Wallets.Wallet wallet, UInt160 accountHash) ResolveSigner(this IExpressFile @this, string name, string password)
            => @this.TryResolveSigner(name, password, out var wallet, out var accountHash)
                ? (wallet, accountHash)
                : throw new Exception("ResolveSigner Failed");

    }
}
