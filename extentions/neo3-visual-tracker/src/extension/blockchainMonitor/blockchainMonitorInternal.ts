import * as neonCore from "@cityofzion/neon-core";
import * as neonRpc from "@cityofzion/neon-core/lib/rpc";
import * as neonTypes from "@cityofzion/neon-core/lib/types";
import * as neonTx from "@cityofzion/neon-core/lib/tx";
import * as vscode from "vscode";

import AddressInfo from "../../shared/addressInfo";
import ApplicationLog from "../../shared/applicationLog";
import BlockchainState from "./blockchainState";
import Log from "../util/log";

const APP_LOG_CACHE_SIZE = 1024;
const BLOCK_CACHE_SIZE = 1024;
const BLOCKS_PER_QUERY = 100;
const LOG_PREFIX = "BlockchainMonitorInternal";
const MAX_RETRIES = 3;
const SCRIPTHASH_GAS = "0xd2a4cff31913016155e38e474a2c06d08be276cf";
const SCRIPTHASH_NEO = "0xef4073a0f2b305a38ec4050e4d3d28bc40ea63f5";
const SLEEP_ON_ERROR_MS = 500;
const SPEED_DETECTION_WINDOW = 4; // Analyze previous 4 block times to calculate block speed
const TRANSACTION_CACHE_SIZE = 1024;

const now = () => new Date().getTime();

let id = 0;

export default class BlockchainMonitorInternal {
  onChange: vscode.Event<number>;

  public readonly id: number;
  private readonly onChangeEmitter: vscode.EventEmitter<number>;
  private readonly rpcClient: neonCore.rpc.RPCClient;

  private disposed: boolean;
  private getPopulatedBlocksSuccess: boolean;
  private state: BlockchainState;
  private tryGetPopulatedBlocks: boolean;

  public static createForPool(url: string, onDispose: () => void) {
    return new BlockchainMonitorInternal(url, onDispose);
  }

  private constructor(
    private readonly rpcUrl: string,
    private readonly onDispose: () => void
  ) {
    this.rpcClient = new neonCore.rpc.RPCClient(rpcUrl);
    this.disposed = false;
    this.getPopulatedBlocksSuccess = false;
    this.id = id++;
    this.state = new BlockchainState();
    this.tryGetPopulatedBlocks = true;
    this.onChangeEmitter = new vscode.EventEmitter<number>();
    this.onChange = this.onChangeEmitter.event;
    this.refreshLoop();
  }

  get healthy() {
    return this.state.isHealthy;
  }

  dispose(fromPool: boolean = false) {
    if (fromPool) {
      this.disposed = true;
      this.onChangeEmitter.dispose();
    } else {
      this.onDispose();
    }
  }

  async getAddress(
    address: string,
    retryOnFailure: boolean = true
  ): Promise<AddressInfo | null> {
    let retry = 0;
    do {
      Log.log(LOG_PREFIX, `Retrieving address ${address} (attempt ${retry++})`);
      try {
        const allBalances = await this.getBalances(address);
        return {
          address,
          allBalances,
          gasBalance: allBalances[SCRIPTHASH_GAS] || 0,
          neoBalance: allBalances[SCRIPTHASH_NEO] || 0,
        };
      } catch (e) {
        Log.warn(
          LOG_PREFIX,
          `Error retrieving address ${address} (${
            e.message || "Unknown error"
          })`
        );
        if (retryOnFailure && retry < MAX_RETRIES) {
          await this.sleepBetweenRetries();
        } else {
          return null;
        }
      }
    } while (retry < MAX_RETRIES);
    return null;
  }

  async getApplicationLog(
    txid: string,
    retryonFailure: boolean = true
  ): Promise<ApplicationLog | null> {
    const cachedLog = this.state.cachedLogs.find((_) => _.txid === txid);
    if (cachedLog) {
      return cachedLog;
    }
    let retry = 0;
    do {
      Log.log(LOG_PREFIX, `Retrieving logs for ${txid} (attempt ${retry++})`);
      try {
        const result = (await this.rpcClient.execute(
          new neonRpc.Query({
            method: "getapplicationlog",
            params: [txid],
          })
        )) as ApplicationLog;
        if (this.state.cachedLogs.length === APP_LOG_CACHE_SIZE) {
          this.state.cachedLogs.shift();
        }
        this.state.cachedLogs.push(result);
        return result;
      } catch (e) {
        Log.warn(
          LOG_PREFIX,
          `Error retrieving app logs for ${txid}: ${
            e.message || "Unknown error"
          }`
        );
        if (retryonFailure && retry < MAX_RETRIES) {
          await this.sleepBetweenRetries();
        } else {
          return null;
        }
      }
    } while (retry < MAX_RETRIES);
    return null;
  }

  async getBlock(
    indexOrHash: string | number,
    retryonFailure: boolean = true
  ): Promise<neonTypes.BlockJson | null> {
    const cachedBlock = this.state.cachedBlocks.find(
      (_) => _.index === indexOrHash || _.hash === indexOrHash
    );
    if (cachedBlock) {
      return cachedBlock;
    }
    let retry = 0;
    do {
      Log.log(
        LOG_PREFIX,
        `Retrieving block ${indexOrHash} (attempt ${retry++})`
      );
      try {
        const block = await this.rpcClient.getBlock(indexOrHash, true);
        // never cache head block
        if (block.index < this.state.lastKnownBlockHeight - 1) {
          if (this.state.cachedBlocks.length === BLOCK_CACHE_SIZE) {
            this.state.cachedBlocks.shift();
          }
          this.state.cachedBlocks.push(block);
        }
        return block;
      } catch (e) {
        Log.warn(
          LOG_PREFIX,
          `Error retrieving block ${indexOrHash}: ${
            e.message || "Unknown error"
          }`
        );
        if (retryonFailure && retry < MAX_RETRIES) {
          await this.sleepBetweenRetries();
        } else {
          return null;
        }
      }
    } while (retry < MAX_RETRIES);
    return null;
  }

