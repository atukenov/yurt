import { errorLogger } from "@/lib/logger";
import { exportOrdersToCSV, exportOrdersToPDF } from "@/lib/orderExport";
import { IOrder } from "@/types";
import { useState } from "react";

interface ExportSectionProps {
  orders: IOrder[];
}

export function ExportSection({ orders }: ExportSectionProps) {
  const [exporting, setExporting] = useState(false);

  if (orders.length === 0) return null;

  const handleExportCSV = async () => {
    try {
      setExporting(true);
      exportOrdersToCSV(
        orders,
        `orders-${new Date().toISOString().split("T")[0]}.csv`
      );
      errorLogger.info("Exported orders to CSV", { count: orders.length });
    } catch (error) {
      errorLogger.error(
        "Error exporting CSV",
        {},
        error instanceof Error ? error : new Error("Export failed")
      );
      alert("Failed to export CSV");
    } finally {
      setExporting(false);
    }
  };

  const handleExportPDF = async () => {
    try {
      setExporting(true);
      exportOrdersToPDF(orders);
      errorLogger.info("Exported orders to PDF", { count: orders.length });
    } catch (error) {
      errorLogger.error(
        "Error exporting PDF",
        {},
        error instanceof Error ? error : new Error("Export failed")
      );
      alert("Failed to export PDF");
    } finally {
      setExporting(false);
    }
  };

  return (
    <div className="flex gap-3 mb-8">
      <button
        onClick={handleExportCSV}
        disabled={exporting}
        className="px-4 py-2 bg-green-600 text-white rounded-lg hover:bg-green-700 disabled:opacity-50 font-semibold transition flex items-center gap-2"
      >
        📥 Export CSV ({orders.length})
      </button>
      <button
        onClick={handleExportPDF}
        disabled={exporting}
        className="px-4 py-2 bg-blue-600 text-white rounded-lg hover:bg-blue-700 disabled:opacity-50 font-semibold transition flex items-center gap-2"
      >
        📄 Export PDF ({orders.length})
      </button>
    </div>
  );
}
