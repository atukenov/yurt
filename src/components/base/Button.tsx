"use client";

import { ButtonHTMLAttributes, ReactNode } from "react";

import clsx from "clsx";
import {
  FaPlus,
  FaMinus,
  FaTimes,
  FaCheck,
  FaChevronLeft,
  FaChevronRight,
  FaChevronDown,
  FaChevronUp,
  FaShoppingCart,
  FaHome,
  FaUtensils,
  FaStar,
  FaPhone,
  FaSignOutAlt,
  FaEye,
  FaEyeSlash,
  FaPen,
  FaInfoCircle,
  FaFileDownload,
  FaLeaf,
  FaEgg,
  FaDrumstickBite,
  FaEnvelope,
  FaUserCircle,
  FaSpinner,
  FaUser,
} from "react-icons/fa";
import { MdDashboard, MdRestaurantMenu } from "react-icons/md";

import "./button.scss";

type IconCode = string;

const iconMap: Record<IconCode, ReactNode> = {
  // Math/Actions
  "2b": <FaPlus />, // plus
  "2d": <FaMinus />, // minus
  f00c: <FaCheck />, // check
  f00d: <FaTimes />, // times/close

  // Chevrons/Arrows
  f053: <FaChevronLeft />, // chevron-left
  f054: <FaChevronRight />, // chevron-right
  f063: <FaChevronDown />, // chevron-down
  f078: <FaChevronUp />, // chevron-up

  // Shopping/Cart
  e43b: <FaShoppingCart />, // shopping-cart
  e1bc: <FaShoppingCart />, // shopping-cart variant

  // Navigation
  f015: <FaHome />, // home
  e09f: <MdDashboard />, // dashboard
  e3e3: <MdRestaurantMenu />, // menu

  // Info/Actions
  f05a: <FaInfoCircle />, // info-circle
  f354: <FaFileDownload />, // file-download

  // Visibility
  f06e: <FaEye />, // visible
  f070: <FaEyeSlash />, // hidden

  // Edit
  f304: <FaPen />, // pen/edit

  // Food/Veg Icons
  f4d8: <FaLeaf />, // leaf (veg)
  f6d6: <FaDrumstickBite />, // drumstick (non-veg)
  f7fb: <FaEgg />, // egg (contains-egg)

  // Reviews/Contact
  f4ad: <FaStar />, // star (reviews)
  f8d3: <FaPhone />, // phone (contact)
  f095: <FaPhone />, // phone variant

  // Auth/User
  f011: <FaSignOutAlt />, // sign-out
  f0e0: <FaEnvelope />, // envelope/email
  e323: <FaUserCircle />, // user-circle
  f86b: <FaUserCircle />, // user-circle variant
  f007: <FaUser />, // user

  // Payment/Checkout
  f09d: <FaCheck />, // check-circle (proceed to pay)

  // Modal close
  e59b: <FaTimes />, // times/close variant
};

type TButton = Omit<ButtonHTMLAttributes<HTMLButtonElement>, "type"> & {
  icon?: IconCode | ReactNode;
  iconType?: "solid" | "duotone" | string;
  size?: "mini" | "small" | "default";
  type?: "primary" | "secondary" | "ghost" | string;
  label?: string;
  loading?: boolean;
  /** html button type (button | submit | reset) */
  htmlType?: "button" | "submit" | "reset";
};

const Button = ({
  icon,
  iconType,
  size = "default",
  type = "primary",
  label,
  loading,
  className,
  children,
  ...rest
}: TButton) => {
  const classes = clsx("btn", `btn-${size}`, `btn-${type}`, className);

  const renderIcon = () => {
    if (!icon) return null;
    if (typeof icon === "string")
      return iconMap[icon] ?? <span className="icon-fallback" />;
    return icon;
  };

  // Pull out htmlType if user passed an html button type to avoid clash with our `type` styling prop
  const r = rest as Record<string, unknown>;
  const htmlType = (r.htmlType as "button" | "submit" | "reset") ?? "button";
  const { htmlType: _, ...otherRest } = r;
  return (
    <button
      className={classes}
      type={htmlType}
      {...(otherRest as Record<string, unknown>)}
    >
      {loading ? (
        <span className="btnIcon loading">
          <FaSpinner />
        </span>
      ) : (
        icon && <span className="btnIcon">{renderIcon()}</span>
      )}
      {label && <span className="btnLabel">{label}</span>}
      {children}
    </button>
  );
};

export default Button;
