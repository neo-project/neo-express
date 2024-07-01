// Copyright (C) 2015-2024 The Neo Project.
//
// BatchCommand.BatchFileCommands.cs file belongs to neo-express project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Neo.Network.P2P.Payloads;
using NeoExpress.Models;
using System.ComponentModel.DataAnnotations;

namespace NeoExpress.Commands
{
    using McMaster.Extensions.CommandLineUtils;
    partial class BatchCommand
    {
        [Command]
        [Subcommand(typeof(Checkpoint), typeof(Contract), typeof(FastForward), typeof(Oracle), typeof(Policy), typeof(Transfer), typeof(TransferNFT), typeof(Wallet))]
        internal class BatchFileCommands
        {
            [Command("checkpoint")]
            [Subcommand(typeof(Create))]
            internal class Checkpoint
            {
                [Command("create")]
                internal class Create
                {
                    [Argument(0, "Checkpoint file name")]
                    [Required]
                    internal string Name { get; init; } = string.Empty;

                    [Option(Description = "Overwrite existing data")]
                    internal bool Force { get; }
                }
            }

            [Command("contract")]
            [Subcommand(typeof(Deploy), typeof(Download), typeof(Invoke), typeof(Run))]
            internal class Contract
            {
                [Command("deploy")]
                internal class Deploy
                {
                    [Argument(0, Description = "Path to contract .nef file")]
                    [Required]
                    internal string Contract { get; init; } = string.Empty;

                    [Argument(1, Description = "Account to pay contract deployment GAS fee")]
                    [Required]
                    internal string Account { get; init; } = string.Empty;

                    [Option(Description = "password to use for NEP-2/NEP-6 account")]
                    internal string Password { get; init; } = string.Empty;

                    [Option(Description = "Witness Scope to use for transaction signer (Default: CalledByEntry)")]
                    [AllowedValues(StringComparison.OrdinalIgnoreCase, "None", "CalledByEntry", "Global")]
                    internal WitnessScope WitnessScope { get; init; } = WitnessScope.CalledByEntry;

                    [Option(Description = "Optional data parameter to pass to _deploy operation")]
                    internal string Data { get; init; } = string.Empty;

                    [Option(Description = "Deploy contract regardless of name conflict")]
                    internal bool Force { get; }
                }

                [Command("download")]
                internal class Download
                {
                    [Argument(0, Description = "Contract invocation hash")]
                    [Required]
                    internal string Contract { get; init; } = string.Empty;

                    [Argument(1, Description = "URL of Neo JSON-RPC Node\nSpecify MainNet (default), TestNet or JSON-RPC URL")]
                    [Required]
                    internal string RpcUri { get; } = string.Empty;

                    [Option(Description = "Block height to get contract state for")]
                    [Required]
                    internal uint Height { get; } = 0;

                    [Option(CommandOptionType.SingleOrNoValue,
                        Description = "Replace contract and storage if it already exists\nDefaults to None if option unspecified, All if option value unspecified")]
                    internal (bool hasValue, ContractCommand.OverwriteForce value) Force { get; init; }
                }

                [Command("invoke")]
                internal class Invoke
                {
                    [Argument(0, Description = "Path to contract invocation JSON file")]
                    [Required]
                    internal string InvocationFile { get; init; } = string.Empty;

                    [Argument(1, Description = "Account to pay contract invocation GAS fee")]
                    [Required]
                    internal string Account { get; init; } = string.Empty;

                    [Option(Description = "password to use for NEP-2/NEP-6 account")]
                    internal string Password { get; init; } = string.Empty;

                    [Option(Description = "Witness Scope to use for transaction signer (Default: CalledByEntry)")]
                    [AllowedValues(StringComparison.OrdinalIgnoreCase, "None", "CalledByEntry", "Global")]
                    internal WitnessScope WitnessScope { get; init; } = WitnessScope.CalledByEntry;
                }

                [Command("run")]
                internal class Run
                {
                    [Argument(0, Description = "Contract name or invocation hash")]
                    [Required]
                    internal string Contract { get; init; } = string.Empty;

                    [Argument(1, Description = "Contract method to invoke")]
                    [Required]
                    internal string Method { get; init; } = string.Empty;

                    [Argument(2, Description = "Arguments to pass to contract")]
                    internal string[] Arguments { get; init; } = Array.Empty<string>();

                    [Option(Description = "Account to pay contract invocation GAS fee")]
                    [Required]
                    internal string Account { get; init; } = string.Empty;

                    [Option(Description = "password to use for NEP-2/NEP-6 account")]
                    internal string Password { get; init; } = string.Empty;

                    [Option(Description = "Witness Scope to use for transaction signer (Default: CalledByEntry)")]
                    [AllowedValues(StringComparison.OrdinalIgnoreCase, "None", "CalledByEntry", "Global")]
                    internal WitnessScope WitnessScope { get; init; } = WitnessScope.CalledByEntry;
                }

                [Command("update")]
                internal class Update
                {
                    [Argument(0, Description = "Contract name or invocation hash")]
                    [Required]
                    internal string Contract { get; init; } = string.Empty;

                    [Argument(1, Description = "Path to contract .nef file")]
                    [Required]
                    internal string NefFile { get; init; } = string.Empty;

                    [Argument(2, Description = "Account to pay contract deployment GAS fee")]
                    [Required]
                    internal string Account { get; init; } = string.Empty;

                    [Option(Description = "Witness Scope to use for transaction signer (Default: CalledByEntry)")]
                    [AllowedValues(StringComparison.OrdinalIgnoreCase, "None", "CalledByEntry", "Global")]
                    internal WitnessScope WitnessScope { get; init; } = WitnessScope.CalledByEntry;

                    [Option(Description = "Password to use for NEP-2/NEP-6 account")]
                    internal string Password { get; init; } = string.Empty;
                }
            }

