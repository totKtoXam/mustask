import Box from '@mui/material/Box';
import Link from '@mui/material/Link';
import List from '@mui/material/List';
import ListItem from '@mui/material/ListItem';
import Typography from '@mui/material/Typography';
import { AccessDenied, ColumnLayout, PageHeader, Section, SectionHead } from '../../../shared/ui';
import { useAuth } from '../../auth/AuthContext';
import { uiKitPermissions } from '../permissions';
import { showcaseRegistry } from '../showcase/registry';

// The panel's UI kit showcase (tech-015 design § 10): every component alive, in every
// state, built from the kit itself. For administrators; the page makes no API request —
// every demo is fed from local data. Under «Настройки / Интерфейс».
//
// Layout (a reading of § 10.2): the contents stand above the sections, not in a sticky
// side column — at the 1536 px window the main area is about 1000 px wide, and a 320 px
// side column would leave the card demo 650 px, under its own 720 px stacking threshold.

const PAGE_TITLE = 'Витрина компонентов';

const INTRO =
  'Здесь каждый компонент интерфейса показан вживую и во всех состояниях. Наведите курсор и нажмите Tab, чтобы увидеть наведение и фокус. Правила и описание каждого компонента — в docs/conventions/ui-kit.md.';

export function UiKitShowcasePage() {
  const { can } = useAuth();
  // The gate is a permission, not a role (ADR-0018): without the code the page answers the
  // canon's «Доступ ограничен» and nothing below it is mounted.
  if (!can(uiKitPermissions.showcaseRead)) {
    return <AccessDenied />;
  }
  return <Showcase />;
}

function Showcase() {
  return (
    <>
      <PageHeader variant="team" title={PAGE_TITLE} />
      <Typography
        variant="body2"
        component="p"
        sx={{ color: 'text.secondary', maxWidth: (theme) => theme.spacing(90) }}
      >
        {INTRO}
      </Typography>
      <Contents />
      <ColumnLayout
        main={
          <>
            {showcaseRegistry.map(({ id, title, usage, Demo }) => (
              <Box
                key={id}
                id={id}
                sx={(theme) => ({
                  scrollMarginTop: `calc(${theme.layout.topBarHeight}px + ${theme.spacing(2)})`,
                })}
              >
                <Section title={title}>
                  <Typography variant="body2" component="p" sx={{ color: 'text.secondary', mb: 2 }}>
                    {usage}
                  </Typography>
                  <Demo />
                </Section>
              </Box>
            ))}
          </>
        }
      />
    </>
  );
}

/** «Содержание»: a link to each section, in the page's order. */
function Contents() {
  return (
    <Box sx={{ mt: 3 }}>
      <SectionHead title="Содержание" divider={false} />
      <Box component="nav" aria-label="Содержание витрины">
        <List
          disablePadding
          sx={{ display: 'flex', flexWrap: 'wrap', columnGap: 3, rowGap: 0.5, pt: 1 }}
        >
          {showcaseRegistry.map(({ id, title }) => (
            <ListItem key={id} disablePadding sx={{ width: 'auto' }}>
              <Link href={`#${id}`} variant="body2">
                {title}
              </Link>
            </ListItem>
          ))}
        </List>
      </Box>
    </Box>
  );
}
