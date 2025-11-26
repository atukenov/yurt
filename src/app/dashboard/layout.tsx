import { ReactNode } from "react";

import PreloadCss from "#components/base/PreloadCss";

export const metadata = {
  title: "OrderWorder ⌘ Admin",
};

export default async function RootLayout({ children }: IRootProps) {
  return (
    <>
      <PreloadCss />
      {children}
    </>
  );
}

interface IRootProps {
  children?: ReactNode;
}
