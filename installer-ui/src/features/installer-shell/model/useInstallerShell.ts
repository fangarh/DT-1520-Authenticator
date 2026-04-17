import { startTransition, useEffect, useState } from "react";
import { installerApi, InstallerApiError } from "../../../shared/api/installer-api";
import type {
  InstallerExecutionReport,
  InstallerNoticeTone,
  InstallerShellInfo,
} from "../../../shared/types/installer-contracts";
import {
  createInstallerRunDraft,
  type InstallerPermission,
  type InstallerRunDraft,
  shouldRequestBootstrapPassword,
} from "./installer-run-draft";

interface InstallerNotice {
  tone: InstallerNoticeTone;
  title: string;
  detail: string;
  actionHint?: string;
}

const buildNotice = (report: InstallerExecutionReport): InstallerNotice => {
  if (report.preflightOnly) {
    return {
      tone: "success",
      title: "Preflight checks passed",
      detail: "The local shell verified installer prerequisites without executing runtime-changing steps.",
      actionHint: "Turn off Preflight only when you are ready to execute the actual install, update or recover flow.",
    };
  }

  if (report.dryRun) {
    return {
      tone: "success",
      title: "Dry-run plan is ready",
      detail: "The bridge returned the planned installer contract without changing runtime state.",
      actionHint: "Review the planned steps and rerun the same mode without Dry run for live execution.",
    };
  }

  if (report.outcome === "failed") {
    return {
      tone: "danger",
      title: "Installer finished with a failed report",
      detail: report.failureMessage ?? "Review validation issues and failed steps before retrying.",
      actionHint: "The shell still preserves the sanitized engine report so the operator can inspect exactly where the run stopped.",
    };
  }

  if (report.outcome === "degraded") {
    return {
      tone: "neutral",
      title: "Installer finished, but diagnostics are degraded",
      detail: "Runtime came up, yet the engine reported worker or service warnings that still need operator action.",
      actionHint: "Check troubleshooting hints before handing off to the runtime Admin UI.",
    };
  }

  return {
    tone: "success",
    title: "Installer flow completed cleanly",
    detail: "The loopback shell received a sanitized success report from the script-first engine and can hand off to runtime operations.",
  };
};

export function useInstallerShell() {
  const [shellInfo, setShellInfo] = useState<InstallerShellInfo | null>(null);
  const [draft, setDraft] = useState<InstallerRunDraft>(() => createInstallerRunDraft(null));
  const [report, setReport] = useState<InstallerExecutionReport | null>(null);
  const [notice, setNotice] = useState<InstallerNotice | null>(null);
  const [pending, setPending] = useState(false);
  const [loadingShellInfo, setLoadingShellInfo] = useState(true);

  useEffect(() => {
    let isActive = true;

    void installerApi.getShellInfo()
      .then((info) => {
        if (!isActive) {
          return;
        }

        startTransition(() => {
          setShellInfo(info);
          setDraft((current) => current.composeFilePath ? current : createInstallerRunDraft(info));
          setLoadingShellInfo(false);
        });
      })
      .catch(() => {
        if (!isActive) {
          return;
        }

        startTransition(() => {
          setLoadingShellInfo(false);
          setNotice({
            tone: "danger",
            title: "Local bridge is unavailable",
            detail: "The installer shell could not contact the loopback bridge on startup.",
            actionHint: "Start the bridge before attempting install, update or recover flows.",
          });
        });
      });

    return () => {
      isActive = false;
    };
  }, []);

  const runInstaller = async () => {
    setPending(true);
    setNotice(null);

    try {
      const response = await installerApi.run({
        ...draft,
        composeFilePath: draft.composeFilePath.trim() || undefined,
        bootstrapAdminPassword: shouldRequestBootstrapPassword(draft) ? draft.bootstrapAdminPassword : undefined,
      });

      startTransition(() => {
        setReport(response.report);
        setDraft((current) => ({ ...current, bootstrapAdminPassword: "" }));
        setNotice(buildNotice(response.report));
      });
    } catch (error) {
      const detail = error instanceof InstallerApiError ? error.message : "Unknown installer bridge failure.";
      startTransition(() => {
        setNotice({
          tone: "danger",
          title: "Installer bridge rejected the request",
          detail,
          actionHint: "Fix the local request shape or wait for the current bridge run to finish.",
        });
      });
    } finally {
      setPending(false);
    }
  };

  return {
    shellInfo,
    draft,
    report,
    notice,
    pending,
    loadingShellInfo,
    setDraft,
    runInstaller,
    setPermissionEnabled: (permission: InstallerPermission, enabled: boolean) => {
      setDraft((current) => ({
        ...current,
        bootstrapAdminPermissions: enabled
          ? Array.from(new Set([...current.bootstrapAdminPermissions, permission]))
          : current.bootstrapAdminPermissions.filter((value) => value !== permission),
      }));
    },
  };
}