  async getTransaction(
    hash: string,
    retryonFailure: boolean = true
  ): Promise<neonTx.TransactionJson | null> {
    const cachedTransaction = this.state.cachedTransactions.find(
      (_) => _.hash === hash
    );
    if (cachedTransaction) {
      return cachedTransaction;
    }
    let retry = 0;
    do {
      Log.log(LOG_PREFIX, `Retrieving tx ${hash} (attempt ${retry++})`);
      try {
        const transaction = await this.rpcClient.getRawTransaction(hash, true);
        if (transaction.blockhash) {
          // only cache transactions that are non-pending
          if (this.state.cachedTransactions.length === TRANSACTION_CACHE_SIZE) {
            this.state.cachedTransactions.shift();
          }
          this.state.cachedTransactions.push(transaction);
        }
        return transaction;
      } catch (e) {
        Log.warn(
          LOG_PREFIX,
          `Error retrieving tx ${hash}: ${e.message || "Unknown error"}`
        );
        if (retryonFailure && retry < MAX_RETRIES) {
          await this.sleepBetweenRetries();
        } else {
          return null;
        }
      }
    } while (retry < MAX_RETRIES);
    return null;
  }

  isBlockPopulated(blockIndex: number) {
    return (
      !this.getPopulatedBlocksSuccess ||
      this.state.populatedBlocks.get(blockIndex)
    );
  }

  isFilterAvailable() {
    return this.getPopulatedBlocksSuccess;
  }

  private async refreshLoop() {
    if (this.disposed) {
      return;
    }
    try {
      await this.updateState();
    } catch (e) {
      Log.error(LOG_PREFIX, "Unexpected error", e.message);
    } finally {
      const refreshInterval = this.state.currentRefreshInterval();
      setTimeout(() => this.refreshLoop(), refreshInterval);
      Log.log(
        LOG_PREFIX,
        `#${this.id}`,
        `Monitoring ${this.rpcUrl}`,
        `Interval: ${refreshInterval}ms`
      );
    }
  }

  private async sleepBetweenRetries() {
    return new Promise((resolve) => setTimeout(resolve, SLEEP_ON_ERROR_MS));
  }

  private async updateState() {
    const wasHealthy = this.healthy;
    let blockHeight = this.state.lastKnownBlockHeight;
    try {
      blockHeight = await this.rpcClient.getBlockCount();
      this.state.isHealthy = true;
    } catch (e) {
      this.state.isHealthy = false;
    }

    let fireChangeEvent =
      blockHeight !== this.state.lastKnownBlockHeight ||
      this.state.isHealthy !== wasHealthy;

    if (this.tryGetPopulatedBlocks) {
      try {
        let start = blockHeight;
        let mayBeMoreResults = true;
        do {
          const count = Math.max(
            2,
            Math.min(start - this.state.lastKnownBlockHeight, BLOCKS_PER_QUERY)
          );
          const result = (await this.rpcClient.execute(
            new neonRpc.Query({
              method: "expressgetpopulatedblocks",
              params: [count, start],
            })
          )) as { blocks: number[]; cacheId: string };
          if (!this.getPopulatedBlocksSuccess) {
            this.getPopulatedBlocksSuccess = true;
            fireChangeEvent = true;
          }
          if (result.cacheId !== this.state.lastKnownCacheId) {
            Log.log(LOG_PREFIX, "Clearing cache");
            this.state = new BlockchainState(result.cacheId);
            fireChangeEvent = true;
          }
          for (const blockNumber of result.blocks) {
            if (!this.state.populatedBlocks.get(blockNumber)) {
              this.state.populatedBlocks.set(blockNumber);
              fireChangeEvent = true;
            }
          }
          start = result.blocks.length
            ? result.blocks[result.blocks.length - 1]
            : 0;
          mayBeMoreResults = result.blocks.length >= count;
        } while (mayBeMoreResults);
      } catch (e) {
        if (e.message?.indexOf("Method not found") !== -1) {
          this.tryGetPopulatedBlocks = false;
        } else {
          throw e;
        }
      }
    }

    this.state.lastKnownBlockHeight = blockHeight;

    if (fireChangeEvent) {
      this.onChangeEmitter.fire(blockHeight);
      this.state.blockTimes.unshift(now());
      this.state.blockTimes.length = Math.min(
        SPEED_DETECTION_WINDOW,
        this.state.blockTimes.length
      );
    }
  }

  private async getBalances(
    address: string
  ): Promise<{ [assetHash: string]: number }> {
    let result: { [assetHash: string]: number } = {};
    const response: any = await this.rpcClient.execute(
      new neonRpc.Query({
        method: "getnep17balances",
        params: [address, 0],
      })
    );
    if (response.balance && Array.isArray(response.balance)) {
      for (const balanceEntry of response.balance) {
        result[balanceEntry.assethash || "Unknown"] =
          parseInt(balanceEntry.amount) || 0;
      }
    }
    return result;
  }
}
