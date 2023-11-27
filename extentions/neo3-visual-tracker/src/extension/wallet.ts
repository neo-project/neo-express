import * as fs from "fs";
import * as neonCore from "@cityofzion/neon-core";

import JSONC from "./util/JSONC";
import Log from "./util/log";

const LOG_PREFIX = "Wallet";

export default class Wallet {
  static async fromJsonFile(path: string): Promise<Wallet | undefined> {
    try {
      const json = JSONC.parse((await fs.promises.readFile(path)).toString());
      if (
        json.name === undefined ||
        json.version === undefined ||
        json.scrypt === undefined ||
        json.accounts === undefined
      ) {
        // Probably not a wallet
        return undefined;
      }
      const result = new Wallet(path, new neonCore.wallet.Wallet(json));
      await result.tryUnlockWithoutPassword();
      return result;
    } catch (e) {
      Log.debug(LOG_PREFIX, "Not a wallet", e.message, path);
      return undefined;
    }
  }

  constructor(
    public readonly path: string,
    private readonly wallet: neonCore.wallet.Wallet
  ) {}

  get accounts() {
    return [...this.wallet.accounts];
  }

  async tryUnlockWithoutPassword() {
    for (let i = 0; i < this.wallet.accounts.length; i++) {
      try {
        if (await this.wallet.decrypt(i, "")) {
          Log.log(
            LOG_PREFIX,
            this.wallet.name,
            "Unlocked account without password:",
            this.wallet.accounts[i].label
          );
        } else {
          Log.log(
            LOG_PREFIX,
            this.wallet.name,
            "Account is password protected:",
            this.wallet.accounts[i].label
          );
        }
      } catch (e) {
        if (`${e.message}`.toLowerCase().indexOf("wrong password") !== -1) {
          Log.log(
            LOG_PREFIX,
            this.wallet.name,
            "Account is password protected:",
            this.wallet.accounts[i].label,
            e.message
          );
        } else {
          Log.warn(
            LOG_PREFIX,
            this.wallet.name,
            "Unexpected error decrypting account",
            this.wallet.accounts[i].label,
            e.message
          );
        }
      }
    }
  }
}
