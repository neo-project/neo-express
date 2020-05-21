<!-- markdownlint-enable -->
# Neo-Express Command Reference

Note, you can pass -?|-h|--help to show a list of supported commands or to show
help information about a specific command.

## `create` command

The `create` command is used to create a new Neo-Express blockchain instance. In
particular, the create command creates one or more consensus node wallets as well
as the multi-signature contracts needed for the management of genesis assets.

Note, the wallets created for a new blockchain instance are not encrypted in the
blockchain instance file. This simplifies the developer workflow by eliminating
the need to manage passwords. However, it also means that all Neo-Express wallets
are insecure and should never be used in a production context.

By default, the `create` command creates a single node Neo-Express blockchain
instance. While a single node blockchain can handle most developer scenarios, it
is also possible to create a four or seven node blockchain via the `--count|-c` option.

All of the information about a Neo-Express blockchain instance is stored in a
single JSON file. By default, this file is named "express.privatenet.json", but
this can be overridden with the `--output|-o` option. For all commands that read
the blockchain instance file, a non-default blockchain instance file can be specified
via the `--input|-i` option.

If the user wants to overwrite an existing blockchain instance file, they must
specify the `--force|-f` option.

When creating a new Neo-Express blockchain instance, the `--preload-gas|-p`
command can be used generate a set of empty blocks. This provides the genesis account
some GAS tokens to start.  

By default, Neo-Express will pick port numbers for the consensus nodes in the
blockchain instance by starting at 49152 and incrementing for each new port number
that needs to be specified. Multiple blockchain instances cannot use the same set
of ports. Once the Neo-Express instance has been created, the port numbers in the
.neo-express.json file can be manually edited as needed.

## `run` command

Once created, a blockchain instance can be started by using the `run` command.
The consensus node index to be run must be passed as an argument to the run command.
So for a single node blockchain, the user would call `neo-express run 0`. Note,
each node of a multi-node block chain must be run separately.

The `run` command supports the `--input` argument for non-default blockchain
instance files.

When the blockchain is run, the user can specify how often a new block is minted.
By default, a new block is minted every 15 seconds. If the user would like to run
at a different rate, they can specify how many seconds per block via the
`--seconds-per-block|-s` argument. Note, the user may specify a different seconds
per block each time a blockchain is run, but all nodes in multi-node blockchain
must use the same value when running.

A blockchain can be reset to the genesis block via the `--reset|-r` argument. Note,
resetting a blockchain is irreversible, though any checkpoints that were made
can still be restored.

### `export` command

The `export` command saves all consensus node wallets in NEP-6 format. It also saves
config.json files for each consensus node as well as a protocol.json file for the
blockchain itself. This allows for standard Neo clients such as Neo-CLI or Neo-GUI
to connect to a running Neo-Express blockchain,

Note, NEP-6 format encrypts wallet information, so the user has to provide a password.
However, since this same information is still stored unencrypted in the blockchain
information file, these accounts still should never be used in a production environment.

The `export` command supports the `--input` argument for non-default blockchain
instance files.

## `wallet` command

The `wallet` command has a series of subcommands for the management of standard
wallets and accounts for use in the Neo-Express blockchain. As stated above, wallet
accounts are stored unencrypted and should never be used in a production context.

All `wallet` subcommands support the `--input` argument for non-default blockchain
instance files.

### `wallet create` subcommand

The `wallet create` command creates a new standard wallet with a single account.
This command takes a single argument that specifies a friendly name that can be used
to reference the wallet. A friendly name like "alice" or "bob" is typically easier
to remember than a base 58 encoded address like AUknGuETph8fpna8WzY9Q8w64nQmuQxHgU.

If the user wants to overwrite an existing wallet, they need to specify the `--force`
option.

### `wallet list` subcommand

The `wallet list` command writes out a list of all the wallet friendly names and
account addresses.

### `wallet delete` subcommand

The `wallet delete` command removes a wallet and its accounts from the blockchain
information file. Note, this command does not modify the blockchain data. So any
assets associated with that wallet are not changed.

### `wallet export` subcommand

Similar the top-level `export` command described above, `wallet export` saves an
existing Neo-Express wallet in the NEP-6 format that can be read by standard Neo
tools.

## `transfer` command

The `transfer` command is used to transfer assets between accounts in a Neo-Express
blockchain. The transfer command has four required arguments

- The asset to transfer. This can be 'neo', 'gas' or the hash of a NEP-5 token
- the amount to transfer as an integer
- the friendly name of the sending account
- the friendly name of the receiving account

Note, Neo-Express reserves the friendly name 'genesis' to refer to the account
holding the tokens created in the genesis block. 'genesis' can be used as any
other friendly name for transfers. For example, to transfer a million genesis neo
tokens to the alice account, execute the command

```shell
$> neo-express transfer neo 1000000 genesis alice
```

The `transfer` command supports the `--input` argument for non-default blockchain
instance files.

## `claim` command

The `claim` command is used for claiming available gas by an account in the
blockchain. The command has two required arguments - the token type to claim
(which must be 'gas') and the friendly name of the wallet account that is
claiming the gas.

```shell
$> neo-express claim gas alice
```

The `claim` command supports the `--input` argument for non-default blockchain
instance files.

## `show` command

The `show` command will display information about an account in the blockchain.
There are multiple subcommands representing the different account information that
is available:

