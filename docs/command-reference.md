<!-- markdownlint-enable -->
# Neo-Express Command Reference

Note, you can pass -?|-h|--help to show a list of supported commands or to show
help information about a specific command.

> Remember, Neo-Express is in preview. You will find bugs and/or missing functionality
> as you use it. Please let us know of any issues you find via our
> [GitHub repo](https://github.com/ngdseattle/neo-express).

## neo-express create

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
this can be overridden with the `--output|-o` option. For all commands that read the
blockchain instance file, a non-default blockchain instance file can be specified
via the `--input|-i` option.

If the user wants to overwrite an existing blockchain instance file, they must
specify the `--force|-f` option.

By default, Neo-Express will pick port numbers for the consensus nodes in the
blockchain instance by starting at 49152 and incrementing for each new port number
that needs to be specified. Multiple blockchain instances cannot use the same set
of ports. The user can specify the starting port when creating a blockchain via the
`--ports|-p` option.

## neo-express run

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

### neo-express export

The `export` command saves all consensus node wallets in NEP-6 format. It also saves
config.json files for each consensus node as well as a protocol.json file for the
blockchain itself. This allows for standard Neo clients such as Neo-CLI or Neo-GUI
to connect to a running Neo-Express blockchain,

Note, NEP-6 format encrypts wallet information, so the user has to provide a password.
However, since this same information is still stored unencrypted in the blockchain
information file, these accounts still should never be used in a production environment.

The `export` command supports the `--input` argument for non-default blockchain
instance files.

## neo-express wallet

The `wallet` command has a series of subcommands for the management of standard
wallets and accounts for use in the Neo-Express blockchain. As stated above, wallet
accounts are stored unencrypted and should never be used in a production context.

All `wallet` subcommands support the `--input` argument for non-default blockchain
instance files.

### neo-express wallet create

The `wallet create` command creates a new standard wallet with a single account.
This command takes a single argument that specifies a friendly name that can be used
to reference the wallet. A friendly name like "alice" or "bob" is typically easier
to remember than a base 58 encoded address like AUknGuETph8fpna8WzY9Q8w64nQmuQxHgU.

If the user wants to overwrite an existing wallet, they need to specify the `--force`
option.

### neo-express wallet list

The `wallet list` command writes out a list of all the wallet friendly names and
account addresses.

### neo-express wallet delete

The `wallet delete` command removes a wallet and its accounts from the blockchain
information file. Note, this command does not modify the blockchain data. So any
assets associated with that wallet are not changed.

### neo-express wallet export

Similar the top-level `export` command described above, `wallet export` saves an
existing Neo-Express wallet in the NEP-6 format that can be read by standard Neo
tools.

## neo-express transfer

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

## neo-express show

The `show` command will display information about an account in the blockchain.
There are three subcommands representing the different account information that
is available:

- account, to see top level account information for a specific account
  (aka [getaccountstate](https://docs.neo.org/docs/en-us/reference/rpc/latest-version/api/getaccountstate.html))
- coins, to see a list of all coins - including spent coins - associated with a
  specific account
- gas, to see the available and unavailable gas for a specific account.

All show subcommands take a wallet account friendly name as a required argument
and supports the `--input` argument for non-default blockchain instance files.

## neo-express claim

The `claim` command is used for claiming available gas by an account in the
blockchain. The command has two required arguments - the token type to claim
(which must be 'gas') and the friendly name of the wallet account that is
claiming the gas.

```shell
$> neo-express claim gas alice
```

The `claim` command supports the `--input` argument for non-default blockchain
instance files.

## neo-express contract

The `contract` command has a series of subcomands for managing smart contracts
on a Neo-Express blockchain

All `contract` subcommands support the `--input` argument for non-default blockchain
instance files.

### neo-express contract deploy

The `contract deploy` command deploys a smart contract to a Neo-Express blockchain.
The command takes a path to an .AVM file generated by a Neo contract
compiler like [neon compiler for .NET](https://github.com/neo-project/neo-devpack-dotnet).

Deploying a contract also reads the .abi.json file generated by the neon compiler.
This file is required as it provides parameter type information that is used when
invoking the contract.

The `contract deploy` command also takes an arguments of the wallet friendly name
that will pay the GAS deployment cost. It takes an optional name argument that
can be used as the contract's friendly name. If the name argument isn't specified,
the filename of the contract is used by default.

If the contract is already deployed, information about the contract is saved to
the neo express blockchain .json file. If the blockchain isn't running, the
contract is not deployed but information about the contract is saved to the neo
express blockchain .json file regardless. Once the contract info is saved, the
contract can be referred to by its short name for deployment purposes.

> Note, during import neo-express will ask the user if the contract uses storage,
> dynamic-invoke or is payable. Future versions of neo-express will read this information
> from the manifest and won't need to ask the user.

### neo-express contract list

The `contract list` command writes out a list of all contracts that have been imported.

### neo-express contract get

The `contract get` command retrieves information about a deployed contract from
a running Neo-Express blockchain
(aka [getcontractstate](https://docs.neo.org/docs/en-us/reference/rpc/latest-version/api/getcontractstate.html)).

### neo-express contract storage

The `contract storage` dumps all the key/value pairs stored in the blockchain for
this contract. This command takes a single argument: the contract to dump storage
records for. For each key/value pair, the command shows both the key and the value
as both a hex-encoded byte array as well as a UTF-8 encoded string.

### neo-express contract invoke

The `contract invoke` command invokes a deployed contract in a running Neo-Express
blockchain. This command has two required arguments: the contract to invoke and
argument values to pass to the contract. This command also has two optional arguments
as well:

- `--function|-f` allows the user to provide the name of public function to invoke.
  When specified, `contract invoke` will parse provided parameters as per the types
  for the function from the .abi.json file. It will then invoke the main entry point,
  passing the provided function name as the first parameter and an array containing
  the provided parameters as the second parameter.
- `--account|-a` allows the user to provide the friendly name of a wallet account
  that will sign the smart contract output. This wallet account will pay any GAS
  charge associated with the contract invocation and will allow the contract to
  make durable changes to the blockchain.

> Note, contract invocation is an area of neo-express that will likely require
> significant improvement as it is tested by users on more real-world contracts.
> Please remember to let us know of any issues you find via our
> [GitHub repo](https://github.com/ngdseattle/neo-express).

## neo-express checkpoint

The `checkpoint` command has a series of subcomands for managing the state of a
Neo-Express blockchain. In particular, allowing a blockchain to be reverted to a
previous known state. While this is never something you would do on a production
blockchain, the ability to revert changes to a Neo-Express blockchain enables a
variety of debug and test scenarios.

Note, all `checkpoint` subcommands require a single-node Neo-Express blockchain.
Multi-node blockchains cannot be check pointed.

All `contract` subcommands support the `--input` argument for non-default blockchain
instance files.

### neo-express checkpoint create

The `checkpoint create` enables the user to create a checkpoint of a Neo-express
blockchain *that is not currently running*. This command has a single optional
argument: the name of the checkpoint. If a name is not provided, a default name
based on the current time and date will be used. If the user wants to overwrite
a checkpoint that has already been created, they must specify the `--force|-f`
option.

> Note, the ability to checkpoint a running blockchain will be added in a future
> version of Neo-Express

### neo-express checkpoint restore

The `checkpoint restore` command enables the user to discard the current state
of a Neo-Express blockchain and replace it with the state from the checkpoint.
If there is no existing blockchain state, restore essentially works as an import.
If there is existing blockchain state, the user must specify the `--force|-f`
option.

Note, `checkpoint restore` validates that the checkpoint being restored matches
the current blockchain. If there is not a match, the restore is canceled without
modifying the current blockchain state.

### neo-express checkpoint run

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
