"use client";

import { FormError, FormField, FormSuccess } from "@/components/FormFields";
import { errorLogger } from "@/lib/logger";
import { RegisterSchema, validateFormData } from "@/lib/validation";
import Link from "next/link";
import { useRouter } from "next/navigation";
import { useState } from "react";

export default function RegisterPage() {
  const router = useRouter();
  const [name, setName] = useState("");
  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");
  const [phone, setPhone] = useState("");
  const [error, setError] = useState("");
  const [errors, setErrors] = useState<Record<string, string>>({});
  const [success, setSuccess] = useState("");
  const [loading, setLoading] = useState(false);

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setError("");
    setErrors({});
    setSuccess("");
    setLoading(true);

    // Validate input with Zod
    const validation = validateFormData(RegisterSchema, {
      email,
      password,
      name,
      phone: phone || undefined,
    });
    if (!validation.success) {
      setErrors(validation.errors || {});
      setLoading(false);
      errorLogger.warn("Register validation failed", { email, name });
      return;
    }

    try {
      const res = await fetch("/api/auth/register", {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ email, password, name, phone }),
      });

      if (!res.ok) {
        const data = await res.json();
        const message = data.error || "Registration failed";
        setError(message);
        errorLogger.warn(
          "Register API failed",
          { email, name },
          new Error(message)
        );
        return;
      }

      setSuccess("Account created successfully! Redirecting to login...");
      errorLogger.info("User registered successfully", { email, name });
      setTimeout(() => router.push("/login?registered=true"), 1500);
    } catch (err) {
      const message =
        err instanceof Error
          ? err.message
          : "An error occurred. Please try again.";
      setError(message);
      errorLogger.error(
        "Register error",
        { email, name },
        err instanceof Error ? err : new Error(message)
      );
    } finally {
      setLoading(false);
    }
  };

  return (
    <div className="min-h-screen bg-gray-50 flex items-center justify-center py-12 px-4 sm:px-6 lg:px-8">
      <div className="w-full max-w-md space-y-8">
        <div className="text-center">
          <h1 className="text-3xl font-bold text-gray-900">☕</h1>
          <h2 className="mt-6 text-3xl font-bold text-gray-900">
            Create Your Account
          </h2>
          <p className="mt-2 text-gray-600">
            Already have an account?{" "}
            <Link
              href="/login"
              className="text-amber-600 hover:text-amber-700 font-semibold"
            >
              Sign in
            </Link>
          </p>
        </div>

        <form className="space-y-6" onSubmit={handleSubmit}>
          {error && <FormError error={error} />}
          {success && <FormSuccess message={success} />}

          <FormField
            id="name"
            label="Full Name"
            type="text"
            value={name}
            onChange={(e) => setName(e.target.value)}
            error={errors?.name}
            placeholder="John Doe"
            required
          />

          <FormField
            id="email"
            label="Email address"
            type="email"
            value={email}
            onChange={(e) => setEmail(e.target.value)}
            error={errors?.email}
            placeholder="you@example.com"
            required
          />

          <FormField
            id="phone"
            label="Phone (Optional)"
            type="tel"
            value={phone}
            onChange={(e) => setPhone(e.target.value)}
            error={errors?.phone}
            placeholder="+1 (555) 123-4567"
          />

          <FormField
            id="password"
            label="Password"
            type="password"
            value={password}
            onChange={(e) => setPassword(e.target.value)}
            error={errors?.password}
            placeholder="••••••••"
            hint="At least 6 characters"
            required
          />

          <button
            type="submit"
            disabled={loading}
            className="w-full px-4 py-2 bg-amber-600 text-white rounded-lg hover:bg-amber-700 disabled:opacity-50 disabled:cursor-not-allowed transition font-semibold"
          >
            {loading ? "Creating account..." : "Create Account"}
          </button>
        </form>

        <p className="text-center text-xs text-gray-600">
          By registering, you agree to our Terms of Service and Privacy Policy
        </p>
      </div>
    </div>
  );
}
