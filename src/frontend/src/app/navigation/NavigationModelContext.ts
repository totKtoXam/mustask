import { createContext, useContext } from 'react';
import { navigationModel } from './model';
import type { NavigationModel } from './types';

/**
 * The model the shell renders: `model.ts` in the app; a component test may
 * provide one holding a test entry, such as an organization-scoped one
 * (US-3).
 */
export const NavigationModelContext = createContext<NavigationModel>(navigationModel);

export function useNavigationModel(): NavigationModel {
  return useContext(NavigationModelContext);
}
