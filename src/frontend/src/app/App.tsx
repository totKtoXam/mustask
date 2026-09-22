import { BrowserRouter } from 'react-router-dom';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import CssBaseline from '@mui/material/CssBaseline';
import { ThemeProvider, createTheme } from '@mui/material/styles';
import { ruRU } from '@mui/material/locale';
import { AuthProvider } from '../features/auth/AuthContext';
import { ErrorBoundary } from '../shared/ui/ErrorBoundary';
import { AppRoutes } from './routes';

const theme = createTheme({}, ruRU);

const queryClient = new QueryClient({
  defaultOptions: {
    queries: {
      // Ошибки API (4xx/5xx с ProblemDetails) ретраями не лечатся — показываем сразу.
      retry: false,
      refetchOnWindowFocus: false,
    },
  },
});

export function App() {
  return (
    <ThemeProvider theme={theme}>
      <CssBaseline />
      <ErrorBoundary>
        <QueryClientProvider client={queryClient}>
          <AuthProvider>
            <BrowserRouter>
              <AppRoutes />
            </BrowserRouter>
          </AuthProvider>
        </QueryClientProvider>
      </ErrorBoundary>
    </ThemeProvider>
  );
}
