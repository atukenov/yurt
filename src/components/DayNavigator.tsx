"use client";

import { addDays, format, isSameDay, parseISO } from "date-fns";
import { useMemo, useState } from "react";

interface DayNavigatorProps {
  onDateChange: (dateString: string) => void;
  defaultDate?: string;
}

export function DayNavigator({ onDateChange, defaultDate }: DayNavigatorProps) {
  // Initialize with provided date or today
  const getInitialDate = () => {
    if (defaultDate) return defaultDate;
    return format(new Date(), "yyyy-MM-dd");
  };

  const [selectedDate, setSelectedDate] = useState<string>(getInitialDate());

  // Get today's date for comparison
  const today = useMemo(() => {
    return format(new Date(), "yyyy-MM-dd");
  }, []);

  // Parse dates as Date objects for comparison
  const selectedDateObj = useMemo(() => {
    return parseISO(selectedDate);
  }, [selectedDate]);

  const todayObj = useMemo(() => {
    return parseISO(today);
  }, [today]);

  // Check if selected date is today
  const isToday = useMemo(
    () => isSameDay(selectedDateObj, todayObj),
    [selectedDateObj, todayObj]
  );

  // Format date for display
  const formatDateDisplay = (dateStr: string): string => {
    return format(parseISO(dateStr), "EEE, MMM d, yyyy");
  };

  // Handle previous day
  const handlePreviousDay = () => {
    const previousDate = addDays(selectedDateObj, -1);
    const newDateString = format(previousDate, "yyyy-MM-dd");
    setSelectedDate(newDateString);
    onDateChange(newDateString);
  };

  // Handle next day
  const handleNextDay = () => {
    if (isToday) return; // Don't allow going past today

    const nextDate = addDays(selectedDateObj, 1);
    const nextDateString = format(nextDate, "yyyy-MM-dd");

    // Ensure we don't go past today
    if (nextDateString <= today) {
      setSelectedDate(nextDateString);
      onDateChange(nextDateString);
    }
  };

  return (
    <div className="flex items-center gap-2 bg-gray-100 rounded-lg p-2">
      {/* Previous Day Button */}
      <button
        onClick={handlePreviousDay}
        aria-label="Go to previous day"
        title="Previous day"
        className="p-2 hover:bg-gray-200 active:bg-gray-300 rounded transition-colors duration-200"
      >
        <svg
          className="w-5 h-5 text-gray-700"
          fill="none"
          stroke="currentColor"
          viewBox="0 0 24 24"
        >
          <path
            strokeLinecap="round"
            strokeLinejoin="round"
            strokeWidth={2}
            d="M15 19l-7-7 7-7"
          />
        </svg>
      </button>

      {/* Date Display */}
      <span className="px-4 py-1 bg-white rounded font-semibold text-gray-900 min-w-[140px] text-center text-sm whitespace-nowrap transition-all duration-300">
        {formatDateDisplay(selectedDate)}
        {isToday && (
          <span className="block text-xs text-amber-600 font-medium">
            Today
          </span>
        )}
      </span>

      {/* Next Day Button */}
      <button
        onClick={handleNextDay}
        aria-label="Go to next day"
        title={isToday ? "Cannot go past today" : "Next day"}
        disabled={isToday}
        className={`p-2 rounded transition-colors duration-200 ${
          isToday
            ? "text-gray-300 cursor-not-allowed opacity-50"
            : "hover:bg-gray-200 active:bg-gray-300 text-gray-700"
        }`}
      >
        <svg
          className="w-5 h-5"
          fill="none"
          stroke="currentColor"
          viewBox="0 0 24 24"
        >
          <path
            strokeLinecap="round"
            strokeLinejoin="round"
            strokeWidth={2}
            d="M9 5l7 7-7 7"
          />
        </svg>
      </button>
    </div>
  );
}
