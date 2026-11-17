// The writes (templates) and the file operations the editor and the check page make. A template write
// invalidates the list and the template; the file reads and the dry run are mutations, never cached.
import { useMutation, useQueryClient } from '@tanstack/react-query';
import {
  copyTemplate,
  createTemplate,
  deleteTemplate,
  readChainSample,
  readFileColumns,
  readFileValues,
  replaceTemplate,
  startDryRun,
  uploadFile,
  type FileSourceOptions,
  type MappingProbe,
  type TemplateInput,
} from '../api/importsApi';
import { importsKeys } from './queries';

export function useCreateTemplate(organizationId: string) {
  const client = useQueryClient();
  return useMutation({
    mutationFn: (input: TemplateInput) => createTemplate(organizationId, input),
    onSuccess: () => client.invalidateQueries({ queryKey: importsKeys.templates(organizationId) }),
  });
}

export function useReplaceTemplate(organizationId: string, templateId: string) {
  const client = useQueryClient();
  return useMutation({
    mutationFn: (input: TemplateInput) => replaceTemplate(organizationId, templateId, input),
    onSuccess: (template) => {
      client.setQueryData(importsKeys.template(organizationId, templateId), template);
      void client.invalidateQueries({ queryKey: importsKeys.templates(organizationId) });
      void client.invalidateQueries({ queryKey: importsKeys.columns(organizationId, templateId) });
    },
  });
}

export function useCopyTemplate(organizationId: string) {
  const client = useQueryClient();
  return useMutation({
    mutationFn: (templateId: string) => copyTemplate(organizationId, templateId),
    onSuccess: () => client.invalidateQueries({ queryKey: importsKeys.templates(organizationId) }),
  });
}

export function useDeleteTemplate(organizationId: string) {
  const client = useQueryClient();
  return useMutation({
    mutationFn: (templateId: string) => deleteTemplate(organizationId, templateId),
    onSettled: () => client.invalidateQueries({ queryKey: importsKeys.templates(organizationId) }),
  });
}

export function useUploadFile(organizationId: string) {
  return useMutation({
    mutationFn: (variables: { file: File; options?: FileSourceOptions }) =>
      uploadFile(organizationId, variables.file, variables.options),
  });
}

export function useReadFileColumns(organizationId: string) {
  return useMutation({
    mutationFn: (variables: { fileId: string; options: FileSourceOptions }) =>
      readFileColumns(organizationId, variables.fileId, variables.options),
  });
}

export function useReadFileValues(organizationId: string) {
  return useMutation({
    mutationFn: (variables: { fileId: string; probe: MappingProbe }) =>
      readFileValues(organizationId, variables.fileId, variables.probe),
  });
}

export function useReadChainSample(organizationId: string) {
  return useMutation({
    mutationFn: (variables: { fileId: string; probe: MappingProbe }) =>
      readChainSample(organizationId, variables.fileId, variables.probe),
  });
}

export function useStartDryRun(organizationId: string) {
  return useMutation({
    mutationFn: (variables: { templateId: string; fileId: string; signal?: AbortSignal }) =>
      startDryRun(organizationId, variables.templateId, variables.fileId, variables.signal),
  });
}
