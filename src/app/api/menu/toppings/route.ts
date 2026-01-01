import cache, { cacheKeys } from "@/lib/cache";
import { connectDB } from "@/lib/mongodb";
import { Topping } from "@/models/Topping";

export async function GET() {
  try {
    const cacheKey = cacheKeys.toppings();

    // Check cache first
    const cachedToppings = cache.get<any[]>(cacheKey);
    if (cachedToppings) {
      return Response.json({ toppings: cachedToppings, fromCache: true });
    }

    await connectDB();
    const toppings = await Topping.find().limit(50);

    // Cache results (5 minute TTL)
    cache.set(cacheKey, toppings, 300);

    return Response.json({ toppings });
  } catch (error) {
    console.error("Error fetching toppings:", error);
    return Response.json(
      { error: "Failed to fetch toppings" },
      { status: 500 }
    );
  }
}
