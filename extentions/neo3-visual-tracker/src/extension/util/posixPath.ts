import * as path from "path";

export default function posixPath(...components: string[]) {
  return path
    .join(...components)
    .split(path.sep)
    .join(path.posix.sep);
}
