import BlockchainMonitor from "./blockchainMonitor";
import BlockchainMonitorInternal from "./blockchainMonitorInternal";

export default class BlockchainMonitorPool {
  private readonly monitors: {
    [rpcUrl: string]:
      | { refCount: number; ref: BlockchainMonitorInternal }
      | undefined;
  };

  constructor() {
    this.monitors = {};
  }

  getMonitor(rpcUrl: string) {
    let monitorRef = this.monitors[rpcUrl] || {
      refCount: 0,
      ref: BlockchainMonitorInternal.createForPool(rpcUrl, () => {
        monitorRef.refCount--;
        if (monitorRef.refCount <= 0) {
          this.monitors[rpcUrl] = undefined;
          monitorRef.ref.dispose(true);
        }
      }),
    };
    monitorRef.refCount++;
    this.monitors[rpcUrl] = monitorRef;
    return new BlockchainMonitor(monitorRef.ref);
  }
}