- `account`, to see top level account information for a specific account
  (aka [getaccountstate](https://docs.neo.org/docs/en-us/reference/rpc/latest-version/api/getaccountstate.html))
- `claimable`, to see claimable gas for a specific account
- `coins`, to see a list of all coins - including spent coins - associated with a
  specific account
- `gas`, to see the available and unavailable gas for a specific account.
- `unspent`, to see a list of all unspent native token transactions associated
  with a specific account.
- `tx`, to see the JSON representation of a specific transaction. For
  InvokeTransactions, the JSON representation of the ApplicationLog is also shown.

All show subcommands except for `tx` take a wallet account friendly name as a
required argument and supports the `--input` argument for non-default blockchain
instance files. For automation scenarios, `show` commands also support a `--json`
argument to return information in an easy-to-parse format for tools rather than
the easy for humans to understand default text format.

## `contract` command

The `contract` command has a series of subcomands for managing smart contracts
on a Neo-Express blockchain

All `contract` subcommands support the `--input` argument for non-default blockchain
instance files.

### `contract deploy` subcommand

The `contract deploy` command deploys a smart contract to a Neo-Express blockchain.
The command takes a path to an .AVM file generated by a Neo contract
compiler like [neon compiler for .NET](https://github.com/neo-project/neo-devpack-dotnet).

Deploying a contract also reads the .abi.json file generated by the neon compiler.
This file is required as it provides parameter type information that is used when
invoking the contract. The `contract deploy` command also takes an argument of
the wallet friendly name that will pay the GAS deployment cost.

By default, `contract deploy` will also save contract metadata to the blockchain.
This step can be skipped via the `--save-metadata:false` argument 

### `contract list` subcommand

The `contract list` command writes out a list of all contracts that have been imported.

### `contract get` subcommand

The `contract get` command retrieves information about a deployed contract from
a running Neo-Express blockchain
(aka [getcontractstate](https://docs.neo.org/docs/en-us/reference/rpc/latest-version/api/getcontractstate.html)).

The contract can be specified either by script hash or path to the contract's
.avm file.

### `contract storage` subcommand

The `contract storage` dumps all the key/value pairs stored in the blockchain for
this contract. This command takes a single argument: the contract to dump storage
records for. For each key/value pair, the command shows both the key and the value
as both a hex-encoded byte array as well as a UTF-8 encoded string.

The contract can be specified either by script hash or path to the contract's
.avm file.

### `contract invoke` subcommand

> Note, the `contract invoke` command changed significantly in Neo-express v1.1.

The `contract invoke` command invokes a deployed contract in a running Neo-Express
blockchain. Contracts invoked with this command can either be broadcast or test invoked.
Contracts that are broadcast can modify the state of the blockchain and require an
account to pay the contract invocation GAS cost (if any). Contracts that are test
invoked do not modify the state of the blockchain but can return information to the
caller. There is no GAS cost for test invocation of a contract.

Both broadcast and test invocation of a contract require a contract invocation file.
This format is described in [NGX-DN12](https://ngdseattle.github.io/design-notes/NDX-DN12%20-%20Neo%20Express%20Invoke%20Files).

For broadcast contract invocations, the wallet friendly name that will pay
the GAS invocation cost and returns the transaction script hash. Once the transaction
is broadcast on the blockchain (this depends on the --seconds-per-block argument),
information about the transaction can be retrieved with the `show tx` command.

For test contract invocations, you must pass the `--test|-t` argument. There is no
need to pass a wallet friendly name, but it is not an error to provide one. Test
invocation will return information about the contract execution, including the GAS
it would cost to broadcast invoke the contract and the results of the test invocation.

## `checkpoint` command

The `checkpoint` command has a series of subcomands for managing the state of a
Neo-Express blockchain. In particular, allowing a blockchain to be reverted to a
previous known state. While this is never something you would do on a production
blockchain, the ability to revert changes to a Neo-Express blockchain enables a
variety of debug and test scenarios.

Note, all `checkpoint` subcommands require a single-node Neo-Express blockchain.
Multi-node blockchains cannot be check pointed.

All `contract` subcommands support the `--input` argument for non-default blockchain
instance files.

### `checkpoint create` subcommand

The `checkpoint create` enables the user to create a checkpoint of a Neo-express
blockchain *that is not currently running*. This command has a single optional
argument: the name of the checkpoint. If a name is not provided, a default name
based on the current time and date will be used. If the user wants to overwrite
a checkpoint that has already been created, they must specify the `--force|-f`
option.

> Note, the ability to checkpoint a running blockchain will be added in a future
> version of Neo-Express

### `checkpoint restore` subcommand

The `checkpoint restore` command enables the user to discard the current state
of a Neo-Express blockchain and replace it with the state from the checkpoint.
If there is no existing blockchain state, restore essentially works as an import.
If there is existing blockchain state, the user must specify the `--force|-f`
option.

Note, `checkpoint restore` validates that the checkpoint being restored matches
the current blockchain. If there is not a match, the restore is canceled without
modifying the current blockchain state.

### `checkpoint run` subcommand

The `checkpoint run` command enables the user run a checkpoint, similar to the
standard `run` command described above. However, checkpoint run stores any changes
to the checkpoint in memory instead of on disk. WHen the blockchain is shut down,
any changes to the blockchain that were saved to memory are discarded. Like the
standard `run` command, the user can control the speed at which the blockchain
mints blocks via the `--seconds-per-block|-s` argument.

`checkpoint run` is of particular use in test scenarios, where the resulting state
of the Neo-Express blockchain is not important beyond validating that all tests pass.

> Note, once a checkpoint is run, there is no way to save changes made to that
> running instance.

Like `checkpoint restore`, `checkpoint run` validates that the checkpoint being
run matches the current blockchain. If there is not a match, the run is canceled.
