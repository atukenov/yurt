import { ReactNode } from "react";

import Script from "next/script";
import { themeController } from "xtreme-ui";

import PreloadCss from "#components/base/PreloadCss";
import { GlobalProvider } from "#components/context";
import { DEFAULT_THEME_COLOR } from "#utils/constants/common";
import { montserrat } from "#utils/helper/fontHelper";
import "./globals.scss";

export const metadata = {
  title: "YURT",
};
export default function RootLayout({ children }: IRootProps) {
  return (
    <html lang="en" className={montserrat.variable} suppressHydrationWarning>
      <head>
        <PreloadCss />
        <Script
          id="xtreme-theme-root"
          strategy="beforeInteractive"
          dangerouslySetInnerHTML={{
            __html: themeController({ color: DEFAULT_THEME_COLOR }),
          }}
          suppressHydrationWarning
        />
      </head>
      <body>
        <GlobalProvider>{children}</GlobalProvider>
      </body>
    </html>
  );
}

interface IRootProps {
  children?: ReactNode;
}
