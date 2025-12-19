import { DashboardProvider } from "#components/context";

import PageContainer from "./_homepage/PageContainer";

export default async function Homepage() {
  return (
    <DashboardProvider>
      <PageContainer />
    </DashboardProvider>
  );
}
