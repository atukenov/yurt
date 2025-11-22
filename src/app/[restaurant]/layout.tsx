import { ReactNode } from "react";

import Script from "next/script";
import { themeController } from "xtreme-ui";

import { getThemeColor } from "#utils/database/helper/getThemeColor";

export default async function RootLayout({ children, params }: IRootProps) {
  const themeColor = await getThemeColor((await params).restaurant);
  return (
    <>
      <Script
        id="xtreme-theme-restaurant"
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
  params: {
    restaurant: string;
  };
}
