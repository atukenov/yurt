"use client";

import { LanguageSelector } from "@/components/LanguageSelector";
import { useToast } from "@/components/Toast";
import { useLanguage } from "@/context/LanguageContext";
import { apiClient } from "@/lib/api-client";
import { translations } from "@/lib/translations";
import { signOut, useSession } from "next-auth/react";
import Link from "next/link";
import { useEffect, useState } from "react";
import { IconContext } from "react-icons";
import { IoArrowBack, IoLogOut } from "react-icons/io5";

interface UserProfile {
  email: string;
  name: string;
  phone: string;
  role: string;
  image: string;
}

export function ProfilePageContent() {
  const { data: session } = useSession();
  const { language } = useLanguage();
  const { showToast } = useToast();
  const t = translations[language].profile;
  const [profile, setProfile] = useState<UserProfile | null>(null);
  const [loading, setLoading] = useState(true);
  const [editing, setEditing] = useState(false);
  const [formData, setFormData] = useState({
    name: "",
    phone: "",
  });

  useEffect(() => {
    fetchProfile();
  }, []);

  const fetchProfile = async () => {
    try {
      const data = await apiClient.get("/auth/profile");
      setProfile(data);
      setFormData({
        name: data.name || "",
        phone: data.phone || "",
      });
    } catch (error) {
      console.error("Error fetching profile:", error);
      showToast(t.failedToLoad, "error");
    } finally {
      setLoading(false);
    }
  };

  const handleInputChange = (e: React.ChangeEvent<HTMLInputElement>) => {
    const { name, value } = e.target;
    setFormData((prev) => ({
      ...prev,
      [name]: value,
    }));
  };

  const handleSaveProfile = async () => {
    try {
      const data = await apiClient.put("/auth/profile", formData);
      setProfile(data);
      setEditing(false);
      showToast(t.updatedSuccess, "success");
    } catch (error) {
      console.error("Error updating profile:", error);
      showToast(t.failedToUpdate, "error");
    }
  };

  if (!session?.user) {
    return (
      <div className="flex items-center justify-center min-h-screen bg-gray-50">
        <div className="text-center">
          <p className="text-gray-600 mb-4">{t.pleaseLogin}</p>
          <Link
            href="/login"
            className="px-6 py-2 bg-[#ffd119] text-black rounded-lg hover:bg-amber-700 transition"
          >
            {t.goToLogin}
          </Link>
        </div>
      </div>
    );
  }

  return (
    <main className="min-h-screen bg-gray-50 pb-20 md:pb-4">
      {/* Header */}
      <header className="bg-white border-b border-gray-200 shadow-sm sticky top-0 z-30">
        <div className="max-w-2xl mx-auto px-4 sm:px-6 lg:px-8 h-16 flex items-center gap-3">
          <Link
            href="/menu"
            className="text-gray-700 hover:text-amber-600 transition"
          >
            <IconContext.Provider value={{ size: "1.5em" }}>
              <IoArrowBack />
            </IconContext.Provider>
          </Link>
          <h1 className="text-xl font-bold text-gray-900">
            {t.profileSettings}
          </h1>
        </div>
      </header>

      {/* Content */}
      <div className="max-w-2xl mx-auto px-4 sm:px-6 lg:px-8 py-6">
        {loading ? (
          <div className="flex justify-center py-12">
            <div className="animate-spin rounded-full h-12 w-12 border-b-2 border-amber-600"></div>
          </div>
        ) : profile ? (
          <div className="space-y-6">
            {/* Profile Section */}
            <div className="bg-white rounded-lg shadow p-6">
              <div className="flex justify-between items-center mb-6">
                <h2 className="text-lg font-semibold text-gray-900">
                  {t.personalInformation}
                </h2>
                <button
                  onClick={() => {
                    if (editing) {
                      setEditing(false);
                      setFormData({
                        name: profile.name || "",
                        phone: profile.phone || "",
                      });
                    } else {
                      setEditing(true);
                    }
                  }}
                  className="px-4 py-2 text-sm font-medium text-amber-600 hover:text-amber-700 transition"
                >
                  {editing ? t.cancel : t.edit}
                </button>
              </div>

              <div className="space-y-4">
                {/* Email (Read-only) */}
                <div>
                  <label className="block text-sm font-medium text-gray-700 mb-1">
                    {t.email}
                  </label>
                  <input
                    type="email"
                    value={profile.email}
                    disabled
                    className="w-full px-4 py-2 border border-gray-300 rounded-lg bg-gray-50 text-gray-700 cursor-not-allowed"
                  />
                </div>

                {/* Full Name */}
                <div>
                  <label className="block text-sm font-medium text-gray-700 mb-1">
                    {t.firstName}
                  </label>
                  {editing ? (
                    <input
                      type="text"
                      name="name"
                      value={formData.name}
                      onChange={handleInputChange}
                      className="w-full px-4 py-2 border border-gray-300 rounded-lg focus:outline-none focus:border-amber-600 focus:ring-1 focus:ring-amber-600"
                    />
                  ) : (
                    <p className="px-4 py-2 text-gray-700">
                      {profile.name || "—"}
                    </p>
                  )}
                </div>

                {/* Phone */}
                <div>
                  <label className="block text-sm font-medium text-gray-700 mb-1">
                    {t.mobileNumber}
                  </label>
                  {editing ? (
                    <input
                      type="tel"
                      name="phone"
                      value={formData.phone}
                      onChange={handleInputChange}
                      placeholder="+1 (555) 000-0000"
                      className="w-full px-4 py-2 border border-gray-300 rounded-lg focus:outline-none focus:border-amber-600 focus:ring-1 focus:ring-amber-600"
                    />
                  ) : (
                    <p className="px-4 py-2 text-gray-700">
                      {profile.phone || "—"}
                    </p>
                  )}
                </div>

                {editing && (
                  <button
                    onClick={handleSaveProfile}
                    className="w-full px-4 py-2 bg-[#ffd119] text-black rounded-lg hover:bg-amber-700 transition font-medium mt-4"
                  >
                    {t.save}
                  </button>
                )}
              </div>
            </div>

            {/* App Settings Section */}
            <div className="bg-white rounded-lg shadow p-6">
              <h2 className="text-lg font-semibold text-gray-900 mb-6">
                {t.appSettings}
              </h2>

              <div className="space-y-4">
                {/* Language Selector */}
                <div>
                  <label className="block text-sm font-medium text-gray-700 mb-3">
                    {t.language}
                  </label>
                  <LanguageSelector />
                </div>
              </div>
            </div>

            {/* Logout Section */}
            <div className="bg-white rounded-lg shadow p-6">
              <button
                onClick={() => signOut()}
                className="w-full flex items-center justify-center gap-2 px-4 py-3 bg-red-50 text-red-600 rounded-lg hover:bg-red-100 transition font-medium"
              >
                <IconContext.Provider value={{ size: "1.2em" }}>
                  <IoLogOut />
                </IconContext.Provider>
                {t.logout}
              </button>
            </div>
          </div>
        ) : (
          <div className="text-center py-12">
            <p className="text-gray-600">Failed to load profile</p>
          </div>
        )}
      </div>
    </main>
  );
}
