import { getCustomerName } from "@/lib/helpers";
import { translations, type Language } from "@/lib/translations";
import { IOrder } from "@/types";

interface OrderCardProps {
  order: IOrder;
  isSelected: boolean;
  isHighlighted: boolean;
  onClick: () => void;
  statusColor: "yellow" | "blue" | "green" | "red";
  language?: Language;
}

export function OrderCard({
  order,
  isSelected,
  isHighlighted,
  onClick,
  statusColor,
  language = "ru",
}: OrderCardProps) {
  const t = translations[language]?.admin || translations.ru.admin;
  const statusLabels = t.statusLabels || {
    pending: "Pending",
    accepted: "Accepted",
    completed: "Completed",
    rejected: "Rejected",
  };
  const colorMap = {
    yellow: {
      border: "border-yellow-200",
      background: "bg-yellow-50",
      hover: "hover:bg-yellow-100",
      selected: "bg-yellow-100 border-yellow-500",
      badge: "bg-yellow-200 text-yellow-800",
    },
    blue: {
      border: "border-blue-200",
      background: "bg-blue-50",
      hover: "hover:bg-blue-100",
      selected: "bg-blue-100 border-blue-500",
      badge: "bg-blue-200 text-blue-800",
    },
    green: {
      border: "border-green-200",
      background: "bg-green-50",
      hover: "hover:bg-green-100",
      selected: "bg-green-100 border-green-500",
      badge: "bg-green-200 text-green-800",
    },
    red: {
      border: "border-red-200",
      background: "bg-red-50",
      hover: "hover:bg-red-100",
      selected: "bg-red-100 border-red-500",
      badge: "bg-red-200 text-red-800",
    },
  };

  const colors = colorMap[statusColor];

  let backgroundColor = colors.background;
  let borderClass = colors.border;

  if (isSelected) {
    backgroundColor = colors.selected;
  } else if (isHighlighted) {
    // Use inline styles for highlighting instead of classes
    backgroundColor = "";
    borderClass = "";
  } else {
    backgroundColor = `${colors.background} ${colors.hover}`;
  }

  return (
    <>
      <style>{`
        @keyframes pulse-highlight {
          0%, 100% {
            background-color: rgb(220, 252, 231);
            border-color: rgb(34, 197, 94);
            box-shadow: 0 10px 15px -3px rgba(34, 197, 94, 0.5);
          }
          50% {
            background-color: rgb(187, 247, 208);
            border-color: rgb(22, 163, 74);
            box-shadow: 0 20px 25px -5px rgba(34, 197, 94, 0.7);
          }
        }
      `}</style>
      <button
        onClick={onClick}
        className={`w-full text-left border rounded-lg p-4 transition-all cursor-pointer ${borderClass} ${backgroundColor}`}
        style={
          isHighlighted
            ? {
                animation:
                  "pulse-highlight 0.5s cubic-bezier(0.4, 0, 0.6, 1) infinite",
                backgroundColor: "rgb(220, 252, 231)",
                borderColor: "rgb(34, 197, 94)",
                boxShadow: "0 10px 15px -3px rgba(34, 197, 94, 0.5)",
              }
            : {}
        }
      >
        <div className="flex justify-between items-start mb-2">
          <span className="font-bold text-gray-900">{order.orderNumber}</span>
          <span className={`text-xs ${colors.badge} px-2 py-1 rounded`}>
            {statusLabels[order.status as keyof typeof statusLabels]}
          </span>
        </div>
        <p className="text-sm text-gray-600 font-medium">
          {getCustomerName(order.customer)}
        </p>
        <p className="text-sm text-gray-500">
          {order.items?.length || 0} items
        </p>
        <p className="text-lg font-bold text-gray-900 mt-3">
          {order.totalPrice.toFixed(2)} ₸
        </p>
        <p className="text-xs text-gray-500 mt-2">
          {new Date(order.createdAt || "").toLocaleDateString()}
        </p>
      </button>
    </>
  );
}
