"use client";

import { useLanguage } from "@/context/LanguageContext";
import { translations } from "@/lib/translations";
import { useCartStore } from "@/store/cart";
import { ILocation } from "@/types";
import { useEffect, useState } from "react";

export function LocationSelector() {
  const [locations, setLocations] = useState<ILocation[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");

  const selectedLocation = useCartStore((state) => state.locationId);
  const setLocation = useCartStore((state) => state.setLocation);

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

  useEffect(() => {
    const fetchLocations = async () => {
      try {
        setLoading(true);
        const res = await fetch("/api/locations");
        if (!res.ok) throw new Error("Failed to fetch locations");
        const data = await res.json();
        setLocations(data.locations || []);

        // Set first location as default if none selected
        if (!selectedLocation && data.locations?.length > 0) {
          setLocation(data.locations[0]._id);
        }
      } catch (err) {
        setError(t.failedLoadLocations);
        console.error("Error fetching locations:", err);
      } finally {
        setLoading(false);
      }
    };

    fetchLocations();
  }, [selectedLocation, setLocation]);

  if (loading) {
    return (
      <div className="px-4 py-2 bg-gray-100 rounded-lg text-gray-600 text-sm">
        {t.loadingLocations}
      </div>
    );
  }

  if (error) {
    return (
      <div className="px-4 py-2 bg-red-100 rounded-lg text-red-600 text-sm">
        {error}
      </div>
    );
  }

  if (locations.length === 0) {
    return (
      <div className="px-4 py-2 bg-yellow-100 rounded-lg text-yellow-600 text-sm">
        {t.noLocationsAvailable}
      </div>
    );
  }

  const currentLocation = locations.find((loc) => loc._id === selectedLocation);

  return (
    <div className="flex items-center whitespace-nowrap gap-2">
      <svg
        className="w-5 h-5 text-gray-600 flex-shrink-0"
        fill="none"
        stroke="currentColor"
        viewBox="0 0 24 24"
      >
        <path
          strokeLinecap="round"
          strokeLinejoin="round"
          strokeWidth={2}
          d="M17.657 16.657L13.414 20.9a1.998 1.998 0 01-2.827 0l-4.244-4.243a8 8 0 1111.314 0z"
        />
        <path
          strokeLinecap="round"
          strokeLinejoin="round"
          strokeWidth={2}
          d="M15 11a3 3 0 11-6 0 3 3 0 016 0z"
        />
      </svg>
      <select
        value={selectedLocation || ""}
        onChange={(e) => setLocation(e.target.value)}
        className="px-3 py-2 border border-gray-300 rounded-lg text-gray-700 hover:border-amber-600 focus:outline-none focus:border-amber-600 focus:ring-1 focus:ring-amber-600 transition bg-white text-sm"
        title={t.location}
      >
        <option value="" disabled>
          {t.selectLocation}
        </option>
        {locations.map((location) => (
          <option key={location._id} value={location._id}>
            {location.name}
          </option>
        ))}
      </select>
    </div>
  );
}
