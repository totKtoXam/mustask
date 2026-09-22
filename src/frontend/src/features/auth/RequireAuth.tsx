import { Outlet } from 'react-router-dom';
import { AnonymousScreen } from './AnonymousScreen';
import { useAuth } from './AuthContext';

/**
 * Guard закрытой части приложения: пока сессия восстанавливается или её нет —
 * один из семи служебных экранов, иначе — содержимое маршрута.
 */
export function RequireAuth() {
  const { status } = useAuth();

  if (status !== 'authenticated') {
    return <AnonymousScreen />;
  }
  return <Outlet />;
}
