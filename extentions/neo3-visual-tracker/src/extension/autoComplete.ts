import * as fs from "fs";
import * as neonSc from "@cityofzion/neon-core/lib/sc";
import * as temp from "temp";
import * as vscode from "vscode";

import ActiveConnection from "./activeConnection";
import AutoCompleteData from "../shared/autoCompleteData";
import BlockchainIdentifier from "./blockchainIdentifier";
import ContractDetector from "./fileDetectors/contractDetector";
import dedupeAndSort from "./util/dedupeAndSort";
import Log from "./util/log";
import NeoExpress from "./neoExpress/neoExpress";
import NeoExpressDetector from "./fileDetectors/neoExpressDetector";
import NeoExpressIo from "./neoExpress/neoExpressIo";
import WalletDetector from "./fileDetectors/walletDetector";

const LOG_PREFIX = "AutoComplete";

export default class AutoComplete {
  onChange: vscode.Event<AutoCompleteData>;

  private readonly onChangeEmitter: vscode.EventEmitter<AutoCompleteData>;
  private readonly wellKnownNames: { [hash: string]: string };

  private latestData: AutoCompleteData;

  private readonly wellKnownManifests: {
    [contractHash: string]: Partial<neonSc.ContractManifestJson>;
  } = {};

  private readonly cachedManifests: {
    [contractHash: string]: Partial<neonSc.ContractManifestJson> | null;
  } = {};

  get data() {
    return this.latestData;
  }

  constructor(
    private readonly context: vscode.ExtensionContext,
    private readonly neoExpress: NeoExpress,
    private readonly activeConnection: ActiveConnection,
    private readonly contractDetector: ContractDetector,
    private readonly walletDetector: WalletDetector,
    neoExpressDetector: NeoExpressDetector
  ) {
    this.latestData = {
      contractManifests: {},
      contractNames: {},
      contractPaths: {},
      wellKnownAddresses: {},
      addressNames: {},
    };
    this.onChangeEmitter = new vscode.EventEmitter<AutoCompleteData>();
    this.onChange = this.onChangeEmitter.event;
    this.wellKnownNames = {};
    this.initializeWellKnownManifests();
    activeConnection.onChange(async () => {
      activeConnection.connection?.blockchainMonitor.onChange(() =>
        this.update("blockchain change detected")
      );
      await this.update("blockchain connection changed");
    });
    activeConnection.connection?.blockchainMonitor.onChange(() =>
      this.update("blockchain change detected (initial connection)")
    );
    contractDetector.onChange(() => this.update("contracts changed"));
    walletDetector.onChange(() => this.update("wallets changed"));
    neoExpressDetector.onChange(() =>
      this.update("neo-express instances changed")
    );
    this.update("initial population required");
  }

  dispose() {
    this.onChangeEmitter.dispose();
  }

  private async initializeWellKnownManifests() {
    Log.log(LOG_PREFIX, "Initializing well-known manifests...");
    const tempFile = await new Promise<temp.OpenFile>((resolve, reject) =>
      temp.open({ suffix: ".neo-express" }, (err, result) =>
        err ? reject(err) : resolve(result)
      )
    );
    let wellKnownContracts: {
      [name: string]: { hash: string; manifest: neonSc.ContractManifestJson };
    } = {};
    try {
      const versionResult = await this.neoExpress.run("-v");
      let cacheKey = "";
      if (versionResult.isError) {
        Log.error(LOG_PREFIX, "Could not determine neo-express version");
      } else {
        const version = versionResult.message.trim().substring(256);
        cacheKey = `wellKnownContracts_${version}`;
        wellKnownContracts = this.context.globalState.get<
          typeof wellKnownContracts
        >(cacheKey, wellKnownContracts);
      }
      if (Object.keys(wellKnownContracts).length) {
        Log.log(LOG_PREFIX, "Using cache");
      } else {
        Log.log(LOG_PREFIX, "Creating temporary instance");
        fs.closeSync(tempFile.fd);
        await fs.promises.unlink(tempFile.path);
        const result = await this.neoExpress.run(
          "create",
          "-f",
          "-c",
          "1",
          tempFile.path
        );
        const identifier = await BlockchainIdentifier.fromNeoExpressConfig(
          this.context.extensionPath,
          tempFile.path
        );
        if (!identifier || result.isError) {
          Log.error(
            LOG_PREFIX,
            "Could not create temporary neo-express instance, built-in contract manifests will be unavailable",
            identifier,
            result.message
          );
        } else {
          const contractList = await NeoExpressIo.contractList(
            this.neoExpress,
            identifier
          );
          for (const contractName of Object.keys(contractList)) {
            const contract = contractList[contractName];
            const manifest = await NeoExpressIo.contractGet(
              this.neoExpress,
              identifier,
              contract.hash
            );
            if (manifest) {
              wellKnownContracts[contractName] = {
                hash: contract.hash,
                manifest,
              };
            }
          }
          if (cacheKey) {
            await this.context.globalState.update(cacheKey, wellKnownContracts);
          }
        }
      }
      for (const wellKnownContractName of Object.keys(wellKnownContracts)) {
        const wellKnownContract = wellKnownContracts[wellKnownContractName];
        this.wellKnownNames[wellKnownContract.hash] = wellKnownContractName;
        this.wellKnownManifests[wellKnownContract.hash] =
          wellKnownContract.manifest;
      }
    } catch (e) {
      Log.error(
        LOG_PREFIX,
        "Error initializing well-known manifests...",
        e.message
      );
    } finally {
      try {
        await fs.promises.unlink(tempFile.path);
      } catch {}
      Log.log(LOG_PREFIX, "Finished initializing well-known manifests...");
      await this.update("finished initializing well-known manifests");
    }
  }

