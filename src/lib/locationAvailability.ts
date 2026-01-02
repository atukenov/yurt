export interface LocationAvailability {
  isOpen: boolean;
  openTime?: string;
  closeTime?: string;
  reason?: string;
}

/**
 * Check if a location is currently open
 */
export function isLocationOpen(location: any): LocationAvailability {
  const now = new Date();
  const dayOfWeek = now.getDay();

  // Convert Sunday (0) to 6 for our days array
  const dayIndex = dayOfWeek === 0 ? 6 : dayOfWeek - 1;
  const days = [
    "monday",
    "tuesday",
    "wednesday",
    "thursday",
    "friday",
    "saturday",
    "sunday",
  ];
  const currentDay = days[dayIndex] as keyof typeof location.workingHours;

  // Check if location is active
  if (!location.isActive) {
    return {
      isOpen: false,
      reason: "Location is currently inactive",
    };
  }

  // Check holidays first
  const today = new Date();
  today.setHours(0, 0, 0, 0);

  for (const holiday of location.holidays || []) {
    const holidayDate = new Date(holiday.date);
    holidayDate.setHours(0, 0, 0, 0);

    if (holidayDate.getTime() === today.getTime()) {
      if (holiday.isClosed) {
        return {
          isOpen: false,
          reason: `Closed for ${holiday.name || "holiday"}`,
        };
      }

      // Check custom holiday hours
      if (holiday.customHours?.open && holiday.customHours?.close) {
        const timeString = formatTime(now);
        const isOpenNow =
          timeString >= holiday.customHours.open &&
          timeString < holiday.customHours.close;

        return {
          isOpen: isOpenNow,
          openTime: holiday.customHours.open,
          closeTime: holiday.customHours.close,
          reason: isOpenNow
            ? undefined
            : `Closed (${holiday.name || "Holiday hours"})`,
        };
      }
    }
  }

  // Check regular working hours
  const hours = location.workingHours[currentDay];
  if (!hours) {
    return {
      isOpen: false,
      reason: "Location information not available",
    };
  }

  const timeString = formatTime(now);
  const isOpen = timeString >= hours.open && timeString < hours.close;

  return {
    isOpen,
    openTime: hours.open,
    closeTime: hours.close,
    reason: !isOpen ? `Location closed. Opens at ${hours.open}` : undefined,
  };
}

/**
 * Get location hours for a specific day
 */
export function getLocationHours(
  location: any,
  date: Date = new Date()
): { open: string; close: string; isClosed?: boolean; reason?: string } {
  // Check if it's a holiday
  const checkDate = new Date(date);
  checkDate.setHours(0, 0, 0, 0);

  for (const holiday of location.holidays || []) {
    const holidayDate = new Date(holiday.date);
    holidayDate.setHours(0, 0, 0, 0);

    if (holidayDate.getTime() === checkDate.getTime()) {
      if (holiday.isClosed) {
        return {
          open: "",
          close: "",
          isClosed: true,
          reason: `Closed for ${holiday.name || "holiday"}`,
        };
      }

      if (holiday.customHours?.open && holiday.customHours?.close) {
        return {
          open: holiday.customHours.open,
          close: holiday.customHours.close,
        };
      }
    }
  }

  // Get regular hours for the day
  const dayOfWeek = date.getDay();
  const dayIndex = dayOfWeek === 0 ? 6 : dayOfWeek - 1;
  const days = [
    "monday",
    "tuesday",
    "wednesday",
    "thursday",
    "friday",
    "saturday",
    "sunday",
  ];
  const currentDay = days[dayIndex] as keyof typeof location.workingHours;
  const hours = location.workingHours[currentDay];

  return {
    open: hours?.open || "N/A",
    close: hours?.close || "N/A",
  };
}

/**
 * Format time string (HH:mm)
 */
function formatTime(date: Date): string {
  const hours = String(date.getHours()).padStart(2, "0");
  const minutes = String(date.getMinutes()).padStart(2, "0");
  return `${hours}:${minutes}`;
}

/**
 * Get next available opening time for a location
 */
export function getNextAvailableTime(location: any): {
  time: string;
  date: Date;
  day: string;
} | null {
  const days = [
    "Monday",
    "Tuesday",
    "Wednesday",
    "Thursday",
    "Friday",
    "Saturday",
    "Sunday",
  ];
  const daysKey = [
    "monday",
    "tuesday",
    "wednesday",
    "thursday",
    "friday",
    "saturday",
    "sunday",
  ];

  // Check next 7 days
  for (let i = 1; i <= 7; i++) {
    const futureDate = new Date();
    futureDate.setDate(futureDate.getDate() + i);
    futureDate.setHours(0, 0, 0, 0);

    const hours = getLocationHours(location, futureDate);
    if (!hours.isClosed && hours.open) {
      const dayOfWeek = futureDate.getDay();
      return {
        time: hours.open,
        date: futureDate,
        day: days[dayOfWeek],
      };
    }
  }

  return null;
}
