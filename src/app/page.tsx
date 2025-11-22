import Script from "next/script";
import { themeController } from "xtreme-ui";

import { DashboardProvider } from "#components/context";
import { DEFAULT_THEME_COLOR } from "#utils/constants/common";
import { getThemeColor } from "#utils/database/helper/getThemeColor";

import PageContainer from "./_homepage/PageContainer";

export default async function Homepage() {
  const color = (await getThemeColor()) ?? DEFAULT_THEME_COLOR;
  return (
    <>
      <Script
        id="xtreme-theme-home"
        strategy="beforeInteractive"
        dangerouslySetInnerHTML={{ __html: themeController({ color }) }}
      />
      <DashboardProvider>
        <PageContainer />
      </DashboardProvider>
    </>
  );
}
