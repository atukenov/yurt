import { Injectable, NotFoundException } from "@nestjs/common";
import { InjectModel } from "@nestjs/mongoose";
import { Model } from "mongoose";
import { Location, LocationDocument } from "../../database/schemas";

function isLocationOpen(location: any): {
  isOpen: boolean;
  openTime?: string;
  closeTime?: string;
  reason?: string;
} {
  const now = new Date();
  const dayOfWeek = now.getDay();
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
  const currentDay = days[dayIndex];

  if (!location.isActive) {
    return { isOpen: false, reason: "Location is currently inactive" };
  }

  const today = new Date();
  today.setHours(0, 0, 0, 0);

  for (const holiday of location.holidays || []) {
    const holidayDate = new Date(holiday.date);
    holidayDate.setHours(0, 0, 0, 0);
    if (holidayDate.getTime() === today.getTime()) {
      if (holiday.isClosed)
        return {
          isOpen: false,
          reason: `Closed for ${holiday.name || "holiday"}`,
        };
      if (holiday.customHours?.open && holiday.customHours?.close) {
        const t = formatTime(now);
        const isOpen =
          t >= holiday.customHours.open && t < holiday.customHours.close;
        return {
          isOpen,
          openTime: holiday.customHours.open,
          closeTime: holiday.customHours.close,
          reason: isOpen
            ? undefined
            : `Closed (${holiday.name || "Holiday hours"})`,
        };
      }
    }
  }

  const hours = location.workingHours?.[currentDay];
  if (!hours)
    return { isOpen: false, reason: "Location information not available" };

  const t = formatTime(now);
  const isOpen = t >= hours.open && t < hours.close;
  return {
    isOpen,
    openTime: hours.open,
    closeTime: hours.close,
    reason: !isOpen ? `Location closed. Opens at ${hours.open}` : undefined,
  };
}

function getLocationHours(location: any, date: Date = new Date()) {
  const checkDate = new Date(date);
  checkDate.setHours(0, 0, 0, 0);

  for (const holiday of location.holidays || []) {
    const holidayDate = new Date(holiday.date);
    holidayDate.setHours(0, 0, 0, 0);
    if (holidayDate.getTime() === checkDate.getTime()) {
      if (holiday.isClosed)
        return {
          open: "",
          close: "",
          isClosed: true,
          reason: `Closed for ${holiday.name || "holiday"}`,
        };
      if (holiday.customHours?.open && holiday.customHours?.close)
        return {
          open: holiday.customHours.open,
          close: holiday.customHours.close,
        };
    }
  }

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
  const currentDay = days[dayIndex];
  const hours = location.workingHours?.[currentDay];
  return { open: hours?.open || "N/A", close: hours?.close || "N/A" };
}

function getNextAvailableTime(location: any) {
  const days = [
    "Monday",
    "Tuesday",
    "Wednesday",
    "Thursday",
    "Friday",
    "Saturday",
    "Sunday",
  ];
  for (let i = 1; i <= 7; i++) {
    const futureDate = new Date();
    futureDate.setDate(futureDate.getDate() + i);
    futureDate.setHours(0, 0, 0, 0);
    const hours = getLocationHours(location, futureDate);
    if (!hours.isClosed && hours.open) {
      const dayOfWeek = futureDate.getDay();
      return { time: hours.open, date: futureDate, day: days[dayOfWeek] };
    }
  }
  return null;
}

function formatTime(date: Date): string {
  return `${String(date.getHours()).padStart(2, "0")}:${String(date.getMinutes()).padStart(2, "0")}`;
}

@Injectable()
export class LocationsService {
  constructor(
    @InjectModel(Location.name) private locationModel: Model<LocationDocument>
  ) {}

  async getActiveLocations() {
    const locations = await this.locationModel.find({
      $or: [{ isActive: true }, { isActive: { $exists: false } }],
    });
    return { locations };
  }

  async getAvailability(id: string) {
    const location = await this.locationModel.findById(id);
    if (!location) throw new NotFoundException("Location not found");

    const availability = isLocationOpen(location);
    const hours = getLocationHours(location);
    const nextAvailable = !availability.isOpen
      ? getNextAvailableTime(location)
      : null;

    return {
      locationId: location._id,
      name: location.name,
      isOpen: availability.isOpen,
      openTime: availability.openTime,
      closeTime: availability.closeTime,
      reason: availability.reason,
      hours,
      nextAvailable,
    };
  }
}
