"use client";

import { ButtonHTMLAttributes, ReactNode } from "react";

import clsx from "clsx";
import { FaEye, FaEyeSlash, FaPen, FaSpinner } from "react-icons/fa";

import "./button.scss";

type IconCode = string;

const iconMap: Record<IconCode, ReactNode> = {
  // legacy xtreme-ui codes mapped to react-icons
  f06e: <FaEye />, // visible
  f070: <FaEyeSlash />, // hidden
  f304: <FaPen />, // edit
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
