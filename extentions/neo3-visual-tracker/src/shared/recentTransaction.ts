import { TransactionJson } from "@cityofzion/neon-core/lib/tx";

import ApplicationLog from "./applicationLog";
import TransactionStatus from "./transactionStatus";

type RecentTransaction = {
  blockchain: string;
  log?: ApplicationLog;
  txid: string;
  state: TransactionStatus;
  tx?: TransactionJson;
};

export default RecentTransaction;
