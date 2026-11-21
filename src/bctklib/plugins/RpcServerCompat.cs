// Copyright (C) 2015-2026 The Neo Project.
//
// RpcServerCompat.cs file belongs to neo-express project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or https://opensource.org/license/MIT for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Neo.Extensions;
using Neo.Extensions.Factories;
using Neo.Json;
using Neo.Network.P2P.Payloads;
using Neo.Persistence;
using Neo.Plugins.RpcServer;
using Neo.Plugins.RpcServer.Model;
using Neo.SmartContract;
using Neo.SmartContract.Iterators;
using Neo.SmartContract.Manifest;
using Neo.SmartContract.Native;
using Neo.VM;
using System.Reflection;
using InteropInterface = Neo.VM.Types.InteropInterface;
using StackItem = Neo.VM.Types.StackItem;

namespace Neo.BlockchainToolkit.Plugins
{
    /// <summary>
    /// Server-side replacement for the Neo.Plugins.RpcServer 3.10.2 handlers that cannot
    /// execute against Neo 3.11.2: the package constructs Transactions with the
    /// ReadOnlyMemory&lt;byte&gt;-typed Script property that Neo 3.11.2 replaced with
    /// Neo.VM.Script, so every invokescript, invokefunction and invokecontractverify request
    /// fails with MissingMethodException before the requested script ever runs.
    /// RpcServer.RegisterMethods overwrites same-named methods, so RPC plugins register compat
    /// handlers with the original [RpcMethod] names to substitute these implementations for
    /// the package ones.
    ///
    /// GetInvokeResult, GetVerificationResult, TraverseIterator and TerminateSession replicate
    /// the corresponding RpcServer handlers, with two documented deviations: the diagnostic
    /// payload (useDiagnostic) is not supported because the package's Diagnostic implementation
    /// is internal, and the wallet invocation processing is not supported because the package's
    /// wallet state is internal. Both deviations are stricter than the package behavior they
    /// replace, which is a hard MissingMethodException.
    ///
    /// Stack items are serialized with the max sizes the package's compiled handlers pass to
    /// Neo.Extensions.StackItemExtensions.ToJson (int.MaxValue for invokescript, invokefunction
    /// and traverseiterator results, RpcServersSettings.MaxStackSize for invokecontractverify),
    /// because Neo 3.11.2 lowered that extension's default parameter from int.MaxValue to
    /// 262140.
    ///
    /// Remove this class once a Neo 3.11.x compatible Neo.Plugins.RpcServer package is published.
    /// </summary>
    public sealed class RpcServerCompat : IDisposable
    {
        readonly NeoSystem system;
        readonly RpcServersSettings settings;
        readonly Dictionary<Guid, CompatSession> sessions = new();
        readonly Timer? timer;

        public RpcServerCompat(NeoSystem system, RpcServersSettings settings)
        {
            this.system = system;
            this.settings = settings;
            if (settings.SessionEnabled)
                timer = new Timer(OnTimer, null, settings.SessionExpirationTime, settings.SessionExpirationTime);
        }

        public void Dispose()
        {
            timer?.Dispose();
            CompatSession[] toBeDestroyed;
            lock (sessions)
            {
                toBeDestroyed = sessions.Values.ToArray();
                sessions.Clear();
            }
            foreach (var session in toBeDestroyed)
                session.Dispose();
        }

        /// <summary>
        /// Runs <paramref name="script"/> in an ApplicationEngine and produces the
        /// invokescript/invokefunction JSON response. Replicates RpcServer.GetInvokeResult.
        /// </summary>
        public JObject GetInvokeResult(byte[] script, Signer[]? signers, Witness[]? witnesses, bool useDiagnostic)
        {
            if (useDiagnostic)
                throw new RpcException(RpcError.InternalServerError.WithData(
                    "diagnostics are not supported by this server"));

            JObject json = new();
            CompatSession session = new(system, script, signers, witnesses, settings.MaxGasInvoke);
            try
            {
                json["script"] = Convert.ToBase64String(script);
                json["state"] = $"{session.Engine.State}";
                // Gas consumed in the unit of datoshi, 1 GAS = 10^8 datoshi
                json["gasconsumed"] = session.Engine.FeeConsumed.ToString();
                json["exception"] = GetExceptionMessage(session.Engine.FaultException);
                json["notifications"] = new JArray(session.Engine.Notifications.Select(n =>
                {
                    return new JObject()
                    {
                        ["eventname"] = n.EventName,
                        ["contract"] = n.ScriptHash.ToString(),
                        ["state"] = ToJson(n.State, session),
                    };
                }));
                var stack = new JArray();
                foreach (var item in session.Engine.ResultStack)
                {
                    try
                    {
                        stack.Add(ToJson(item, session));
                    }
                    catch (Exception ex)
                    {
                        stack.Add("error: " + ex.Message);
                    }
                }
                json["stack"] = stack;
            }
            catch
            {
                session.Dispose();
                throw;
            }
            if (session.Iterators.Count == 0 || !settings.SessionEnabled)
            {
                session.Dispose();
            }
            else
            {
                Guid id = Guid.NewGuid();
                json["session"] = id.ToString();
                lock (sessions)
                {
                    sessions.Add(id, session);
                }
            }
            return json;
        }

