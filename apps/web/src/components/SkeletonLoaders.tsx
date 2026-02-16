// Skeleton loader components for smooth loading states

export function MenuItemSkeleton() {
  return (
    <div className="rounded-lg shadow-sm overflow-hidden animate-pulse">
      <div className="aspect-video bg-gray-300"></div>
      <div className="p-4 space-y-3">
        <div className="h-4 bg-gray-300 rounded w-3/4"></div>
        <div className="h-3 bg-gray-200 rounded w-full"></div>
        <div className="h-3 bg-gray-200 rounded w-5/6"></div>
        <div className="flex justify-between items-center pt-2">
          <div className="h-4 bg-gray-300 rounded w-16"></div>
          <div className="h-10 bg-amber-300 rounded w-20"></div>
        </div>
      </div>
    </div>
  );
}

export function MenuGridSkeleton({ count = 6 }: { count?: number }) {
  return (
    <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-4">
      {Array.from({ length: count }).map((_, i) => (
        <MenuItemSkeleton key={i} />
      ))}
    </div>
  );
}

export function OrderCardSkeleton() {
  return (
    <div className="rounded-lg shadow-sm border-l-4 p-4 bg-white animate-pulse">
      <div className="mb-3 pb-3 border-b space-y-2">
        <div className="flex justify-between items-start gap-2">
          <div className="flex-1">
            <div className="h-3 bg-gray-200 rounded w-12 mb-1"></div>
            <div className="h-5 bg-gray-300 rounded w-32"></div>
          </div>
          <div className="h-8 bg-gray-300 rounded-full w-8"></div>
        </div>
        <div className="h-6 bg-gray-300 rounded-full w-20"></div>
      </div>

      <div className="space-y-2 mb-4 flex-1">
        {Array.from({ length: 3 }).map((_, i) => (
          <div key={i} className="flex justify-between">
            <div className="h-3 bg-gray-200 rounded w-24"></div>
            <div className="h-3 bg-gray-200 rounded w-16"></div>
          </div>
        ))}
      </div>

      <div className="flex gap-2 pt-3 border-t">
        <div className="flex-1 h-9 bg-gray-300 rounded"></div>
      </div>
    </div>
  );
}

export function OrderGridSkeleton({ count = 3 }: { count?: number }) {
  return (
    <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-4">
      {Array.from({ length: count }).map((_, i) => (
        <OrderCardSkeleton key={i} />
      ))}
    </div>
  );
}

export function OrderDetailsSkeleton() {
  return (
    <div className="min-h-screen bg-gray-50 py-8 px-4">
      <div className="max-w-2xl mx-auto animate-pulse">
        {/* Header */}
        <div className="mb-6">
          <div className="h-6 bg-gray-300 rounded w-32 mb-4"></div>
        </div>

        {/* Order Card */}
        <div className="bg-white rounded-lg shadow-lg p-6 space-y-6">
          {/* Header with order number and status */}
          <div className="border-b pb-6 space-y-4">
            <div className="flex justify-between gap-4">
              <div className="flex-1">
                <div className="h-3 bg-gray-200 rounded w-24 mb-2"></div>
                <div className="h-8 bg-gray-300 rounded w-48"></div>
              </div>
              <div className="h-12 w-12 bg-gray-300 rounded"></div>
            </div>
            <div className="h-6 bg-gray-300 rounded w-24"></div>
          </div>

          {/* Order Summary */}
          <div className="grid grid-cols-2 md:grid-cols-4 gap-4">
            {Array.from({ length: 4 }).map((_, i) => (
              <div key={i} className="bg-gray-50 p-4 rounded-lg space-y-2">
                <div className="h-3 bg-gray-200 rounded w-16"></div>
                <div className="h-6 bg-gray-300 rounded w-24"></div>
              </div>
            ))}
          </div>

          {/* Order Items */}
          <div className="space-y-3">
            <div className="h-5 bg-gray-300 rounded w-20 mb-4"></div>
            {Array.from({ length: 3 }).map((_, i) => (
              <div
                key={i}
                className="flex justify-between p-3 bg-gray-50 rounded-lg"
              >
                <div className="flex-1 space-y-2">
                  <div className="h-4 bg-gray-300 rounded w-32"></div>
                  <div className="h-3 bg-gray-200 rounded w-24"></div>
                  <div className="h-3 bg-gray-200 rounded w-20"></div>
                </div>
                <div className="text-right space-y-1">
                  <div className="h-4 bg-gray-300 rounded w-20"></div>
                  <div className="h-3 bg-gray-200 rounded w-12"></div>
                </div>
              </div>
            ))}
          </div>

          {/* Order Details */}
          <div className="border-t pt-6 space-y-3">
            <div className="h-5 bg-gray-300 rounded w-32 mb-4"></div>
            {Array.from({ length: 4 }).map((_, i) => (
              <div key={i} className="flex justify-between">
                <div className="h-3 bg-gray-200 rounded w-32"></div>
                <div className="h-3 bg-gray-200 rounded w-40"></div>
              </div>
            ))}
          </div>
        </div>
      </div>
    </div>
  );
}

export function CheckoutSkeleton() {
  return (
    <div className="min-h-screen bg-gray-50 py-8 px-4 animate-pulse">
      <div className="max-w-2xl mx-auto space-y-6">
        {/* Header */}
        <div className="h-8 bg-gray-300 rounded w-48"></div>

        {/* Main sections */}
        {Array.from({ length: 3 }).map((_, i) => (
          <div key={i} className="bg-white rounded-lg shadow p-6 space-y-4">
            <div className="h-6 bg-gray-300 rounded w-40 mb-4"></div>
            <div className="space-y-2">
              {Array.from({ length: 2 }).map((_, j) => (
                <div key={j} className="h-10 bg-gray-200 rounded"></div>
              ))}
            </div>
          </div>
        ))}

        {/* Button */}
        <div className="h-12 bg-gray-300 rounded-lg"></div>
      </div>
    </div>
  );
}

export function TableSkeleton({
  rows = 5,
  columns = 4,
}: {
  rows?: number;
  columns?: number;
}) {
  return (
    <div className="space-y-2 animate-pulse">
      {Array.from({ length: rows }).map((_, i) => (
        <div key={i} className="flex gap-3 bg-white p-4 rounded-lg">
          {Array.from({ length: columns }).map((_, j) => (
            <div
              key={j}
              className="h-4 bg-gray-200 rounded"
              style={{ width: `${100 / columns}%` }}
            ></div>
          ))}
        </div>
      ))}
    </div>
  );
}

export function PulseLoader({ text = "Loading..." }: { text?: string }) {
  return (
    <div className="flex items-center justify-center gap-2">
      <div className="flex gap-1">
        <div className="w-2 h-2 bg-[#ffd119] rounded-full animate-pulse"></div>
        <div
          className="w-2 h-2 bg-[#ffd119] rounded-full animate-pulse"
          style={{ animationDelay: "0.1s" }}
        ></div>
        <div
          className="w-2 h-2 bg-[#ffd119] rounded-full animate-pulse"
          style={{ animationDelay: "0.2s" }}
        ></div>
      </div>
      <span className="text-gray-600 ml-2">{text}</span>
    </div>
  );
}
