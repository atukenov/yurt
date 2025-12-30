import { IOrder } from "@/types";

export function exportOrdersToCSV(
  orders: IOrder[],
  filename: string = "orders.csv"
) {
  try {
    // Prepare CSV header
    const headers = [
      "Order ID",
      "Customer",
      "Email",
      "Status",
      "Payment Method",
      "Total Price",
      "Location",
      "Created Date",
      "Item Count",
      "Items",
    ];

    // Prepare CSV rows
    const rows = orders.map((order) => [
      order._id,
      typeof order.customer === "object" ? order.customer.name : "Unknown",
      typeof order.customer === "object" ? order.customer.email : "Unknown",
      order.status,
      order.paymentMethod || "Not specified",
      `$${order.totalPrice.toFixed(2)}`,
      typeof order.location === "object" ? order.location.name : "Unknown",
      new Date(order.createdAt).toLocaleDateString(),
      order.items.length,
      order.items
        .map((item) => {
          const menuItemName =
            typeof item.menuItem === "object" ? item.menuItem.name : "Unknown";
          return `${item.quantity}x ${menuItemName}`;
        })
        .join("; "),
    ]);

    // Combine headers and rows
    const csvContent = [
      headers.join(","),
      ...rows.map((row) =>
        row
          .map((cell) =>
            typeof cell === "string" && cell.includes(",")
              ? `"${cell.replace(/"/g, '""')}"` // Escape quotes in CSV
              : cell
          )
          .join(",")
      ),
    ].join("\n");

    // Create blob and download
    const blob = new Blob([csvContent], { type: "text/csv;charset=utf-8;" });
    const link = document.createElement("a");
    const url = URL.createObjectURL(blob);

    link.setAttribute("href", url);
    link.setAttribute("download", filename);
    link.style.visibility = "hidden";

    document.body.appendChild(link);
    link.click();
    document.body.removeChild(link);
  } catch (error) {
    console.error("Error exporting to CSV:", error);
    throw error;
  }
}

export function exportOrdersToPDF(orders: IOrder[]) {
  try {
    // Create a simple HTML table for PDF generation
    const htmlContent = `
      <html>
        <head>
          <style>
            body { font-family: Arial, sans-serif; margin: 20px; }
            h1 { text-align: center; color: #333; }
            table { width: 100%; border-collapse: collapse; margin-top: 20px; }
            th, td { border: 1px solid #ddd; padding: 8px; text-align: left; }
            th { background-color: #f2f2f2; font-weight: bold; }
            tr:nth-child(even) { background-color: #f9f9f9; }
            .status-pending { color: orange; }
            .status-accepted { color: green; }
            .status-rejected { color: red; }
            .status-completed { color: blue; }
          </style>
        </head>
        <body>
          <h1>Orders Report</h1>
          <p>Generated: ${new Date().toLocaleString()}</p>
          <table>
            <thead>
              <tr>
                <th>Order ID</th>
                <th>Customer</th>
                <th>Status</th>
                <th>Payment</th>
                <th>Total</th>
                <th>Location</th>
                <th>Date</th>
              </tr>
            </thead>
            <tbody>
              ${orders
                .map(
                  (order) => `
                <tr>
                  <td>${order._id}</td>
                  <td>${
                    typeof order.customer === "object"
                      ? order.customer.name
                      : "Unknown"
                  }</td>
                  <td class="status-${order.status}">${order.status}</td>
                  <td>${order.paymentMethod || "N/A"}</td>
                  <td>$${order.totalPrice.toFixed(2)}</td>
                  <td>${
                    typeof order.location === "object"
                      ? order.location.name
                      : "Unknown"
                  }</td>
                  <td>${new Date(order.createdAt).toLocaleDateString()}</td>
                </tr>
              `
                )
                .join("")}
            </tbody>
          </table>
          <p style="margin-top: 20px; text-align: center; color: #999; font-size: 12px;">
            Total Orders: ${orders.length}
          </p>
        </body>
      </html>
    `;

    // Open in new window and print to PDF
    const newWindow = window.open("", "", "height=600,width=800");
    if (newWindow) {
      newWindow.document.write(htmlContent);
      newWindow.document.close();
      // Delay to ensure content is rendered before printing
      setTimeout(() => {
        newWindow.print();
      }, 250);
    }
  } catch (error) {
    console.error("Error exporting to PDF:", error);
    throw error;
  }
}

export function formatOrdersForClipboard(orders: IOrder[]): string {
  const lines = orders.map(
    (order) =>
      `${order._id} | ${
        typeof order.customer === "object" ? order.customer.name : "Unknown"
      } | ${order.status} | $${order.totalPrice.toFixed(2)}`
  );
  return lines.join("\n");
}
