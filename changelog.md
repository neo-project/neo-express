# Neo Express Change Log

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

This project uses [NerdBank.GitVersioning](https://github.com/AArnott/Nerdbank.GitVersioning)
to manage version numbers. This tool automatically sets the Semantic Versioning Patch
value based on the [Git height](https://github.com/AArnott/Nerdbank.GitVersioning#what-is-git-height)
of the commit that generated the build. As such, released versions of this tool
will not have contiguous patch numbers. Initial major and minor releases will be documented
in this file without a patch number. Patch version will be included for bug fix releases, but
may not exactly match a publicly released version.

## Unreleased

### Added 

* Added NeoExpress `fastfwd` command (#182)
* Added NeoExpress `policy sync` command and batch file operation (#192)
* Added `--rpc-uri` option to `policy get` command (#192)
* Added `--json` option to `policy get` command (#192)
* Added additional policy settings to `policy set` command (#192)
* Added `--trace` option to `oracle response` command (#190)

### Changed

* Enable transactions costing greater than 20 GAS in offline mode (#190)
* Print internal exception (if any) when `contract deploy` command fails (#190)
* Print internal exception (if any) when any command involving a user contract fails (#192)

### Removed

* Removed `Policy` argument from NeoExpress `policy get` command. Command now retrieves all policy settings (#192)
## [3.0.19] 2021-10-12

### Added

* NeoTrace tool (#178)

### Changed

* Moved ExpressApplicationEngine from NeoExpress
  to [Blockchain Toolkit Library](https://github.com/ngdenterprise/neo-blockchaintoolkit-library)
  and renamed as TraceApplicationEngine in order to share with NeoTrace
* Update dependencies for Neo 3.0.3 release

## [3.0.13] 2021-09-08

### Documentation

* Updated [command reference](docs/command-reference.md) for Neo-Express N3
* Updated original command reference to reflect use in Neo-Express for Neo Legacy and renamed
  as [legacy-command-reference.md](docs/legacy-command-reference.md)

### Added

* `policy` command (#173)
* `contract hash` command (#170)
* `chain.SecondsPerBlock` setting (#171)
* `--stack-trace` option (#174)

### Fixed

* Update `oracle response` error message when oracles are not enabled (#175)

## [3.0.5] 2021-08-10

### Changed

* Update dependencies for Neo 3.0.2 release

## [3.0] - 2021-08-02

### Changed

* Neo N3 release support
* Bumped major version to 3 for consistency with Neo N3 release
* Update dependencies

## [2.0.50-preview] - 2021-07-21

### Changes

* Neo N3 RC4 support
* Adapt to Trace Model Changes in lib-bctk (#154)
* Update Dependencies (#166)
* ContractInvokeAsync should print "Invocation" not "Depoloyment" (8bd9f4368e2917bce84c45b2eed1919c27d611bb)
* Pass AdditionalGas option value to ContractInvokeAsync in contract run + invoke (#163, fixes #161)

### Added

* Add `stop` command (#156, fixes #153)
* Add `contract run` command (#157, fixes #150)
* Log fault tx message when running (#159, fixes #155)
* Add checkpoint reset support to batch command (#160, fixes #151)
* Add rpc.BindAddress setting (#165, fixes #164)

## [2.0.39-preview] - 2021-06-15

### Changes

* Neo N3 RC3 support
* Contract invoke should check to ensure either account or --results is specified (#145)
* Updated GitHub + Azure DevOps yaml files (#145)
* Update Neo.BlockchainToolkit3 dependency

### Added

* contract invoke --results needs a mechanism to specify signers (#147)

## [2.0.37-preview] - 2021-06-04

### Changed

* Update Neo.BlockchainToolkit3 dependency

## [2.0.35-preview] - 2021-06-04

### Changed

* Neo N3 RC3 support

## [2.0.32-preview] - 2021-05-04

### Added

* Write a known message to the console once the DB lock has been acquired (#139)
* Gracefully handle multiple contracts w/ same name in contract storage (#137)
* add ProtocolSettings to trace (#141)
* Add NEP2/6 support (#142)

## [2.0.26-preview] - 2021-05-04

### Changed

* Neo N3 RC2 support

## [2.0.23-preview] - 2021-04-20

### Fixed

* Ensure node path exists before using it
* consistency in GetNodePath use

## [2.0.21-preview] - 2021-03-21

### Changed

* Neo N3 RC1 support

## [2.0.9-preview] - 2021-02-08

### Changed

* Neo 3 Preview 5 support
* Moved Neo 2 version of neo express to `master-2.x` branch for consistency with other Neo projects

## [1.2.85-insiders] - 2020-12-29

### Changed

* Neo 3 Preview 4 support

### Added

* Offline Mode
* Oracle Commands
* NEP17 tracker RPC methods

## [1.2.20-insiders] - 2020-08-03

### Added

* Debug Trace Capture support

## [1.2.16-insiders] - 2020-08-03

### Changed

* Neo 3 Preview 3 support

## [1.2.1-insiders] - 2020-06-22

### Added

* Neo 3 Preview 2 support

## [1.1.28] - 2020-05-28

- Added `--preload-gas` option to the `create` command that generates and claims a
  specified amount of GAS in the genesis account.

## [1.0.8] - 2020-02-25

### Fixed

- [don't block waiting for the user to hit 'q' when neo-express run/checkpoint run fails](https://github.com/neo-project/neo-express/issues/39)

## [1.0] - 2020-02-06

### Added

- Added `express-get-populated-blocks` RPC endpoint for use by
  [Neo Visual DevTracker](https://github.com/neo-project/neo-visual-tracker)
- Modified `show` commands to provide user friendly output. Added --json argument to
  support automation scenarios.
- Added `show gas` as an alias for `show unclaimed`
- Added `show unspent` as an alias for `show unspents`
- Updated `contract deploy` to read metadata from .abi.json file (when available)

### Changed

- Updated to .NET Core 3.1
- Bumped version number for official release of Neo Blockchain Toolkit 1.0

### Removed

- Stopped writing  deprecated `debug-port` property to .neo-express.json file. Existing
  files with a non-zero `debug-port` value will maintain the specified value, but
  Neo-Express ignores this property.

## [0.9.95] - 2019-12-02

### Fixed

- [don't attach tx input/outputs to invocations with zero fee](https://github.com/neo-project/neo-express/issues/12)
- [getclaimable txid prefixed with `0x`](https://github.com/neo-project/neo-express/issues/13)
- [neo-express can return stale data from getaccountstate](https://github.com/neo-project/neo-express/issues/21)
- [need better error message when vm faults during smart contract invocation](https://github.com/neo-project/neo-express/issues/11)

## [0.9.82] - 2019-11-13

### Added

- Added ``--json`` argument to ``contract storage`` command. This dumps the contract
  storage to the console in JSON that is compatible with the
  [Neo Smart Contract Debugger](https://github.com/neo-project/neo-debugger).

### Changed

- Fixed ExpressChain.GetAccount extension method to work as expected for
  genesis account on an neo-express instance with more than one node.

## [0.9] - 2019-11-11

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
