import { connectDB } from "@/lib/mongodb";
import { MenuItem } from "@/models/MenuItem";

export async function GET(request: Request) {
  try {
    await connectDB();

    const { searchParams } = new URL(request.url);
    const category = searchParams.get("category");
    const search = searchParams.get("search");
    const locationId = searchParams.get("locationId");

    let query: any = { isAvailable: true };

    if (category) {
      query.category = category;
    }

    if (search) {
      query.$text = { $search: search };
    }

    const items = await MenuItem.find(query).limit(50);

    return Response.json({ items });
  } catch (error) {
    console.error("Error fetching menu items:", error);
    return Response.json(
      { error: "Failed to fetch menu items" },
      { status: 500 }
    );
  }
}
