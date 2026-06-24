<!-- markdownlint-enable -->
# NeoDebug Command Reference

NeoDebug (`neodebug`) is a source-level debugger for Neo N3 smart contracts. It is a
[Debug Adapter Protocol](https://microsoft.github.io/debug-adapter-protocol/) host: an editor
launches it and speaks DAP over standard in/out, letting you set breakpoints in your C# (or
other supported language) source, step through execution, and inspect arguments, locals,
static fields, the evaluation stack, and contract storage.

NeoDebug debugs a **recorded execution trace** (a `.neo-trace` file). Because the execution is
a recording, you can step **backward** as well as forward — *time-travel debugging*.

## Installation

NeoDebug is distributed as a .NET global tool:

```shell
dotnet tool install Neo.Debug -g
dotnet tool update Neo.Debug -g
```

Confirm the install with:

```shell
neodebug --version
```

## Workflow

1. **Compile** your contract with debug information. The Neo C# compiler (`nccs`) emits a
   `<contract>.nef`, a `<contract>.manifest.json`, and a `<contract>.nefdbgnfo` (a compressed
   `*.debug.json`, the [NEP-19](https://github.com/neo-project/proposals) source map). Use the
   *extended*, unoptimized debug build so every statement has a sequence point.

2. **Capture a trace.** Produce a `.neo-trace` for the invocation you want to debug — for
   example with [NeoTrace](trace-command-reference.md) against a public transaction, or by
   starting a Neo-Express node with `neoxp run --trace` (which writes
   `<txhash>.neo-trace` files as it executes).

3. **Configure** a launch configuration (see below) that points at the contract and the trace.

4. **Debug.** Launch from your editor's debug view. Set breakpoints on source lines; use
   continue, step in/out/over, and — because this is a recorded trace — step back and reverse
   continue to move backward through the execution.

## Launch configuration

`neodebug` is a DAP stdio host, so an editor (or any DAP client) spawns it and sends a `launch`
request whose configuration carries these properties:

| Property | Required | Description |
| --- | --- | --- |
| `program` | yes | Path to the compiled `.nef`. Its sibling `.manifest.json` and `.nefdbgnfo`/`.debug.json` are loaded automatically. |
| `invocation` | yes | Either `{ "trace-file": "<path>" }` to **replay** a recorded trace, or `{ "operation": "<method>", "args": [ ... ] }` to **deploy and run** the contract live. |
| `return-types` | no | Array of cast hints (`int`, `bool`, `string`, `hex`, `byte[]`, `addr`) for rendering the method's return values. |
| `sourceFileMap` | no | Object remapping the document paths baked into the debug info to their location on this machine. |

A VS Code `launch.json` entry looks like:

```jsonc
{
  "version": "0.2.0",
  "configurations": [
    {
      "name": "Debug Neo contract (trace)",
      "type": "neo-contract",
      "request": "launch",
      "program": "${workspaceFolder}/bin/sc/Contract.nef",
      "invocation": {
        "trace-file": "${workspaceFolder}/traces/0xabc...neo-trace"
      },
      "return-types": [ "int" ]
    }
  ]
}
```

To **deploy and run the contract live** instead, give the invocation an `operation` (and optional
`args`) rather than a `trace-file`; the contract is deployed into a fresh, single-block in-process
chain and the debugger stops at the call:

```jsonc
{
  "name": "Debug Neo contract (live)",
  "type": "neo-contract",
  "request": "launch",
  "program": "${workspaceFolder}/bin/sc/Contract.nef",
  "invocation": {
    "operation": "transfer",
    "args": [ "@NXV7ZhHiyM1aHXwpVsRZC6BwNFP2jghXAq", 100 ]
  },
  "return-types": [ "bool" ]
}
```

> The repository includes a [VS Code extension](../extensions/neodebug-vscode/README.md) that
> registers the `neo-contract` debug type and launches `neodebug` from your `PATH`. Set
> `neo-contract.debugAdapterPath` in VS Code when you need to use a specific debugger build.
> Other DAP clients can also launch `neodebug` and send the same configuration.

## Debug views

NeoDebug presents two views. The `--debug-view` option selects the initial view; a DAP client can
switch views later with NeoDebug's `debugview` request:

- **Source** (default) — steps and breakpoints follow your source lines.
- **Disassembly** — steps and breakpoints follow the NeoVM instructions, with the evaluation
  stack and slots exposed as raw values.

If source debug information is unavailable, a source-mode launch falls back to disassembly and
reports the fallback in the debug console.

## Evaluating expressions

In the debug console you can evaluate:

- named arguments, locals, and static fields by name;
- slots by index — `#arg0`, `#local1`, `#static0`, `#eval0`, `#result0`;
- contract storage rows — `#storage[...]`;
- with a leading cast — `(int)`, `(bool)`, `(string)`, `(hex)`, `(byte[])`, `(addr)`.

## Replay vs. live

NeoDebug supports two ways to drive an invocation:

- **Replay** (`invocation.trace-file`) — steps a recorded `.neo-trace`. Because the execution is a
  recording, you can step **backward** (time-travel).
- **Live** (`invocation.operation`) — deploys the contract into a fresh, single-block in-process
  chain and steps the call as it really executes. Live debugging cannot step backward.

The live launch runs against a throwaway local chain seeded only with the contract under debug;
multi-contract scenarios, signer/account resolution against a Neo-Express chain, checkpoints, and
oracle responses are not yet wired into the launcher.
