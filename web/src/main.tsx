import { StrictMode } from 'react';
import { createRoot } from 'react-dom/client';
import { BrowserRouter } from 'react-router-dom';
import { CssBaseline, ThemeProvider, createTheme } from '@mui/material';
import { ApiProvider } from './api/ApiProvider';
import { AppRoutes } from './AppRoutes';
import { SessionProvider } from './session/SessionProvider';

const theme = createTheme({
  palette: { primary: { main: '#2f5d62' }, background: { default: '#f7f8f8' } },
  typography: { fontFamily: '"Atkinson Hyperlegible", system-ui, sans-serif' },
});

createRoot(document.getElementById('root')!).render(
  <StrictMode>
    <ThemeProvider theme={theme}>
      <CssBaseline />
      <ApiProvider>
        <SessionProvider>
          <BrowserRouter>
            <AppRoutes />
          </BrowserRouter>
        </SessionProvider>
      </ApiProvider>
    </ThemeProvider>
  </StrictMode>,
);
