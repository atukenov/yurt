import { connectDB } from "@/lib/mongodb";
import { User } from "@/models/User";
import { z } from "zod";

const registerSchema = z.object({
  email: z.string().email(),
  password: z
    .string()
    .length(4, "PIN must be exactly 4 digits")
    .regex(/^\d+$/, "PIN must contain only digits"),
  name: z.string().min(1),
  phone: z.string().optional(),
});

export async function POST(request: Request) {
  try {
    await connectDB();
    const body = await request.json();

    const validation = registerSchema.safeParse(body);
    if (!validation.success) {
      return Response.json({ error: "Invalid input" }, { status: 400 });
    }

    const { email, password, name, phone } = validation.data;

    // Check if user exists
    const existingUser = await User.findOne({ email });
    if (existingUser) {
      return Response.json({ error: "User already exists" }, { status: 409 });
    }

    // Create user with PIN (no hashing needed for 4-digit PIN)
    const user = await User.create({
      email,
      pinCode: password,
      name,
      phone,
      role: "customer",
    });

    return Response.json(
      {
        message: "User registered successfully",
        user: {
          id: user._id,
          email: user.email,
          name: user.name,
        },
      },
      { status: 201 }
    );
  } catch (error) {
    console.error("Registration error:", error);
    return Response.json({ error: "Internal server error" }, { status: 500 });
  }
}
