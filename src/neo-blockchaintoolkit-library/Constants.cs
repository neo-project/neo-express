// Copyright (C) 2023 neo-project
//
// neo-express is free software distributed under the
// MIT software license, see the accompanying file LICENSE in
// the main directory of the project for more details.

using System.Collections.Generic;

namespace Neo.BlockchainToolkit
{
    public static class Constants
    {
        public const string EXPRESS_EXTENSION = ".neo-express";
        public const string DEFAULT_EXPRESS_FILENAME = "default" + EXPRESS_EXTENSION;
        public const string WORKNET_EXTENSION = ".neo-worknet";
        public const string DEFAULT_WORKNET_FILENAME = "default" + WORKNET_EXTENSION;


        public static readonly IReadOnlyList<string> MAINNET_RPC_ENDPOINTS = new[]
        {
            "http://seed1.neo.org:10332",
            "http://seed2.neo.org:10332",
            "http://seed3.neo.org:10332",
            "http://seed4.neo.org:10332",
            "http://seed5.neo.org:10332"
        };

        public static readonly IReadOnlyList<string> TESTNET_RPC_ENDPOINTS = new[]
        {
            "http://seed1t5.neo.org:20332",
            "http://seed2t5.neo.org:20332",
            "http://seed3t5.neo.org:20332",
            "http://seed4t5.neo.org:20332",
            "http://seed5t5.neo.org:20332"
        };
    }
}
