import { capitalize } from "xtreme-ui";
import { FaHome, FaStar, FaPhone, FaSignOutAlt } from "react-icons/fa";
import { MdRestaurantMenu } from "react-icons/md";

import { CustomerProvider } from "#components/context";
import NavSideBar from "#components/layout/NavSideBar";

import PageContainer from "./_components/PageContainer";
import "./restaurant.scss";

const navItems = [
  { label: "explore", value: "explore", icon: <FaHome /> },
  { label: "menu", value: "menu", icon: <MdRestaurantMenu /> },
  { label: "reviews", value: "reviews", icon: <FaStar /> },
  { label: "contact", value: "contact", icon: <FaPhone /> },
  { label: "sign out", value: "signout", icon: <FaSignOutAlt /> },
];

export async function generateMetadata({
  params,
  searchParams,
}: IMetaDataProps) {
  const p = await params;
  const s = await searchParams;
  return {
    title: `${capitalize(p.restaurant)}${s.tab ? ` • ${capitalize(s.tab)}` : ""}`,
  };
}

const Restaurant = () => {
  return (
    <CustomerProvider>
      <div className="restaurant">
        <NavSideBar navItems={navItems} defaultTab="menu" foot />
        <PageContainer />
      </div>
    </CustomerProvider>
  );
};

export default Restaurant;

interface IMetaDataProps {
  params: {
    restaurant: string;
  };
  searchParams: {
    tab?: string;
    [key: string]: string | undefined;
  };
}
