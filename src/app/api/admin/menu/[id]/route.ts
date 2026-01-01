import { authOptions } from "@/lib/auth";
import cache, { cacheKeys } from "@/lib/cache";
import { connectDB } from "@/lib/mongodb";
import { MenuItem } from "@/models/MenuItem";
import { getServerSession } from "next-auth/next";
import { z } from "zod";

const updateMenuItemSchema = z.object({
  name: z.string().optional(),
  description: z.string().optional(),
  category: z
    .enum(["hot", "cold", "latte", "cappuccino", "espresso", "specialty"])
    .optional(),
  basePrice: z.number().min(0).optional(),
  sizes: z
    .array(
      z.object({
        size: z.enum(["small", "medium", "large"]),
        priceModifier: z.number(),
      })
    )
    .optional(),
  image: z.string().optional(),
  isAvailable: z.boolean().optional(),
  preparationTime: z.number().optional(),
});

export async function PUT(
  request: Request,
  context: { params: Promise<{ id: string }> }
) {
  try {
    const { id } = await context.params;
    await connectDB();
    const session = await getServerSession(authOptions);

    if (!session?.user?.id || session.user.role !== "admin") {
      return Response.json({ error: "Unauthorized" }, { status: 403 });
    }

    const body = await request.json();
    const validation = updateMenuItemSchema.safeParse(body);

    if (!validation.success) {
      return Response.json({ error: "Invalid input" }, { status: 400 });
    }

    const item = await MenuItem.findByIdAndUpdate(id, validation.data, {
      new: true,
    });

    if (!item) {
      return Response.json({ error: "Menu item not found" }, { status: 404 });
    }

    // Invalidate menu cache when item is updated
    cache.delete(cacheKeys.menuItems());
    if (item.category) {
      cache.delete(cacheKeys.menuItems(item.category));
    }

    return Response.json({ item });
  } catch (error) {
    console.error("Error updating menu item:", error);
    return Response.json(
      { error: "Failed to update menu item" },
      { status: 500 }
    );
  }
}

export async function DELETE(
  request: Request,
  context: { params: Promise<{ id: string }> }
) {
  try {
    const { id } = await context.params;
    await connectDB();
    const session = await getServerSession(authOptions);

    if (!session?.user?.id || session.user.role !== "admin") {
      return Response.json({ error: "Unauthorized" }, { status: 403 });
    }

    const item = await MenuItem.findByIdAndDelete(id);

    // Invalidate menu cache when item is deleted
    cache.delete(cacheKeys.menuItems());
    if (item?.category) {
      cache.delete(cacheKeys.menuItems(item.category));
    }

    return Response.json({ message: "Menu item deleted" });
  } catch (error) {
    console.error("Error deleting menu item:", error);
    return Response.json(
      { error: "Failed to delete menu item" },
      { status: 500 }
    );
  }
}
