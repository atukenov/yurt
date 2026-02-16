import { IOrder } from "@/types";
import { OrderCard } from "./OrderCard";

interface OrderColumnSectionProps {
  title: string;
  orders: IOrder[];
  selectedOrderId?: string;
  highlightedOrders: { [key: string]: boolean };
  onSelectOrder: (order: IOrder) => void;
  statusColor: "yellow" | "blue" | "green" | "red";
  headerColor: "yellow" | "blue" | "green" | "red";
}

export function OrderColumnSection({
  title,
  orders,
  selectedOrderId,
  highlightedOrders,
  onSelectOrder,
  statusColor,
  headerColor,
}: OrderColumnSectionProps) {
  const bgColorMap = {
    yellow: "bg-yellow-50",
    blue: "bg-blue-50",
    green: "bg-green-50",
    red: "bg-red-50",
  };

  return (
    <div className="bg-white rounded-lg shadow">
      <div
        className={`border-b border-gray-200 px-6 py-4 ${bgColorMap[headerColor]}`}
      >
        <h3 className="text-lg font-bold text-gray-900">
          {title} ({orders.length})
        </h3>
      </div>
      <div className="p-4 space-y-4 max-h-96 overflow-y-auto">
        {orders.length === 0 ? (
          <p className="text-center text-gray-500 py-8">
            No {title.toLowerCase()}
          </p>
        ) : (
          orders.map((order) => (
            <OrderCard
              key={order._id}
              order={order}
              isSelected={selectedOrderId === order._id}
              isHighlighted={highlightedOrders[order._id] || false}
              onClick={() => onSelectOrder(order)}
              statusColor={statusColor}
            />
          ))
        )}
      </div>
    </div>
  );
}
