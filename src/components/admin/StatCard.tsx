interface StatCardProps {
  label: string;
  value: number;
  color: "gray" | "yellow" | "blue" | "green" | "red";
}

export function StatCard({ label, value, color }: StatCardProps) {
  const colorMap = {
    gray: "text-gray-900",
    yellow: "text-yellow-600",
    blue: "text-blue-600",
    green: "text-green-600",
    red: "text-red-600",
  };

  return (
    <div className="bg-white rounded-lg shadow p-6">
      <div className="flex items-center justify-between">
        <div>
          <p className="text-sm text-gray-600">{label}</p>
          <p className={`text-3xl font-bold ${colorMap[color]}`}>{value}</p>
        </div>
      </div>
    </div>
  );
}
