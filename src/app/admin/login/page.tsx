import Script from "next/script";
import { themeController } from "xtreme-ui";

import { DashboardProvider } from "#components/context";
import { DEFAULT_THEME_COLOR } from "#utils/constants/common";
import { getThemeColor } from "#utils/database/helper/getThemeColor";

import AdminLoginPageContainer from "./AdminLoginPageContainer";

export default async function AdminLoginPage() {
  const color = (await getThemeColor()) ?? DEFAULT_THEME_COLOR;
  return (
    <>
      <Script
        id="xtreme-theme-admin-login"
        strategy="beforeInteractive"
        dangerouslySetInnerHTML={{ __html: themeController({ color }) }}
      />
      <DashboardProvider>
        <AdminLoginPageContainer />
      </DashboardProvider>
    </>
  );
}
