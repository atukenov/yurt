import { ReactNode } from "react";

import PreloadCss from "#components/base/PreloadCss";
import { GlobalProvider } from "#components/context";
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
