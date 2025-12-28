"use client";

import { ITopping } from "@/types";
import { useSession } from "next-auth/react";
import { useRouter } from "next/navigation";
import { useEffect, useState } from "react";

export default function AdminToppingsPage() {
  const router = useRouter();
  const { data: session, status } = useSession();

  const [toppings, setToppings] = useState<ITopping[]>([]);
  const [showForm, setShowForm] = useState(false);
  const [editingId, setEditingId] = useState<string | null>(null);
  const [loading, setLoading] = useState(true);
  const [formData, setFormData] = useState<{
    name: string;
    price: number;
    category: "syrup" | "shot" | "milk" | "topping";
    description: string;
  }>({
    name: "",
    price: 0,
    category: "topping",
    description: "",
  });

  const categories = ["syrup", "shot", "milk", "topping"];

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

    fetchToppings();
  }, [status, session, router]);

  const fetchToppings = async () => {
    try {
      const res = await fetch("/api/admin/toppings");
      if (res.ok) {
        const data = await res.json();
        setToppings(data.toppings);
      }
    } catch (error) {
      console.error("Error fetching toppings:", error);
    } finally {
      setLoading(false);
    }
  };

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();

    try {
      const method = editingId ? "PUT" : "POST";
      const url = editingId
        ? `/api/admin/toppings/${editingId}`
        : "/api/admin/toppings";

      const res = await fetch(url, {
        method,
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify(formData),
      });

      if (res.ok) {
        fetchToppings();
        setShowForm(false);
        setEditingId(null);
        setFormData({
          name: "",
          price: 0,
          category: "topping",
          description: "",
        });
      }
    } catch (error) {
      console.error("Error saving topping:", error);
    }
  };

  const handleDelete = async (id: string) => {
    if (!confirm("Are you sure?")) return;

    try {
      const res = await fetch(`/api/admin/toppings/${id}`, {
        method: "DELETE",
      });
      if (res.ok) {
        fetchToppings();
      }
    } catch (error) {
      console.error("Error deleting topping:", error);
    }
  };

  const handleEdit = (topping: ITopping) => {
    setEditingId(topping._id);
    setFormData({
      name: topping.name,
      price: topping.price,
      category: topping.category || "topping",
      description: topping.description || "",
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
        <h1 className="text-3xl font-bold text-gray-900">
          Toppings Management
        </h1>
        <button
          onClick={() => {
            setShowForm(!showForm);
            setEditingId(null);
            setFormData({
              name: "",
              price: 0,
              category: "topping",
              description: "",
            });
          }}
          className="px-4 py-2 bg-amber-600 text-white rounded-lg hover:bg-amber-700 font-semibold"
        >
          {showForm ? "Cancel" : "Add New Topping"}
        </button>
      </div>

      {showForm && (
        <div className="bg-white rounded-lg shadow p-6 mb-8">
          <h2 className="text-xl font-bold text-gray-900 mb-6">
            {editingId ? "Edit" : "Add New"} Topping
          </h2>
          <form onSubmit={handleSubmit} className="space-y-6">
            <div className="grid grid-cols-1 md:grid-cols-2 gap-6">
              <div>
                <label className="block text-sm font-medium text-gray-700 mb-2">
                  Name *
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
                  Category *
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
                  Price *
                </label>
                <input
                  type="number"
                  required
                  step="0.01"
                  min="0"
                  value={String(formData.price || 0)}
                  onChange={(e) =>
                    setFormData({
                      ...formData,
                      price: parseFloat(e.target.value) || 0,
                    })
                  }
                  className="w-full px-3 py-2 border border-gray-300 rounded-lg focus:outline-none focus:ring-2 focus:ring-amber-500"
                />
              </div>
            </div>

            <div>
              <label className="block text-sm font-medium text-gray-700 mb-2">
                Description
              </label>
              <textarea
                value={formData.description}
                onChange={(e) =>
                  setFormData({ ...formData, description: e.target.value })
                }
                className="w-full px-3 py-2 border border-gray-300 rounded-lg focus:outline-none focus:ring-2 focus:ring-amber-500"
                rows={3}
              />
            </div>

            <button
              type="submit"
              className="px-6 py-2 bg-amber-600 text-white rounded-lg hover:bg-amber-700 font-semibold"
            >
              {editingId ? "Update Topping" : "Add Topping"}
            </button>
          </form>
        </div>
      )}

      {loading ? (
        <div className="text-center py-12">
          <p className="text-gray-600">Loading toppings...</p>
        </div>
      ) : (
        <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-6">
          {toppings.map((topping) => (
            <div key={topping._id} className="bg-white rounded-lg shadow p-6">
              <div className="flex justify-between items-start mb-4">
                <div className="flex-1">
                  <h3 className="font-bold text-lg text-gray-900">
                    {topping.name}
                  </h3>
                  {topping.description && (
                    <p className="text-sm text-gray-600 mt-1">
                      {topping.description}
                    </p>
                  )}
                </div>
              </div>

              <div className="flex justify-between items-center mb-4">
                <div>
                  <p className="text-xs text-gray-600">Price</p>
                  <p className="text-xl font-bold text-amber-600">
                    ${topping.price.toFixed(2)}
                  </p>
                </div>
                <span className="inline-block px-3 py-1 bg-gray-200 text-gray-700 rounded-full text-xs font-semibold capitalize">
                  {topping.category || "topping"}
                </span>
              </div>

              <div className="flex gap-2">
                <button
                  onClick={() => handleEdit(topping)}
                  className="flex-1 px-3 py-2 bg-blue-100 text-blue-700 rounded-lg hover:bg-blue-200 transition font-semibold text-sm"
                >
                  Edit
                </button>
                <button
                  onClick={() => handleDelete(topping._id)}
                  className="flex-1 px-3 py-2 bg-red-100 text-red-700 rounded-lg hover:bg-red-200 transition font-semibold text-sm"
                >
                  Delete
                </button>
              </div>
            </div>
          ))}
        </div>
      )}
    </div>
  );
}
