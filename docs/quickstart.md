<!-- markdownlint-enable -->

# NeoExpress Quickstart

This article is divided into the following sections: 

[Setting up a private chain using NeoExpress](#setting-up-a-private-chain-using-neoexpress)

[Writing and compiling smart contracts with NeoDevpackDotnet](#writing-and-compiling-smart-contracts-with-neodevpackdotnet)

[Deploying and invoking smart contracts using NeoExpress](#deploying-and-invoking-smart-contracts-using-NeoExpress)

The following steps are applicable to multiple system platforms, such as Windows, macOS, and Ubuntu.

## Setting up a private chain using NeoExpress

### Install NeoExpress via Release Package

1. Download the latest release package from [neo-express releases](https://github.com/neo-project/neo-express/releases) for your operating system.
2. Unzip the package on your local machine.
3. Run the `neoxp.exe` command in the terminal from the directory where you unzipped the package

### Usage Guide

- Create a new local Neo network:

  ```shell
  .\neoxp create
  ```

  Use this command to create a single node private chain (local blockchain network) creating both genesis wallet and node1 wallet. 

- List all wallets:

  ```shell
  .\neoxp wallet list
  ```

  The `wallet list` command writes out a list of all the wallets - including consensus node wallets - 
  along with their account addresses, private and public keys.

- Show genesis account balance:

  `genesis` to use the consensus node multi-sig account which holds the genesis NEO and GAS.

  ```shell
  .\neoxp show balances genesis
  ```

- Send 1 gas from genesis account to node1 account:

  ```shell
  .\neoxp transfer 1 gas genesis node1
  ```

Please review the [Command Reference](command-reference.md) to get an understanding of Neo-Express capabilities.

## Writing and compiling smart contracts with NeoDevpackDotnet

We have completed setting up the private chain and configuring the node. In this section we will walk you through configuring the environment, writing, and compiling an NEP17 contract using C#.

## Installing tools

Download and install [Visual Studio Code](https://code.visualstudio.com/Download)

1. Download and install [.NET 8.0 SDK](https://dotnet.microsoft.com/download)

2. Run the command line and enter the following command to check if you have installed SDK successfully.

   ```shell
   dotnet --list-sdks
   ```

   If there is no issue the SDK version number is displayed.

## Installing contract template

[Neo.SmartContract.Template](https://www.nuget.org/packages/Neo.SmartContract.Template) is a project template used when developing Neo smart contracts. After installing the template, you can create a Neo smart contract project using either the Terminal or Visual Studio.

Install the template

```shell
dotnet new install Neo.SmartContract.Template
```

List all dotnet templates

```shell
dotnet new list
```

These default templates are available after installing [Neo.SmartContract.Template](https://www.nuget.org/packages/Neo.SmartContract.Template):

- neocontractowner - Standard contract template with the Owner, including the GetOwner and SetOwner methods.
- neocontractoracle - A contract template using OracleRequest.
- neocontractnep17 - NEP-17 contract template, including the Mint and Burn methods.

More Neo.SmartContract.Template information can be found [here](https://developers.neo.org/docs/n3/develop/write/dotnet#neosmartcontracttemplate).

### Create a project using templates with Terminal

```shell
dotnet new neocontractnep17 
```

The project name defaults to the name of the current directory. You can also specify the project name with `-n, --name <name>`, e.g. `dotnet new neocontractnep17 -n MyFirstContract`.

## Neo.Compiler.CSharp

[Neo.Compiler.CSharp](https://www.nuget.org/packages/Neo.Compiler.CSharp) (nccs) is the Neo smart contract compiler that compiles the C# language into NeoVM executable OpCodes.

### Install the compiler

```undefined
dotnet tool install --global Neo.Compiler.CSharp
```

### Compile the contract file with Terminal

In the Terminal interface, go to the project path and run the following command to build your contract：

```shell
dotnet build
```

or

```shell
nccs
```

Related contract files are outputted under `bin\sc` path in the contract project directory.

More Neo.Compiler.CSharp information can be found [here](https://developers.neo.org/docs/n3/develop/write/dotnet#neocompilercsharp).

## Deploying and invoking smart contracts using NeoExpress

Copy the smart contract file, include `*.nef` and `*.manifest.json` to the neoxp directory.

### Deploy

Run the following command. Note: please replace `hello.nef` with the name of your contract file.

```shell
> .\neoxp contract deploy hello.nef genesis
Deployment of hello (0x4e97b0370712bf9f5f0bbb7beb5e4127fac55040) Transaction 0x5933870616f13ceb41462fbae1d460edf998defda9d5c3f074ad785465130cf7 submitted
```

### Invoke

To invoke a smart contract, use the `neoxp` run command, see [here](command-reference.md#neoxp-contract-run).

The --results option indicates a trial run, which queries the results of the execution without sending a contract.

```
> .\neoxp contract run 0x4e97b0370712bf9f5f0bbb7beb5e4127fac55040 symbol --results
VM State:     HALT
Gas Consumed: 1364220
Result Stack:
  4558414d504c45(EXAMPLE)
```

"EXAMPLE" is the symbol of our test contract. 

4558414d504c45 is hexadecimal little-endian string of "EXAMPLE".

If we want to call the contract and send the transaction, we can execute it:

```shell
> .\neoxp contract run 0x4e97b0370712bf9f5f0bbb7beb5e4127fac55040 symbol --account genesis
Invocation Transaction 0x1bf5b40cf217c278c331e915f6fc0e0164c7ae84113375947a529e3f2ae8411b submitted
```

