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
  const [pin, setPin] = useState("");
  const [phone, setPhone] = useState("+7");
  const [error, setError] = useState("");
  const [errors, setErrors] = useState<Record<string, string>>({});
  const [success, setSuccess] = useState("");
  const [loading, setLoading] = useState(false);

  const handlePinChange = (e: React.ChangeEvent<HTMLInputElement>) => {
    const value = e.target.value.replace(/\D/g, "").slice(0, 4);
    setPin(value);
  };

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setError("");
    setErrors({});
    setSuccess("");
    setLoading(true);

    // Validate input with Zod
    const validation = validateFormData(RegisterSchema, {
      email,
      password: pin,
      name,
      phone,
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
        body: JSON.stringify({ email, password: pin, name, phone }),
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
    <div className="min-h-screen bg-gradient-to-br from-amber-50 via-white to-orange-50 flex items-center justify-center py-12 px-4 sm:px-6 lg:px-8">
      <div className="w-full max-w-md space-y-8">
        <div className="text-center">
          <h1 className="text-4xl font-bold text-gray-900">☕🇰🇿</h1>
          <h2 className="mt-6 text-3xl font-bold text-gray-900">
            Join Yurt Coffee
          </h2>
          <p className="mt-2 text-gray-600">
            Create your account and start ordering
          </p>
        </div>

        <form className="space-y-4" onSubmit={handleSubmit}>
          {error && <FormError error={error} />}
          {success && <FormSuccess message={success} />}

          <div className="bg-white rounded-lg shadow-sm p-4 border border-gray-100">
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
          </div>

          <div className="bg-white rounded-lg shadow-sm p-4 border border-gray-100">
            <FormField
              id="email"
              label="Email Address"
              type="email"
              value={email}
              onChange={(e) => setEmail(e.target.value)}
              error={errors?.email}
              placeholder="you@example.com"
              required
            />
          </div>

          <div className="bg-white rounded-lg shadow-sm p-4 border border-gray-100">
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
          </div>

          <div className="bg-white rounded-lg shadow-sm p-6 border border-gray-100">
            <label className="block text-sm font-semibold text-gray-700 mb-3">
              Create 4-Digit PIN
            </label>
            <p className="text-xs text-gray-500 mb-3">
              You'll use this PIN to sign in quickly
            </p>
            <input
              type="text"
              id="pin"
              value={pin}
              onChange={handlePinChange}
              placeholder="••••"
              maxLength={4}
              disabled={loading}
              className={`w-full text-center text-4xl font-bold tracking-[0.5em] border-2 rounded-lg py-4 transition-all ${
                pin.length === 4
                  ? "border-green-400 bg-green-50"
                  : "border-gray-300 bg-gray-50"
              } focus:outline-none focus:ring-2 focus:ring-amber-500 focus:border-transparent disabled:opacity-50`}
            />
            <p className="mt-3 text-xs text-gray-500 text-center">
              {pin.length}/4 digits entered
            </p>
            {errors?.password && (
              <p className="mt-2 text-sm text-red-600">{errors.password}</p>
            )}
          </div>

          <button
            type="submit"
            disabled={loading || pin.length !== 4}
            className="w-full px-4 py-3 bg-gradient-to-r from-amber-400 to-amber-500 text-black rounded-lg hover:from-amber-500 hover:to-amber-600 disabled:opacity-50 disabled:cursor-not-allowed transition font-bold text-lg shadow-md hover:shadow-lg"
          >
            {loading ? "Creating Account..." : "Create Account"}
          </button>
        </form>

        <div className="relative">
          <div className="absolute inset-0 flex items-center">
            <div className="w-full border-t border-gray-300" />
          </div>
          <div className="relative flex justify-center text-sm">
            <span className="px-2 bg-gradient-to-br from-amber-50 via-white to-orange-50 text-gray-600">
              Already have an account?
            </span>
          </div>
        </div>

        <Link
          href="/login"
          className="w-full px-4 py-3 border-2 border-amber-400 text-amber-600 rounded-lg hover:bg-amber-50 transition font-semibold text-center block"
        >
          Sign In
        </Link>

        <p className="text-center text-xs text-gray-600">
          By registering, you agree to our Terms of Service and Privacy Policy
        </p>
      </div>
    </div>
  );
}
