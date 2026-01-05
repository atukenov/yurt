"use client";

import { useToast } from "@/components/Toast";
import { useState } from "react";
import { MdAdd, MdDelete } from "react-icons/md";

interface WorkingHours {
  day: string;
  open: string;
  close: string;
}

interface Holiday {
  date: string;
  name: string;
  isClosed: boolean;
  customHours?: {
    open: string;
    close: string;
  };
}

interface LocationHoursManagerProps {
  locationId: string;
  initialHours?: WorkingHours[];
  initialHolidays?: Holiday[];
  onSave?: () => void;
}

const DAYS = [
  "Monday",
  "Tuesday",
  "Wednesday",
  "Thursday",
  "Friday",
  "Saturday",
  "Sunday",
];

export function LocationHoursManager({
  locationId,
  initialHours = DAYS.map((day) => ({
    day,
    open: "09:00",
    close: "17:00",
  })),
  initialHolidays = [],
  onSave,
}: LocationHoursManagerProps) {
  const [hours, setHours] = useState<WorkingHours[]>(initialHours);
  const [holidays, setHolidays] = useState<Holiday[]>(initialHolidays);
  const [newHolidayDate, setNewHolidayDate] = useState("");
  const [newHolidayName, setNewHolidayName] = useState("");
  const [newHolidayIsClosed, setNewHolidayIsClosed] = useState(true);
  const [loading, setLoading] = useState(false);
  const { showToast } = useToast();

  const handleHourChange = (
    dayIndex: number,
    field: "open" | "close",
    value: string
  ) => {
    const newHours = [...hours];
    newHours[dayIndex][field] = value;
    setHours(newHours);
  };

  const handleAddHoliday = () => {
    if (!newHolidayDate || !newHolidayName) {
      showToast("Please fill in all fields", "error");
      return;
    }

    const newHoliday: Holiday = {
      date: newHolidayDate,
      name: newHolidayName,
      isClosed: newHolidayIsClosed,
    };

    setHolidays([...holidays, newHoliday]);
    setNewHolidayDate("");
    setNewHolidayName("");
    setNewHolidayIsClosed(true);
  };

  const handleDeleteHoliday = (index: number) => {
    setHolidays(holidays.filter((_, i) => i !== index));
  };

  const handleSave = async () => {
    setLoading(true);
    try {
      const res = await fetch(`/api/locations/${locationId}/hours`, {
        method: "PUT",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({
          workingHours: hours,
          holidays,
        }),
      });

      if (!res.ok) {
        throw new Error("Failed to update hours");
      }

      showToast("Hours updated successfully", "success");
      onSave?.();
    } catch (error) {
      showToast(
        error instanceof Error ? error.message : "Failed to update hours",
        "error"
      );
    } finally {
      setLoading(false);
    }
  };

  return (
    <div className="space-y-6">
      {/* Working Hours */}
      <div>
        <h3 className="text-lg font-semibold text-gray-900 mb-4">
          Working Hours
        </h3>
        <div className="space-y-3">
          {hours.map((hour, index) => (
            <div
              key={hour.day}
              className="flex items-center gap-4 bg-gray-50 p-4 rounded"
            >
              <span className="w-24 font-medium text-gray-700">{hour.day}</span>
              <input
                type="time"
                value={hour.open}
                onChange={(e) =>
                  handleHourChange(index, "open", e.target.value)
                }
                className="px-3 py-2 border border-gray-300 rounded"
              />
              <span className="text-gray-600">to</span>
              <input
                type="time"
                value={hour.close}
                onChange={(e) =>
                  handleHourChange(index, "close", e.target.value)
                }
                className="px-3 py-2 border border-gray-300 rounded"
              />
            </div>
          ))}
        </div>
      </div>

      {/* Holidays */}
      <div>
        <h3 className="text-lg font-semibold text-gray-900 mb-4">
          Holidays & Special Days
        </h3>

        {/* Add Holiday Form */}
        <div className="bg-gray-50 p-4 rounded mb-4 space-y-3">
          <div>
            <label className="block text-sm font-medium text-gray-700 mb-1">
              Date
            </label>
            <input
              type="date"
              value={newHolidayDate}
              onChange={(e) => setNewHolidayDate(e.target.value)}
              className="w-full px-3 py-2 border border-gray-300 rounded"
            />
          </div>
          <div>
            <label className="block text-sm font-medium text-gray-700 mb-1">
              Holiday Name
            </label>
            <input
              type="text"
              value={newHolidayName}
              onChange={(e) => setNewHolidayName(e.target.value)}
              placeholder="e.g., New Year's Day"
              className="w-full px-3 py-2 border border-gray-300 rounded"
            />
          </div>
          <div>
            <label className="flex items-center">
              <input
                type="checkbox"
                checked={newHolidayIsClosed}
                onChange={(e) => setNewHolidayIsClosed(e.target.checked)}
                className="w-4 h-4"
              />
              <span className="ml-2 text-sm text-gray-700">Closed all day</span>
            </label>
          </div>
          <button
            onClick={handleAddHoliday}
            className="w-full flex items-center justify-center gap-2 px-4 py-2 bg-blue-600 text-white rounded hover:bg-blue-700"
          >
            <MdAdd /> Add Holiday
          </button>
        </div>

        {/* Holiday List */}
        {holidays.length > 0 && (
          <div className="space-y-2">
            {holidays.map((holiday, index) => (
              <div
                key={index}
                className="flex items-center justify-between bg-gray-50 p-4 rounded"
              >
                <div>
                  <p className="font-medium text-gray-900">{holiday.name}</p>
                  <p className="text-sm text-gray-600">
                    {new Date(holiday.date).toLocaleDateString()}
                    {holiday.isClosed
                      ? " - Closed"
                      : ` - ${holiday.customHours?.open} to ${holiday.customHours?.close}`}
                  </p>
                </div>
                <button
                  onClick={() => handleDeleteHoliday(index)}
                  className="text-red-600 hover:text-red-700 p-2"
                >
                  <MdDelete size={20} />
                </button>
              </div>
            ))}
          </div>
        )}
      </div>

      {/* Save Button */}
      <button
        onClick={handleSave}
        disabled={loading}
        className="w-full px-4 py-2 bg-[#ffd119] text-black rounded hover:bg-amber-700 disabled:opacity-50 font-semibold"
      >
        {loading ? "Saving..." : "Save Hours & Holidays"}
      </button>
    </div>
  );
}
