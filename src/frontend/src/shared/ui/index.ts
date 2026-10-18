// The UI kit's one public entry (tech-015 design § 4.1, `docs/conventions/ui-kit.md`):
// a slice imports a kit part from `shared/ui` and from nothing deeper. A kit
// component imports its siblings by their own files, never through this one.

// Icons (P2)
export { BacklogIcon } from './icons/BacklogIcon';
export {
  ThinAddIcon,
  ThinChevronDownIcon,
  ThinChevronRightIcon,
  ThinChevronUpIcon,
  ThinCloseIcon,
  ThinCollapseIcon,
  ThinExpandIcon,
  ThinMoreVerticalIcon,
  ThinPersonIcon,
  ThinRefreshIcon,
  ThinRemoveIcon,
  ThinSaveIcon,
  ThinUndoIcon,
} from './icons/ThinIcons';

// Buttons and menus (P2)
export { ActionButton, type ActionButtonProps, type ActionTone } from './actions/ActionButton';
export { IconAction, type IconActionProps, type IconActionSize } from './actions/IconAction';
export {
  MenuButton,
  type MenuButtonProps,
  type MenuChoice,
  type MenuEntry,
  type MenuTriggerProps,
} from './actions/MenuButton';
export { MoreActionsMenu, type MoreActionsMenuProps } from './actions/MoreActionsMenu';
export { SplitButton, type SplitButtonProps } from './actions/SplitButton';
export type { DataAttributes, PassThrough } from './passThrough';

// Marks, people and tags (P3)
export { PersonLabel, type PersonLabelProps } from './marks/PersonLabel';
export { StateLabel, type StateLabelProps } from './marks/StateLabel';
export { Tag, type TagProps, type TagTone } from './marks/Tag';
export { TagList, type TagListItem, type TagListProps } from './marks/TagList';
export { TypeLabel, type TypeLabelProps } from './marks/TypeLabel';
export { UserAvatar, type UserAvatarProps } from './marks/UserAvatar';

// Text helpers (P3)
export { InlineEmpty, type InlineEmptyProps } from './text/InlineEmpty';
export { LiveRegion, type LiveRegionProps } from './text/LiveRegion';
export { TruncatedText, type TruncatedTextProps } from './text/TruncatedText';
export { VisuallyHidden, type VisuallyHiddenProps } from './text/VisuallyHidden';

// Fields (P4)
export { InlineDateField, type InlineDateFieldProps } from './fields/InlineDateField';
export {
  InlineMultiSelectField,
  type InlineMultiSelectFieldProps,
} from './fields/InlineMultiSelectField';
export {
  InlineMultiPickerField,
  type InlineMultiPickerFieldProps,
} from './fields/InlineMultiPickerField';
export { InlineNumberField, type InlineNumberFieldProps } from './fields/InlineNumberField';
export {
  InlinePickerField,
  type InlinePickerFieldProps,
  type PickerInputReason,
} from './fields/InlinePickerField';
export {
  InlineSelectField,
  type InlineSelectFieldProps,
  type SelectOption,
} from './fields/InlineSelectField';
export { InlineTextArea, type InlineTextAreaProps } from './fields/InlineTextArea';
export { InlineTextField, type InlineTextFieldProps } from './fields/InlineTextField';
export { PersonField, type PersonFieldProps } from './fields/PersonField';
export type {
  AdornmentPlacement,
  FieldFrameKind,
  FieldInputProps,
  FieldProps,
  FieldSize,
  LabelPlacement,
} from './fields/fieldTypes';

// Surfaces, sections and columns (P5)
export { ColumnLayout, type ColumnLayoutProps } from './layout/ColumnLayout';
export { Section, SectionHead, type SectionHeadProps, type SectionProps } from './layout/Section';
export {
  Surface,
  type SurfacePadding,
  type SurfaceProps,
  type SurfaceVariant,
} from './layout/Surface';

