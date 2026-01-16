import { authOptions } from "@/lib/auth";
import cache, { cacheKeys } from "@/lib/cache";
import { connectDB } from "@/lib/mongodb";
import { Location } from "@/models/Location";
import { getServerSession } from "next-auth/next";
import { z } from "zod";

const updateLocationSchema = z.object({
  name: z.string().optional(),
  address: z.string().optional(),
  city: z.string().optional(),
  zipCode: z.string().optional(),
  phone: z.string().optional(),
  latitude: z.number().optional(),
  longitude: z.number().optional(),
  isActive: z.boolean().optional(),
  workingHours: z
    .record(
      z.string(),
      z.object({
        open: z.string(),
        close: z.string(),
      }),
    )
    .optional(),
});

export async function PUT(
  request: Request,
  context: { params: Promise<{ id: string }> },
) {
  try {
    const { id } = await context.params;
    await connectDB();
    const session = await getServerSession(authOptions);

    if (!session?.user?.id || session.user.role !== "admin") {
      return Response.json({ error: "Unauthorized" }, { status: 403 });
    }

    const body = await request.json();
    const validation = updateLocationSchema.safeParse(body);

    if (!validation.success) {
      return Response.json({ error: "Invalid input" }, { status: 400 });
    }

    const location = await Location.findByIdAndUpdate(id, validation.data, {
      new: true,
    });

    if (!location) {
      return Response.json({ error: "Location not found" }, { status: 404 });
    }

    // Invalidate locations cache when location is updated
    cache.delete(cacheKeys.locations());
    cache.delete(cacheKeys.adminLocations());

    return Response.json({ location });
  } catch (error) {
    console.error("Error updating location:", error);
    return Response.json(
      { error: "Failed to update location" },
      { status: 500 },
    );
  }
}

export async function DELETE(
  request: Request,
  context: { params: Promise<{ id: string }> },
) {
  try {
    const { id } = await context.params;
    await connectDB();
    const session = await getServerSession(authOptions);

    if (!session?.user?.id || session.user.role !== "admin") {
      return Response.json({ error: "Unauthorized" }, { status: 403 });
    }

    await Location.findByIdAndDelete(id);

    // Invalidate locations cache when location is deleted
    cache.delete(cacheKeys.locations());
    cache.delete(cacheKeys.adminLocations());

    return Response.json({ message: "Location deleted" });
  } catch (error) {
    console.error("Error deleting location:", error);
    return Response.json(
      { error: "Failed to delete location" },
      { status: 500 },
    );
  }
}
