// Returns true only when the error actually reports an unsupported RPC method.
// A missing/non-string message must NOT be treated as "method not found": doing
// so would permanently disable the populated-blocks query and swallow a transient
// error instead of letting the refresh loop retry it.
export default function isMethodNotFoundError(e: unknown): boolean {
  const message = (e as { message?: unknown } | null | undefined)?.message;
  return typeof message === "string" && message.indexOf("Method not found") !== -1;
}
