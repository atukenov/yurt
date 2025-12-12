"use client";

import { ReactNode, useEffect } from "react";

import clsx from "clsx";
import { useSession } from "next-auth/react";
import { useRouter } from "next/navigation";

import { useQueryParams } from "#utils/hooks/useQueryParams";

import "./navSideBar.scss";

const NavSideBar = (props: TNavSideBar) => {
  const { head, foot, navItems, defaultTab } = props;
  const router = useRouter();
  const session = useSession();
  const queryParams = useQueryParams();
  const tab = queryParams.get("tab") ?? "";

  const classList = clsx("menu", head && "head", foot && "foot");

  const onNavClick = (tab: string) => {
    if (tab === "signout") return router.push("/logout");
		if (tab === "explore") return router.push("/");
    queryParams.set({ tab });
  };

  useEffect(() => {
    if (!tab) queryParams.set({ tab: defaultTab });
  }, [defaultTab, queryParams, tab]);

  return (
    <div className="navSideBar">
      <div className={classList}>
        {navItems.map((item, key) => {
          if (item.value === "signout" && session.status !== "authenticated")
            return null;

          const active = tab === item.value;
          return (
            <div
              key={key}
              className={clsx("navItem", active && "active")}
              onClick={() => onNavClick(item.value)}
            >
              <div className="navItemContent">
                <span className="navIcon">{item.icon}</span>
                <p>{item.label}</p>
              </div>
            </div>
          );
        })}
      </div>
    </div>
  );
};

export default NavSideBar;

type TNavSideBar = {
  navItems: Array<{ label: string; value: string; icon: ReactNode }>;
  defaultTab: string;
  head?: boolean;
  foot?: boolean;
};
