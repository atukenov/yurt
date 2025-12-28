import { authOptions } from "@/lib/auth";
import { connectDB } from "@/lib/mongodb";
import { Notification } from "@/models/Notification";
import { getServerSession } from "next-auth/next";

export async function GET(request: Request) {
  try {
    await connectDB();
    const session = await getServerSession(authOptions);

    if (!session?.user?.id) {
      return Response.json({ error: "Unauthorized" }, { status: 401 });
    }

    const { searchParams } = new URL(request.url);
    const unreadOnly = searchParams.get("unreadOnly") === "true";

    const query: any = { recipient: session.user.id };
    if (unreadOnly) {
      query.read = false;
    }

    const notifications = await Notification.find(query)
      .populate("order", "orderNumber status")
      .sort({ createdAt: -1 })
      .limit(50);

    return Response.json({ notifications });
  } catch (error) {
    console.error("Error fetching notifications:", error);
    return Response.json(
      { error: "Failed to fetch notifications" },
      { status: 500 }
    );
  }
}
