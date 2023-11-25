import fs from "fs";
import * as neonRpc from "@cityofzion/neon-core/lib/rpc";
import * as neonSc from "@cityofzion/neon-core/lib/sc";

import ActiveConnection from "../activeConnection";
import DetectorBase from "./detectorBase";
import JSONC from "../util/JSONC";
import Log from "../util/log";

const LOG_PREFIX = "ContractDetector";
const SEARCH_PATTERN = "**/*.nef";

type ContractMap = {
  [contractName: string]: {
    manifest: Partial<neonSc.ContractManifestJson>;
    absolutePathToNef: string;
    deploymentRequired: boolean;
    deployed: boolean;
  };
};

export default class ContractDetector extends DetectorBase {
  contracts: ContractMap = {};

  constructor(private readonly activeConnection: ActiveConnection) {
    super(SEARCH_PATTERN);

    activeConnection.onChange(async () => {
      activeConnection.connection?.blockchainMonitor.onChange(() => this.run());
      await this.run();
    });

    activeConnection.connection?.blockchainMonitor.onChange(() => {
      this.run();
    });

    this.run();
  }

  static async getContractStateByName(
    rpcClient: neonRpc.RPCClient,
    contractName: string
  ) {
    // TODO: Replace with a call to an RPC method that also exists on TestNet/MainNet
    //       See: https://github.com/neo-project/neo-modules/issues/426
    try {
      return (await rpcClient.execute(
        new neonRpc.Query({
          method: "expressgetcontractstate",
          params: [contractName],
        })
      )) as any[];
    } catch (e) {
      if (`${e.message}`.toLowerCase().indexOf("method not found") !== -1) {
        return [];
      }
      throw e;
    }
  }

  async processFiles() {
    let deploymentStatusChanged = false;
    const newSnapshot: ContractMap = {};
    for (const absolutePathToNef of this.files) {
      const manifest = await ContractDetector.tryGetManifest(absolutePathToNef);
      const contractName: string = (manifest as any)?.name || "";
      if (manifest && contractName) {
        let deploymentRequired = false;
        let deployed = false;
        const connection = this.activeConnection.connection;
        if (connection) {
          try {
            const result = await ContractDetector.getContractStateByName(
              connection.rpcClient,
              contractName
            );
            if (result.length) {
              deployed = true;
            } else {
              deploymentRequired = true;
            }
          } catch (e) {
            Log.warn(
              LOG_PREFIX,
              "Could not query for contract",
              contractName,
              absolutePathToNef,
              "Error:",
              e.message
            );
          }
        }
        if (
          this.contracts[contractName] &&
          (this.contracts[contractName].deploymentRequired !==
            deploymentRequired ||
            this.contracts[contractName].deployed !== deployed)
        ) {
          deploymentStatusChanged = true;
        }
        newSnapshot[contractName] = {
          manifest,
          absolutePathToNef,
          deploymentRequired,
          deployed,
        };
      }
    }
    this.contracts = newSnapshot;
    return deploymentStatusChanged;
  }

  static async tryGetManifest(fullPathToNef: string) {
    try {
      const fullPathToManifest = fullPathToNef.replace(
        /\.nef$/,
        ".manifest.json"
      );
      return JSONC.parse(
        (await fs.promises.readFile(fullPathToManifest)).toString()
      ) as Partial<neonSc.ContractManifestJson>;
    } catch (e) {
      Log.warn(LOG_PREFIX, "Error parsing", fullPathToNef, e.message);
      return undefined;
    }
  }

  private async run() {
    try {
      if (await this.processFiles()) {
        this.onChangeEmitter.fire();
      }
    } catch (e) {
      Log.error(LOG_PREFIX, "Unexpected error", e.message);
    }
  }
}
