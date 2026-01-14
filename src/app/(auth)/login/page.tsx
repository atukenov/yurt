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
  const [pin, setPin] = useState("");
  const [errors, setErrors] = useState<Record<string, string>>({});
  const [error, setError] = useState("");
  const [success, setSuccess] = useState("");
  const [loading, setLoading] = useState(false);

  const handlePinChange = (e: React.ChangeEvent<HTMLInputElement>) => {
    const value = e.target.value.replace(/\D/g, "").slice(0, 4);
    setPin(value);
  };

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setError("");
    setSuccess("");
    setErrors({});
    setLoading(true);

    try {
      // Validate input
      const validation = validateFormData(LoginSchema, { phone, password: pin });
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
          setError("Invalid phone number or PIN");
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
    <div className="min-h-screen bg-gradient-to-br from-amber-50 via-white to-orange-50 flex items-center justify-center py-12 px-4 sm:px-6 lg:px-8">
      <div className="w-full max-w-md space-y-8">
        <div className="text-center">
          <div className="flex justify-center">
            <Image src="/images/logo.png" alt="logo" width={250} height={50} />
          </div>
          <h2 className="mt-6 text-3xl font-bold text-gray-900">
            Welcome Back
          </h2>
          <p className="mt-2 text-gray-600">
            Sign in to your Yurt Coffee account
          </p>
        </div>

        <form className="space-y-6" onSubmit={handleSubmit}>
          {error && <FormError error={error} />}

          <div className="bg-white rounded-lg shadow-sm p-6 border border-gray-100">
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
              Enter 4-Digit PIN
            </label>
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
            {loading ? "Signing in..." : "Sign In"}
          </button>
        </form>

        <div className="relative">
          <div className="absolute inset-0 flex items-center">
            <div className="w-full border-t border-gray-300" />
          </div>
          <div className="relative flex justify-center text-sm">
            <span className="px-2 bg-gradient-to-br from-amber-50 via-white to-orange-50 text-gray-600">
              New to Yurt Coffee?
            </span>
          </div>
        </div>

        <Link
          href="/register"
          className="w-full px-4 py-3 border-2 border-amber-400 text-amber-600 rounded-lg hover:bg-amber-50 transition font-semibold text-center block"
        >
          Create Account
        </Link>
      </div>
    </div>
  );
}
