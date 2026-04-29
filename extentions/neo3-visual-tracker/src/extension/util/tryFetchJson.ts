import JSONC from "./JSONC";
import Log from "./log";

const LOG_PREFIX = "tryFetchJson";

// Attempts to retrieve a URL using a HTTP GET request. Expects the server to respond with
// a HTTP 200 status code and provide valid JSONC in the response body. Upon success, the
// parsed JSONC object is returned. Upon failure, a warning is logged and an empty object
// is returned.
export default async function tryFetchJson(
  protocol: "https" | "http",
  host: string,
  path: string
): Promise<any> {
  const url = `${protocol}://${host}${encodeURI(path)}`;
  try {
    const response = await fetch(url);
    if (response.status !== 200) {
      Log.warn(
        LOG_PREFIX,
        `Got HTTP code ${response.status} when attempting to download ${url}`
      );
      return {};
    }

    const content = await response.text();
    try {
      return JSONC.parse(content);
    } catch (e : any) {
      Log.warn(LOG_PREFIX, `Exception ("${e}") when parsing JSON from ${url}`);
      return {};
    }
  } catch (e : any) {
    Log.warn(LOG_PREFIX, `Error ("${e}") when sending request to ${url}`);
    return {};
  }
}
