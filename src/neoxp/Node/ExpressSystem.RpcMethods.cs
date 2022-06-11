using System;
using System.Linq;
using Neo;
using Neo.IO.Json;
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

        // [RpcMethod]
        // public JObject? ExpressGetContractState(JArray @params)
        // {
        //     using var snapshot = neoSystem.GetSnapshot();

        //     if (@params[0] is JNumber number)
        //     {
        //         var id = (int)number.AsNumber();
        //         foreach (var nativeContract in Contracts)
        //         {
        //             if (id == nativeContract.Id)
        //             {
        //                 var contract = ContractManagement.GetContract(snapshot, nativeContract.Hash);
        //                 return contract?.ToJson() ?? throw new RpcException(-100, $"Failed to retrieve contract state for {nativeContract.Name}");
        //             }
        //         }
        //     }

        //     var param = @params[0].AsString();

        //     if (UInt160.TryParse(param, out var scriptHash))
        //     {
        //         var contract = ContractManagement.GetContract(snapshot, scriptHash);
        //         return contract?.ToJson() ?? throw new RpcException(-100, "Unknown contract");
        //     }

        //     var contracts = new JArray();
        //     foreach (var contract in ContractManagement.ListContracts(snapshot))
        //     {
        //         if (param.Equals(contract.Manifest.Name, StringComparison.OrdinalIgnoreCase))
        //         {
        //             contracts.Add(contract.ToJson());
        //         }
        //     }
        //     return contracts;
        // }



    }
}