        /// <summary>
        /// Runs the verify method of the contract identified by <paramref name="scriptHash"/> in
        /// a verification ApplicationEngine and produces the invokecontractverify JSON response.
        /// Replicates RpcServer.GetVerificationResult.
        /// </summary>
        public JObject GetVerificationResult(UInt160 scriptHash, ContractParameter[] args, Signer[]? signers = null, Witness[]? witnesses = null)
        {
            var snapshot = system.GetSnapshotCache();
            try
            {
                var contract = NativeContract.ContractManagement.GetContract(snapshot, scriptHash)
                    .NotNull_Or(RpcError.UnknownContract);
                var method = contract.Manifest.Abi.GetMethod(ContractBasicMethod.Verify, args.Length)
                    .NotNull_Or(RpcErrorFactory.InvalidContractVerification(contract.Hash, args.Length));
                ((int)method.ReturnType == (int)ContractParameterType.Boolean).True_Or(
                    RpcErrorFactory.InvalidContractVerification("The verify method doesn't return boolean value."));

                // The pinned package builds this container with a ReadOnlyMemory<byte> Script
                // holding a single RET opcode; Neo 3.11.2 requires the Neo.VM.Script type.
                var tx = new Transaction
                {
                    Signers = signers ?? [new Signer { Account = scriptHash }],
                    Attributes = [],
                    Witnesses = witnesses ?? [],
                    Script = new Script(new byte[] { 0x40 /* RET */ }),
                };

                using ApplicationEngine engine = ApplicationEngine.Create(
                    TriggerType.Verification, tx, snapshot.CloneCache(), null, system.Settings, settings.MaxGasInvoke);
                engine.LoadContract(contract, method, CallFlags.ReadOnly);
                byte[] script = [];
                if (args.Length > 0)
                {
                    using var opScript = new ScriptBuilder();
                    for (int i = args.Length - 1; i >= 0; i--)
                        opScript.EmitPush(args[i]);
                    script = opScript.ToArray();
                    if (tx.Witnesses is null)
                        tx.Witnesses = [new Witness { InvocationScript = script }];
                    engine.LoadScript(new Script(script), -1, 0, p => p.CallFlags = CallFlags.None);
                }

                JObject json = new()
                {
                    ["script"] = Convert.ToBase64String(script),
                    ["state"] = $"{engine.Execute()}",
                    // Gas consumed in the unit of datoshi, 1 GAS = 10^8 datoshi
                    ["gasconsumed"] = engine.FeeConsumed.ToString(),
                    ["exception"] = GetExceptionMessage(engine.FaultException),
                };
                try
                {
                    json["stack"] = new JArray(engine.ResultStack.Select(p => p.ToJson(settings.MaxStackSize)));
                }
                catch (Exception ex)
                {
                    json["exception"] = ex.Message;
                }
                return json;
            }
            finally
            {
                snapshot.Dispose();
            }
        }

