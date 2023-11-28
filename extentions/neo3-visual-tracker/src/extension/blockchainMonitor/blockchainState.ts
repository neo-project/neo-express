import * as bitset from "bitset";
import * as neonTypes from "@cityofzion/neon-core/lib/types";
import * as neonTx from "@cityofzion/neon-core/lib/tx";

const MIN_REFRESH_INTERVAL_MS = 1000;
const INITIAL_REFRESH_INTERVAL_MS = 5000;
const MAX_REFRESH_INTERVAL_MS = 1000 * 30;

export default class BlockchainState {
  public readonly blockTimes: number[];
  public readonly cachedBlocks: neonTypes.BlockJson[];
  public readonly cachedLogs: any[];
  public readonly cachedTransactions: neonTx.TransactionJson[];
  public readonly populatedBlocks: bitset.BitSet;

  public isHealthy: boolean;
  public lastKnownBlockHeight: number;

  constructor(public readonly lastKnownCacheId: string = "") {
    this.blockTimes = [];
    this.cachedBlocks = [];
    this.cachedLogs = [];
    this.cachedTransactions = [];
    this.populatedBlocks = new bitset.default();
    this.isHealthy = false;
    this.lastKnownBlockHeight = 0;

    // Always consider the genesis block as "populated" (even though technically
    // it has zero transactions, it is an significant part of the chain history):
    this.populatedBlocks.set(0);
  }

  currentRefreshInterval() {
    let differencesSum: number = 0;
    let differencesCount: number = 0;
    let previous: number | undefined = undefined;
    for (const timestamp of this.blockTimes) {
      if (previous !== undefined) {
        differencesSum += previous - timestamp;
        differencesCount++;
      }
      previous = timestamp;
    }
    if (differencesCount < 3) {
      return INITIAL_REFRESH_INTERVAL_MS;
    }
    return Math.min(
      MAX_REFRESH_INTERVAL_MS,
      Math.max(
        Math.round((1.0 / 2.0) * (differencesSum / differencesCount)),
        MIN_REFRESH_INTERVAL_MS
      )
    );
  }
}
