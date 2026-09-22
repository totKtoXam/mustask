import type { ReactNode } from 'react';
import { useAuth } from '../../auth/AuthContext';

/**
 * Ровно один из двух режимов обязателен — типом, а не соглашением: раньше `any`/`all` были оба опциональны, и забытый или
 * опечатанный проп означал пустой список условий, который проверки читали
 * как «условий нет — пускаем» (fail open). Компонент, задуманный как первый
 * и переиспользуемый route-level гейт приложения, не должен разрешать
 * доступ по умолчанию.
 */
type RequirePermissionProps =
  | { any: string[]; all?: never; fallback: ReactNode; children: ReactNode }
  | { all: string[]; any?: never; fallback: ReactNode; children: ReactNode };

/**
 * Первый в приложении route-level гейт по правам. Всегда
 * через `useAuth().can(code)` — никогда по имени роли. Не
 * заменяет `RequireAuth` (тот — про сессию, этот — про право), а
 * оборачивается внутрь него.
 */
export function RequirePermission(props: RequirePermissionProps) {
  const { fallback, children } = props;
  const { can } = useAuth();

  // Защита в глубину поверх типовой гарантии выше: пустой список тоже
  // отказывает, и даже проп, обойдённый мимо типов вовсе (ни `any`, ни
  // `all` не передан), не падает и не пускает — читается как «условий нет».
  const anyList = 'any' in props ? props.any : undefined;
  const allList = 'all' in props ? props.all : undefined;
  const ok = anyList
    ? anyList.length > 0 && anyList.some((code) => can(code))
    : allList
      ? allList.length > 0 && allList.every((code) => can(code))
      : false;

  if (!ok) {
    return <>{fallback}</>;
  }

  return <>{children}</>;
}
