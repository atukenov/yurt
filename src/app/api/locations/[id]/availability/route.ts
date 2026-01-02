import {
  getLocationHours,
  getNextAvailableTime,
  isLocationOpen,
} from "@/lib/locationAvailability";
import { connectDB } from "@/lib/mongodb";
import { Location } from "@/models/Location";
import { NextRequest, NextResponse } from "next/server";

export async function GET(
  request: NextRequest,
  { params }: { params: Promise<{ id: string }> }
) {
  try {
    await connectDB();

    const { id } = await params;
    const location = await Location.findById(id);
    if (!location) {
      return NextResponse.json(
        { error: "Location not found" },
        { status: 404 }
      );
    }

    const availability = isLocationOpen(location);
    const hours = getLocationHours(location);
    const nextAvailable = !availability.isOpen
      ? getNextAvailableTime(location)
      : null;

    return NextResponse.json({
      locationId: location._id,
      name: location.name,
      isOpen: availability.isOpen,
      openTime: availability.openTime,
      closeTime: availability.closeTime,
      reason: availability.reason,
      hours,
      nextAvailable,
    });
  } catch (error) {
    console.error("Error checking location availability:", error);
    return NextResponse.json(
      { error: "Failed to check availability" },
      { status: 500 }
    );
  }
}
