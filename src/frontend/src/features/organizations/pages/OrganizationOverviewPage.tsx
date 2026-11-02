import { PageHeader } from '../../../shared/ui/PageHeader';

/**
 * «Обзор» of the organization (feat-043 design § 5.1). Step 9 of the plan builds its sections;
 * until then the address is served by this header alone, so that the menu's first entry opens.
 */
export function OrganizationOverviewPage() {
  return <PageHeader title="Обзор" />;
}
