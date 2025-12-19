import { DashboardProvider } from "#components/context";

import AboutPageContainer from "./AboutPageContainer";

export default async function AboutPage() {
  return (
    <DashboardProvider>
      <AboutPageContainer />
    </DashboardProvider>
  );
}
