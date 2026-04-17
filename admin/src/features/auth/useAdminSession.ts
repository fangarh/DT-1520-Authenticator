import { startTransition, useEffect, useState } from "react";
import { adminApi } from "../../shared/api/admin-api";
import { mapAdminError } from "../../shared/problem/problem-messages";
import type { AdminSession } from "../../shared/types/admin-contracts";

type SessionStatus = "bootstrapping" | "anonymous" | "authenticated";

interface SessionState {
  status: SessionStatus;
  current: AdminSession | null;
  pending: boolean;
  error: string | null;
}

export function useAdminSession() {
  const [state, setState] = useState<SessionState>({
    status: "bootstrapping",
    current: null,
    pending: false,
    error: null,
  });

  useEffect(() => {
    let isActive = true;

    void adminApi.getSession()
      .then((current) => {
        if (!isActive) {
          return;
        }

        startTransition(() => {
          setState({
            status: "authenticated",
            current,
            pending: false,
            error: null,
          });
        });
      })
      .catch((error: unknown) => {
        if (!isActive) {
          return;
        }

        const message = mapAdminError(error);
        startTransition(() => {
          setState({
            status: "anonymous",
            current: null,
            pending: false,
            error: error instanceof Error && "status" in error && (error as { status?: number }).status === 401
              ? null
              : message.detail,
          });
        });
      });

    return () => {
      isActive = false;
    };
  }, []);

  async function login(username: string, password: string) {
    setState((current) => ({ ...current, pending: true, error: null }));
    try {
      const session = await adminApi.login(username, password);
      startTransition(() => {
        setState({
          status: "authenticated",
          current: session,
          pending: false,
          error: null,
        });
      });
    } catch (error) {
      const message = mapAdminError(error);
      startTransition(() => {
        setState({
          status: "anonymous",
          current: null,
          pending: false,
          error: `${message.title}. ${message.detail}`,
        });
      });
    }
  }

  async function logout() {
    setState((current) => ({ ...current, pending: true, error: null }));
    try {
      await adminApi.logout();
      startTransition(() => {
        setState({
          status: "anonymous",
          current: null,
          pending: false,
          error: null,
        });
      });
    } catch (error) {
      const message = mapAdminError(error);
      startTransition(() => {
        setState((current) => ({
          ...current,
          pending: false,
          error: `${message.title}. ${message.detail}`,
        }));
      });
    }
  }

  return {
    ...state,
    login,
    logout,
  };
}
