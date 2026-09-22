import { Navigate, Route, Routes } from 'react-router-dom';
import { AnonymousScreen } from '../features/auth/AnonymousScreen';
import { RequireAuth } from '../features/auth/RequireAuth';
import { LanguageFormPage, LanguagesRegistryPage } from '../features/dictionaries';
import { HomePage } from '../features/home/HomePage';
import { AppLayout } from './AppLayout';

export function AppRoutes() {
  return (
    <Routes>
      {/* Вне RequireAuth: возврат сюда после RP-initiated logout не должен
          перепроверять сессию сетевым запросом. */}
      <Route path="/signed-out" element={<AnonymousScreen />} />
      <Route element={<RequireAuth />}>
        <Route element={<AppLayout />}>
          <Route index element={<Navigate to="/home" replace />} />
          <Route path="/home" element={<HomePage />} />
          {/* Реестр языков и форма языка на двух путях (создание и
              редактирование) — каждая страница гейтирует свой маршрут сама
              (RequirePermission), поэтому здесь никакого дополнительного
              гейта. */}
          <Route path="/dictionaries/languages" element={<LanguagesRegistryPage />} />
          <Route path="/dictionaries/languages/new" element={<LanguageFormPage />} />
          <Route path="/dictionaries/languages/:code" element={<LanguageFormPage />} />
          <Route path="*" element={<Navigate to="/home" replace />} />
        </Route>
      </Route>
    </Routes>
  );
}
