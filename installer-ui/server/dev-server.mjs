import { spawn } from "node:child_process";
import { dirname, resolve } from "node:path";
import { fileURLToPath } from "node:url";

const currentDirectoryPath = dirname(fileURLToPath(import.meta.url));
const packageRootPath = resolve(currentDirectoryPath, "..");
const viteEntryPath = resolve(packageRootPath, "node_modules", "vite", "bin", "vite.js");
const mockMode = process.argv.includes("--mock");
const childProcesses = [];
let shuttingDown = false;

const terminateChildren = (signal) => {
  if (shuttingDown) {
    return;
  }

  shuttingDown = true;
  for (const child of childProcesses) {
    if (!child.killed) {
      child.kill(signal);
    }
  }
};

const startProcess = (command, argumentsList) => {
  const child = spawn(command, argumentsList, {
    cwd: packageRootPath,
    stdio: "inherit",
  });

  childProcesses.push(child);
  child.on("exit", (code, signal) => {
    if (shuttingDown) {
      return;
    }

    terminateChildren(signal ?? "SIGTERM");
    process.exit(code ?? 1);
  });

  return child;
};

startProcess("node", [
  resolve(currentDirectoryPath, "installer-bridge.mjs"),
  ...(mockMode ? ["--mock"] : []),
]);

startProcess("node", [
  viteEntryPath,
  "--host",
  "127.0.0.1",
  "--port",
  "4181",
]);

process.on("SIGINT", () => {
  terminateChildren("SIGINT");
  process.exit(0);
});

process.on("SIGTERM", () => {
  terminateChildren("SIGTERM");
  process.exit(0);
});
