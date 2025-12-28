import { authOptions } from "@/lib/auth";
import { connectDB } from "@/lib/mongodb";
import { Topping } from "@/models/Topping";
import { getServerSession } from "next-auth/next";
import { z } from "zod";

const updateToppingSchema = z.object({
  name: z.string().min(1).optional(),
  price: z.number().min(0).optional(),
  category: z.enum(["syrup", "shot", "milk", "topping"]).optional(),
  description: z.string().optional(),
});

export async function PUT(
  request: Request,
  { params }: { params: Promise<{ id: string }> }
) {
  try {
    const { id } = await params;
    await connectDB();
    const session = await getServerSession(authOptions);

    if (!session?.user?.id || session.user.role !== "admin") {
      return Response.json({ error: "Unauthorized" }, { status: 403 });
    }

    const body = await request.json();
    const validatedData = updateToppingSchema.parse(body);

    const topping = await Topping.findByIdAndUpdate(id, validatedData, {
      new: true,
    });

    if (!topping) {
      return Response.json({ error: "Topping not found" }, { status: 404 });
    }

    return Response.json({ topping });
  } catch (error) {
    if (error instanceof z.ZodError) {
      return Response.json({ error: error.errors }, { status: 400 });
    }
    console.error("Error updating topping:", error);
    return Response.json(
      { error: "Failed to update topping" },
      { status: 500 }
    );
  }
}

export async function DELETE(
  request: Request,
  { params }: { params: Promise<{ id: string }> }
) {
  try {
    const { id } = await params;
    await connectDB();
    const session = await getServerSession(authOptions);

    if (!session?.user?.id || session.user.role !== "admin") {
      return Response.json({ error: "Unauthorized" }, { status: 403 });
    }

    const topping = await Topping.findByIdAndDelete(id);

    if (!topping) {
      return Response.json({ error: "Topping not found" }, { status: 404 });
    }

    return Response.json({ message: "Topping deleted successfully" });
  } catch (error) {
    console.error("Error deleting topping:", error);
    return Response.json(
      { error: "Failed to delete topping" },
      { status: 500 }
    );
  }
}
