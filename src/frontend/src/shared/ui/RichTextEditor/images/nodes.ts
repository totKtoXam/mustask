import { Extension, Node } from '@tiptap/core';
import { NodeSelection, Plugin, PluginKey } from '@tiptap/pm/state';
import { ReactNodeViewRenderer } from '@tiptap/react';
import { storedAlt } from '../../../lib/richText';
import { removeImageAt } from './commands';
import { IMAGE_ID } from './host';
import { slotsPlugin } from './insert';
import { ImageView, PlaceholderView } from './ImageViews';
import type { ImageRuntime } from './runtime';

interface RuntimeOptions {
  runtime: ImageRuntime;
}

/**
 * The stored image (BR-IM-7): a block of its own, an atom, with the reference and the text
 * alternative and no address — the address is derived from the reference by the host.
 */
export const RtImage = Node.create<RuntimeOptions>({
  name: 'rtImage',
  group: 'block imageBlock',
  atom: true,
  selectable: true,
  draggable: true,

  addOptions() {
    return { runtime: undefined as unknown as ImageRuntime };
  },

  addAttributes() {
    // The schema is the judge of what the editor holds: the alternative is read in its stored form.
    return {
      imageId: {
        default: '',
        parseHTML: (element: HTMLElement) =>
          (element.getAttribute('data-image-id') ?? '').trim().toLowerCase(),
      },
      alt: {
        default: '',
        parseHTML: (element: HTMLElement) => storedAlt(element.getAttribute('alt') ?? ''),
      },
    };
  },

  parseHTML() {
    return [
      {
        tag: 'img[data-image-id]',
        getAttrs: (element) => {
          const id = (element.getAttribute('data-image-id') ?? '').trim().toLowerCase();
          return IMAGE_ID.test(id) ? null : false;
        },
      },
    ];
  },

  renderHTML({ node }) {
    return [
      'img',
      { 'data-image-id': node.attrs.imageId as string, alt: node.attrs.alt as string },
    ];
  },

  addNodeView() {
    return ReactNodeViewRenderer(ImageView);
  },
});

/**
 * An upload in progress (design § Placeholder): a block like an image, never parsed, never stored —
 * the canonical form drops its empty element, and a save waits for it (BR-IM-14).
 */
export const RtImagePlaceholder = Node.create<RuntimeOptions>({
  name: 'rtImagePlaceholder',
  group: 'block imageBlock',
  atom: true,
  selectable: true,

  addOptions() {
    return { runtime: undefined as unknown as ImageRuntime };
  },

  addAttributes() {
    return { token: { default: '' } };
  },

  parseHTML() {
    return [];
  },

  renderHTML({ node }) {
    return ['div', { 'data-rt-image-placeholder': node.attrs.token as string }];
  },

  addNodeView() {
    return ReactNodeViewRenderer(PlaceholderView);
  },
});

/**
 * The keys of a selected image (design § Keyboard and screen reader) and the invisible slots of the
 * reduction queue. Enter and Space open the full-size view, Delete and Backspace remove, Shift+F10
 * and the ContextMenu key open the image menu, printable keys are ignored (typing never replaces a
 * selected image, D-3).
 */
export const RtImageKeys = Extension.create<RuntimeOptions>({
  name: 'rtImageKeys',

  addOptions() {
    return { runtime: undefined as unknown as ImageRuntime };
  },

  addProseMirrorPlugins() {
    const { runtime } = this.options;
    const { editor } = this;

    return [
      slotsPlugin(),
      new Plugin({
        key: new PluginKey('rtImageKeys'),
        props: {
          handleKeyDown: (view, event) => {
            runtime.pointerSelection = false;
            const { selection } = view.state;
            if (!(selection instanceof NodeSelection)) return false;
            const name = selection.node.type.name;
            if (name !== 'rtImage' && name !== 'rtImagePlaceholder') return false;

            const plain = !event.ctrlKey && !event.metaKey && !event.altKey;
            if (name === 'rtImage') {
              const target = () => {
                const attrs = selection.node.attrs as { imageId: string; alt: string };
                return {
                  imageId: attrs.imageId,
                  alt: attrs.alt === '' ? runtime.copy.defaultAlt : attrs.alt,
                  pos: selection.from,
                };
              };
              if ((event.key === 'Enter' || event.key === ' ') && plain && !event.shiftKey) {
                event.preventDefault();
                runtime.ui.openView(target());
                return true;
              }
              if (event.key === 'ContextMenu' || (event.key === 'F10' && event.shiftKey)) {
                event.preventDefault();
                runtime.ui.openMenu(target(), view.nodeDOM(selection.from) as HTMLElement | null);
                return true;
              }
            }
            if (!editor.isEditable) return false;
            if (event.key === 'Delete' || event.key === 'Backspace') {
              event.preventDefault();
              removeImageAt(editor, selection.from, runtime);
              return true;
            }
            // Typing never replaces a selected image: an accidental overwrite would drop it silently.
            return plain && event.key.length === 1;
          },
          handleTextInput: (view) => {
            const { selection } = view.state;
            return selection instanceof NodeSelection && selection.node.type.name === 'rtImage';
          },
          handleDOMEvents: {
            mousedown: () => {
              runtime.pointerSelection = true;
              return false;
            },
            touchstart: () => {
              runtime.pointerSelection = true;
              return false;
            },
          },
        },
      }),
    ];
  },
});
