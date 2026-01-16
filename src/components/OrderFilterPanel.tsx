"use client";

import { useLanguage } from "@/context/LanguageContext";
import { translations } from "@/lib/translations";
import { useEffect, useState } from "react";
import { DayNavigator } from "./DayNavigator";

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
  const getTodayDateString = () => {
    const today = new Date();
    return today.toISOString().split("T")[0];
  };

  const [selectedDate, setSelectedDate] =
    useState<string>(getTodayDateString());
  const [selectedPaymentMethod, setSelectedPaymentMethod] =
    useState<string>("");
  const [selectedLocationId, setSelectedLocationId] = useState<string>("");
  const [isInitialized, setIsInitialized] = useState(false);

  // Safely access language context with fallback
  let language: "en" | "ru" = "en";
  let t = translations.en.client;
  try {
    const langContext = useLanguage();
    language = langContext.language;
    t = translations[language]?.client || translations.en.client;
  } catch (e) {
    // If language context not available, use English as default
    t = translations.en.client;
  }

  // Initialize filters on mount with today's date
  useEffect(() => {
    if (!isInitialized) {
      onFiltersChange({
        startDate: selectedDate,
        endDate: selectedDate,
        paymentMethod: undefined,
        locationId: undefined,
      });
      setIsInitialized(true);
    }
  }, []);

  const handleDateChange = (date: string) => {
    setSelectedDate(date);
    onFiltersChange({
      startDate: date,
      endDate: date,
      paymentMethod: selectedPaymentMethod || undefined,
      locationId: selectedLocationId || undefined,
    });
  };

  const handlePaymentMethodChange = (method: string) => {
    const newMethod = selectedPaymentMethod === method ? "" : method;
    setSelectedPaymentMethod(newMethod);
    onFiltersChange({
      startDate: selectedDate,
      endDate: selectedDate,
      paymentMethod: newMethod || undefined,
      locationId: selectedLocationId || undefined,
    });
  };

  const handleLocationChange = (locationId: string) => {
    const newLocationId = selectedLocationId === locationId ? "" : locationId;
    setSelectedLocationId(newLocationId);
    onFiltersChange({
      startDate: selectedDate,
      endDate: selectedDate,
      paymentMethod: selectedPaymentMethod || undefined,
      locationId: newLocationId || undefined,
    });
  };

  return (
    <div className="bg-white rounded-lg shadow-md p-4 mb-6">
      <div className="flex items-center gap-3 flex-wrap justify-start">
        {/* Day Navigator */}
        <DayNavigator
          onDateChange={handleDateChange}
          defaultDate={selectedDate}
        />

        {/* Payment Method Dropdown */}
        <select
          value={selectedPaymentMethod}
          onChange={(e) => handlePaymentMethodChange(e.target.value)}
          title={t.paymentMethodLabel}
          className={`px-4 py-2 rounded-lg border-2 font-medium transition appearance-none cursor-pointer text-sm ${
            selectedPaymentMethod
              ? "border-amber-600 bg-amber-50 text-gray-900"
              : "border-gray-300 bg-white text-gray-700 hover:border-gray-400"
          }`}
        >
          <option value="">💳 {t.allMethods}</option>
          <option value="cash">💰 {t.cash}</option>
          <option value="card">🏧 {t.card}</option>
          <option value="stripe">💎 {t.stripe}</option>
        </select>

        {/* Location Dropdown */}
        <select
          value={selectedLocationId}
          onChange={(e) => handleLocationChange(e.target.value)}
          title={t.location}
          className={`px-4 py-2 rounded-lg border-2 font-medium transition appearance-none cursor-pointer text-sm ${
            selectedLocationId
              ? "border-amber-600 bg-amber-50 text-gray-900"
              : "border-gray-300 bg-white text-gray-700 hover:border-gray-400"
          }`}
        >
          <option value="">📍 {t.allLocations}</option>
          {locations.map((location) => (
            <option key={location._id} value={location._id}>
              {location.name}
            </option>
          ))}
        </select>

        {/* Reset Button - Only show if filters are active */}
        {(selectedPaymentMethod || selectedLocationId) && (
          <button
            onClick={() => {
              setSelectedPaymentMethod("");
              setSelectedLocationId("");
              onFiltersChange({
                startDate: selectedDate,
                endDate: selectedDate,
                paymentMethod: undefined,
                locationId: undefined,
              });
            }}
            className="px-3 py-2 text-sm font-semibold text-gray-600 bg-gray-100 rounded-lg hover:bg-gray-200 transition"
            title={language === "ru" ? "Сбросить фильтры" : "Reset filters"}
          >
            ✕
          </button>
        )}
      </div>
    </div>
  );
}