            [Command("fastfwd")]
            internal class FastForward
            {
                [Argument(0, Description = "Number of blocks to mint")]
                [Required]
                internal uint Count { get; init; }

                [Option(Description = "Timestamp delta for last generated block")]
                internal string TimestampDelta { get; init; } = string.Empty;
            }

            [Command("oracle")]
            [Subcommand(typeof(Enable), typeof(Response))]
            internal class Oracle
            {
                [Command("enable")]
                internal class Enable
                {
                    [Argument(0, Description = "Account to pay contract invocation GAS fee")]
                    [Required]
                    internal string Account { get; init; } = string.Empty;

                    [Option(Description = "password to use for NEP-2/NEP-6 account")]
                    internal string Password { get; init; } = string.Empty;
                }

                [Command("response")]
                internal class Response
                {
                    [Argument(0, Description = "URL of oracle request")]
                    [Required]
                    internal string Url { get; init; } = string.Empty;

                    [Argument(1, Description = "Path to JSON file with oracle response content")]
                    [Required]
                    internal string ResponsePath { get; init; } = string.Empty;

                    [Option(Description = "Oracle request ID")]
                    internal ulong? RequestId { get; }
                }
            }

            [Command("policy")]
            [Subcommand(typeof(Block), typeof(Set), typeof(Sync), typeof(Unblock))]
            internal class Policy
            {
                [Command("block")]
                internal class Block
                {
                    [Argument(0, Description = "Account to block")]
                    [Required]
                    internal string ScriptHash { get; init; } = string.Empty;

                    [Argument(1, Description = "Account to pay contract invocation GAS fee")]
                    [Required]
                    internal string Account { get; init; } = string.Empty;

                    [Option(Description = "password to use for NEP-2/NEP-6 sender")]
                    internal string Password { get; init; } = string.Empty;
                }

                [Command("set")]
                internal class Set
                {
                    [Argument(0, Description = "Policy to set")]
                    [Required]
                    internal PolicySettings Policy { get; init; }

                    [Argument(1, Description = "New Policy Value")]
                    [Required]
                    internal decimal Value { get; set; }

                    [Argument(2, Description = "Account to pay contract invocation GAS fee")]
                    [Required]
                    internal string Account { get; init; } = string.Empty;

                    [Option(Description = "password to use for NEP-2/NEP-6 sender")]
                    internal string Password { get; init; } = string.Empty;
                }

                [Command("sync")]
                internal class Sync
                {
                    [Argument(0, Description = "Source of policy values. Must be path to policy settings JSON file")]
                    [Required]
                    internal string Source { get; init; } = string.Empty;

                    [Option(Description = "Account to pay contract invocation GAS fee")]
                    [Required]
                    internal string Account { get; init; } = string.Empty;

                    [Option(Description = "password to use for NEP-2/NEP-6 sender")]
                    internal string Password { get; init; } = string.Empty;
                }

                [Command("unblock")]
                internal class Unblock
                {
                    [Argument(0, Description = "Account to unblock")]
                    [Required]
                    internal string ScriptHash { get; init; } = string.Empty;

                    [Argument(1, Description = "Account to pay contract invocation GAS fee")]
                    [Required]
                    internal string Account { get; init; } = string.Empty;

                    [Option(Description = "password to use for NEP-2/NEP-6 sender")]
                    internal string Password { get; init; } = string.Empty;
                }
            }

            [Command("transfer")]
            internal class Transfer
            {
                [Argument(0, Description = "Amount to transfer")]
                [Required]
                internal string Quantity { get; init; } = string.Empty;

                [Argument(1, Description = "Asset to transfer (symbol or script hash)")]
                [Required]
                internal string Asset { get; init; } = string.Empty;

                [Argument(2, Description = "Account to send asset from")]
                [Required]
                internal string Sender { get; init; } = string.Empty;

                [Argument(3, Description = "Account to send asset to")]
                [Required]
                internal string Receiver { get; init; } = string.Empty;

                [Option(Description = "Optional data parameter to pass to transfer operation")]
                internal string Data { get; init; } = string.Empty;

                [Option(Description = "password to use for NEP-2/NEP-6 sender")]
                internal string Password { get; init; } = string.Empty;
            }
            [Command("transfernft")]
            internal class TransferNFT
            {
                [Argument(0, Description = "NFT Contract (Symbol or Script Hash)")]
                [Required]
                internal string Contract { get; init; } = string.Empty;

                [Argument(1, Description = "TokenId of NFT (Format: HEX, BASE64)")]
                [Required]
                internal string TokenId { get; init; } = string.Empty;

                [Argument(2, Description = "Account to send NFT from (Format: Wallet name, WIF)")]
                [Required]
                internal string Sender { get; init; } = string.Empty;

                [Argument(3, Description = "Account to send NFT to (Format: Script Hash, Address, Wallet name)")]
                [Required]
                internal string Receiver { get; init; } = string.Empty;

                [Option(Description = "Optional data parameter to pass to transfer operation")]
                internal string Data { get; init; } = string.Empty;

                [Option(Description = "password to use for NEP-2/NEP-6 sender")]
                internal string Password { get; init; } = string.Empty;
            }

            [Command("wallet")]
            [Subcommand(typeof(Create))]
            internal class Wallet
            {
                [Command("create")]
                internal class Create
                {
                    [Argument(0, Description = "Wallet name")]
                    [Required]
                    internal string Name { get; init; } = string.Empty;

                    [Option(Description = "Overwrite existing data")]
                    internal bool Force { get; }

                    [Option(Description = "Private key for account (Format: HEX or WIF)\nDefault: Random")]
                    internal string PrivateKey { get; set; } = string.Empty;
                }
            }
        }
    }
}
