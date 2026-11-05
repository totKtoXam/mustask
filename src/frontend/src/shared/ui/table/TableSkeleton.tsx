import { LoadingRegion, Shape } from '../feedback/skeletonParts';
import { BodyRow } from './BodyRow';
import { Cell } from './Cell';
import { HeadCell } from './HeadCell';
import { TableCard } from './TableCard';

export interface TableSkeletonProps {
  /** The real column labels: the table's structure is there at once. */
  headers: readonly string[];
  /** Default 5. */
  rows?: number;
}

/** A shape inside a cell: a text line's height, most of the cell's width. */
const SHAPE_HEIGHT = 16;

/**
 * A table while it loads (tech-015 design § 5.11, § 5.12): the real header labels and
 * skeleton rows in the same card, one `role="status"` «Загрузка данных» with the table
 * itself hidden from assistive technology. The rows are the real row height, so nothing
 * moves when the data arrives.
 */
export function TableSkeleton({ headers, rows = 5 }: TableSkeletonProps) {
  return (
    <LoadingRegion>
      <TableCard
        aria-label="Загрузка данных"
        head={headers.map((header) => (
          <HeadCell key={header} label={header} />
        ))}
      >
        {Array.from({ length: rows }, (_unused, rowIndex) => (
          <BodyRow key={rowIndex}>
            {headers.map((header, columnIndex) => (
              <Cell key={`${columnIndex}-${header}`}>
                <Shape width="70%" height={SHAPE_HEIGHT} />
              </Cell>
            ))}
          </BodyRow>
        ))}
      </TableCard>
    </LoadingRegion>
  );
}
