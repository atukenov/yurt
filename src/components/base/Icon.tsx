import { ReactNode } from "react";
import { IconCode, iconMap } from "./iconMap";

type TIcon = {
  code?: IconCode;
  className?: string;
  type?: "solid" | "duotone" | string;
  size?: number;
  children?: ReactNode;
};

const Icon = ({ code, className, type, size, children }: TIcon) => {
  // If children provided (direct ReactNode), render it
  if (children) {
    return (
      <span className={className} style={{ fontSize: size }}>
        {children}
      </span>
    );
  }

  // If code provided, look it up in the map
  if (code) {
    const icon = iconMap[code];
    if (icon) {
      return (
        <span className={className} style={{ fontSize: size }}>
          {icon}
        </span>
      );
    }
    // Fallback for unmapped codes
    console.warn(`Icon code '${code}' not mapped to react-icons`);
    return (
      <span className={className} style={{ fontSize: size }}>
        �
      </span>
    );
  }

  return null;
};

export default Icon;