// The card (P5)
export {
  BoxedTabPanel,
  BoxedTabs,
  type BoxedTab,
  type BoxedTabPanelProps,
  type BoxedTabsProps,
} from './card/BoxedTabs';
export { CardHeaderBar, type CardHeaderBarProps } from './card/CardHeaderBar';
export { PropertyBand, type PropertyBandProps } from './card/PropertyBand';
export { TypeStripe, type TypeStripeProps } from './card/TypeStripe';

// Dialogs and the page panel (P5)
export {
  DialogShell,
  type DialogCloseReason,
  type DialogGuard,
  type DialogShellProps,
} from './dialog/DialogShell';
export { PanelShell, type PanelShellProps } from './dialog/PanelShell';
export { usePageHead, type PageHeadOptions } from './usePageHead';

// Tables (P7)
export { BodyRow, type BodyRowProps, type RowAttributes } from './table/BodyRow';
export { Cell, type CellAttributes, type CellPadding, type CellProps } from './table/Cell';
export { DataTable, type DataColumn, type DataTableProps } from './table/DataTable';
export {
  HeadCell,
  type HeadCellAttributes,
  type HeadCellKind,
  type HeadCellProps,
  type HeadCellSort,
  type HeadCellSortDirection,
} from './table/HeadCell';
export { RowControl, type RowControlProps } from './table/RowControl';
export type { RowCue } from './table/rowStyles';
export {
  TableCard,
  type TableBodyAttributes,
  type TableCardProps,
  type TableColumn,
} from './table/TableCard';
export { TablePager, type TablePagerProps } from './table/TablePager';
export { TableSkeleton, type TableSkeletonProps } from './table/TableSkeleton';
export { TreeCell, type TreeCellProps } from './table/TreeCell';

// Notices and loading (P6)
export { CardSkeleton, type CardSkeletonProps } from './feedback/CardSkeleton';
export { FieldsSkeleton, type FieldsSkeletonProps } from './feedback/FieldsSkeleton';
export {
  InlineNotice,
  type InlineNoticeAction,
  type InlineNoticeProps,
} from './feedback/InlineNotice';
export { NoticeProvider } from './feedback/NoticeProvider';
export type { NoticeApi, NoticeSeverity } from './feedback/noticeContext';
export { useNotice } from './feedback/useNotice';

// The flat files that predate the kit stay where they are until their family is
// rebuilt (design § 8.1); the entry re-exports them.
export { ConfirmDialog, type ConfirmDialogProps } from './ConfirmDialog';
export { DialogTitleBar, type DialogTitleBarProps } from './DialogTitleBar';
export { ErrorBanner } from './ErrorBanner';
export { ErrorBoundary } from './ErrorBoundary';
export { FilterBar, type FilterBarProps } from './FilterBar';
export { FormDialog, type FormDialogProps } from './FormDialog';
export { FormFooter, FormPanel, type FormPanelProps } from './FormPanel';
export { FormSkeleton } from './FormSkeleton';
export { LoadingBox } from './LoadingBox';
export { PageHeader, type Command, type PageHeaderProps, type PageView } from './PageHeader';
export { PageTrail } from './PageTrail';
export { RegistrySkeleton } from './RegistrySkeleton';
export {
  SortableHeaderCell,
  type SortableHeaderCellProps,
  type SortDirection,
} from './SortableHeaderCell';
export {
  AccessDenied,
  NotFound,
  StateFrame,
  type NotFoundLevel,
  type StateFrameAction,
  type StateFrameProps,
} from './StateFrame';
export {
  TreeRegistry,
  type TreeRegistryAction,
  type TreeRegistryColumn,
  type TreeRegistryHandle,
  type TreeRegistryNode,
  type TreeRegistryProps,
} from './TreeRegistry';
export { CategoryMarker, TypeMark } from './WorkTrackingMarks';
export type { TrailSegment } from './pageChrome';
