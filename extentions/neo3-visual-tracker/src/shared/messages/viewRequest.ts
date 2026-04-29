type ViewRequest = {
  retrieveViewState?: boolean;
  typedRequest?: Record<string, unknown>;
};

function isRecord(value: unknown): value is Record<string, unknown> {
  return typeof value === "object" && value !== null && !Array.isArray(value);
}

function parseViewRequest(value: unknown): ViewRequest | undefined {
  if (!isRecord(value)) {
    return undefined;
  }

  const request: ViewRequest = {};
  if (value.retrieveViewState === true) {
    request.retrieveViewState = true;
  }

  if (Object.prototype.hasOwnProperty.call(value, "typedRequest")) {
    if (!isRecord(value.typedRequest)) {
      return undefined;
    }
    request.typedRequest = value.typedRequest;
  }

  return request;
}

export { parseViewRequest };
export default ViewRequest;
