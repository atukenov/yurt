import { ReactNode } from "react";

import Script from "next/script";
import { themeController } from "xtreme-ui";

import { getThemeColor } from "#utils/database/helper/getThemeColor";
import { RestaurantClientLayout } from "./_layout/ClientLayout";

export default async function RestaurantLayout({
  children,
  params,
}: IRootProps) {
  const themeColor = await getThemeColor((await params).restaurant);
  return (
    <>
      <Script
        id="xtreme-theme-restaurant"
        strategy="beforeInteractive"
        dangerouslySetInnerHTML={{
          __html: themeController({ color: themeColor }),
        }}
        suppressHydrationWarning
      />
      <RestaurantClientLayout>{children}</RestaurantClientLayout>
    </>
  );
}

interface IRootProps {
  children?: ReactNode;
  params: Promise<{
    restaurant: string;
  }>;
}
