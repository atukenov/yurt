import { authOptions } from "@/lib/auth";
import { connectDB } from "@/lib/mongodb";
import { Location } from "@/models/Location";
import { getServerSession } from "next-auth";
import { NextRequest, NextResponse } from "next/server";

export async function PUT(
  request: NextRequest,
  { params }: { params: Promise<{ id: string }> }
) {
  try {
    const session = await getServerSession(authOptions);

    if (!session?.user || session.user.role !== "admin") {
      return NextResponse.json({ error: "Unauthorized" }, { status: 401 });
    }

    const body = await request.json();
    const { workingHours, holidays } = body;

    await connectDB();

    const { id } = await params;
    const location = await Location.findByIdAndUpdate(
      id,
      {
        ...(workingHours && { workingHours }),
        ...(holidays && { holidays }),
      },
      { new: true }
    );

    if (!location) {
      return NextResponse.json(
        { error: "Location not found" },
        { status: 404 }
      );
    }

    return NextResponse.json({
      message: "Location hours updated successfully",
      location,
    });
  } catch (error) {
    console.error("Error updating location hours:", error);
    return NextResponse.json(
      { error: "Failed to update location hours" },
      { status: 500 }
    );
  }
}
