import { authOptions } from "@/lib/auth";
import cache, { cacheKeys } from "@/lib/cache";
import { connectDB } from "@/lib/mongodb";
import { Location } from "@/models/Location";
import { getServerSession } from "next-auth/next";

export async function POST() {
  try {
    const session = await getServerSession(authOptions);

    if (!session?.user?.id || session.user.role !== "admin") {
      return Response.json({ error: "Unauthorized" }, { status: 403 });
    }

    // Clear locations cache
    const cacheKey = cacheKeys.locations();
    cache.delete(cacheKey);

    // Fetch fresh locations
    await connectDB();
    const locations = await Location.find({
      $or: [{ isActive: true }, { isActive: { $exists: false } }],
    });

    return Response.json({
      message: "Cache cleared and locations refreshed",
      count: locations.length,
      locations: locations.map((loc: any) => ({
        name: loc.name,
        isActive: loc.isActive ?? true,
      })),
    });
  } catch (error) {
    console.error("Error clearing cache:", error);
    return Response.json({ error: "Failed to clear cache" }, { status: 500 });
  }
}
