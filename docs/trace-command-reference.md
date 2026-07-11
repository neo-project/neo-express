<!-- markdownlint-enable -->
# NeoTrace Command Reference

NeoTrace generates `.neo-trace` files for transactions on public Neo N3 blockchains. These
files can be opened in the [Neo Smart Contract Debugger](https://github.com/neo-project/neo-debugger)
to step through the recorded execution.

> NeoTrace depends on the target node running the StateService plugin module with
> `FullState` enabled. The official MainNet and TestNet JSON-RPC nodes are configured this
> way.
>
> For local Neo-Express transactions, use the `--trace` option on Neo-Express commands
> such as `neoxp run`, `neoxp contract invoke`, or `neoxp contract run`. NeoTrace is for
> replaying transactions from StateService-enabled public-chain RPC nodes.

Each traced transaction is written to a `<transaction-hash>.neo-trace` file in the current
directory.

NeoTrace records script execution, stack state, logs, notifications, and results for public
chain transactions. It does not include per-instruction storage snapshots from StateService,
because downloading full public-chain contract storage during every trace step can make
large contracts impractical to replay. For local Neo-Express contract debugging with storage
snapshots, use the `--trace` option on the relevant `neoxp` command.

## neotrace block

```
Usage: neotrace block [Options] <BlockIdentifier>

Arguments:
  <BlockIdentifier>  Block index or hash

Options:
  --rpc-uri <RPC_URI>  URL of a Neo JSON-RPC node. Specify MainNet (the default),
                       TestNet, or a JSON-RPC URL.
  --timeout <SECONDS>  Maximum tracing time in seconds. Defaults to 300. Use 0
                       to disable the timeout.
```

Traces every transaction in the specified block. The block can be identified by index or
by hash. Blocks with many or complex transactions can take a while because NeoTrace
reconstructs the previous block state from StateService proofs before replaying each
transaction.

```shell
neotrace block 365110 --rpc-uri testnet
```

## neotrace transaction

```
Usage: neotrace transaction [Options] <TransactionHash>

Arguments:
  <TransactionHash>  Transaction hash

Options:
  --rpc-uri <RPC_URI>  URL of a Neo JSON-RPC node. Specify MainNet (the default),
                       TestNet, or a JSON-RPC URL.
  --timeout <SECONDS>  Maximum tracing time in seconds. Defaults to 300. Use 0
                       to disable the timeout.
```

Traces the specified transaction. `tx` is an alias for `transaction`.

```shell
neotrace tx 0xef1917b8601828e1d2f3ed0954907ea611cb734771609ce0ce2b654bb5c78005 --rpc-uri testnet
```
