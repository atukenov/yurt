import {
  getCustomerEmail,
  getCustomerName,
  getCustomerPhone,
  getLocationName,
  getMenuItemName,
} from "@/lib/helpers";
import { IOrder } from "@/types";

interface OrderDetailsProps {
  order: IOrder;
  onAccept: (prepTime: number) => void;
  onReject: (reason: string, comment: string) => void;
  onComplete: () => void;
}

function getToppingName(topping: any) {
  return topping && typeof topping === "object"
    ? topping.name || ""
    : topping || "";
}

export function OrderDetailsSidebar({
  order,
  onAccept,
  onReject,
  onComplete,
}: OrderDetailsProps) {
  const statusConfig = {
    pending: {
      color: "yellow-100",
      textColor: "yellow-800",
      bgGradient: "from-yellow-50 to-yellow-100",
      label: "Pending",
    },
    accepted: {
      color: "blue-100",
      textColor: "blue-800",
      bgGradient: "from-blue-50 to-blue-100",
      label: "Accepted",
    },
    completed: {
      color: "green-100",
      textColor: "green-800",
      bgGradient: "from-green-50 to-green-100",
      label: "Completed",
    },
    rejected: {
      color: "red-100",
      textColor: "red-800",
      bgGradient: "from-red-50 to-red-100",
      label: "Rejected",
    },
    cancelled: {
      color: "gray-100",
      textColor: "gray-800",
      bgGradient: "from-gray-50 to-gray-100",
      label: "Cancelled",
    },
  };

  const status = statusConfig[order.status as keyof typeof statusConfig];
  const paymentStatusColor =
    order.paymentStatus === "completed" ? "text-green-600" : "text-yellow-600";

  return (
    <div className="lg:col-span-1">
      <div className="bg-white rounded-lg shadow sticky top-8 max-h-[calc(100vh-10rem)] overflow-y-auto">
        <div
          className={`border-b border-gray-200 px-6 py-4 bg-gradient-to-r ${status.bgGradient}`}
        >
          <h3 className="text-lg font-bold text-gray-900">
            Order {order.orderNumber}
          </h3>
          <p className="text-sm text-gray-600 mt-1">
            {new Date(order.createdAt || "").toLocaleString()}
          </p>
        </div>

        <div className="p-6 space-y-6">
          {/* Customer Info */}
          <div>
            <p className="text-xs text-gray-600 uppercase font-bold">
              Customer
            </p>
            <p className="font-semibold text-gray-900 mt-2">
              {getCustomerName(order.customer)}
            </p>
            <p className="text-sm text-gray-600">
              {getCustomerEmail(order.customer)}
            </p>
            <p className="text-sm text-gray-600">
              {getCustomerPhone(order.customer)}
            </p>
          </div>

          {/* Location Info */}
          <div>
            <p className="text-xs text-gray-600 uppercase font-bold">
              Location
            </p>
            <p className="font-semibold text-gray-900 mt-2">
              {getLocationName(order.location)}
            </p>
          </div>

          {/* Order Items */}
          <div>
            <p className="text-xs text-gray-600 uppercase font-bold mb-3">
              Items ({order.items?.length || 0})
            </p>
            <div className="space-y-3">
              {order.items?.map((item, idx) => (
                <div
                  key={idx}
                  className="bg-gray-50 rounded-lg p-3 border border-gray-200"
                >
                  <div className="flex justify-between items-start mb-2">
                    <p className="font-semibold text-gray-900">
                      {getMenuItemName(item.menuItem)}
                    </p>
                    <span className="text-xs bg-amber-100 text-amber-800 px-2 py-1 rounded">
                      x{item.quantity}
                    </span>
                  </div>

                  <div className="text-sm text-gray-600 space-y-1">
                    <p>
                      <span className="font-medium">Size:</span>{" "}
                      {item.size.charAt(0).toUpperCase() + item.size.slice(1)}
                    </p>

                    {item.toppings && item.toppings.length > 0 && (
                      <div>
                        <p className="font-medium">Toppings:</p>
                        <ul className="list-disc list-inside ml-0 text-xs">
                          {item.toppings.map((topping, tIdx) => (
                            <li key={tIdx}>{getToppingName(topping)}</li>
                          ))}
                        </ul>
                      </div>
                    )}

                    {item.specialInstructions && (
                      <p>
                        <span className="font-medium">Note:</span>{" "}
                        {item.specialInstructions}
                      </p>
                    )}
                  </div>

                  <p className="text-sm font-semibold text-gray-900 mt-2">
                    {item.priceAtOrder?.toFixed(2) || "0.00"} ₸
                  </p>
                </div>
              ))}
            </div>
          </div>

          {/* Order Notes */}
          {order.notes && (
            <div>
              <p className="text-xs text-gray-600 uppercase font-bold">Notes</p>
              <p className="text-sm text-gray-700 mt-2 bg-blue-50 p-3 rounded border border-blue-200">
                {order.notes}
              </p>
            </div>
          )}

          {/* Rejection Reason */}
          {order.rejectionReason && (
            <div>
              <p className="text-xs text-gray-600 uppercase font-bold">
                Rejection Reason
              </p>
              <p className="text-sm text-gray-700 mt-2 bg-red-50 p-3 rounded border border-red-200">
                <span className="font-semibold">{order.rejectionReason}</span>
                {order.rejectionComment && (
                  <>
                    <br />
                    {order.rejectionComment}
                  </>
                )}
              </p>
            </div>
          )}

          {/* Order Summary */}
          <div className="border-t border-gray-200 pt-4">
            <div className="flex justify-between items-center mb-3">
              <span className="text-gray-600">Subtotal:</span>
              <span className="font-semibold">
                {(order.totalPrice || 0).toFixed(2)} ₸
              </span>
            </div>
            <div className="flex justify-between items-center mb-3">
              <span className="text-gray-600">Payment:</span>
              <span className="text-sm">
                {order.paymentMethod.charAt(0).toUpperCase() +
                  order.paymentMethod.slice(1)}
                {" • "}
                <span className={`font-semibold ${paymentStatusColor}`}>
                  {order.paymentStatus.charAt(0).toUpperCase() +
                    order.paymentStatus.slice(1)}
                </span>
              </span>
            </div>
            <div className="flex justify-between items-center mb-3">
              <span className="text-gray-600">Status:</span>
              <span
                className={`text-sm font-semibold px-2 py-1 rounded ${
                  order.status === "pending"
                    ? "bg-yellow-100 text-yellow-800"
                    : order.status === "accepted"
                    ? "bg-blue-100 text-blue-800"
                    : order.status === "completed"
                    ? "bg-green-100 text-green-800"
                    : "bg-red-100 text-red-800"
                }`}
              >
                {status.label}
              </span>
            </div>
          </div>

          {/* Action Buttons */}
          <div className="bg-gray-50 rounded-lg p-4 space-y-3 border border-gray-200">
            {order.status === "pending" && (
              <>
                <div>
                  <label className="text-xs text-gray-600 uppercase font-bold">
                    Prep Time
                  </label>
                  <select
                    defaultValue="15"
                    id="prep-time"
                    className="w-full mt-1 px-3 py-2 border border-gray-300 rounded-lg focus:outline-none focus:ring-2 focus:ring-amber-500"
                  >
                    <option value="10">10 minutes</option>
                    <option value="15">15 minutes</option>
                    <option value="30">30 minutes</option>
                  </select>
                </div>
                <button
                  onClick={() => {
                    const prepTime = parseInt(
                      (
                        document.getElementById(
                          "prep-time"
                        ) as HTMLSelectElement
                      )?.value || "15"
                    );
                    onAccept(prepTime);
                  }}
                  className="w-full px-4 py-2 bg-green-600 text-white rounded-lg hover:bg-green-700 font-semibold transition"
                >
                  ✓ Accept Order
                </button>

                <div>
                  <label className="text-xs text-gray-600 uppercase font-bold">
                    Reject Reason
                  </label>
                  <select
                    defaultValue="no_milk"
                    id="reject-reason"
                    className="w-full mt-1 px-3 py-2 border border-gray-300 rounded-lg focus:outline-none focus:ring-2 focus:ring-amber-500"
                  >
                    <option value="no_milk">No Milk</option>
                    <option value="no_coffee_beans">No Coffee Beans</option>
                    <option value="size_unavailable">Size Unavailable</option>
                    <option value="equipment_issue">Equipment Issue</option>
                    <option value="custom">Custom Reason</option>
                  </select>
                </div>

                <textarea
                  id="reject-comment"
                  placeholder="Additional comment (optional)"
                  className="w-full px-3 py-2 border border-gray-300 rounded-lg focus:outline-none focus:ring-2 focus:ring-amber-500 text-sm"
                  rows={2}
                />

                <button
                  onClick={() => {
                    const reason =
                      (
                        document.getElementById(
                          "reject-reason"
                        ) as HTMLSelectElement
                      )?.value || "";
                    const comment =
                      (
                        document.getElementById(
                          "reject-comment"
                        ) as HTMLTextAreaElement
                      )?.value || "";
                    onReject(reason, comment);
                  }}
                  className="w-full px-4 py-2 bg-red-600 text-white rounded-lg hover:bg-red-700 font-semibold transition"
                >
                  ✗ Reject Order
                </button>
              </>
            )}

            {order.status === "accepted" && (
              <button
                onClick={onComplete}
                className="w-full px-4 py-2 bg-blue-600 text-white rounded-lg hover:bg-blue-700 font-semibold transition"
              >
                ✓ Mark as Completed
              </button>
            )}

            {order.status === "completed" && (
              <div className="bg-green-100 border border-green-300 rounded-lg p-3 text-center">
                <p className="text-green-800 font-semibold">
                  ✓ Order Completed
                </p>
              </div>
            )}

            {order.status === "rejected" && (
              <div className="bg-red-100 border border-red-300 rounded-lg p-3 text-center">
                <p className="text-red-800 font-semibold">✗ Order Rejected</p>
              </div>
            )}
          </div>
        </div>
      </div>
    </div>
  );
}
