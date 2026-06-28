# Neo Smart Contract Debugger (VS Code)

A source-level, time-travel debugger for Neo N3 smart contracts. This extension registers the
`neo-contract` debug type in VS Code and drives the [`neodebug`](../../docs/debugger-command-reference.md)
tool (the `Neo.Debug` global tool) as a [Debug Adapter Protocol](https://microsoft.github.io/debug-adapter-protocol/)
host.

## Prerequisites

Install the debugger tool:

```shell
dotnet tool install Neo.Debug -g
```

The extension runs `neodebug` from your `PATH`. To point at a different build, set
`neo-contract.debugAdapterPath` in your settings.

## Usage

Add a configuration to `.vscode/launch.json`. Replay a recorded trace (supports stepping backward):

```jsonc
{
  "name": "Debug Neo contract (trace)",
  "type": "neo-contract",
  "request": "launch",
  "program": "${workspaceFolder}/bin/sc/Contract.nef",
  "invocation": { "trace-file": "${workspaceFolder}/traces/transaction.neo-trace" },
  "return-types": [ "int" ]
}
```

…or deploy and run the contract live:

```jsonc
{
  "name": "Debug Neo contract (live)",
  "type": "neo-contract",
  "request": "launch",
  "program": "${workspaceFolder}/bin/sc/Contract.nef",
  "invocation": { "operation": "transfer", "args": [ "@NXV7ZhHiyM1aHXwpVsRZC6BwNFP2jghXAq", 100 ] }
}
```

Set breakpoints in your C# source, then start debugging. See the
[NeoDebug command reference](../../docs/debugger-command-reference.md) for the full launch-configuration
schema, the source/disassembly views, and debug-console expression evaluation.

## Packaging

This is a build-free JavaScript extension — it has no `node_modules` and no compile step. Package it
with [`vsce`](https://github.com/microsoft/vscode-vsce):

```shell
npx @vscode/vsce package
```
