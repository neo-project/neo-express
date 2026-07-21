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

### Example: debug a local contract call

Assume `Contract.csproj` uses `Neo.BuildTasks`, produces `bin/sc/Contract.nef`, and exposes a
parameterless `getValue` method. From the contract project directory, build and deploy it to a
fresh Neo-Express instance, then invoke the method with tracing enabled:

```shell
dotnet build ./Contract.csproj
neoxp create --force --output ./debug.neo-express
neoxp contract deploy --input ./debug.neo-express ./bin/sc/Contract.nef genesis
neoxp contract run --input ./debug.neo-express --trace --account genesis Contract getValue
```

The final command prints the transaction hash and writes `<transaction-hash>.neo-trace` in the
current directory. Put that file in `traces/`, replace the placeholder in the launch configuration
below with its file name, set a breakpoint in the contract source, and start the **Debug Neo
contract (trace)** configuration from VS Code's Run and Debug view.

## Launch configuration

`neodebug` is a DAP stdio host, so an editor (or any DAP client) spawns it and sends a `launch`
request whose configuration carries these properties:

| Property | Required | Description |
| --- | --- | --- |
| `program` | yes | Path to the compiled `.nef`. Its sibling `.nefdbgnfo`/`.debug.json` is loaded automatically for source mapping. |
| `invocation.trace-file` | yes | Path to the `.neo-trace` recording to replay. |
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

## Current scope

NeoDebug currently debugs recorded traces (`invocation.trace-file`). Launching a fresh
in-process invocation (deploy-and-run without a pre-recorded trace) is planned; until then, a
launch configuration without a `trace-file` reports that it is not yet supported and points you
at NeoTrace.