        /// <summary>
        /// Returns the next <paramref name="count"/> items of a previously returned iterator.
        /// Replicates RpcServer.TraverseIterator.
        /// </summary>
        public JToken TraverseIterator(Guid sessionId, Guid iteratorId, int count)
        {
            settings.SessionEnabled.True_Or(RpcError.SessionsDisabled);

            Result.True_Or(() => count <= settings.MaxIteratorResultItems,
                RpcError.InvalidParams.WithData($"Invalid iterator items count {nameof(count)}"));

            CompatSession session;
            lock (sessions)
            {
                session = Result.Ok_Or(() => sessions[sessionId], RpcError.UnknownSession);
                session.ResetExpiration();
            }

            var iterator = Result.Ok_Or(() => session.Iterators[iteratorId], RpcError.UnknownIterator);
            var json = new JArray();
            while (count-- > 0 && iterator.Next())
                // int.MaxValue restated explicitly (see class remarks): the package's compiled
                // handler materializes the 3.10.x ToJson default, which 3.11.2 lowered to 262140.
                json.Add(iterator.Value(out _).ToJson(int.MaxValue));
            return json;
        }

        /// <summary>
        /// Disposes a session created by GetInvokeResult. Replicates RpcServer.TerminateSession.
        /// </summary>
        public JToken TerminateSession(Guid sessionId)
        {
            settings.SessionEnabled.True_Or(RpcError.SessionsDisabled);

            CompatSession? session = null;
            bool result;
            lock (sessions)
            {
                result = Result.Ok_Or(() => sessions.Remove(sessionId, out session), RpcError.UnknownSession);
            }
            if (result)
                session?.Dispose();
            return result;
        }

        void OnTimer(object? state)
        {
            List<(Guid Id, CompatSession Session)> toBeDestroyed = new();
            lock (sessions)
            {
                foreach (var (id, session) in sessions)
                    if (DateTime.UtcNow >= session.StartTime + settings.SessionExpirationTime)
                        toBeDestroyed.Add((id, session));
                foreach (var (id, _) in toBeDestroyed)
                    sessions.Remove(id);
            }
            foreach (var (_, session) in toBeDestroyed)
                session.Dispose();
        }

        static JObject ToJson(StackItem item, CompatSession session)
        {
            // int.MaxValue restated explicitly (see class remarks): the package's compiled
            // ToJson(StackItem, Session) passes the 3.10.x ToJson default, which 3.11.2 lowered
            // to 262140.
            var json = item.ToJson(int.MaxValue);
            if (item is InteropInterface interopInterface && interopInterface.GetInterface<object>() is IIterator iterator)
            {
                Guid id = Guid.NewGuid();
                session.Iterators.Add(id, iterator);
                json["interface"] = nameof(IIterator);
                json["id"] = id.ToString();
            }
            return json;
        }

        static string? GetExceptionMessage(Exception? exception)
        {
            if (exception == null)
                return null;

            // ApplicationEngine.FaultException wraps the VM failure; the client expects the
            // base exception message (replicates RpcServer.GetExceptionMessage, which first
            // unwraps TargetInvocationException)
            if (exception is TargetInvocationException targetException
                && targetException.InnerException is not null)
                exception = targetException.InnerException;
            return exception.GetBaseException().Message;
        }

        /// <summary>
        /// Compat replacement for the package's (internal) Session class: the engine,
        /// its snapshot and any iterators returned by the executed script.
        /// The Transaction container is constructed with the Neo 3.11.2 Script type.
        /// </summary>
        sealed class CompatSession : IDisposable
        {
            public readonly StoreCache Snapshot;
            public readonly ApplicationEngine Engine;
            public readonly Dictionary<Guid, IIterator> Iterators = new();
            public DateTime StartTime;

            public CompatSession(NeoSystem system, byte[] script, Signer[]? signers, Witness[]? witnesses, long datoshi)
            {
                Snapshot = system.GetSnapshotCache();
                if (signers == null)
                    signers = new[] { new Signer { Account = UInt160.Zero, Scopes = WitnessScope.None } };
                var tx = new Transaction
                {
                    Version = 0,
                    Nonce = RandomNumberFactory.NextUInt32(),
                    ValidUntilBlock = NativeContract.Ledger.CurrentIndex(Snapshot)
                        + system.GetMaxValidUntilBlockIncrement(),
                    Signers = signers,
                    Attributes = [],
                    Script = new Script(script),
                    Witnesses = witnesses ?? []
                };
                Engine = ApplicationEngine.Run(script, Snapshot, container: tx, settings: system.Settings, gas: datoshi);
                ResetExpiration();
            }

            public void ResetExpiration()
            {
                StartTime = DateTime.UtcNow;
            }

            public void Dispose()
            {
                Engine.Dispose();
                Snapshot.Dispose();
            }
        }
    }
}
