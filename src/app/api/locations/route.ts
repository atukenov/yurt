import { connectDB } from "@/lib/mongodb";
import { Location } from "@/models/Location";

export async function GET() {
  try {
    await connectDB();
    const locations = await Location.find({ isActive: true });
    return Response.json({ locations });
  } catch (error) {
    console.error("Error fetching locations:", error);
    return Response.json(
      { error: "Failed to fetch locations" },
      { status: 500 }
    );
  }
}
