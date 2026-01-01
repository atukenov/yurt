import cache, { cacheKeys } from "@/lib/cache";
import { connectDB } from "@/lib/mongodb";
import { MenuItem } from "@/models/MenuItem";

export async function GET(request: Request) {
  try {
    const { searchParams } = new URL(request.url);
    const category = searchParams.get("category");
    const search = searchParams.get("search");
    const locationId = searchParams.get("locationId");

    // Generate cache key based on query parameters
    const cacheKey = cacheKeys.menuItems(
      category || undefined,
      search || undefined
    );

    // Check cache first - only cache when no search parameter
    if (!search) {
      const cachedItems = cache.get<any[]>(cacheKey);
      if (cachedItems) {
        return Response.json({ items: cachedItems, fromCache: true });
      }
    }

    await connectDB();

    let query: any = { isAvailable: true };

    if (category) {
      query.category = category;
    }

    if (search) {
      query.$text = { $search: search };
    }

    const items = await MenuItem.find(query).limit(50);

    // Cache results (5 minute TTL) - only cache non-search results
    if (!search) {
      cache.set(cacheKey, items, 300);
    }

    return Response.json({ items });
  } catch (error) {
    console.error("Error fetching menu items:", error);
    return Response.json(
      { error: "Failed to fetch menu items" },
      { status: 500 }
    );
  }
}
