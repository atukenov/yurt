"use client";

import { FormError, FormField } from "@/components/FormFields";
import { errorLogger } from "@/lib/logger";
import { LoginSchema, validateFormData } from "@/lib/validation";
import { signIn } from "next-auth/react";
import Image from "next/image";
import Link from "next/link";
import { useRouter } from "next/navigation";
import { useState } from "react";

export default function LoginPage() {
  const router = useRouter();
  const [phone, setPhone] = useState("+7");
  const [password, setPassword] = useState("");
  const [errors, setErrors] = useState<Record<string, string>>({});
  const [error, setError] = useState("");
  const [success, setSuccess] = useState("");
  const [loading, setLoading] = useState(false);

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setError("");
    setSuccess("");
    setErrors({});
    setLoading(true);

    try {
      // Validate input
      const validation = validateFormData(LoginSchema, { phone, password });
      if (!validation.success || !validation.data) {
        setErrors(validation.errors || {});
        setLoading(false);
        return;
      }

      const result = await signIn("credentials", {
        phone: validation.data.phone,
        password: validation.data.password,
        redirect: false,
      });

      if (result?.error) {
        errorLogger.warn("Login failed", { phone });
        if (result.error === "CredentialsSignin") {
          setError("Invalid phone number or password");
        } else {
          setError(result.error || "Login failed");
        }
      } else if (result?.ok) {
        setSuccess("Login successful! Redirecting...");
        // Fetch session to check user role
        const response = await fetch("/api/auth/session");
        const session = await response.json();

        // Redirect based on role
        if (session?.user?.role === "admin") {
          router.push("/admin");
        } else {
          router.push("/menu");
        }
      }
    } catch (err) {
      const message = err instanceof Error ? err.message : "An error occurred";
      errorLogger.error(
        "Login error",
        { phone },
        err instanceof Error ? err : undefined
      );
      setError(message);
    } finally {
      setLoading(false);
    }
  };

  return (
    <div className="min-h-screen bg-gray-50 flex items-center justify-center py-12 px-4 sm:px-6 lg:px-8">
      <div className="w-full max-w-md space-y-8">
        <div className="text-center">
          <div className="flex justify-center">
            <Image src="/images/logo.png" alt="logo" width={250} height={50} />
          </div>
          <h2 className="mt-6 text-3xl font-bold text-gray-900">
            Sign in to Your Account
          </h2>
          <p className="mt-2 text-gray-600">
            Or{" "}
            <Link
              href="/register"
              className="text-amber-600 hover:text-amber-700 font-semibold"
            >
              create a new account
            </Link>
          </p>
        </div>

        <form className="space-y-6" onSubmit={handleSubmit}>
          {error && <FormError error={error} />}

          <FormField
            id="phone"
            label="Phone Number (Kazakhstan)"
            type="tel"
            value={phone}
            onChange={(e) => setPhone(e.target.value)}
            error={errors?.phone}
            placeholder="+7 (700) 123-4567"
            required
          />

          <FormField
            id="password"
            label="Password"
            type="password"
            value={password}
            onChange={(e) => setPassword(e.target.value)}
            error={errors?.password}
            placeholder="••••••••"
            required
          />

          <button
            type="submit"
            disabled={loading}
            className="w-full px-4 py-2 bg-[#ffd119] text-black rounded-lg hover:bg-amber-700 disabled:opacity-50 disabled:cursor-not-allowed transition font-semibold"
          >
            {loading ? "Signing in..." : "Sign In"}
          </button>
        </form>

        <p className="text-center text-sm text-gray-600">
          New to Yurt Coffee?{" "}
          <Link
            href="/register"
            className="text-amber-600 hover:text-amber-700 font-semibold"
          >
            Register here
          </Link>
        </p>
      </div>
    </div>
  );
}
