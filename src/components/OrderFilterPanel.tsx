"use client";

import { useState } from "react";
import { FormField, SelectField } from "./FormFields";

export interface OrderFilters {
  status?: string;
  paymentMethod?: string;
  locationId?: string;
  searchQuery?: string;
  startDate?: string;
  endDate?: string;
}

interface OrderFilterPanelProps {
  onFiltersChange: (filters: OrderFilters) => void;
  locations: Array<{ _id: string; name: string }>;
}

export function OrderFilterPanel({
  onFiltersChange,
  locations,
}: OrderFilterPanelProps) {
  const [isExpanded, setIsExpanded] = useState(false);
  const [filters, setFilters] = useState<OrderFilters>({
    status: "all",
    paymentMethod: "",
    locationId: "",
    searchQuery: "",
    startDate: "",
    endDate: "",
  });

  const handleFilterChange = (field: keyof OrderFilters, value: string) => {
    const newFilters = { ...filters, [field]: value || undefined };
    setFilters(newFilters);
    onFiltersChange(newFilters);
  };

  const handleReset = () => {
    const resetFilters: OrderFilters = {
      status: "all",
      paymentMethod: "",
      locationId: "",
      searchQuery: "",
      startDate: "",
      endDate: "",
    };
    setFilters(resetFilters);
    onFiltersChange(resetFilters);
  };

  const hasActiveFilters =
    (filters.status && filters.status !== "all") ||
    filters.paymentMethod ||
    filters.locationId ||
    filters.searchQuery ||
    filters.startDate ||
    filters.endDate;

  return (
    <div className="bg-white rounded-lg shadow mb-6">
      <button
        onClick={() => setIsExpanded(!isExpanded)}
        className="w-full px-6 py-4 flex items-center justify-between hover:bg-gray-50 transition"
      >
        <div className="flex items-center gap-3">
          <span className="text-lg font-semibold text-gray-900">
            🔍 Advanced Filters
          </span>
          {hasActiveFilters && (
            <span className="px-2 py-1 text-xs font-bold bg-blue-100 text-blue-700 rounded-full">
              Active
            </span>
          )}
        </div>
        <svg
          className={`w-5 h-5 transition-transform ${
            isExpanded ? "rotate-180" : ""
          }`}
          fill="none"
          stroke="currentColor"
          viewBox="0 0 24 24"
        >
          <path
            strokeLinecap="round"
            strokeLinejoin="round"
            strokeWidth={2}
            d="M19 14l-7 7m0 0l-7-7m7 7V3"
          />
        </svg>
      </button>

      {isExpanded && (
        <div className="px-6 py-4 border-t border-gray-200 bg-gray-50">
          <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-4 mb-4">
            {/* Search */}
            <FormField
              id="search"
              label="Search"
              type="text"
              value={filters.searchQuery || ""}
              onChange={(e) =>
                handleFilterChange("searchQuery", e.target.value)
              }
              placeholder="Order #, customer name..."
            />

            {/* Status Filter */}
            <SelectField
              id="status"
              label="Status"
              value={filters.status || "all"}
              onChange={(e) => handleFilterChange("status", e.target.value)}
              options={[
                { value: "all", label: "All Statuses" },
                { value: "pending", label: "Pending" },
                { value: "accepted", label: "Accepted" },
                { value: "rejected", label: "Rejected" },
                { value: "completed", label: "Completed" },
              ]}
            />

            {/* Payment Method Filter */}
            <SelectField
              id="paymentMethod"
              label="Payment Method"
              value={filters.paymentMethod || ""}
              onChange={(e) =>
                handleFilterChange("paymentMethod", e.target.value)
              }
              options={[
                { value: "", label: "All Methods" },
                { value: "cash", label: "Cash" },
                { value: "card", label: "Card" },
                { value: "stripe", label: "Stripe" },
              ]}
            />

            {/* Location Filter */}
            <SelectField
              id="locationId"
              label="Location"
              value={filters.locationId || ""}
              onChange={(e) => handleFilterChange("locationId", e.target.value)}
              options={[
                { value: "", label: "All Locations" },
                ...locations.map((loc) => ({
                  value: loc._id,
                  label: loc.name,
                })),
              ]}
            />

            {/* Start Date */}
            <FormField
              id="startDate"
              label="Start Date"
              type="date"
              value={filters.startDate || ""}
              onChange={(e) => handleFilterChange("startDate", e.target.value)}
            />

            {/* End Date */}
            <FormField
              id="endDate"
              label="End Date"
              type="date"
              value={filters.endDate || ""}
              onChange={(e) => handleFilterChange("endDate", e.target.value)}
            />
          </div>

          {/* Reset Button */}
          {hasActiveFilters && (
            <button
              onClick={handleReset}
              className="px-4 py-2 text-sm text-gray-700 bg-white border border-gray-300 rounded-lg hover:bg-gray-50 transition"
            >
              Reset Filters
            </button>
          )}
        </div>
      )}
    </div>
  );
}
