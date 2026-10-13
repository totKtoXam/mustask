import { Fragment, useId, useState, type ReactNode } from 'react';
import {
  BoxedTabPanel,
  BoxedTabs,
  CardHeaderBar,
  ColumnLayout,
  InlineNotice,
  PropertyBand,
  Section,
  SectionHead,
} from '../../../../shared/ui';
import type { CardColumnItem, CardFrameProps } from './cardFrame';

/** The one tab the product has. */
const DETAILS = 'details';

/** A column's items as the kit's sections; a column with nothing in it is not drawn. */
function column(items: readonly CardColumnItem[]): ReactNode {
  if (items.length === 0) {
    return undefined;
  }
  return items.map((item) =>
    'node' in item ? (
      <Fragment key={item.key}>{item.node}</Fragment>
    ) : (
      <Section key={item.key} title={item.title}>
        {item.content}
        {item.subsection && (
          <>
            <SectionHead
              id={item.subsection.id}
              title={item.subsection.title}
              count={item.subsection.count}
              headingLevel={3}
              divider={false}
            />
            {item.subsection.content}
          </>
        )}
      </Section>
    ),
  );
}

/** The kit's parts a card's content is put into; `undefined` where there is no card yet. */
export interface CardParts {
  header: ReactNode;
  band: ReactNode;
  alert: ReactNode;
  body: ReactNode;
}

const NO_PARTS: CardParts = {
  header: undefined,
  band: undefined,
  alert: undefined,
  body: undefined,
};

/**
 * The card's content put into the kit's parts (feat-029 design § 4.2–§ 4.5), the same
 * for the page and for the dialog: the header bar, the property band with its tab
 * strip, the notice, and the body — the tab's panel holding the three columns.
 * `headingLevel` is 1 on the page and 2 in the dialog. The dialog's one shell is drawn
 * before the card has published a frame (design § 6), so `frame` may be `null`: no
 * parts yet, the tab kept for when they come.
 */
export function useCardParts(frame: CardFrameProps | null, headingLevel: 1 | 2): CardParts {
  const idBase = useId();
  const [selected, setSelected] = useState(DETAILS);
  if (frame === null) {
    return NO_PARTS;
  }
  return partsOf(frame, headingLevel, idBase, selected, setSelected);
}

function partsOf(
  frame: CardFrameProps,
  headingLevel: 1 | 2,
  idBase: string,
  selected: string,
  setSelected: (value: string) => void,
): CardParts {
  const places = frame.tabs.extra;

  const header = (
    <CardHeaderBar
      appearance={frame.appearance}
      typeLabel={frame.typeName}
      identifier={frame.identifier}
      typeHref={headingLevel === 2 ? frame.pagePath : undefined}
      headingLevel={headingLevel}
      headingId={frame.headingId}
      title={frame.title}
      meta={frame.headerMeta}
      actions={frame.headerActions}
    />
  );
  const band = (
    <PropertyBand
      blocks={frame.bandBlocks}
      meta={frame.bandMeta}
      tabs={
        <BoxedTabs
          idBase={idBase}
          aria-label={frame.tabs.label}
          value={selected}
          onChange={setSelected}
          tabs={[
            ...places.map((place) => place.tab),
            { value: DETAILS, label: frame.tabs.details },
          ]}
        />
      }
    />
  );
  const alert = frame.notice ? (
    <InlineNotice severity={frame.notice.severity}>{frame.notice.text}</InlineNotice>
  ) : undefined;
  const body = (
    <>
      <BoxedTabPanel idBase={idBase} value={DETAILS} selected={selected === DETAILS}>
        <ColumnLayout
          main={column(frame.main)}
          side={column(frame.side)}
          extra={column(frame.extra)}
        />
      </BoxedTabPanel>
      {places.map((place) => (
        <BoxedTabPanel
          key={place.tab.value}
          idBase={idBase}
          value={place.tab.value}
          selected={selected === place.tab.value}
        >
          {place.panel}
        </BoxedTabPanel>
      ))}
    </>
  );
  return { header, band, alert, body };
}
