<!-- markdownlint-enable -->
# Neo-Express N3 Command Reference

> Note, this is the command reference for Neo-Express 3.0, targeting N3.
> The [Command Reference](docs/legacy-command-reference.md) for the Neo Legacy 
> compatible version of Neo-Express is also available.

> Note, you can pass -?|-h|--help to show a list of supported commands or to show
> help information about a specific command.

## Specifying Signing and Non-Signing Accounts

Many of the Neo-Express commands require the user to specify account information. In some cases, this
account is used to sign a transaction that is submitted to the blockchain network. 

### Specifying a Signing Account

A account used for signing must have an accessable private key. Signing accounts can be specified in
multiple ways:

- `genesis` to use the consensus node multi-sig account which holds the genesis NEO and GAS
- Neo-Express wallet nickname (see `wallet create` below). Note, this includes `node1` etc to specify
  the default wallet account associated with each consensus node
- A [standard NEP-2 Passphrase-protected private key](https://github.com/neo-project/proposals/blob/master/nep-2.mediawiki).
    - When using a NEP-2 protected private key, the passphrase must be specified using the `--password` option
- The path to a [standard NEP-6 JSON wallet](https://github.com/neo-project/proposals/blob/master/nep-6.mediawiki).
    - When using a NEP-6 wallet, the password must be specified using the `--password` option. 
    - Note, Neo-Express only supports NEP-6 wallets with either a single account or a single default account

### Specifying a Non-Signing Account

A account used that is not used for signing doesn't need an accessable private key. Non-Signing accounts
can be specified in multiple ways:

- `genesis` to use the consensus node multi-sig account which holds the genesis NEO and GAS
- Neo-Express wallet nickname (see `wallet create` below). Note, this includes `node1` etc to specify
  the default wallet account associated with each consensus node
- A standard Neo N3 address such as `Ne4Ko2JkzjAd8q2sasXsQCLfZ7nu8Gm5vR`

## neoxp create

```
Usage: neoxp create [options] <Output>

Arguments:
  Output                                  name of .neo-express file to create (Default: ./default.neo-express)

Options:
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
Usage: neoxp run [options] <NodeIndex>

Arguments:
  NodeIndex                                   Index of node to run

Options:
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
per block value can be modified via [a setting](docs/settings.md#chainsecondsperblock) in the .neo-express
file.

> Note, the user may specify a different seconds per block value each time a blockchain is run, but
> all nodes in multi-node blockchain must use the same value when running.

By default, the blockchain network persists information to disk when a new block is minted. For development
purposes, it is sometimes useful to run the blockchain network without saving new block persisting
new blocks. By using the `--discard` option, new blocks are saved in memory only and are discarded when
the blockchain network is shut down.

## neoxp stop

```
Usage: neoxp stop [options] <NodeIndex>

Arguments:
  NodeIndex           Index of node to stop

Options:
  -i|--input <INPUT>  Path to neo-express data file
  -a|--all            Stop all nodes
```

When running in a terminal window, neo-express can be shutdown via standard CTRL-C or CTRL-BREAK operations.
Additonally, you can stop a running neo-express network via the `stop` command. Like the `run` command, the
`stop` command takes a node index to stop, defaulting to 0. The `--all` option shuts down all running consensus
nodes in the network.

## neoxp reset

```
Usage: neoxp reset [options] <NodeIndex>

Arguments:
  NodeIndex           Index of node to reset

Options:
  -i|--input <INPUT>  Path to neo-express data file
  -f|--force          Overwrite existing data
  -a|--all            Reset all nodes
```

A Neo-express blockchain network can be reset back to its genesis block via the `reset` command. This
is useful for keeping the Neo-express blockchain network in a known state for test and debug purposes.
Like the `stop` command, the node index defaults to 0 or the `--all` option can be used to reset all
nodes. The `--force` option must be specified in order to discard existing blockchain network state.

### neoxp export

```
Usage: neoxp export [options]

Options:
  -i|--input <INPUT>  Path to neo-express data file
```

The `export` command saves the wallet and settings of each consensus node in a standard format. This
allows for standard Neo node implementations such as Neo-CLI to connect to a running Neo-Express
blockchain network.

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
Usage: neoxp wallet create [options] <Name>

Arguments:
  Name                Wallet name

Options:
  -f|--force          Overwrite existing data
  -i|--input <INPUT>  Path to neo-express data file
```

The `wallet create` command creates a new standard wallet with a single account. This command takes
a single argument that specifies a friendly name that can be used to reference the wallet. A friendly
name like "alice" or "bob" is typically easier to remember than a base 58 encoded address like
Ne4Ko2JkzjAd8q2sasXsQCLfZ7nu8Gm5vR.

To overwrite an existing wallet, the `--force` option must be specified.

### neoxp wallet list

```
Usage: neoxp wallet list [options]

Options:
  -i|--input <INPUT>  Path to neo-express data file
```

The `wallet list` command writes out a list of all the wallets - including consensus node wallets - 
along with their account addresses, private and public keys.

### neoxp wallet delete

```
Usage: neoxp wallet delete [options] <Name>

Arguments:
  Name                Wallet name

Options:
  -f|--force          Overwrite existing data
  -i|--input <INPUT>  Path to neo-express data file
```

The `wallet delete` command removes a wallet and its accounts from the blockchain network file. This
command does not modify the blockchain data, so any assets associated with that wallet are not changed.

### neoxp wallet export

```
Usage: neoxp wallet export [options] <Name>

Arguments:
  Name                  Wallet name

Options:
  -i|--input <INPUT>    Path to neo-express data file
  -o|--output <OUTPUT>  NEP-6 wallet name (Defaults to Neo-Express name if unspecified)
  -f|--force            Overwrite existing data
```

Similar the top-level `export` command described above, `wallet export` saves an existing Neo-Express
wallet in the [NEP-6 wallet format](https://github.com/neo-project/proposals/blob/master/nep-6.mediawiki)
that can be read by standard Neo tools.

> Note, the standard [NEP-6 wallet format](https://github.com/neo-project/proposals/blob/master/nep-6.mediawiki)
> encrypts wallet information, so the user has to provide a password. However, since this same information
> is still stored unencrypted in the blockchain information file, these accounts still should never
> be used in a production environment.

## neoxp transfer

```
Usage: neoxp transfer [options] <Quantity> <Asset> <Sender> <Receiver>

Arguments:
  Quantity                  Amount to transfer
  Asset                     Asset to transfer (symbol or script hash)
  Sender                    Account to send asset from
  Receiver                  Account to send asset to

Options:
  -p|--password <PASSWORD>  password to use for NEP-2/NEP-6 sender
  -i|--input <INPUT>        Path to neo-express data file
  -t|--trace                Enable contract execution tracing
  -j|--json                 Output as JSON
```

The `transfer` command is used to transfer assets between accounts in a Neo-Express
blockchain network. The transfer command has four required arguments

- the quantity to transfer as an integer or `all` to transfer all assets of the specified type 
- The asset to transfer. This can be specified as contract hash or
  [NEP-17](https://github.com/neo-project/proposals/blob/master/nep-17.mediawiki)
  token symbol such as `neo` or `gas`
- Signing account that is sending the asset
- Non-signing account that is receiving the asset

## neoxp contract

The `contract` command has a series of subcomands for managing smart contracts
on a Neo-Express blockchain network

### neoxp contract deploy

```
Usage: neoxp contract deploy [options] <Contract> <Account>

Arguments:
  Contract                            Path to contract .nef file
  Account                             Account to pay contract deployment GAS fee

Options:
  -w|--witness-scope <WITNESS_SCOPE>  Witness Scope to use for transaction signer
                                      Default: CalledByEntry
                                      Allowed values are: None, CalledByEntry, Global.
  -p|--password <PASSWORD>            password to use for NEP-2/NEP-6 account
  -i|--input <INPUT>                  Path to neo-express data file
  -t|--trace                          Enable contract execution tracing
  -f|--force                          Deploy contract regardless of name conflict
  -j|--json                           Output as JSON
```

The `contract deploy` command deploys a smart contract to a Neo-Express blockchain. The command takes
a path to an .NEF file generated by a Neo contract compiler like 
[NCCS compiler for .NET](https://github.com/neo-project/neo-devpack-dotnet).
Additionally, the command requires the signing account that will pay the GAS deployment fee.

By default, Neo-Express will not deploy multiple contracts with the same name to avoid developer
confusion. This behavior can be overridden with the `--force` option.

### neoxp contract invoke

```
Usage: neoxp contract invoke [options] <InvocationFile> <Account>

Arguments:
  InvocationFile                      Path to contract invocation JSON file
  Account                             Account to pay contract invocation GAS fee

Options:
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
```

The `contract invoke` command generates a script from an
[invocation file](https://github.com/ngdenterprise/design-notes/blob/master/NDX-DN12%20-%20Neo%20Express%20Invoke%20Files.md)
and submits it to the Neo-Express blockchain network as a transaction.

A script can be invoked either for results (specified via the `--results` option) or to make changes
(specified via the signed account argument). If a script is submitted for results, it may read information
stored in the blockchain, but any changes made to blockchain data will not be saved. If a submitted
for changes, a signed account must be specified and any results returned by the script will not be available 
immediately. For scripts submitted for changes, a transaction ID is returned and the execution results can 
be retrieved via the `show transaction` command (described below).

### neoxp contract run

```
Usage: neoxp contract run [options] <Contract> <Method> <Arguments>

Arguments:
  Contract                            Contract name or invocation hash
  Method                              Contract method to invoke
  Arguments                           Arguments to pass to the invoked method

Options:
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
```

Like `contract invoke`, the `contract run` command generates a script and submits it to the Neo-Express
blockchain network as a transaction wither for results or changes. However, unlike `contract invoke`, 
the `contract run` command generates the script from command line parameters instead of an invocation
file. The command line constraints limit the flexibility of `contract run` relative to `contract invoke`,
but saves the developer from needing to create an invocation file for simple contract invocation scenarios.

Instead of a path to an invocation file, The `contract run` command takes arguments specifying the contract
(either by name or hash) and the method to invoke, plus zero or more contract arguments. These contract
arguments are string encoded values, following similar rules to 
[string arguments in an invocation file](https://github.com/ngdenterprise/design-notes/blob/master/NDX-DN12%20-%20Neo%20Express%20Invoke%20Files.md#args-property).

### neoxp contract get

```
Usage: neoxp contract get [options] <Contract>

Arguments:
  Contract            Contract name or invocation hash

Options:
  -i|--input <INPUT>  Path to neo-express data file
```  

The `contract get` command retrieves the manifest of a deployed contract.

### neoxp contract list

```
Usage: neoxp contract list [options]

Options:
  -i|--input <INPUT>  Path to neo-express data file
  -j|--json           Output as JSON
```

The `contract list` command writes out the name and contract hash of every contract deployed in a
Neo-express blockchain network. This includes native contracts that are part of the core Neo platform.

### neoxp contract hash

```
Usage: neoxp contract hash [options] <Contract> <Account>

Arguments:
  Contract            Path to contract .nef file
  Account             Account that would deploy the contract

Options:
  -i|--input <INPUT>  Path to neo-express data file
```

The `contract hash` command calculates what the contract hash would be from a path to an .NEF file
and the non-signing account information of the account that would deploy the contract.

> Note, deploying the contract requires a signing account, but calculating the contract hash
> does not require private key information.

### neoxp contract storage

```
Usage: neoxp contract storage [options] <Contract>

Arguments:
  Contract            Contract name or invocation hash

Options:
  -i|--input <INPUT>  Path to neo-express data file
  -j|--json           Output as JSON
```
The `contract storage` commands dumps all the key/value pairs stored in the blockchain for
this contract. This command takes a single argument indicating the contract to dump storage
records for. For each key/value pair, the command shows both the key and the value
as both a hex-encoded byte array as well as a UTF-8 encoded string.

## neoxp show

The `show` command will display information from the blockchain. There are multiple subcommands 
representing the different  information that is available:

- `show balance` will display the balance of a single NEP-17 asset (including NEO and GAS) of a specific account
- `show balances` will display the balance of all NEP-17 asset (including NEO and GAS) owned by a specific account
- `show block` with display the contents of a single block, specified by index or hash
- `show transaction` with display the contents of a transaction specified by hash and its execution results if available
  - `show tx` is an alias for `show transaction`

## neoxp checkpoint

The `checkpoint` command has a series of subcomands for managing the state of a
Neo-Express blockchain. In particular, allowing a blockchain to be reverted to a
previous known state. While this is never something you would do on a production
blockchain, the ability to revert changes to a Neo-Express blockchain enables a
variety of debug and test scenarios.

> Note, all `checkpoint` subcommands require a single-node Neo-Express blockchain.
> Multi-node blockchains cannot be check pointed.

### neoxp checkpoint create

```
Usage: neoxp checkpoint create [options] <Checkpoint file name>

Arguments:
  Checkpoint file name

Options:
  -i|--input <INPUT>    Path to neo-express data file
  -f|--force            Overwrite existing data
```

The `checkpoint create` enables the user to create a checkpoint of a Neo-express blockchain. This command
takes a single argument: the name of the checkpoint. If the user wants to overwrite a checkpoint that has
already been created, they must specify the `--force` option.

### neoxp checkpoint restore

```
Usage: neoxp checkpoint restore [options] <Checkpoint file name>

Arguments:
  Checkpoint file name

Options:
  -i|--input <INPUT>    Path to neo-express data file
  -f|--force            Overwrite existing data
```

The `checkpoint restore` command enables the user to discard the current state of a Neo-Express blockchain
and replace it with the state from the checkpoint. If there is no existing blockchain state, restore
essentially works as an import. If there is existing blockchain state, the user must specify the `--force` option.

> Note, `checkpoint restore` validates that the checkpoint being restored matches the current blockchain. 
> If there is not a match, the restore is canceled without modifying the current blockchain state.

### neoxp checkpoint run

```
Usage: neoxp checkpoint run [options] <Checkpoint file name>

Arguments:
  Checkpoint file name

Options:
  -i|--input <INPUT>                          Path to neo-express data file
  -s|--seconds-per-block <SECONDS_PER_BLOCK>  Time between blocks
  -t|--trace                                  Enable contract execution tracing
```

The `checkpoint run` command enables the user run a checkpoint, similar to the standard `run` command
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
