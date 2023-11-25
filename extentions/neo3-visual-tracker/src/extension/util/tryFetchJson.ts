import * as wget from "wget-improved";

import JSONC from "./JSONC";
import Log from "./log";

const LOG_PREFIX = "tryFetchJson";

// Attempts to retrieve a URL using a HTTP GET request. Expects the server to respond with
// a HTTP 200 status code and provide valid JSONC in the response body. Upon success, the
// parsed JSONC object is returned. Upon failure, a warning is logged and an empty object
// is returned.
export default function tryFetchJson(
  protocol: "https" | "http",
  host: string,
  path: string
): any {
  return new Promise((resolve) => {
    const request = wget.request(
      { host, method: "GET", path: encodeURI(path), protocol },
      (response) => {
        if (response.statusCode !== 200) {
          Log.warn(
            LOG_PREFIX,
            `Got HTTP code ${response.statusCode} when attempting to download ${protocol}://${host}${path}`
          );
          resolve({});
        } else {
          let content = "";
          response.on("error", (err) => {
            Log.warn(
              LOG_PREFIX,
              `Error ("${err}") when processing response from ${protocol}://${host}${path}`
            );
            resolve({});
          });
          response.on("data", (data) => {
            content = content + data;
          });
          response.on("end", () => {
            try {
              resolve(JSONC.parse(content));
            } catch (e) {
              Log.warn(
                LOG_PREFIX,
                `Exception ("${e}") when parsing JSON from ${protocol}://${host}${path}`
              );
              resolve({});
            }
          });
        }
      }
    );
    request.on("error", (err) => {
      Log.warn(
        LOG_PREFIX,
        `Error ("${err}") when sending request to ${protocol}://${host}${path}`
      );
      resolve({});
    });
    request.end();
  });
}
