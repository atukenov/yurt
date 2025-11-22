import { ReactNode } from "react";

import Script from "next/script";
import { themeController } from "xtreme-ui";

import { getThemeColor } from "#utils/database/helper/getThemeColor";

export const metadata = {
  title: "OrderWorder ⌘ Admin",
};

export default async function RootLayout({ children }: IRootProps) {
  const themeColor = await getThemeColor();
  return (
    <>
      <Script
        id="xtreme-theme"
        strategy="beforeInteractive"
        dangerouslySetInnerHTML={{
          __html: themeController({ color: themeColor }),
        }}
      />
      {children}
    </>
  );
}

interface IRootProps {
  children?: ReactNode;
}
