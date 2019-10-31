# Neo Express Change Log

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

This project uses [NerdBank.GitVersioning](https://github.com/AArnott/Nerdbank.GitVersioning)
to manage version numbers. This tool automatically sets the Semantic Versioning Patch
value based on the [Git height](https://github.com/AArnott/Nerdbank.GitVersioning#what-is-git-height)
of the commit that generated the build. As such, released versions of this extension
will not have contiguous patch numbers. Initial major and minor releases will be documented
in this file without a patch number. Patch version will be included for bug fix releases, but
may not exactly match a publicly released version.

## [0.9] - Unreleased

### Added

- Added RPC endpoint to mirror behavior of
  [ApplicationLogs](https://github.com/neo-project/neo-plugins/tree/b5388d753a2da1d59583dd9c66835e29ca7fd6f3/ApplicationLogs)
  plugin.
  - [`getapplicationlog`](https://docs.neo.org/docs/en-us/reference/rpc/latest-version/api/getapplicationlog.html)
- Added RPC endpoint to mirror behavior of
  [RpcSystemAssetTracker](https://github.com/neo-project/neo-plugins/tree/b5388d753a2da1d59583dd9c66835e29ca7fd6f3/RpcSystemAssetTracker)
  plugin.
  - [`getclaimable`](https://docs.neo.org/docs/en-us/reference/rpc/latest-version/api/getclaimable.html),
    [`getunclaimed`](https://docs.neo.org/docs/en-us/reference/rpc/latest-version/api/getunclaimed.html) and
    [`getunspents`](https://docs.neo.org/docs/en-us/reference/rpc/latest-version/api/getunspents.html)
- Added `--online` option to `checkpoint create` command to create a checkpoint while neo-express is running.
- Added `show claimable` and `show unspents` commands.
- Added `all` quantity support for `transfer` command.
- Support for UTXO inputs/outputs on express-invoke-contract RPC endpoint.

### Changed

- Changed `show gas` command to `show unclaimed` to match
  [RpcSystemAssetTracker](https://github.com/neo-project/neo-plugins/tree/b5388d753a2da1d59583dd9c66835e29ca7fd6f3/RpcSystemAssetTracker)
  [`getunclaimed`](https://docs.neo.org/docs/en-us/reference/rpc/latest-version/api/getunclaimed.html)
  RPC endpoint name.
- Updated to .NET Core 3.0 and C# 8 (with nullable types enabled).
- Refactored neo-express to merge abstraction and express2 libraries back into
  neo-express.
  - This separation was originally done to enable neo-express to support
    Neo 2 and 3. However, this approach would not work, so it was undone.
- updated Neo branding as per https://neo.org/presskit

### Removed

- Removed custom `express-show-gas` RPC endpoint in favor of
  [RpcSystemAssetTracker](https://github.com/neo-project/neo-plugins/tree/b5388d753a2da1d59583dd9c66835e29ca7fd6f3/RpcSystemAssetTracker)
  plugin compatible [`getunclaimed`](https://docs.neo.org/docs/en-us/reference/rpc/latest-version/api/getunclaimed.html)
  RPC endpoint.
- Removed `contract import` command. `contract deploy` now accepts a path to a Neo VM .avm file,
  a path to a directory containing the .avm file, or a contract short name for a previously deployed
  smart contract.

## [0.8] - 2019-09-13

Initial Release
