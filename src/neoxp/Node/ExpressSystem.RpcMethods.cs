using System;
using System.Linq;
using System.Threading.Tasks;
using Neo;
using Neo.IO.Json;
using Neo.Network.P2P.Payloads;
using Neo.Plugins;
using Neo.SmartContract;
using static Neo.SmartContract.Native.NativeContract;

namespace NeoExpress.Node
{
    partial class ExpressSystem
    {
        [RpcMethod]
        public JObject ExpressShutdown(JArray @params)
        {
            const int SHUTDOWN_TIME = 2;

            var proc = System.Diagnostics.Process.GetCurrentProcess();
            var response = new JObject();
            response["process-id"] = proc.Id;

            Utility.Log(nameof(ExpressSystem), LogLevel.Info, $"ExpressShutdown requested. Shutting down in {SHUTDOWN_TIME} seconds");
            shutdownTokenSource.CancelAfter(TimeSpan.FromSeconds(SHUTDOWN_TIME));
            return response;
        }

        [RpcMethod]
        public JObject ExpressGetPopulatedBlocks(JArray @params)
        {
            using var snapshot = neoSystem.GetSnapshot();
            var height = Ledger.CurrentIndex(snapshot);

            var count = @params.Count >= 1 ? uint.Parse(@params[0].AsString()) : 20;
            count = count > 100 ? 100 : count;

            var start = @params.Count >= 2 ? uint.Parse(@params[1].AsString()) : height;
            start = start > height ? height : start;

            var populatedBlocks = new JArray();
            var index = start;
            while (true)
            {
                var hash = Ledger.GetBlockHash(snapshot, index)
                    ?? throw new Exception($"GetBlockHash for {index} returned null");
                var block = Ledger.GetTrimmedBlock(snapshot, hash)
                    ?? throw new Exception($"GetTrimmedBlock for {index} returned null");

                System.Diagnostics.Debug.Assert(block.Index == index);

                if (index == 0 || block.Hashes.Length > 0)
                {
                    populatedBlocks.Add(index);
                }

                if (index == 0 || populatedBlocks.Count >= count) break;
                index--;
            }

            var response = new JObject();
            response["cacheId"] = cacheId;
            response["blocks"] = populatedBlocks;
            return response;
        }

        [RpcMethod]
        public JObject? ExpressCreateCheckpoint(JArray @params)
        {
            string path = @params[0].AsString();
            CreateCheckpoint(path);
            return path;
        }

        [RpcMethod]
        public JObject? ExpressListContracts(JArray @params)
        {
            var contracts = ListContracts();

            var json = new JArray();
            foreach (var contract in contracts)
            {
                json.Add(new JObject()
                {
                    ["hash"] = contract.hash.ToString(),
                    ["manifest"] = contract.manifest.ToJson()
                });
            }
            return json;
        }

        [RpcMethod]
        public JObject ExpressListTokenContracts(JArray _)
        {

            var json = new JArray();
            foreach (var contract in ListTokenContracts())
            {
                json.Add(contract.ToJson());
            }
            return json;
        }

        [RpcMethod]
        public JObject? ExpressListOracleRequests(JArray _)
        {
            var json = new JArray();
            foreach (var (requestId, request) in ListOracleRequests())
            {
                json.Add(new JObject()
                {
                    ["requestid"] = requestId,
                    ["originaltxid"] = $"{request.OriginalTxid}",
                    ["gasforresponse"] = request.GasForResponse,
                    ["url"] = request.Url,
                    ["filter"] = request.Filter,
                    ["callbackcontract"] = $"{request.CallbackContract}",
                    ["callbackmethod"] = request.CallbackMethod,
                    ["userdata"] = Convert.ToBase64String(request.UserData),
                });
            }
            return json;
        }

        [RpcMethod]
        public JObject? ExpressGetContractStorage(JArray @params)
        {
            var scriptHash = UInt160.Parse(@params[0].AsString());
            var json = new JArray();
            foreach (var (key, value) in ListStorages(scriptHash))
            {
                json.Add(new JObject()
                {
                    ["key"] = Convert.ToBase64String(key.Span),
                    ["value"] = Convert.ToBase64String(value.Span),
                });
            }
            return json;
        }

        [RpcMethod]
        public async Task<JObject?> ExpressFastForwardAsync(JArray @params)
        {
            var blockCount = (uint)@params[0].AsNumber();
            var timestampDelta = TimeSpan.Parse(@params[1].AsString());
            await FastForwardAsync(blockCount, timestampDelta).ConfigureAwait(false);
            return true;
        }

        [RpcMethod]
        public async Task<JObject?> ExpressSubmitOracleResponseAsync(JArray @params)
        {
            var response = JsonToOracleResponse(@params[0]);
            var txHash = await SubmitOracleResponseAsync(response).ConfigureAwait(false);
            return $"{txHash}";

            static OracleResponse JsonToOracleResponse(JObject json)
            {
                var id = (ulong)json["id"].AsNumber();
                var code = (OracleResponseCode)json["code"].AsNumber();
                var result = Convert.FromBase64String(json["result"].AsString());
                return new OracleResponse()
                {
                    Id = id,
                    Code = code,
                    Result = result
                };
            }
        }

        [RpcMethod]
        public JObject ExpressPersistContract(JObject @params)
        {
            var state = Neo.Network.RPC.RpcClient.ContractStateFromJson(@params[0]["state"]);
            var storagePairs = ((JArray)@params[0]["storage"])
                .Select(s => (
                    s["key"].AsString(),
                    s["value"].AsString())
                ).ToArray();
            var force = Enum.Parse<Commands.ContractCommand.OverwriteForce>(@params[0]["force"].AsString());

            return PersistContract(state, storagePairs, force);
        }

        // TODO NEXT: 
        //  * Also need to make sure all the standard RPC methods that express implements such as GetApplicationLog
        //    and GetNep17Balances are implemented in in ExpressSystem.RpcMethods

    }
}