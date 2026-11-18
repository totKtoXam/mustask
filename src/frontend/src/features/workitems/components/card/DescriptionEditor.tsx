import { memo, useMemo, type FocusEvent, type Ref } from 'react';
import {
  RichTextEditor,
  type RichTextEditorHandle,
} from '../../../../shared/ui/RichTextEditor/RichTextEditor';
import { itemImagesHost } from '../../api/workItemImagesApi';
import { cardTexts } from '../../lib/cardTexts';
import { TEXT_HELPER } from '../../lib/fields';
import type { ElementRef } from '../../lib/fieldRegistry';

interface DescriptionEditorProps {
  /** The canonical html the draft holds (`draftOf`: a legacy plain text already converted). */
  value: string;
  error: string | undefined;
  /** A save is running or the card is read-only: the editor shows the text and takes no edit. */
  disabled: boolean;
  onChange: (html: string) => void;
  editorRef: Ref<RichTextEditorHandle>;
  /** Where the item's images live (feat-078): the editor's images are stored on this item. */
  organizationId: string;
  projectId: string;
  identifier: string;
  /** The viewer holds no edit right: the images show, the insert control is absent. */
  readOnly: boolean;
  inputRef?: ElementRef;
}

/**
 * «Описание» of the card (feat-018): the rich-text editor under the section's head. The editor
 * reports after a short delay; the card's save asks it to report at once (`editorRef.flush()`,
 * plan § 7.2). A refused save focuses the field through the wrapper, which hands focus on to
 * the editor's body.
 */
function DescriptionEditorView({
  value,
  error,
  disabled,
  onChange,
  editorRef,
  organizationId,
  projectId,
  identifier,
  readOnly,
  inputRef,
}: DescriptionEditorProps) {
  function focusBody(event: FocusEvent<HTMLDivElement>) {
    if (event.target === event.currentTarget) {
      event.currentTarget.querySelector<HTMLElement>('[contenteditable="true"]')?.focus();
    }
  }

  const images = useMemo(
    () => ({ ...itemImagesHost(organizationId, projectId, identifier), canInsert: !readOnly }),
    [organizationId, projectId, identifier, readOnly],
  );

  return (
    <div ref={inputRef} tabIndex={-1} onFocus={focusBody} style={{ outline: 'none' }}>
      <RichTextEditor
        ref={editorRef}
        label={cardTexts.description.label}
        value={value}
        disabled={disabled}
        helperText={TEXT_HELPER}
        errorText={error}
        images={images}
        onChange={({ html }) => onChange(html)}
      />
    </div>
  );
}

export const DescriptionEditor = memo(DescriptionEditorView);
