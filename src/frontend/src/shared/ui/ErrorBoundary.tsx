import { Component, type ErrorInfo, type ReactNode } from 'react';
import Box from '@mui/material/Box';
import Button from '@mui/material/Button';
import Typography from '@mui/material/Typography';
import ErrorOutlineOutlinedIcon from '@mui/icons-material/ErrorOutlineOutlined';
import { formatDocumentTitle } from '../lib/documentTitle';

interface ErrorBoundaryProps {
  children: ReactNode;
}

interface ErrorBoundaryState {
  hasError: boolean;
}

/**
 * Глобальный барьер ошибок рендера: без него необработанное исключение
 * в любом экране размонтирует всё дерево и оставляет пустую страницу.
 * Технические детали на экран не выводятся — только в консоль.
 *
 * The crash screen sits outside the shell on the application background
 * (mod-015 design § 11.4, C-15): an icon, «Что-то пошло не так» as the
 * page's `h1` and «Обновить страницу»; the tab reads «Mustask».
 */
export class ErrorBoundary extends Component<ErrorBoundaryProps, ErrorBoundaryState> {
  state: ErrorBoundaryState = { hasError: false };

  static getDerivedStateFromError(): ErrorBoundaryState {
    return { hasError: true };
  }

  componentDidCatch(error: Error, info: ErrorInfo) {
    // Технические детали — только в консоль, на экран не выводятся.
    console.error(error, info.componentStack);
    document.title = formatDocumentTitle(null);
  }

  handleReload = () => {
    window.location.reload();
  };

  render() {
    if (this.state.hasError) {
      return (
        <Box
          role="alert"
          sx={{
            display: 'flex',
            flexDirection: 'column',
            alignItems: 'center',
            justifyContent: 'center',
            minHeight: '100vh',
            gap: 2,
            textAlign: 'center',
            px: 2,
            bgcolor: 'background.default',
          }}
        >
          <ErrorOutlineOutlinedIcon fontSize="large" sx={{ color: 'text.secondary' }} />
          <Typography variant="h1" component="h1">
            Что-то пошло не так
          </Typography>
          <Button variant="contained" onClick={this.handleReload}>
            Обновить страницу
          </Button>
        </Box>
      );
    }

    return this.props.children;
  }
}
