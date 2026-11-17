// One hook per read, on the keys of design § 6: ['imports', 'templates', params], ['imports', 'template', id].
import { keepPreviousData, useQuery } from '@tanstack/react-query';
import {
  getCatalogue,
  getDryRun,
  getTemplate,
  getTemplateColumns,
  getTemplates,
  type ResultFilter,
  type TemplatesQuery,
} from '../api/importsApi';

export const importsKeys = {
  all: ['imports'] as const,
  catalogue: (organizationId: string) => ['imports', 'catalogue', organizationId] as const,
  templates: (organizationId: string, params?: TemplatesQuery) =>
    params
      ? (['imports', 'templates', organizationId, params] as const)
      : (['imports', 'templates', organizationId] as const),
  template: (organizationId: string, templateId: string) =>
    ['imports', 'template', organizationId, templateId] as const,
  columns: (organizationId: string, templateId: string) =>
    ['imports', 'columns', organizationId, templateId] as const,
  dryRun: (organizationId: string, dryRunId: string, result: ResultFilter) =>
    ['imports', 'dry-run', organizationId, dryRunId, result] as const,
};

/** The catalogue (fields, converters, limits) does not change while the panel is open. */
export function useCatalogue(organizationId: string, enabled = true) {
  return useQuery({
    queryKey: importsKeys.catalogue(organizationId),
    queryFn: () => getCatalogue(organizationId),
    staleTime: Infinity,
    enabled,
  });
}

export function useTemplates(organizationId: string, params: TemplatesQuery, enabled = true) {
  return useQuery({
    queryKey: importsKeys.templates(organizationId, params),
    queryFn: () => getTemplates(organizationId, params),
    placeholderData: keepPreviousData,
    enabled,
  });
}

export function useTemplate(organizationId: string, templateId: string, enabled = true) {
  return useQuery({
    queryKey: importsKeys.template(organizationId, templateId),
    queryFn: () => getTemplate(organizationId, templateId),
    enabled,
  });
}

export function useTemplateColumns(organizationId: string, templateId: string, enabled: boolean) {
  return useQuery({
    queryKey: importsKeys.columns(organizationId, templateId),
    queryFn: () => getTemplateColumns(organizationId, templateId),
    enabled,
  });
}

/** One page of a dry run's report for a result filter: «all» or one of the five results. */
export function useDryRun(
  organizationId: string,
  dryRunId: string | null,
  result: ResultFilter,
  enabled = true,
) {
  return useQuery({
    queryKey: importsKeys.dryRun(organizationId, dryRunId ?? '', result),
    queryFn: () => getDryRun(organizationId, dryRunId ?? '', result),
    enabled: enabled && dryRunId !== null,
    placeholderData: keepPreviousData,
  });
}
