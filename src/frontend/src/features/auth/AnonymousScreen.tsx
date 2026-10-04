import type { ReactNode } from 'react';
import Alert from '@mui/material/Alert';
import Box from '@mui/material/Box';
import Button from '@mui/material/Button';
import Card from '@mui/material/Card';
import CardContent from '@mui/material/CardContent';
import Typography from '@mui/material/Typography';
import { PRODUCT_NAME } from '../../shared/config/product';
import { useDocumentTitle } from '../../shared/lib/documentTitle';
import { LoadingBox } from '../../shared/ui/LoadingBox';
import { useAuth } from './AuthContext';

/**
 * The frame of an auth screen: outside the shell, on the application
 * background, a centred card headed by the product name (mod-015 design
 * § 11.3, C-15). The browser tab reads «Mustask» in every state.
 */
function Shell({ children }: { children: ReactNode }) {
  useDocumentTitle(null);
  return (
    <Box
      sx={{
        minHeight: '100vh',
        display: 'flex',
        alignItems: 'center',
        justifyContent: 'center',
        bgcolor: 'background.default',
        px: 2,
      }}
    >
      <Card
        variant="outlined"
        sx={{ width: (theme) => theme.layout.authCardWidth, maxWidth: '100%' }}
      >
        <CardContent sx={{ p: 4 }}>
          <Typography variant="h1" component="h1" gutterBottom>
            {PRODUCT_NAME}
          </Typography>
          {children}
        </CardContent>
      </Card>
    </Box>
  );
}

/** Вторая строка `login_failed` по коду `authError`, закрытый список из трёх значений. */
function loginFailedDetail(code: string | null): string {
  switch (code) {
    case 'access_denied':
      return 'Вход отменён на странице входа.';
    case 'idp_unavailable':
      return 'Сервис аутентификации недоступен, повторите позже.';
    case 'sign_in_failed':
      return 'Не удалось завершить вход, попробуйте ещё раз.';
    default:
      return 'Предыдущая попытка входа не была завершена.';
  }
}

/**
 * Семь состояний одного служебного экрана: вне `AppLayout`,
 * читает всё из `useAuth()` напрямую — вызывающему (`RequireAuth`, маршрут
 * `/signed-out`) нечего ему передавать.
 */
export function AnonymousScreen() {
  const { status, reason, loginFailedCode, signIn, retryBootstrap, retrySignOut } = useAuth();

  if (status !== 'anonymous') {
    return (
      <Shell>
        <LoadingBox />
        <Typography variant="body2" color="text.secondary" align="center">
          Выполняется вход…
        </Typography>
      </Shell>
    );
  }

  switch (reason) {
    case 'signed_out':
      return (
        <Shell>
          <Alert severity="info" sx={{ mb: 3 }}>
            Вы вышли из системы.
          </Alert>
          <Button variant="contained" fullWidth onClick={() => signIn('/home')}>
            Войти
          </Button>
        </Shell>
      );
    case 'login_failed':
      return (
        <Shell>
          <Alert severity="error" sx={{ mb: 3 }}>
            <Typography variant="body1">Вход не выполнен.</Typography>
            <Typography variant="body2">{loginFailedDetail(loginFailedCode)}</Typography>
          </Alert>
          <Button variant="contained" fullWidth onClick={() => signIn()}>
            Войти
          </Button>
        </Shell>
      );
    case 'expired':
      return (
        <Shell>
          <Alert severity="warning" sx={{ mb: 3 }}>
            Сессия истекла, войдите заново.
          </Alert>
          <Button variant="contained" fullWidth onClick={() => signIn()}>
            Войти
          </Button>
        </Shell>
      );
    case 'auth_unavailable':
      return (
        <Shell>
          <Alert severity="warning" sx={{ mb: 3 }}>
            Сервис аутентификации недоступен, повторите позже.
          </Alert>
          <Button variant="contained" fullWidth onClick={retryBootstrap}>
            Повторить
          </Button>
        </Shell>
      );
    case 'logout_failed':
      return (
        <Shell>
          <Alert severity="error" sx={{ mb: 3 }}>
            Выход не завершён: сервис аутентификации недоступен.
          </Alert>
          <Button variant="contained" fullWidth onClick={retrySignOut}>
            Повторить выход
          </Button>
        </Shell>
      );
    default:
      return null;
  }
}
