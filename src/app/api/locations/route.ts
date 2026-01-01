import cache, { cacheKeys } from "@/lib/cache";
import { connectDB } from "@/lib/mongodb";
import { Location } from "@/models/Location";

export async function GET() {
  try {
    const cacheKey = cacheKeys.locations();

    // Check cache first
    const cachedLocations = cache.get<any[]>(cacheKey);
    if (cachedLocations) {
      return Response.json({ locations: cachedLocations, fromCache: true });
    }

    await connectDB();
    const locations = await Location.find({ isActive: true });

    // Cache results (10 minute TTL)
    cache.set(cacheKey, locations, 600);

    return Response.json({ locations });
  } catch (error) {
    console.error("Error fetching locations:", error);
    return Response.json(
      { error: "Failed to fetch locations" },
      { status: 500 }
    );
  }
}
