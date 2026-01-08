"use client";

import { useLanguage } from "@/context/LanguageContext";
import { translations } from "@/lib/translations";
import { IMenuItem } from "@/types";
import { useSession } from "next-auth/react";
import { useRouter } from "next/navigation";
import { useEffect, useState } from "react";

export const dynamic = "force-dynamic";

export default function AdminMenuPage() {
  const router = useRouter();
  const { data: session, status } = useSession();

  // Safely access language context with fallback
  let language: "en" | "ru" | "ar" = "en";
  let t = translations.en.admin;
  try {
    const langContext = useLanguage();
    language = langContext.language;
    t = translations[language]?.admin || translations.en.admin;
  } catch (e) {
    // If language context not available, use English as default
    t = translations.en.admin;
  }

  const [items, setItems] = useState<IMenuItem[]>([]);
  const [showForm, setShowForm] = useState(false);
  const [editingId, setEditingId] = useState<string | null>(null);
  const [loading, setLoading] = useState(true);
  const [formData, setFormData] = useState<{
    name: string;
    description: string;
    image: string;
    category:
      | "hot"
      | "cold"
      | "latte"
      | "cappuccino"
      | "espresso"
      | "specialty";
    basePrice: number;
    preparationTime: number;
  }>({
    name: "",
    description: "",
    image: "",
    category: "hot",
    basePrice: 0,
    preparationTime: 5,
  });

  const categories = [
    "hot",
    "cold",
    "latte",
    "cappuccino",
    "espresso",
    "specialty",
  ];

  useEffect(() => {
    // Wait for session to load
    if (status === "loading") {
      return;
    }

    // Check if user is authenticated
    if (status === "unauthenticated") {
      router.push("/login");
      return;
    }

    // Check if user is admin
    if (session?.user?.role !== "admin") {
      router.push("/");
      return;
    }

    fetchItems();
  }, [status, session, router]);

  const fetchItems = async () => {
    try {
      const res = await fetch("/api/admin/menu");
      if (res.ok) {
        const data = await res.json();
        setItems(data.items);
      }
    } catch (error) {
      console.error("Error fetching menu items:", error);
    } finally {
      setLoading(false);
    }
  };

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();

    try {
      const method = editingId ? "PUT" : "POST";
      const url = editingId
        ? `/api/admin/menu/${editingId}`
        : "/api/admin/menu";

      const res = await fetch(url, {
        method,
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({
          ...formData,
          sizes: [
            { size: "small", priceModifier: -0.5 },
            { size: "medium", priceModifier: 0 },
            { size: "large", priceModifier: 0.5 },
          ],
        }),
      });

      if (res.ok) {
        fetchItems();
        setShowForm(false);
        setEditingId(null);
        setFormData({
          name: "",
          description: "",
          image: "",
          category: "hot",
          basePrice: 0,
          preparationTime: 5,
        });
      }
    } catch (error) {
      console.error("Error saving menu item:", error);
    }
  };

  const handleDelete = async (id: string) => {
    if (!confirm(t.areYouSure)) return;

    try {
      const res = await fetch(`/api/admin/menu/${id}`, { method: "DELETE" });
      if (res.ok) {
        fetchItems();
      }
    } catch (error) {
      console.error("Error deleting item:", error);
    }
  };

  const handleEdit = (item: IMenuItem) => {
    setEditingId(item._id);
    setFormData({
      name: item.name,
      description: item.description || "",
      image: item.image || "",
      category: item.category,
      basePrice: item.basePrice,
      preparationTime: item.preparationTime,
    });
    setShowForm(true);
  };

  // Show loading state while checking authentication
  if (status === "loading" || session?.user?.role !== "admin") {
    return (
      <div className="min-h-screen flex items-center justify-center">
        <p className="text-gray-600">Loading...</p>
      </div>
    );
  }

  return (
    <div className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8 py-8">
      <div className="flex justify-between items-center mb-8">
        <h1 className="text-3xl font-bold text-gray-900">{t.menuManagement}</h1>
        <button
          onClick={() => {
            setShowForm(!showForm);
            setEditingId(null);
            setFormData({
              name: "",
              description: "",
              image: "",
              category: "hot",
              basePrice: 0,
              preparationTime: 5,
            });
          }}
          className="px-4 py-2 bg-[#ffd119] text-black rounded-lg hover:bg-amber-700 font-semibold"
        >
          {showForm ? t.cancel : t.addNewItem}
        </button>
      </div>

      {showForm && (
        <div className="bg-white rounded-lg shadow p-6 mb-8">
          <h2 className="text-xl font-bold text-gray-900 mb-6">
            {editingId ? t.editMenuItemTitle : t.addNewMenuItemTitle}
          </h2>
          <form onSubmit={handleSubmit} className="space-y-6">
            <div className="grid grid-cols-1 md:grid-cols-2 gap-6">
              <div>
                <label className="block text-sm font-medium text-gray-700 mb-2">
                  {t.name} *
                </label>
                <input
                  type="text"
                  required
                  value={formData.name}
                  onChange={(e) =>
                    setFormData({ ...formData, name: e.target.value })
                  }
                  className="w-full px-3 py-2 border border-gray-300 rounded-lg focus:outline-none focus:ring-2 focus:ring-amber-500"
                />
              </div>

              <div>
                <label className="block text-sm font-medium text-gray-700 mb-2">
                  {t.category} *
                </label>
                <select
                  required
                  value={formData.category}
                  onChange={(e) =>
                    setFormData({
                      ...formData,
                      category: e.target.value as any,
                    })
                  }
                  className="w-full px-3 py-2 border border-gray-300 rounded-lg focus:outline-none focus:ring-2 focus:ring-amber-500"
                >
                  {categories.map((cat) => (
                    <option key={cat} value={cat}>
                      {cat}
                    </option>
                  ))}
                </select>
              </div>

              <div>
                <label className="block text-sm font-medium text-gray-700 mb-2">
                  {t.basePrice} *
                </label>
                <input
                  type="number"
                  required
                  step="0.01"
                  min="0"
                  value={formData.basePrice}
                  onChange={(e) =>
                    setFormData({
                      ...formData,
                      basePrice: parseFloat(e.target.value),
                    })
                  }
                  className="w-full px-3 py-2 border border-gray-300 rounded-lg focus:outline-none focus:ring-2 focus:ring-amber-500"
                />
              </div>

              <div>
                <label className="block text-sm font-medium text-gray-700 mb-2">
                  {t.preparationTime}
                </label>
                <input
                  type="number"
                  min="1"
                  value={formData.preparationTime}
                  onChange={(e) =>
                    setFormData({
                      ...formData,
                      preparationTime: parseInt(e.target.value),
                    })
                  }
                  className="w-full px-3 py-2 border border-gray-300 rounded-lg focus:outline-none focus:ring-2 focus:ring-amber-500"
                />
              </div>
            </div>

            <div>
              <label className="block text-sm font-medium text-gray-700 mb-2">
                {t.description}
              </label>
              <textarea
                value={formData.description}
                onChange={(e) =>
                  setFormData({ ...formData, description: e.target.value })
                }
                className="w-full px-3 py-2 border border-gray-300 rounded-lg focus:outline-none focus:ring-2 focus:ring-amber-500"
                rows={4}
              />
            </div>

            <div>
              <label className="block text-sm font-medium text-gray-700 mb-2">
                {t.imageUrl}
              </label>
              <input
                type="url"
                placeholder={t.imagePlaceholder}
                value={formData.image}
                onChange={(e) =>
                  setFormData({ ...formData, image: e.target.value })
                }
                className="w-full px-3 py-2 border border-gray-300 rounded-lg focus:outline-none focus:ring-2 focus:ring-amber-500"
              />
              {formData.image && (
                <div className="mt-3">
                  <p className="text-xs text-gray-600 mb-2">{t.preview}</p>
                  <img
                    src={formData.image}
                    alt="Preview"
                    className="h-32 w-32 object-cover rounded-lg"
                    onError={(e) => {
                      (e.target as HTMLImageElement).src = "";
                    }}
                  />
                </div>
              )}
            </div>

            <button
              type="submit"
              className="px-6 py-2 bg-[#ffd119] text-black rounded-lg hover:bg-amber-700 font-semibold"
            >
              {editingId ? t.updateItem : t.addItem}
            </button>
          </form>
        </div>
      )}

      {loading ? (
        <div className="text-center py-12">
          <p className="text-gray-600">{t.loadingItems}</p>
        </div>
      ) : (
        <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-6">
          {items.map((item) => (
            <div key={item._id} className="bg-white rounded-lg shadow p-6">
              <div className="bg-gray-100 rounded-lg h-40 flex items-center justify-center mb-4 overflow-hidden">
                {item.image ? (
                  <img
                    src={item.image}
                    alt={item.name}
                    className="w-full h-full object-cover"
                    onError={(e) => {
                      (e.target as HTMLImageElement).style.display = "none";
                      if ((e.target as HTMLImageElement).nextElementSibling) {
                        (
                          (e.target as HTMLImageElement)
                            .nextElementSibling as HTMLElement
                        ).style.display = "flex";
                      }
                    }}
                  />
                ) : null}
                {!item.image && (
                  <div className="w-full h-full flex items-center justify-center bg-amber-100 text-4xl">
                    ☕
                  </div>
                )}
              </div>
              <h3 className="font-bold text-lg text-gray-900 mb-1">
                {item.name}
              </h3>
              <p className="text-sm text-gray-600 mb-3">{item.description}</p>
              <div className="flex justify-between items-center mb-4">
                <div>
                  <p className="text-xs text-gray-600">{t.price}</p>
                  <p className="text-xl font-bold text-[#d4ad10]">
                    {item.basePrice.toFixed(2)}{" "}
                    <span className="text-black">₸</span>
                  </p>
                </div>
                <div className="text-right">
                  <p className="text-xs text-gray-600">{t.prepTime}</p>
                  <p className="text-lg font-bold text-gray-900">
                    {item.preparationTime}m
                  </p>
                </div>
              </div>
              <span className="inline-block px-3 py-1 bg-gray-200 text-gray-700 rounded-full text-xs font-semibold mb-4 capitalize">
                {item.category}
              </span>
              <div className="flex gap-2">
                <button
                  onClick={() => handleEdit(item)}
                  className="flex-1 px-3 py-2 bg-blue-100 text-blue-700 rounded-lg hover:bg-blue-200 transition font-semibold text-sm"
                >
                  {t.edit}
                </button>
                <button
                  onClick={() => handleDelete(item._id)}
                  className="flex-1 px-3 py-2 bg-red-100 text-red-700 rounded-lg hover:bg-red-200 transition font-semibold text-sm"
                >
                  {t.delete}
                </button>
              </div>
            </div>
          ))}
        </div>
      )}
    </div>
  );
}
