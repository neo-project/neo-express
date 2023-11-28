import TypedValue from "./typedValue";

type ApplicationLog = {
  txid?: string;
  executions?: Partial<{
    trigger: string;
    vmstate: string;
    exception: string | null;
    gasconsumed: string;
    stack: TypedValue[];
    notifications: Partial<{
      contract: string;
      eventname: string;
      state: TypedValue;
    }>[];
  }>[];
};

export default ApplicationLog;
