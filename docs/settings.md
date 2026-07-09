# Neo-Express for N3 Settings Reference

The `.neo-express` file for Neo N3 compatible versions of N3 includes a `settings` object property. 
This document details the values that Neo-Express reads from the `settings` object.

## `chain.SecondsPerBlock`

The `chain.SecondsPerBlock` Neo-Express setting corresponds to the `MillisecondsPerBlock`
[config.json property](https://github.com/neo-project/neo-node/blob/5e3ffcb957e4e8fd8182307f68a70e653557e7d0/neo-cli/config.json#L28)
and the `--seconds-per-block` option for the Neo-Express `run` command. 

By default, Neo-Express mints a new block every 15 seconds. The `chain.SecondsPerBlock` setting can
be used to override the default behavior. If the `run` command `--seconds-per-block` option is specified,
the `chain.SecondsPerBlock` setting is ignored.

`chain.SecondsPerBlock` is specified as an unsigned integer. If you specify an invalid unsigned integer
value for this setting, Neo-Express reverts to the default.

Example usage:

``` json
  "settings": {
    "chain.SecondsPerBlock": "5" // Mint a new block every 5 seconds
  }
```

## `chain.AutoMine`

When `chain.AutoMine` is enabled on a single-node chain, transactions submitted against a running
node — transfers, contract deploys and invocations, `execute`, policy and candidate operations —
are confirmed immediately: instead of entering the mempool and waiting for the next dBFT block
(15 seconds by default, 1 second minimum), Neo-Express wraps the transaction in a consensus-signed
block and submits the block directly, the same way the offline node and the `fastfwd` command
already produce blocks. The regular dBFT block timer keeps running alongside and continues to mint
empty blocks on its normal schedule.

`chain.AutoMine` is specified as a boolean and defaults to `false`. It only takes effect on
single-node chains, where the Neo-Express process holds every consensus key; the setting is
ignored on four- and seven-node chains. In the unlikely event that a submitted block races an
in-flight dBFT block at the same height, Neo-Express retries once on the new chain tip and then
falls back to normal mempool submission.

Example usage:

``` json
  "settings": {
    "chain.AutoMine": "true" // Confirm transactions immediately on a single node chain
  }
```

## `protocol.*`

Neo-Express can read Neo protocol configuration values from the `settings` object. These values
override the defaults used to build the `ProtocolSettings` passed to the Neo node. The network
magic, address version, consensus committee, validators count and seed list continue to come from
the top-level `.neo-express` chain definition.

Supported protocol settings:

- `protocol.MillisecondsPerBlock`
- `protocol.MaxTransactionsPerBlock`
- `protocol.MemoryPoolMaxTransactions`
- `protocol.MaxTraceableBlocks`
- `protocol.MaxValidUntilBlockIncrement`
- `protocol.InitialGasDistribution`
- `protocol.Hardforks.<HardforkName>`

The `chain.SecondsPerBlock` setting and the `run --seconds-per-block` option still override
`protocol.MillisecondsPerBlock`.

Protocol values are specified as unsigned integers, except `protocol.MemoryPoolMaxTransactions`,
which is a positive integer. Hardfork names use the Neo `Hardfork` enum names, such as
`HF_Echidna` or `HF_Faun`, and values are activation block heights.

Example usage:

``` json
  "settings": {
    "protocol.MillisecondsPerBlock": "3000",
    "protocol.MaxTransactionsPerBlock": "1024",
    "protocol.MemoryPoolMaxTransactions": "100000",
    "protocol.MaxTraceableBlocks": "2102400",
    "protocol.MaxValidUntilBlockIncrement": "86400",
    "protocol.InitialGasDistribution": "5200000000000000",
    "protocol.Hardforks.HF_Echidna": "0",
    "protocol.Hardforks.HF_Faun": "0"
  }
```

## `policy.*`

Neo-Express can also initialize native policy values from the `settings` object. These values are
written to the chain state only while the chain is still at genesis height. If the chain has already
produced blocks, use the `policy set` command to change policy values through a transaction.

Supported native policy settings:

- `policy.GasPerBlock`
- `policy.MinimumDeploymentFee`
- `policy.CandidateRegistrationFee`
- `policy.OracleRequestFee`
- `policy.NetworkFeePerByte`
- `policy.StorageFeeFactor`
- `policy.ExecutionFeeFactor`
- `policy.MillisecondsPerBlock`
- `policy.MaxValidUntilBlockIncrement`
- `policy.MaxTraceableBlocks`

GAS-denominated policy values are specified in datoshi. One GAS is `100000000` datoshi.
`policy.StorageFeeFactor`, `policy.ExecutionFeeFactor`, `policy.MillisecondsPerBlock`,
`policy.MaxValidUntilBlockIncrement` and `policy.MaxTraceableBlocks` are whole-number factors or
counts.

When `HF_Echidna` is active at genesis height, Neo stores block time, max valid-until-block
increment and max traceable blocks in the native Policy contract. In that case Neo-Express uses
`policy.MillisecondsPerBlock`, `policy.MaxValidUntilBlockIncrement` and
`policy.MaxTraceableBlocks` if they are present; otherwise it initializes those native policy values
from `protocol.MillisecondsPerBlock`, `protocol.MaxValidUntilBlockIncrement` and
`protocol.MaxTraceableBlocks`.

Example usage:

``` json
  "settings": {
    "policy.NetworkFeePerByte": "2000",
    "policy.StorageFeeFactor": "200000",
    "policy.ExecutionFeeFactor": "40",
    "policy.MinimumDeploymentFee": "2000000000"
  }
```

## `dbft.MaxBlockSystemFee`

The `dbft.MaxBlockSystemFee` Neo-Express setting corresponds to the `MaxBlockSystemFee`
DBFT plugin setting. This setting specifies the maximum transaction system fee, in
datoshi, accepted by the consensus service.

This setting defaults to `2000000000` datoshi, or 20 GAS. If you specify an invalid or
negative integer value for this setting, Neo-Express reverts to the default. One GAS is
`100000000` datoshi.

Example usage:

``` json
  "settings": {
    "dbft.MaxBlockSystemFee": "4000000000" // support transaction system fees up to 40 GAS
  }
```

## `rpc.BindAddress`

The `rpc.BindAddress` Neo-Express setting corresponds to the `BindAddress`
[RpcServer config property](https://github.com/neo-project/neo-modules/blob/20880c3373c4f446968946504cf79280a7e4721f/src/RpcServer/config.json#L6).

By default, Neo-Express only listens for JSON-RPC requests on the loopback address. This means that
JSON-RPC requests must originate on the same machine as Neo-Express is running in order to be serviced.
While this is the most secure approach, it limits the ability of the developer to test cross machine
scenarios, especially ones that involve mobile devices. The `rpc.BindAddress` setting can be used to
override the default behavior.

The `rpc.BindAddress` field accepts an IP address in dotted quad notation. It specifies the IP Address
that the JSON-RPC server will listen on for client requests. Typically, to enable remote access to
a Neo-Express instance, you would specify the `rpc.BindAddress` to be `0.0.0.0`. 

If you specify an invalid IP Address, Neo-Express reverts to the default loopback `BindAddress`
(aka `127.0.0.1`).

> **Security note:** The Neo-Express RPC server exposes unauthenticated administrative methods
> that control the node, including `expressshutdown` (stops the node), `expresscreatecheckpoint`,
> `expresspersiststorage` and `expresspersistcontract` (modify chain state). The default loopback
> `BindAddress` limits these to processes on the same machine. Binding to any other address (for
> example `0.0.0.0`) makes them reachable, without authentication, by any host that can route to
> that address. Only override the default on a trusted network and only for the duration of the
> testing scenario that requires it.

Example usage:

``` json
  "settings": {
    "rpc.BindAddress": "0.0.0.0" // listens for JSON-RPC requests on all network interfaces
  }
```

## `rpc.MaxFee`

The `rpc.MaxFee` Neo-Express setting corresponds to the `MaxFee`
[RpcServer config property](https://github.com/neo-project/neo-modules/blob/20880c3373c4f446968946504cf79280a7e4721f/src/RpcServer/config.json#L14).
This setting specifies a maximum Network Fee for the
[`sendfrom`](https://docs.neo.org/docs/en-us/reference/rpc/latest-version/api/sendfrom.html),
[`sendmany`](https://docs.neo.org/docs/en-us/reference/rpc/latest-version/api/sendmany.html)
and [`sendtoaddress`](https://docs.neo.org/docs/en-us/reference/rpc/latest-version/api/sendtoaddress.html)
JSON-RPC methods. 

This setting defaults to 0.1 GAS. If you specify an invalid decimal value for this setting, Neo-Express reverts to the default.

Example usage:

``` json
  "settings": {
    "rpc.MaxFee": "0.2" // support higher network fee for send[from/many/toaddress] methods
  }
```

## `rpc.MaxGasInvoke`

The `rpc.MaxGasInvoke` Neo-Express setting corresponds to the `MaxGasInvoke`
[RpcServer config property](https://github.com/neo-project/neo-modules/blob/20880c3373c4f446968946504cf79280a7e4721f/src/RpcServer/config.json#L13).
This setting specifies maximum limit in GAS for the
[invokefunction](https://docs.neo.org/docs/en-us/reference/rpc/latest-version/api/invokefunction.html)
and [invokescript](https://docs.neo.org/docs/en-us/reference/rpc/latest-version/api/invokescript.html)
JSON-RPC methods.

This setting defaults to 10.0 GAS. If you specify an invalid decimal value for this setting, Neo-Express reverts to the default.

Example usage:

``` json
  "settings": {
    "rpc.MaxGasInvoke": "15" // support higher GAS limit for invoke[function/script] methods
  }
```

## `rpc.MaxIteratorResultItems`

The `rpc.MaxIteratorResultItems` Neo-Express setting corresponds to the `MaxIteratorResultItems`
[RpcServer config property](https://github.com/neo-project/neo-modules/blob/20880c3373c4f446968946504cf79280a7e4721f/src/RpcServer/config.json#L16).
This setting specifies maximum number of items returned to the RPC caller when there is an iterator
on the result stack.

This setting defaults to 100 items. If you specify an invalid or negative integer value for this setting,
Neo-Express reverts to the default.

Example usage:

``` json
  "settings": {
    "rpc.MaxIteratorResultItems": "150" // support higher item limit for iterator results
  }
```

## `rpc.SessionEnabled`

The `rpc.SessionEnabled` Neo-Express setting corresponds to the `SessionEnabled`
[RpcServer config property](https://github.com/neo-project/neo-modules/blob/20880c3373c4f446968946504cf79280a7e4721f/src/RpcServer/config.json#L19).
This setting specifies if iterator sessions are enabled.

This setting defaults to `true`. If you specify an invalid boolean value for this setting, 
Neo-Express reverts to the default.

Example usage:

``` json
  "settings": {
    "rpc.SessionEnabled": "false" // disable sessions
  }
```
