import { authOptions } from "@/lib/auth";
import { connectDB } from "@/lib/mongodb";
import { MenuItem } from "@/models/MenuItem";
import { getServerSession } from "next-auth/next";
import { z } from "zod";

const createMenuItemSchema = z.object({
  name: z.string().min(1),
  description: z.string().optional(),
  category: z.enum([
    "hot",
    "cold",
    "latte",
    "cappuccino",
    "espresso",
    "specialty",
  ]),
  basePrice: z.number().min(0),
  sizes: z.array(
    z.object({
      size: z.enum(["small", "medium", "large"]),
      priceModifier: z.number(),
    })
  ),
  image: z.string().optional(),
  preparationTime: z.number().default(5),
});

export async function GET() {
  try {
    await connectDB();
    const items = await MenuItem.find();
    return Response.json({ items });
  } catch (error) {
    console.error("Error fetching menu items:", error);
    return Response.json(
      { error: "Failed to fetch menu items" },
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
    const validation = createMenuItemSchema.safeParse(body);

    if (!validation.success) {
      return Response.json({ error: "Invalid input" }, { status: 400 });
    }

    const item = await MenuItem.create(validation.data);

    return Response.json(
      {
        message: "Menu item created",
        item,
      },
      { status: 201 }
    );
  } catch (error) {
    console.error("Error creating menu item:", error);
    return Response.json(
      { error: "Failed to create menu item" },
      { status: 500 }
    );
  }
}
