import { connectDB } from "@/lib/mongodb";
import { User } from "@/models/User";
import CredentialsProvider from "next-auth/providers/credentials";
import { z } from "zod";

const credentialsSchema = z.object({
  phone: z.string().min(1),
  password: z.string().min(1),
});

export const authOptions = {
  providers: [
    CredentialsProvider({
      name: "Credentials",
      credentials: {
        phone: { label: "Phone Number", type: "tel" },
        password: { label: "PIN", type: "text" },
      },
      async authorize(credentials) {
        const parseResult = credentialsSchema.safeParse(credentials);
        if (!parseResult.success) {
          throw new Error("Invalid credentials");
        }

        await connectDB();

        const user = await User.findOne({ phone: credentials?.phone });
        if (!user) {
          throw new Error("User not found");
        }

        // Compare PIN directly (no hashing needed for 4-digit PIN)
        if (credentials?.password !== user.pinCode) {
          throw new Error("Invalid PIN");
        }

        return {
          id: user._id.toString(),
          email: user.email,
          name: user.name,
          role: user.role,
        };
      },
    }),
  ],
  pages: {
    signIn: "/login",
  },
  callbacks: {
    async jwt({ token, user }: any) {
      if (user) {
        token.id = user.id;
        token.role = user.role;
      }
      return token;
    },
    async session({ session, token }: any) {
      if (session.user) {
        session.user.id = token.id;
        session.user.role = token.role;
      }
      return session;
    },
  },
  secret: process.env.NEXTAUTH_SECRET,
};
