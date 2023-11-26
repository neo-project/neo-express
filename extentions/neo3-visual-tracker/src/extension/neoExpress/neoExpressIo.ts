import * as neonSc from "@cityofzion/neon-core/lib/sc";

import BlockchainIdentifier from "../blockchainIdentifier";
import JSONC from "../util/JSONC";
import Log from "../util/log";
import NeoExpress from "./neoExpress";

const LOG_PREFIX = "NeoExpressIo";

export default class NeoExpressIo {
  static async contractGet(
    neoExpress: NeoExpress,
    identifer: BlockchainIdentifier,
    hashOrNefPath: string
  ): Promise<neonSc.ContractManifestJson | null> {
    if (identifer.blockchainType !== "express") {
      return null;
    }
    const output = await neoExpress.run(
      "contract",
      "get",
      hashOrNefPath,
      "-i",
      identifer.configPath
    );
    if (output.isError || !output.message) {
      return null;
    }
    try {
      return JSONC.parse(output.message) as neonSc.ContractManifestJson;
    } catch (e) {
      throw Error(`Get contract error: ${e.message}`);
    }
  }

  static async contractList(
    neoExpress: NeoExpress,
    identifer: BlockchainIdentifier
  ): Promise<{
    [name: string]: { hash: string };
  }> {
    if (identifer.blockchainType !== "express") {
      return {};
    }
    const output = await neoExpress.run(
      "contract",
      "list",
      "-i",
      identifer.configPath,
      "--json"
    );
    if (output.isError) {
      Log.error(LOG_PREFIX, "List contract invoke error", output.message);
      return {};
    }
    try {
      let result: {
        [name: string]: { hash: string };
      } = {};
      let contractSummaries = JSONC.parse(output.message);
      for (const contractSummary of contractSummaries) {
        const hash = contractSummary.hash;
        result[contractSummary.name] = { hash };
      }
      return result;
    } catch (e) {
      throw Error(`List contract parse error: ${e.message}`);
    }
  }

  static async contractStorage(
    neoExpress: NeoExpress,
    identifer: BlockchainIdentifier,
    contractName: string
  ): Promise<{ key?: string; value?: string; constant?: boolean }[]> {
    if (identifer.blockchainType !== "express") {
      return [];
    }
    const output = await neoExpress.run(
      "contract",
      "storage",
      contractName,
      "-i",
      identifer.configPath,
      "--json"
    );
    if (output.isError) {
      Log.error(LOG_PREFIX, "Contract storage retrieval error", output.message);
      throw Error(output.message);
    }
    try {
      return (
        ((JSONC.parse(output.message).storages || []) as {
          key?: string;
          value?: string;
          constant?: boolean;
        }[]) || []
      );
    } catch (e) {
      throw Error(`Contract storage parse error: ${e.message}`);
    }
  }
}
