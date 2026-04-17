import { AdminApiError } from "../api/admin-api";
import type { ProblemDetails } from "../types/admin-contracts";

export interface ProblemMessage {
  title: string;
  detail: string;
  actionHint: string;
}

function hasText(value: string | undefined | null, fragment: string): boolean {
  return value?.toLowerCase().includes(fragment.toLowerCase()) ?? false;
}

export function mapAdminProblem(problem: ProblemDetails | null, fallbackStatus?: number): ProblemMessage {
  const status = problem?.status ?? fallbackStatus ?? 0;
  const detail = problem?.detail?.trim();

  if (status === 401) {
    return {
      title: "Сессия недействительна",
      detail: detail ?? "Выполните вход заново и повторите действие.",
      actionHint: "Получите новый CSRF token и авторизуйтесь повторно.",
    };
  }

  if (status === 403) {
    return {
      title: "Недостаточно прав",
      detail: detail ?? "Текущая учетная запись не может выполнять это действие.",
      actionHint: "Проверьте наличие permission `enrollments.read` или `enrollments.write`.",
    };
  }

  if (status === 404) {
    return {
      title: "Объект не найден",
      detail: detail ?? "Backend не нашел enrollment или application client для указанного контекста.",
      actionHint: "Проверьте tenant, external user и при необходимости application client.",
    };
  }

  if (status === 409 && hasText(detail, "multiple active application clients")) {
    return {
      title: "Нужен application client",
      detail: detail ?? "Tenant содержит несколько активных application clients.",
      actionHint: "Заполните `applicationClientId` явно и повторите start.",
    };
  }

  if (status === 409 && hasText(detail, "too many invalid")) {
    return {
      title: "Лимит попыток исчерпан",
      detail: detail ?? "Подтверждение заблокировано после неудачных попыток.",
      actionHint: "Запустите новый enrollment или replacement и подтвердите новым кодом.",
    };
  }

  if (status === 409) {
    return {
      title: "Состояние изменилось",
      detail: detail ?? "Enrollment уже находится в другом состоянии.",
      actionHint: "Обновите current state и повторите допустимое действие.",
    };
  }

  if (status === 422) {
    return {
      title: "Действие отклонено policy",
      detail: detail ?? "Текущий policy contour не разрешает этот enrollment action.",
      actionHint: "Проверьте tenant policy и operator контекст.",
    };
  }

  return {
    title: problem?.title?.trim() || "Операция не выполнена",
    detail: detail ?? "Backend вернул непредвиденную ошибку.",
    actionHint: "Повторите действие или проверьте backend logs.",
  };
}

export function mapAdminError(error: unknown): ProblemMessage {
  if (error instanceof AdminApiError) {
    return mapAdminProblem(error.problem, error.status);
  }

  return {
    title: "Связь с backend недоступна",
    detail: error instanceof Error && error.message.trim()
      ? error.message
      : "Не удалось выполнить запрос к admin runtime.",
    actionHint: "Проверьте доступность backend и повторите действие.",
  };
}