  private async update(reason: string) {
    Log.log(LOG_PREFIX, `Computing updated AutoCompleteData because ${reason}`);

    const newData: AutoCompleteData = {
      contractManifests: { ...this.wellKnownManifests },
      contractPaths: {},
      contractNames: { ...this.wellKnownNames },
      wellKnownAddresses: {},
      addressNames: {},
    };

    const wallets = [...this.walletDetector.wallets];
    for (const wallet of wallets) {
      for (const account of wallet.accounts) {
        newData.addressNames[account.address] =
          newData.addressNames[account.address] || [];
        newData.addressNames[account.address].push(
          account.label || wallet.path
        );
        newData.addressNames[account.address] = dedupeAndSort(
          newData.addressNames[account.address]
        );
      }
    }

    const workspaceContracts = Object.values(this.contractDetector.contracts);
    for (const workspaceContract of workspaceContracts) {
      const manifest = workspaceContract.manifest;
      const contractName = (manifest as any)?.name;
      const contractPath = workspaceContract.absolutePathToNef;
      if (contractName) {
        newData.contractManifests[contractName] = manifest;
        newData.contractPaths[contractName] =
          newData.contractPaths[contractName] || [];
        newData.contractPaths[contractName].push(contractPath);
        newData.contractPaths[contractName] = dedupeAndSort(
          newData.contractPaths[contractName]
        );
      }
    }

    const connection = this.activeConnection.connection;

    newData.wellKnownAddresses =
      (await connection?.blockchainIdentifier.getWalletAddresses()) || {};

    for (const walletName of Object.keys(newData.wellKnownAddresses)) {
      const walletAddress = newData.wellKnownAddresses[walletName];
      newData.addressNames[walletAddress] =
        newData.addressNames[walletAddress] || [];
      newData.addressNames[walletAddress].push(walletName);
      newData.addressNames[walletAddress] = dedupeAndSort(
        newData.addressNames[walletAddress]
      );
    }

    if (connection?.blockchainIdentifier?.blockchainType === "express") {
      try {
        const deployedContracts = await NeoExpressIo.contractList(
          this.neoExpress,
          connection.blockchainIdentifier
        );
        for (const contractName of Object.keys(deployedContracts)) {
          const deployedContract = deployedContracts[contractName];
          const contractHash = deployedContract.hash;
          if (!this.cachedManifests[contractHash]) {
            this.cachedManifests[contractHash] = await NeoExpressIo.contractGet(
              this.neoExpress,
              connection.blockchainIdentifier,
              contractHash
            );
          }
          const manifest = this.cachedManifests[contractHash];
          if (manifest) {
            newData.contractManifests[contractHash] = manifest;
            newData.contractManifests[contractName] = manifest;
          }
          newData.contractNames[contractHash] = contractName;
        }
      } catch (e) {
        Log.warn(
          LOG_PREFIX,
          "Could not list neo-express contracts",
          connection.blockchainIdentifier.configPath,
          e.message
        );
      }
    }

    const changed = JSON.stringify(this.latestData) !== JSON.stringify(newData);

    this.latestData = newData;

    if (changed) {
      this.onChangeEmitter.fire(newData);
    }
  }
}
