<!-- markdownlint-enable -->
# NeoTrace Command Reference

NeoTrace generates `.neo-trace` files for transactions on public Neo N3 blockchains. These
files can be opened in [NeoDebug](debugger-command-reference.md) to step through the recorded
execution, forwards and backwards.

> NeoTrace depends on the target node running the StateService plugin module with
> `FullState` enabled. The official MainNet and TestNet JSON-RPC nodes are configured this
> way.

Each traced transaction is written to a `<transaction-hash>.neo-trace` file in the current
directory.

## neotrace block

```
Usage: neotrace block [Options] <BlockIdentifier>

Arguments:
  <BlockIdentifier>  Block index or hash

Options:
  --rpc-uri <RPC_URI>  URL of a Neo JSON-RPC node. Specify MainNet (the default),
                       TestNet, or a JSON-RPC URL.
```

Traces every transaction in the specified block. The block can be identified by index or
by hash.

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
```

Traces the specified transaction. `tx` is an alias for `transaction`.

```shell
neotrace tx 0xef1917b8601828e1d2f3ed0954907ea611cb734771609ce0ce2b654bb5c78005 --rpc-uri testnet
```
