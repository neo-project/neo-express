import { useState, useLayoutEffect } from "react";

export default function useWindowHeight() {
  const [height, setHeight] = useState(window.innerHeight);
  useLayoutEffect(() => {
    const onresize = () => setHeight(window.innerHeight);
    window.addEventListener("resize", onresize);
    return () => window.removeEventListener("resize", onresize);
  }, []);
  return height;
}
