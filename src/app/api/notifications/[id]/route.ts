import { authOptions } from "@/lib/auth";
import { connectDB } from "@/lib/mongodb";
import { Notification } from "@/models/Notification";
import { getServerSession } from "next-auth/next";

export async function PUT(
  request: Request,
  context: { params: Promise<{ id: string }> }
) {
  try {
    const { id } = await context.params;
    await connectDB();
    const session = await getServerSession(authOptions);

    if (!session?.user?.id) {
      return Response.json({ error: "Unauthorized" }, { status: 401 });
    }

    const notification = await Notification.findOneAndUpdate(
      { _id: id, recipient: session.user.id },
      { read: true },
      { new: true }
    );

    if (!notification) {
      return Response.json(
        { error: "Notification not found" },
        { status: 404 }
      );
    }

    return Response.json({ notification });
  } catch (error) {
    console.error("Error updating notification:", error);
    return Response.json(
      { error: "Failed to update notification" },
      { status: 500 }
    );
  }
}
