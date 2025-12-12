import Script from "next/script";
import { themeController } from "xtreme-ui";

import { DashboardProvider } from "#components/context";
import { DEFAULT_THEME_COLOR } from "#utils/constants/common";
import { getThemeColor } from "#utils/database/helper/getThemeColor";

import AboutPageContainer from "./AboutPageContainer";

export default async function AboutPage() {
  const color = (await getThemeColor()) ?? DEFAULT_THEME_COLOR;
  return (
    <>
      <Script
        id="xtreme-theme-about"
        strategy="beforeInteractive"
        dangerouslySetInnerHTML={{ __html: themeController({ color }) }}
      />
      <DashboardProvider>
        <AboutPageContainer />
      </DashboardProvider>
    </>
  );
}
