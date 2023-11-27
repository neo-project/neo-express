import * as neonTypes from "@cityofzion/neon-core/lib/types";
import * as neonTx from "@cityofzion/neon-core/lib/tx";
import * as vscode from "vscode";

import AddressInfo from "../../shared/addressInfo";
import ApplicationLog from "../../shared/applicationLog";
import BlockchainMonitorInternal from "./blockchainMonitorInternal";

export default class BlockchainMonitor {
  onChange: vscode.Event<number>;

  private readonly onChangeEmitter: vscode.EventEmitter<number>;

  constructor(
    private readonly blockchainMonitorInternal: BlockchainMonitorInternal
  ) {
    this.onChangeEmitter = new vscode.EventEmitter<number>();
    this.onChange = this.onChangeEmitter.event;
    blockchainMonitorInternal.onChange((n) => this.onChangeEmitter.fire(n));
  }

  get healthy() {
    return this.blockchainMonitorInternal.healthy;
  }

  dispose() {
    this.onChangeEmitter.dispose();
    this.blockchainMonitorInternal.dispose();
  }

  getAddress(
    address: string,
    retryOnFailure: boolean = true
  ): Promise<AddressInfo | null> {
    return this.blockchainMonitorInternal.getAddress(address, retryOnFailure);
  }

  getBlock(
    indexOrHash: string | number,
    retryonFailure: boolean = true
  ): Promise<neonTypes.BlockJson | null> {
    return this.blockchainMonitorInternal.getBlock(indexOrHash, retryonFailure);
  }

  async getTransaction(
    hash: string,
    retryonFailure: boolean = true
  ): Promise<{ tx: neonTx.TransactionJson; log?: ApplicationLog } | null> {
    const tx = await this.blockchainMonitorInternal.getTransaction(
      hash,
      retryonFailure
    );
    if (tx) {
      const log = await this.blockchainMonitorInternal.getApplicationLog(
        hash,
        false
      );
      return { tx, log: log || undefined };
    }
    return null;
  }

  isBlockPopulated(blockIndex: number) {
    return this.blockchainMonitorInternal.isBlockPopulated(blockIndex);
  }

  isFilterAvailable() {
    return this.blockchainMonitorInternal.isFilterAvailable();
  }
}
