import { getCustomerName } from "@/lib/helpers";
import { IOrder } from "@/types";

interface OrderCardProps {
  order: IOrder;
  isSelected: boolean;
  isHighlighted: boolean;
  onClick: () => void;
  statusColor: "yellow" | "blue" | "green" | "red";
}

export function OrderCard({
  order,
  isSelected,
  isHighlighted,
  onClick,
  statusColor,
}: OrderCardProps) {
  const colorMap = {
    yellow: {
      border: "border-yellow-200",
      background: "bg-yellow-50",
      hover: "hover:bg-yellow-100",
      selected: "bg-yellow-100 border-yellow-500",
      highlighted: "bg-yellow-50 border-yellow-400 shadow-md animate-pulse",
      badge: "bg-yellow-200 text-yellow-800",
    },
    blue: {
      border: "border-blue-200",
      background: "bg-blue-50",
      hover: "hover:bg-blue-100",
      selected: "bg-blue-100 border-blue-500",
      highlighted: "bg-blue-50 border-blue-400 shadow-md animate-pulse",
      badge: "bg-blue-200 text-blue-800",
    },
    green: {
      border: "border-green-200",
      background: "bg-green-50",
      hover: "hover:bg-green-100",
      selected: "bg-green-100 border-green-500",
      highlighted: "bg-green-50 border-green-400 shadow-md animate-pulse",
      badge: "bg-green-200 text-green-800",
    },
    red: {
      border: "border-red-200",
      background: "bg-red-50",
      hover: "hover:bg-red-100",
      selected: "bg-red-100 border-red-500",
      highlighted: "bg-red-50 border-red-400 shadow-md animate-pulse",
      badge: "bg-red-200 text-red-800",
    },
  };

  const colors = colorMap[statusColor];

  let backgroundColor = colors.background;
  if (isSelected) {
    backgroundColor = colors.selected;
  } else if (isHighlighted) {
    backgroundColor = colors.highlighted;
  } else {
    backgroundColor = `${colors.background} ${colors.hover}`;
  }

  const statusLabels = {
    pending: "Pending",
    accepted: "Accepted",
    completed: "Completed",
    rejected: "Rejected",
  };

  return (
    <button
      onClick={onClick}
      className={`w-full text-left border ${colors.border} rounded-lg p-4 transition-all cursor-pointer ${backgroundColor}`}
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
      <p className="text-sm text-gray-500">{order.items?.length || 0} items</p>
      <p className="text-lg font-bold text-gray-900 mt-3">
        {order.totalPrice.toFixed(2)} ₸
      </p>
      <p className="text-xs text-gray-500 mt-2">
        {new Date(order.createdAt || "").toLocaleDateString()}
      </p>
    </button>
  );
}
