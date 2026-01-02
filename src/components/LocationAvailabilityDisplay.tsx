"use client";

import { useEffect, useState } from "react";
import { IconContext } from "react-icons";
import { MdCheckCircle, MdError } from "react-icons/md";

interface LocationAvailabilityStatus {
  locationId: string;
  name: string;
  isOpen: boolean;
  openTime?: string;
  closeTime?: string;
  reason?: string;
  hours?: {
    open: string;
    close: string;
  };
  nextAvailable?: {
    time: string;
    date: string;
    day: string;
  };
}

export function LocationAvailabilityDisplay({
  locationId,
  onAvailabilityChange,
}: {
  locationId: string;
  onAvailabilityChange?: (isAvailable: boolean) => void;
}) {
  const [availability, setAvailability] =
    useState<LocationAvailabilityStatus | null>(null);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    const fetchAvailability = async () => {
      try {
        const res = await fetch(`/api/locations/${locationId}/availability`);
        if (res.ok) {
          const data = await res.json();
          setAvailability(data);
          onAvailabilityChange?.(data.isOpen);
        }
      } catch (error) {
        console.error("Error fetching location availability:", error);
      } finally {
        setLoading(false);
      }
    };

    fetchAvailability();
    const interval = setInterval(fetchAvailability, 60000); // Update every minute

    return () => clearInterval(interval);
  }, [locationId, onAvailabilityChange]);

  if (loading) {
    return null;
  }

  if (!availability) {
    return null;
  }

  const bgColor = availability.isOpen
    ? "bg-green-50 border-green-200"
    : "bg-red-50 border-red-200";
  const textColor = availability.isOpen ? "text-green-800" : "text-red-800";
  const icon = availability.isOpen ? (
    <MdCheckCircle className="text-green-600" />
  ) : (
    <MdError className="text-red-600" />
  );

  return (
    <div className={`border rounded-lg p-4 ${bgColor}`}>
      <div className="flex items-start gap-3">
        <IconContext.Provider value={{ size: "1.5em" }}>
          {icon}
        </IconContext.Provider>
        <div className="flex-1">
          <h3 className={`font-semibold ${textColor}`}>{availability.name}</h3>
          <p className={`text-sm ${textColor}`}>
            {availability.isOpen ? (
              <>
                🟢 Open Now
                {availability.openTime && availability.closeTime && (
                  <span className="block">
                    Hours: {availability.openTime} - {availability.closeTime}
                  </span>
                )}
              </>
            ) : (
              <>
                🔴 {availability.reason || "Currently Closed"}
                {availability.nextAvailable && (
                  <span className="block mt-2">
                    Next available: {availability.nextAvailable.day} at{" "}
                    {availability.nextAvailable.time}
                  </span>
                )}
              </>
            )}
          </p>
        </div>
      </div>
    </div>
  );
}
