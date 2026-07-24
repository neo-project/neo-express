import { TransactionJson } from "@cityofzion/neon-core/lib/tx";

import ApplicationLog from "./applicationLog";
import TransactionStatus from "./transactionStatus";

type RecentTransaction = {
  account?: string;
  blockchain: string;
  log?: ApplicationLog;
  operation?: string;
  txid: string;
  state: TransactionStatus;
  submittedAt?: string;
  tx?: TransactionJson;
};

export default RecentTransaction;
