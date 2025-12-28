import { authOptions } from "@/lib/auth";
import { connectDB } from "@/lib/mongodb";
import { Location } from "@/models/Location";
import { getServerSession } from "next-auth/next";
import { z } from "zod";

const createLocationSchema = z.object({
  name: z.string().min(1),
  address: z.string().min(1),
  city: z.string().min(1),
  zipCode: z.string().min(1),
  phone: z.string().optional(),
  latitude: z.number().optional(),
  longitude: z.number().optional(),
});

export async function GET() {
  try {
    await connectDB();
    const locations = await Location.find();
    return Response.json({ locations });
  } catch (error) {
    console.error("Error fetching locations:", error);
    return Response.json(
      { error: "Failed to fetch locations" },
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
    const validation = createLocationSchema.safeParse(body);

    if (!validation.success) {
      return Response.json({ error: "Invalid input" }, { status: 400 });
    }

    const location = await Location.create(validation.data);

    return Response.json(
      {
        message: "Location created",
        location,
      },
      { status: 201 }
    );
  } catch (error) {
    console.error("Error creating location:", error);
    return Response.json(
      { error: "Failed to create location" },
      { status: 500 }
    );
  }
}
