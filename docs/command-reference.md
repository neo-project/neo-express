<!-- markdownlint-enable -->
# Neo-Express N3 Command Reference

> Note: This is the command reference for Neo-Express 3.6, targeting N3.
>
> You can pass -?|-h|--help to show a list of supported commands or to show
> help information about a specific command.

## Specifying Signing and Non-Signing Accounts

Many of the Neo-Express commands require the user to specify account information. In some cases, this
account is used to sign a transaction that is submitted to the blockchain network. 

### Specifying a Signing Account

An account used for signing must have an accessible private key. Signing accounts can be specified in
multiple ways:

- `genesis` to use the consensus node multi-sig account which holds the genesis NEO and GAS
- Neo-Express wallet nickname (see `wallet create` below). Note, this includes `node1` etc to specify
  the default wallet account associated with each consensus node
- A [WIF encoded](https://developer.bitcoin.org/devguide/wallets.html#wallet-import-format-wif) private key
- A [standard NEP-2 Passphrase-protected private key](https://github.com/neo-project/proposals/blob/master/nep-2.mediawiki).
    - When using a NEP-2 protected private key, the passphrase must be specified using the `--password` option
- The path to a [standard NEP-6 JSON wallet](https://github.com/neo-project/proposals/blob/master/nep-6.mediawiki).
    - When using a NEP-6 wallet, the password must be specified using the `--password` option. 
    - Note, Neo-Express only supports NEP-6 wallets with either a single account or a single default account

NEP-2 private key and NEP-6 JSON wallet are password protected. When using one of these methods, the password
can be specified using the `--password` option. If the password is not specified on the command line, Neo-Express
will prompt the user to enter the password.

> Note, `neoxp batch` command does not support interactive prompting. Using a NEP-2 private key or NEP-6 wallet
> with `neoxp batch` also requires specifying the `--password` option. Needless to say, storing a password in 
> an unencrpted batch file is not secure, and developers should not use wallets associated with production, mainnet
> assets with Neo-Express.

### Specifying a Non-Signing Account

A account used that is not used for signing doesn't need an accessible private key. Non-Signing accounts
can be specified in multiple ways:

- `genesis` to use the consensus node multi-sig account which holds the genesis NEO and GAS
- Neo-Express wallet nickname (see `wallet create` below). Note, this includes `node1` etc to specify
  the default wallet account associated with each consensus node
- A standard Neo N3 address such as `Ne4Ko2JkzjAd8q2sasXsQCLfZ7nu8Gm5vR`
- A [WIF encoded](https://developer.bitcoin.org/devguide/wallets.html#wallet-import-format-wif) private key

## neoxp create

```
Usage: neoxp create [Options] [Output]

Arguments:
[Options]:
  -o|--output <OUTPUT>                    Name of .neo-express file to create
                                          Default location is home directory as:
                                          Linux: $HOME/.neo-express/default.neo-express
                                          Windows: %UserProfile%\.neo-express\default.neo-express
  -c|--count <COUNT>                      Number of consensus nodes to create
                                          Default: 1
                                          Allowed values are: 1, 4, 7.
  -a|--address-version <ADDRESS_VERSION>  Version to use for addresses in this blockchain instance
                                          Default: 53
  -f|--force                              Overwrite existing data
```

The `create` command is used to create a new Neo-Express blockchain network for local development
purposes. In particular, the create command creates one or more consensus node wallets as well as
the multi-signature contracts needed for the management of genesis assets.

Note, the wallets created for a new blockchain instance are not encrypted in the blockchain instance
file. This simplifies the developer workflow by eliminating the need to manage passwords. However,
it also means that all Neo-Express wallets are insecure and should never be used in a production context.

By default, the `create` command creates a single node Neo-Express blockchain network. While a single
node blockchain network can handle most developer scenarios, it is also possible to create a four or
seven node blockchain via the `--count` option.

All of the information about a Neo-Express blockchain network is stored in a single JSON file. By default,
this file is named "default.neo-express", but this can be overridden with the `--output` option.
For all commands Neo-Express commands besides create, you can specify a non-default blockchain network file
via the `--input` option.

## neoxp run

```
Usage: neoxp run [Options]

[Options]:
  -n|--node-index <NODE_INDEX>                Index of node to run (Default: 0)
  -i|--input <INPUT>                          Path to neo-express data file
  -s|--seconds-per-block <SECONDS_PER_BLOCK>  Time between blocks
  -d|--discard                                Discard blockchain changes on shutdown
  -t|--trace                                  Enable contract execution tracing
```

Once created, a Neo-Express blockchain network is started with the `run` command. The consensus
node index to be run must be passed as an argument to the run command. If not specified, the node
index defaults to 0, indicating the first consensus node. So for a single node blockchain network,
the user can simply call `neo-express run`. Note, each node of a multi-node blockchain network must
be run separately.

When the blockchain is run, the user can specify how often a new block is minted. By default, a new
block is minted every 15 seconds. If the user would like to run at a different rate, they can specify
how many seconds per block via the `--seconds-per-block` argument. Additionally, the default seconds
per block value can be modified via [a setting](settings.md#chainsecondsperblock) in the .neo-express
file.

> Note, the user may specify a different seconds per block value each time a blockchain is run, but
> all nodes in multi-node blockchain must use the same value when running.

By default, the blockchain network persists information to disk when a new block is minted. For development
purposes, it is sometimes useful to run the blockchain network without saving new block persisting
new blocks. By using the `--discard` option, new blocks are saved in memory only and are discarded when
the blockchain network is shut down.

## neoxp stop

```
Usage: neoxp stop [Options]

[Options]:
  -n|--node-index <NODE_INDEX>    Index of node to stop (Default: 0)
  -i|--input <INPUT>              Path to neo-express data file
  -a|--all            Stop all nodes
```

When running in a terminal window, neo-express can be shutdown via standard CTRL-C or CTRL-BREAK operations.
Additionally, you can stop a running neo-express network via the `stop` command. Like the `run` command, the
`stop` command takes a node index to stop, defaulting to 0. The `--all` option shuts down all running consensus
nodes in the network.

## neoxp reset

```
Usage: neoxp reset [Options]

[Options]:
  -n|--node-index <NODE_INDEX>  Index of node to reset (Default: 0)
  -i|--input <INPUT>            Path to neo-express data file
  -f|--force                    Overwrite existing data
  -a|--all                      Reset all nodes
```

A Neo-express blockchain network can be reset back to its genesis block via the `reset` command. This
is useful for keeping the Neo-express blockchain network in a known state for test and debug purposes.
Like the `stop` command, the node index defaults to 0 or the `--all` option can be used to reset all
nodes. The `--force` option must be specified in order to discard existing blockchain network state.

### neoxp export

```
Usage: neoxp export [Options]

Arguments:
[Options]:
  -i|--input <INPUT>  Path to neo-express data file
```

The `export` command saves the wallet and settings of each consensus node in a standard format. The exported files can be found under the root directory of neoxp.exe. This allows for standard Neo node implementations such as Neo-CLI to connect to a running Neo-Express blockchain network.


> Note, the standard [NEP-6 wallet format](https://github.com/neo-project/proposals/blob/master/nep-6.mediawiki)
> encrypts wallet information, so the user has to provide a password. However, since this same information
> is still stored unencrypted in the blockchain information file, these accounts still should never
> be used in a production environment.

## neoxp wallet

The `wallet` command has a series of subcommands for the management of standard wallets and accounts
for use in the Neo-Express blockchain network.

> As stated above, Neo-Express wallet accounts are stored unencrypted and should never be used in a
> production context.

### neoxp wallet create

```
Usage: neoxp wallet create [Options] <Name>

Arguments:
[Options]:
  -f|--force          Overwrite existing data
  -i|--input <INPUT>  Path to neo-express data file
<Name>: Wallet name
```

The `wallet create` command creates a new standard wallet with a single account. This command takes
a single argument that specifies a friendly name that can be used to reference the wallet. A friendly
name like "alice" or "bob" is typically easier to remember than a base 58 encoded address like
Ne4Ko2JkzjAd8q2sasXsQCLfZ7nu8Gm5vR.

To overwrite an existing wallet, the `--force` option must be specified.

### neoxp wallet list

```
Usage: neoxp wallet list [Options]

Arguments:
[Options]:
  -i|--input <INPUT>  Path to neo-express data file
```

The `wallet list` command writes out a list of all the wallets - including consensus node wallets - 
along with their account addresses, private and public keys.

### neoxp wallet delete

```
Usage: neoxp wallet delete [Options] <Name>

Arguments:
[Options]:
  -f|--force          Overwrite existing data
  -i|--input <INPUT>  Path to neo-express data file
<Name>: Wallet name
```

The `wallet delete` command removes a wallet and its accounts from the blockchain network file. This
command does not modify the blockchain data, so any assets associated with that wallet are not changed.

To delete a private net wallet, the `--force` option must be specified.

### neoxp wallet export

```
Usage: neoxp wallet export [Options] <Name>

Arguments:
[Options]:
  -i|--input <INPUT>    Path to neo-express data file
  -o|--output <OUTPUT>  NEP-6 wallet name (Defaults to Neo-Express name if unspecified)
  -f|--force            Overwrite existing data
<Name>: Wallet name
```

Similar to the top-level `export` command described above, `wallet export` saves an existing Neo-Express
wallet in the [NEP-6 wallet format](https://github.com/neo-project/proposals/blob/master/nep-6.mediawiki)
that can be read by standard Neo tools.

> Note, the standard [NEP-6 wallet format](https://github.com/neo-project/proposals/blob/master/nep-6.mediawiki)
> encrypts wallet information, so the user has to provide a password. However, since this same information
> is still stored unencrypted in the blockchain information file, these accounts still should never
> be used in a production environment.

## neoxp transfer

```
Usage: neoxp transfer [Options] <Quantity> <Asset> <Sender> <Receiver>

Arguments:
[Options]:
  -p|--password <PASSWORD>  password to use for NEP-2/NEP-6 sender
  -i|--input <INPUT>        Path to neo-express data file
  -t|--trace                Enable contract execution tracing
  -j|--json                 Output as JSON
<Quantity>: Amount to transfer
<Asset>: Asset to transfer (symbol or script hash)
<Sender>: Account to send asset from
<Receiver>: Account to send asset to
```

The `transfer` command is used to transfer assets between accounts in a Neo-Express
blockchain network. The transfer command has four required arguments:

- the quantity to transfer as an integer or `all` to transfer all assets of the specified type 

  > Note, You cannot transfer all GAS tokens in an account as you have to reserve some GAS tokens to cover the transaction fee.

- The asset to transfer. This can be specified as contract hash or
  [NEP-17](https://github.com/neo-project/proposals/blob/master/nep-17.mediawiki)
  token symbol such as `neo` or `gas`
  
- Signing account that is sending the asset

- Non-signing account that is receiving the asset

## neoxp transfernft

```
Usage: neoxp transfernft [options] <Contract> <TokenId> <Sender> <Receiver>

Arguments:
  Contract                  NFT Contract (Symbol or Script Hash)
  TokenId                   TokenId of NFT (Format: HEX, BASE64)
  Sender                    Account to send NFT from (Format: Wallet name, WIF)
  Receiver                  Account to send NFT to (Format: Script Hash, Address, Wallet name)

Options:
  -d|--data <DATA>          Optional data parameter to pass to transfer operation
  -p|--password <PASSWORD>  password to use for NEP-2/NEP-6 sender
  -i|--input <INPUT>        Path to neo-express data file
  -t|--trace                Enable contract execution tracing
  -j|--json                 Output as JSON
```

The `transfernft` command is used to transfer NFT assets between accounts in a Neo-Express
blockchain network. The transfer command has the following required arguments:

- The contract hash or NEP-11 token symbol of the NFT asset to transfer. 
- The TokenID in the HEX or BASE64 format of the NFT asset to transfer.
- Signing account that is sending the NFT asset.
- Non-signing account that is receiving the NFT asset.

## neoxp contract

The `contract` command has a series of subcommands for managing smart contracts
on a Neo-Express blockchain network

### neoxp contract deploy

```
Usage: neoxp contract deploy [Options] <Contract> <Account>

Arguments:
[Options]:
  -w|--witness-scope <WITNESS_SCOPE>  Witness Scope to use for transaction signer
                                      Default: CalledByEntry
                                      Allowed values are: None, CalledByEntry, Global.
  -p|--password <PASSWORD>            password to use for NEP-2/NEP-6 account
  -i|--input <INPUT>                  Path to neo-express data file
  -t|--trace                          Enable contract execution tracing
  -f|--force                          Deploy contract regardless of name conflict
  -j|--json                           Output as JSON
<Contract>: Path to contract .nef file
<Account>: Account to pay contract deployment GAS fee
```

The `contract deploy` command deploys a smart contract to a Neo-Express blockchain. The command takes
a path to an .NEF file generated by a Neo contract compiler like 
[NCCS compiler for .NET](https://github.com/neo-project/neo-devpack-dotnet).
Additionally, the command requires the signing account that will pay the GAS deployment fee.

By default, Neo-Express will not deploy multiple contracts with the same name to avoid developer
confusion. This behavior can be overridden with the `--force` option.

### neoxp contract invoke

```
Usage: neoxp contract invoke [Options] <InvocationFile> <Account>

Arguments:
[Options]:
  -w|--witness-scope <WITNESS_SCOPE>  Witness Scope to use for transaction
                                      signer (Default: CalledByEntry)
                                      Allowed values are: None, CalledByEntry,
                                      Global.
  -r|--results                        Invoke contract for results (does not cost
                                      GAS)
  -g|--gas                            Additional GAS to apply to the contract
                                      invocation
  -p|--password <PASSWORD>            password to use for NEP-2/NEP-6 account
  -t|--trace                          Enable contract execution tracing
  -j|--json                           Output as JSON
  -i|--input <INPUT>                  Path to neo-express data file
<InvocationFile>: Path to contract invocation JSON file
<Account>: Account to pay contract invocation GAS fee
```

The `contract invoke` command generates a script from an [invocation file](Neo Express Invocation File.md) and submits it to the Neo-Express blockchain network as a transaction.

A script can be invoked either for results (specified via the `--results` option) or to make changes
(specified via the signed account argument). If a script is submitted for results, it may read information
stored in the blockchain, but any changes made to blockchain data will not be saved. If a submitted
for changes, a signed account must be specified and any results returned by the script will not be available 
immediately. For scripts submitted for changes, a transaction ID is returned and the execution results can 
be retrieved via the `show transaction` command (described below).

### neoxp contract run

```
Usage: neoxp contract run [Options] <Contract> <Method> <Arguments>

Arguments:
[Options]:
  -a|--account <ACCOUNT>              Account to pay contract invocation GAS fee
  -w|--witness-scope <WITNESS_SCOPE>  Witness Scope to use for transaction
                                      signer (Default: CalledByEntry)
                                      Allowed values are: None, CalledByEntry,
                                      Global.
  -r|--results                        Invoke contract for results (does not cost
                                      GAS)
  -g|--gas                            Additional GAS to apply to the contract
                                      invocation
  -p|--password <PASSWORD>            password to use for NEP-2/NEP-6 account
  -t|--trace                          Enable contract execution tracing
  -j|--json                           Output as JSON
  -i|--input <INPUT>                  Path to neo-express data file
<Contract>: Contract name or invocation hash
<Method>: Contract method to invoke
<Arguments>: Arguments to pass to the invoked method
```

Like `contract invoke`, the `contract run` command generates a script and submits it to the Neo-Express
blockchain network as a transaction wither for results or changes. However, unlike `contract invoke`, 
the `contract run` command generates the script from command line parameters instead of an invocation
file. The command line constraints limit the flexibility of `contract run` relative to `contract invoke`,
but saves the developer from needing to create an invocation file for simple contract invocation scenarios.

Instead of a path to an invocation file, The `contract run` command takes arguments specifying the contract
(either by name or hash) and the method to invoke, plus zero or more contract arguments. These contract
arguments are string encoded values, following similar rules to [string arguments in an invocation file](Neo Express Invocation File.md#args-property).

### neoxp contract get

```
Usage: neoxp contract get [Options] <Contract>

Arguments:
[Options]:
  -i|--input <INPUT>  Path to neo-express data file
<Contract>: Contract name or invocation hash
```

The `contract get` command retrieves the manifest of a deployed contract.

### neoxp contract list

```
Usage: neoxp contract list [Options]

Arguments:
[Options]:
  -i|--input <INPUT>  Path to neo-express data file
  -j|--json           Output as JSON
```

The `contract list` command writes out the name and contract hash of every contract deployed in a
Neo-express blockchain network. This includes native contracts that are part of the core Neo platform.

### neoxp contract hash

```
Usage: neoxp contract hash [Options] <Contract> <Account>

Arguments:
[Options]:
  -i|--input <INPUT>  Path to neo-express data file
<Contract>:Path to contract .nef file
<Account>:Account that would deploy the contract
```

The `contract hash` command calculates what the contract hash would be from a path to an .NEF file
and the non-signing account information of the account that would deploy the contract.

> Note, deploying the contract requires a signing account, but calculating the contract hash
> does not require private key information.

### neoxp contract storage

```
Usage: neoxp contract storage [Options] <Contract>

Arguments:
[Options]:
  -i|--input <INPUT>  Path to neo-express data file
  -j|--json           Output as JSON
<Contract>: Contract name or invocation hash
```
The `contract storage` commands dumps all the key/value pairs stored in the blockchain for
this contract. This command takes a single argument indicating the contract to dump storage
records for. For each key/value pair, the command shows both the key and the value
as both a hex-encoded byte array as well as a UTF-8 encoded string.

### neoxp contract update

Update a contract that has been deployed to a neo-express instance.

```
Usage: neoxp contract update [Options] <Contract> <Contract_File> <Account>

Arguments:
[Options]:
  -w|--witness-scope <WITNESS_SCOPE>  Witness Scope to use for transaction signer
                                      Default: CalledByEntry
                                      Allowed values are: None, CalledByEntry, Global.
  -p|--password <PASSWORD>            password to use for NEP-2/NEP-6 account
  -i|--input <INPUT>                  Path to neo-express data file
  -t|--trace                          Enable contract execution tracing
  -j|--json                           Output as JSON
<Contract>: Contract name or invocation hash
<Contract_File>: Path to contract .nef file
<Account>: Account to pay contract deployment GAS fee
```

> Note, The smart contract will need to have a method with this signature: e.g.
>
> C#: `public static bool Update(ByteString nefFile, string manifest)`
>
> Python: `def update(nef: bytes, manifest: str):`
>

### neoxp contract download

Download contract and its storage from remote chain into the local chain.

```
Usage: neoxp contract download [Options] <Contract> <RpcUri>

Arguments:
  Contract              Target contract hash
  RpcUri                URL of Neo JSON-RPC Node
                        Specify MainNet (default), TestNet or JSON-RPC URL

[Options]:
  -i|--input <INPUT>    Path to neo-express data file
  -h|--height <HEIGHT>  Block height to get contract state for
                        Default value is: 0 (latest).
  -f|--force[:<FORCE>]  Replace contract and storage if it already exists
                        Defaults to None if option unspecified, All if option value unspecified
                        Allowed values are: None, All, ContractOnly, StorageOnly.
  -?|--help             Show help information.
```

### neoxp contract validate

The `neoxp contract validate` checks a given contract for compliance with proposal specification. It has two subcommands.

#### nepxp contract validate nep11

```
Usage: neoxp contract validate nep11 [options] <ContractHash>

Arguments:
  ContractHash        Path to contract .nef file

Options:
  -i|--input <INPUT>  Path to neo-express data file
```

This command checks if the specified contract is NEP-11 compliant.

#### nepxp contract validate nep17

```
Usage: neoxp contract validate nep17 [options] <ContractHash>

Arguments:
  ContractHash        Path to contract .nef file

Options:
  -i|--input <INPUT>  Path to neo-express data file
```

This command checks if the specified contract is NEP-17 compliant.

## neoxp show

The `show` command displays information from the blockchain. There are multiple subcommands 
representing the different  information that is available.

### neoxp show balance

```
Usage: neoxp show balance [options] <Asset> <Account>

Arguments:
  Asset               Asset to show balance of (symbol or script hash)
  Account             Account to show asset balance for

Options:
  -i|--input <INPUT>  Path to neo-express data file
```

The `show balance` displays the balance of a single NEP-17 asset (including NEO and GAS) of a specific account.

### neoxp show balances

```
Usage: neoxp show balances [options] <Account>

Arguments:
  Account             Account to show asset balances for

Options:
  -i|--input <INPUT>  Path to neo-express data file
```

The `show balances` displays the balance of all NEP-17 asset (including NEO and GAS) owned by a specific account.

### neoxp show block

```
Usage: neoxp show block [options] <BlockHash>

Arguments:
  BlockHash           Optional block hash or index. Show most recent block if
                      unspecified

Options:
  -i|--input <INPUT>  Path to neo-express data file
```

The `show block` displays the contents of a single block, specified by index or hash.

### neoxp show nft

```
Usage: neoxp show nft [options] <Contract> <Account>

Arguments:
  Contract            NFT Contract (Symbol or Script Hash)
  Account             Account to show NFT (Format: Script Hash, Address, Wallet name)

Options:
  -i|--input <INPUT>  Path to neo-express data file
```

The `show nft` displays the content of an NFT contract for a specified account. The output consists of TokenId in Base64 and big-endian Hex string formats.

### neoxp show notifications

```
Usage: neoxp show notifications [options]

Options:
  -c|--contract <CONTRACT>      Limit shown notifications to the specified contract
  -n|--count                    Limit number of shown notifications
  -e|--event-name <EVENT_NAME>  Limit shown notifications to specified event name
  -i|--input <INPUT>            Path to neo-express data file
```

The `show notifications` displays contract notifications in JSON format.

### neoxp show transaction

```
Usage: neoxp show transaction [options] <TransactionHash>

Arguments:
  TransactionHash     Transaction hash

Options:
  -i|--input <INPUT>  Path to neo-express data file
```

The `show transaction` displays the contents of a transaction specified by hash and its execution results if available.

`show tx` is an alias for `show transaction`

## neoxp candidate

The `candidate` command has a series of subcommands for managing candidates election in the Neo-Express blockchain.

### neoxp candidate list

```
Usage: neoxp candidate list [options]

Options:
  -t|--trace          Enable contract execution tracing
  -j|--json           Output as JSON
  -i|--input <INPUT>  Path to neo-express data file
```

This command lists candidates, including the candidate public key and the number of votes received.

### neoxp candidate register

```
Usage: neoxp candidate register [options] <Account>

Arguments:
  Account                   Account to register candidate

Options:
  -p|--password <PASSWORD>  Password to use for NEP-2/NEP-6 account
  -t|--trace                Enable contract execution tracing
  -j|--json                 Output as JSON
  -i|--input <INPUT>        Path to neo-express data file
```

This command registers a specified account as candidate.

### neoxp candidate unregister

```
Usage: neoxp candidate unregister [options] <Account>

Arguments:
  Account                   Account to unregister candidate

Options:
  -p|--password <PASSWORD>  Password to use for NEP-2/NEP-6 account
  -t|--trace                Enable contract execution tracing
  -j|--json                 Output as JSON
  -i|--input <INPUT>        Path to neo-express data file
```

This command unregisters a specified candidate account.

### neoxp candidate vote

```
Usage: neoxp candidate vote [options] <Account> <PublicKey>

Arguments:
  Account                   Account to vote
  PublicKey                 Candidate publickey

Options:
  -p|--password <PASSWORD>  Password to use for NEP-2/NEP-6 account
  -t|--trace                Enable contract execution tracing
  -j|--json                 Output as JSON
  -i|--input <INPUT>        Path to neo-express data file
```

This command votes for a specified account with public key.

### neoxp candidate unvote

```
Usage: neoxp candidate unvote [options] <Account>

Arguments:
  Account                   Account to unvote

Options:
  -p|--password <PASSWORD>  Password to use for NEP-2/NEP-6 account
  -t|--trace                Enable contract execution tracing
  -j|--json                 Output as JSON
  -i|--input <INPUT>        Path to neo-express data file
```

This command cancels the voting for a specified account with public key.

## neoxp checkpoint

The `checkpoint` command has a series of subcommands for managing the state of a Neo-Express blockchain.
In particular, allowing a blockchain to be reverted to a previous known state. While this is never
something you would do on a production blockchain, the ability to revert changes to a Neo-Express blockchain
enables a variety of debug and test scenarios.

> Note, all `checkpoint` subcommands require a single-node Neo-Express blockchain.
> Multi-node blockchains cannot be check pointed.

### neoxp checkpoint create

```
Usage: neoxp checkpoint create [Options] <Checkpoint>

Arguments:
[Options]:
  -i|--input <INPUT>    Path to neo-express data file
  -f|--force            Overwrite existing data
<Checkpoint>: Checkpoint file name
```

The `checkpoint create` enables the user to create a checkpoint of a Neo-express blockchain. This command
takes a single argument: the name of the checkpoint. If the user wants to overwrite a checkpoint that has
already been created, they must specify the `--force` option.

### neoxp checkpoint restore

```
Usage: neoxp checkpoint restore [Options] <Checkpoint>

Arguments:
[Options]:
  -i|--input <INPUT>    Path to neo-express data file
  -f|--force            Overwrite existing data
<Checkpoint>: Checkpoint file name
```

The `checkpoint restore` command enables the user to discard the current state of a Neo-Express blockchain
and replace it with the state from the checkpoint. If there is no existing blockchain state, restore
essentially works as an import. If there is existing blockchain state, the user must specify the `--force` option.

> Note, `checkpoint restore` validates that the checkpoint being restored matches the current blockchain. 
> If there is not a match, the restore is canceled without modifying the current blockchain state.

### neoxp checkpoint run

```
Usage: neoxp checkpoint run [Options] <Checkpoint>

Arguments:
[Options]:
  -i|--input <INPUT>                          Path to neo-express data file
  -s|--seconds-per-block <SECONDS_PER_BLOCK>  Time between blocks
  -t|--trace                                  Enable contract execution tracing
<Checkpoint>: Checkpoint file name
```

The `checkpoint run` command enables the user to run a checkpoint, similar to the standard `run` command
described above. However, checkpoint run stores any changes to the checkpoint in memory instead of on
disk. When the blockchain is shut down, any changes to the blockchain that were saved to memory are 
discarded.

Like the standard `run` command, the user can control the speed at which the blockchain mints blocks
via the `--seconds-per-block` argument.

`checkpoint run` is of particular use in test scenarios, where the resulting state of the Neo-Express
blockchain is not important beyond validating that all tests pass.

> Note, once a checkpoint is run, there is no way to save changes made to that running instance.

> Note, like `checkpoint restore`, `checkpoint run` validates that the checkpoint being run matches
> the current blockchain. If there is not a match, the run is canceled.

## neoxp batch

```
Usage: neoxp batch [Options] <BatchFile>

Arguments:
[Options]:
  -r|--reset <CHECKPOINT>    Reset blockchain to genesis or specified checkpoint
                             before running batch file commands
  -t|--trace                 Enable contract execution tracing
  -i|--input <INPUT>         Path to neo-express data file
<BatchFile>: Path to batch file to run
```

The `neo batch` command executes a series of blockchain modifying commands against a single Neo-express
instance. Since the blockchain is only initialized once for the batch, it is usually faster than running
the individual commands separately.

> Note, the Neo-Express blockchain network cannot be running when the `batch` command is run

Each batch command supports the same arguments and options as their normal command as documented
in this file except for `--input` and `--trace`. These arguments are specified on the entire batch
rather than on a command by command basis.

Additionally, the blockchain can be reset back to these genesis block or to a specified checkpoint
via the `--reset` argument. Using the `--reset` argument without specifying a checkpoint is operationally
the same as using the `reset` command. Using the `--reset` argument with a checkpoint is operationally
the same as using the `checkpoint restore` command.

The commands supported in a batch file include:

* `checkpoint create`
* `contract deploy`
* `contract invoke`
* `contract run`
* `fastfwd`
* `oracle enable`
* `oracle response`
* `policy block`
* `policy set`
* `policy sync`
* `policy unblock`
* `transfer`
* `transfernft`

## neoxp execute

```
Usage: neoxp execute [options] <InputText>

Arguments:
  InputText            A neo-vm script (Format: HEX,BASE64,Filename)

Options:
  -a|--account <ACCOUNT>              Account to pay invocation GAS fee
  -w|--witness-scope <WITNESS_SCOPE>  Witness Scope for transaction                                                             signer(s) (Allowed: None,
                                      CalledByEntry, Global)
                                      Allowed values are: None,
                                      CalledByEntry, Global.
                                      Default value is:
                                      CalledByEntry.
  -r|--results                        Invoke contract for results
                                      (does not cost GAS)
  -g|--gas                            Additional GAS to apply to
                                      the contract invocation
                                      Default value is: 0.
  -p|--password <PASSWORD>            password to use for
                                      NEP-2/NEP-6 account
  -t|--trace                          Enable contract execution
                                      tracing
```

This command invokes a custom script, the input text will be converted to script with a priority: hex, base64, file path.

## neoxp fastfwd

```
Usage: neoxp fastfwd [Options] <Count>

Arguments:
[Options]:
  -i|--input <INPUT>  Path to neo-express data file
  -?|-h|--help        Show help information.
<Count>: Number of blocks to mint  
```

The `fastfwd` command generates the specified number of empty blocks. This is useful for testing scenarios
such as voting on a proposal where some amount of time (measured in minted blocks) must pass between
operations.

## neoxp oracle

The `oracle` command has a series of subcommands for configuring Neo-express' oracle subsystem as well
as responding to oracle requests.

> Note, unlike Neo N3 MainNet and TestNet, Neo-Express does not automatically fulfill oracle requests
> by retrieving files from the internet. Instead, oracle requests are manually fulfilled via the 
> `oracle response` command.

### neoxp oracle enable

Enable oracles for neo-express instance

```
Usage: neoxp oracle enable [Options] <Account>

Arguments:
[Options]:
  -p|--password <PASSWORD>  password to use for NEP-2/NEP-6 sender
  -i|--input <INPUT>        Path to neo-express data file
  -t|--trace                Enable contract execution tracing
  -j|--json                 Output as JSON
<Account>: Account to pay contract invocation GAS fee
```

A new Neo N3 blockchain (including a freshly created or reset Neo-Express blockchain) does not have
oracle roles enabled. The `oracle enable` command enables the Neo-Express consensus nodes to also
respond to oracle requests (via the `oracle response` command detailed below)

> Note, enabling oracles on a Neo N3 blockchain can only be performed by the governing committee.
> In a Neo-Express blockchain, this is typically the `genesis` account. 

### neoxp oracle response

```
Usage: neoxp oracle response [Options] <Url> <ResponsePath>

Arguments:
[Options]:
  -r|--request-id <REQUEST_ID>    Oracle request ID
  -i|--input <INPUT>              Path to neo-express data file
  -t|--trace                      Enable contract execution tracing
  -j|--json                       Output as JSON
<Url>: URL of oracle request
<ResponsePath>: Path to JSON file with oracle response content
```

The `oracle response` command enables a developer to submit a response for an existing oracle request.
The command takes two arguments: The url of the file being requested and the path to a local JSON file
containing the oracle response content. 

> Note, it is possible for there to be multiple oracle requests for the same url outstanding at a time.
> In this case, all outstanding oracle requests are fulfilled by a single call to `oracle response`
> unless the `--request-id` option is specified. The request ID can be retrieved via the `oracle requests`
> command described below.

### neoxp oracle requests

```
Usage: neoxp oracle requests [Options]

Arguments:
[Options]:
  -i|--input <INPUT>  Path to neo-express data file
```

The `oracle requests` command lists the request id, url and transaction hash that made the oracle request.

### neoxp oracle list

```
Usage: neoxp oracle list [Options]

Arguments:
[Options]:
  -i|--input <INPUT>  Path to neo-express data file
```

The `oracle list` command lists public key of each oracle node in a Neo-express blockchain network. 
Typically, these are the Neo-express consensus nodes when oracles have been enabled.

## neoxp policy

The `policy` command has a series of subcommands for configuring Neo-express' policy subsystem.

> Note, changing Neo N3 blockchain policy (`set`, `sync`, `block` and `unblock`) can only be performed by
> the governing committee. In a Neo-Express blockchain, this is typically the `genesis` account. 

### neoxp policy get

Retrieve current value of a blockchain policy

```
Usage: neoxp policy get [Options]

Arguments:
[Options]:
  -r|--rpc-uri <RPC_URI>  URL of Neo JSON-RPC Node
                          Specify MainNet (default), TestNet or JSON-RPC URL
  -i|--input <INPUT>      Path to neo-express data file
  -j|--json               Output as JSON
```

> Note, older versions of neoxp supported a `Policy` argument.
> This argument has been removed as of the 3.1 version of neoxp.

The `policy get` command retrieves the current values of Neo blockchain network policy. By default, the
`policy get` command retrieves the policy values of the local Neo-Express blockchain. However, this command
can retrieve the network policy settings from a remote public Neo blockchain network - including MainNet and 
TestNet - by specifying the `--rpc-uri` argument. This can be used to synchronize the local Neo-Express policy
with a well known public Neo network like Neo MainNet. 

The `--json` option specifies the policy values should be emitted as JSON. This JSON content, if saved to a
local file, can be used as the input for the `policy sync` command described below.

### neoxp policy set

```
Usage: neoxp policy set [Options] <Policy> <Value> <Account>

Arguments:
[Options]:
  -p|--password <PASSWORD>  password to use for NEP-2/NEP-6 sender
  -i|--input <INPUT>        Path to neo-express data file
  -t|--trace                Enable contract execution tracing
  -j|--json                 Output as JSON
<Policy>: Policy to set. Allowed values are: GasPerBlock, MinimumDeploymentFee, CandidateRegistrationFee, OracleRequestFee, NetworkFeePerByte, StorageFeeFactor, ExecutionFeeFactor.
<Value>: New Policy Value
<Account>: Account to pay contract invocation GAS fee
```

The `policy set` command updates the current value of the specified Neo-Express network policy.

### neoxp policy sync
```
Synchronize local policy values with public Neo network

Usage: neoxp policy sync [Options] <Source> <Account>

Arguments:
[Options]:
  -p|--password <PASSWORD>  password to use for NEP-2/NEP-6 sender
  -i|--input <INPUT>        Path to neo-express data file
  -t|--trace                Enable contract execution tracing
  -j|--json                 Output as JSON
<Source>: Source of policy values. Must be local policy settings JSON file or the URL of Neo JSON-RPC Node For Node URL,"MainNet" or "TestNet" can be specified in addition to a standard HTTP URL
<Account>: Account to pay contract invocation GAS fee
```

The `policy sync` command updates the all the network policy values of the specified Neo-Express blockchain instance.
The policy values source can be a well known public Neo blockchain network (aka MainNet or TestNet), the URL for a 
JSON-RPC node of another public Neo network or the path to a local JSON file. The JSON file format must match the format
emitted by the `policy get --json` command described above.

> Note: when using `policy sync` in a `batch` command file, the policy settings must be retrieved from a local JSON 
> file. Reading policy settings from a remote Neo network during a `batch` operation is not supported.

### neoxp policy block

```
Usage: neoxp policy block [Options] <ScriptHash> <Account>

Arguments:
[Options]:
  -p|--password <PASSWORD>  password to use for NEP-2/NEP-6 sender
  -i|--input <INPUT>        Path to neo-express data file
  -t|--trace                Enable contract execution tracing
  -j|--json                 Output as JSON
<ScriptHash>: Account to block
<Account>: Account to pay contract invocation GAS fee
```

The `policy block` command blocks the specified non-signing user or contract account. The account
to block can be specified in the following ways:

- Neo-Express wallet nickname (see `wallet create` above). 
  - Note, only Neo-Express wallets created by `wallet create` may be blocked. Consensus nodes and 
    genesis accounts cannot be blocked via `policy block`.
- Contract name
  - Note, only deployed contracts may be blocked. Native contracts cannot be blocked via `policy block`.
- A standard Neo N3 address such as `Ne4Ko2JkzjAd8q2sasXsQCLfZ7nu8Gm5vR`

### neoxp policy unblock

```
Usage: neoxp policy unblock [Options] <ScriptHash> <Account>

Arguments:
[Options]:
  -p|--password <PASSWORD>  password to use for NEP-2/NEP-6 sender
  -i|--input <INPUT>        Path to neo-express data file
  -t|--trace                Enable contract execution tracing
  -j|--json                 Output as JSON
<ScriptHash>: Account to unblock
<Account>: Account to pay contract invocation GAS fee
```

The `policy unblock` command unblocks the specified non-signing user or contract account. The account
to block is specified as described in `policy block` above

### neoxp policy isblocked

```
Usage: neoxp policy isBlocked [Options] <ScriptHash>

Arguments:
[Options]:
  -i|--input <INPUT>  Path to neo-express data file
<ScriptHash>: Account to check block status of
```

The `policy isblocked` command checks the blocked status of the specified non-signing user or contract
account. The account to check is specified as described in `policy block` above
