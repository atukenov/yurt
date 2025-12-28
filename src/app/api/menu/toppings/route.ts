import { connectDB } from "@/lib/mongodb";
import { Topping } from "@/models/Topping";

export async function GET() {
  try {
    await connectDB();
    const toppings = await Topping.find().limit(50);
    return Response.json({ toppings });
  } catch (error) {
    console.error("Error fetching toppings:", error);
    return Response.json(
      { error: "Failed to fetch toppings" },
      { status: 500 }
    );
  }
}
