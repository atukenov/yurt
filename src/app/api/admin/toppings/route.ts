import { authOptions } from "@/lib/auth";
import { connectDB } from "@/lib/mongodb";
import { Topping } from "@/models/Topping";
import { getServerSession } from "next-auth/next";
import { z } from "zod";

const createToppingSchema = z.object({
  name: z.string().min(1),
  price: z.number().min(0),
  category: z.enum(["syrup", "shot", "milk", "topping"]).default("topping"),
  description: z.string().optional(),
});

export async function GET() {
  try {
    await connectDB();
    const toppings = await Topping.find();
    return Response.json({ toppings });
  } catch (error) {
    console.error("Error fetching toppings:", error);
    return Response.json(
      { error: "Failed to fetch toppings" },
      { status: 500 }
    );
  }
}

export async function POST(request: Request) {
  try {
    await connectDB();
    const session = await getServerSession(authOptions);

    if (!session?.user?.id || session.user.role !== "admin") {
      return Response.json({ error: "Unauthorized" }, { status: 403 });
    }

    const body = await request.json();
    const validatedData = createToppingSchema.parse(body);

    const topping = new Topping(validatedData);
    await topping.save();

    return Response.json({ topping }, { status: 201 });
  } catch (error) {
    if (error instanceof z.ZodError) {
      return Response.json({ error: error.errors }, { status: 400 });
    }
    console.error("Error creating topping:", error);
    return Response.json(
      { error: "Failed to create topping" },
      { status: 500 }
    );
  }
}
