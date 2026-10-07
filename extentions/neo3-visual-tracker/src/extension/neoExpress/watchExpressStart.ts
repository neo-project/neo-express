type PtyEvents = {
  onDidWrite: (listener: (data: string) => void) => { dispose(): void } | void;
  onDidExit: (listener: (code: number | null) => void) => { dispose(): void } | void;
};

export function watchForExpressStart(
  pty: PtyEvents,
  timeoutMs = 30000
): Promise<boolean> {
  return new Promise((resolve) => {
    let settled = false;
    const finish = (value: boolean) => {
      if (!settled) {
        settled = true;
        resolve(value);
      }
    };
    const timeout = setTimeout(() => finish(false), timeoutMs);
    pty.onDidWrite((data) => {
      if (data.indexOf("Neo express is running") !== -1) {
        clearTimeout(timeout);
        finish(true);
      }
    });
    pty.onDidExit((code) => {
      clearTimeout(timeout);
      finish(code === 0);
    });
  });
}
