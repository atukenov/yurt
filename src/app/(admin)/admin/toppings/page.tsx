"use client";

import { ITopping } from "@/types";
import { useSession } from "next-auth/react";
import { useRouter } from "next/navigation";
import { useEffect, useState } from "react";

export default function AdminToppingsPage() {
  const router = useRouter();
  const { data: session, status } = useSession();

  const [toppings, setToppings] = useState<ITopping[]>([]);
  const [filteredToppings, setFilteredToppings] = useState<ITopping[]>([]);
  const [showForm, setShowForm] = useState(false);
  const [editingId, setEditingId] = useState<string | null>(null);
  const [loading, setLoading] = useState(true);
  const [searchQuery, setSearchQuery] = useState("");
  const [selectedCategory, setSelectedCategory] = useState<string>("all");
  const [message, setMessage] = useState<{
    type: "success" | "error";
    text: string;
  } | null>(null);
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

  // Filter toppings based on search and category
  useEffect(() => {
    let filtered = toppings;

    // Filter by search query
    if (searchQuery) {
      filtered = filtered.filter(
        (topping) =>
          topping.name.toLowerCase().includes(searchQuery.toLowerCase()) ||
          topping.description?.toLowerCase().includes(searchQuery.toLowerCase())
      );
    }

    // Filter by category
    if (selectedCategory !== "all") {
      filtered = filtered.filter(
        (topping) => (topping.category || "topping") === selectedCategory
      );
    }

    setFilteredToppings(filtered);
  }, [toppings, searchQuery, selectedCategory]);

  const fetchToppings = async () => {
    try {
      const res = await fetch("/api/admin/toppings");
      if (res.ok) {
        const data = await res.json();
        setToppings(data.toppings);
      }
    } catch (error) {
      console.error("Error fetching toppings:", error);
      showMessage("Failed to load toppings", "error");
    } finally {
      setLoading(false);
    }
  };

  const showMessage = (text: string, type: "success" | "error") => {
    setMessage({ type, text });
    setTimeout(() => setMessage(null), 4000);
  };

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();

    if (!formData.name.trim()) {
      showMessage("Topping name is required", "error");
      return;
    }

    if (formData.price < 0) {
      showMessage("Price must be greater than or equal to 0", "error");
      return;
    }

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
        await fetchToppings();
        setShowForm(false);
        setEditingId(null);
        setFormData({
          name: "",
          price: 0,
          category: "topping",
          description: "",
        });
        showMessage(
          editingId
            ? "Topping updated successfully"
            : "Topping added successfully",
          "success"
        );
      } else {
        showMessage("Failed to save topping", "error");
      }
    } catch (error) {
      console.error("Error saving topping:", error);
      showMessage("An error occurred while saving", "error");
    }
  };

  const handleDelete = async (id: string, name: string) => {
    if (!confirm(`Are you sure you want to delete "${name}"?`)) return;

    try {
      const res = await fetch(`/api/admin/toppings/${id}`, {
        method: "DELETE",
      });
      if (res.ok) {
        await fetchToppings();
        showMessage("Topping deleted successfully", "success");
      } else {
        showMessage("Failed to delete topping", "error");
      }
    } catch (error) {
      console.error("Error deleting topping:", error);
      showMessage("An error occurred while deleting", "error");
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
    window.scrollTo({ top: 0, behavior: "smooth" });
  };

  const handleCancel = () => {
    setShowForm(false);
    setEditingId(null);
    setFormData({
      name: "",
      price: 0,
      category: "topping",
      description: "",
    });
  };

  const getCategoryStats = () => {
    return categories.map((cat) => ({
      category: cat,
      count: toppings.filter((t) => (t.category || "topping") === cat).length,
    }));
  };

  // Show loading state while checking authentication
  if (status === "loading" || session?.user?.role !== "admin") {
    return (
      <div className="min-h-screen flex items-center justify-center">
        <p className="text-gray-600">Loading...</p>
      </div>
    );
  }

  const stats = getCategoryStats();

  return (
    <div className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8 py-8">
      {/* Message Alert */}
      {message && (
        <div
          className={`mb-6 p-4 rounded-lg ${
            message.type === "success"
              ? "bg-green-50 text-green-800 border border-green-200"
              : "bg-red-50 text-red-800 border border-red-200"
          }`}
        >
          {message.type === "success" ? "✓ " : "✕ "}
          {message.text}
        </div>
      )}

      {/* Header */}
      <div className="flex justify-between items-center mb-8">
        <div>
          <h1 className="text-3xl font-bold text-gray-900">
            Toppings Management
          </h1>
          <p className="text-gray-600 text-sm mt-1">
            Total toppings: {toppings.length}
          </p>
        </div>
        <button
          onClick={() => {
            if (showForm) {
              handleCancel();
            } else {
              setShowForm(true);
            }
          }}
          className="px-4 py-2 bg-amber-600 text-white rounded-lg hover:bg-amber-700 font-semibold transition"
        >
          {showForm ? "Cancel" : "+ Add New Topping"}
        </button>
      </div>

      {/* Stats Cards */}
      {!showForm && (
        <div className="grid grid-cols-2 md:grid-cols-4 gap-4 mb-8">
          {stats.map((stat) => (
            <div
              key={stat.category}
              className="bg-white rounded-lg shadow p-4 cursor-pointer hover:shadow-lg transition"
              onClick={() => setSelectedCategory(stat.category)}
            >
              <p className="text-xs text-gray-600 uppercase mb-1 capitalize">
                {stat.category}
              </p>
              <p className="text-2xl font-bold text-amber-600">{stat.count}</p>
            </div>
          ))}
        </div>
      )}

      {/* Add/Edit Form */}
      {showForm && (
        <div className="bg-white rounded-lg shadow p-6 mb-8">
          <h2 className="text-xl font-bold text-gray-900 mb-6">
            {editingId ? "Edit Topping" : "Add New Topping"}
          </h2>
          <form onSubmit={handleSubmit} className="space-y-6">
            <div className="grid grid-cols-1 md:grid-cols-2 gap-6">
              <div>
                <label className="block text-sm font-medium text-gray-700 mb-2">
                  Name <span className="text-red-600">*</span>
                </label>
                <input
                  type="text"
                  required
                  value={formData.name}
                  onChange={(e) =>
                    setFormData({ ...formData, name: e.target.value })
                  }
                  placeholder="e.g., Extra Shot, Vanilla Syrup"
                  className="w-full px-3 py-2 border border-gray-300 rounded-lg focus:outline-none focus:ring-2 focus:ring-amber-500"
                />
              </div>

              <div>
                <label className="block text-sm font-medium text-gray-700 mb-2">
                  Category <span className="text-red-600">*</span>
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
                      {cat.charAt(0).toUpperCase() + cat.slice(1)}
                    </option>
                  ))}
                </select>
              </div>

              <div>
                <label className="block text-sm font-medium text-gray-700 mb-2">
                  Price <span className="text-red-600">*</span>
                </label>
                <div className="relative">
                  <span className="absolute left-3 top-2 text-gray-500">$</span>
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
                    className="w-full pl-6 pr-3 py-2 border border-gray-300 rounded-lg focus:outline-none focus:ring-2 focus:ring-amber-500"
                  />
                </div>
              </div>

              <div>
                <label className="block text-sm font-medium text-gray-700 mb-2">
                  Description
                </label>
                <input
                  type="text"
                  value={formData.description}
                  onChange={(e) =>
                    setFormData({ ...formData, description: e.target.value })
                  }
                  placeholder="Optional description"
                  className="w-full px-3 py-2 border border-gray-300 rounded-lg focus:outline-none focus:ring-2 focus:ring-amber-500"
                />
              </div>
            </div>

            <div className="flex gap-3">
              <button
                type="submit"
                className="px-6 py-2 bg-amber-600 text-white rounded-lg hover:bg-amber-700 font-semibold transition"
              >
                {editingId ? "Update Topping" : "Add Topping"}
              </button>
              <button
                type="button"
                onClick={handleCancel}
                className="px-6 py-2 bg-gray-200 text-gray-700 rounded-lg hover:bg-gray-300 font-semibold transition"
              >
                Cancel
              </button>
            </div>
          </form>
        </div>
      )}

      {/* Search and Filter */}
      {!showForm && (
        <div className="bg-white rounded-lg shadow p-4 mb-6">
          <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
            <div>
              <input
                type="text"
                placeholder="Search by name or description..."
                value={searchQuery}
                onChange={(e) => setSearchQuery(e.target.value)}
                className="w-full px-4 py-2 border border-gray-300 rounded-lg focus:outline-none focus:ring-2 focus:ring-amber-500"
              />
            </div>
            <div>
              <select
                value={selectedCategory}
                onChange={(e) => setSelectedCategory(e.target.value)}
                className="w-full px-4 py-2 border border-gray-300 rounded-lg focus:outline-none focus:ring-2 focus:ring-amber-500"
              >
                <option value="all">All Categories</option>
                {categories.map((cat) => (
                  <option key={cat} value={cat}>
                    {cat.charAt(0).toUpperCase() + cat.slice(1)}
                  </option>
                ))}
              </select>
            </div>
          </div>
        </div>
      )}

      {/* Toppings List */}
      {loading ? (
        <div className="text-center py-12">
          <p className="text-gray-600">Loading toppings...</p>
        </div>
      ) : filteredToppings.length === 0 ? (
        <div className="text-center py-12 bg-gray-50 rounded-lg">
          <svg
            className="w-12 h-12 text-gray-400 mx-auto mb-4"
            fill="none"
            stroke="currentColor"
            viewBox="0 0 24 24"
          >
            <path
              strokeLinecap="round"
              strokeLinejoin="round"
              strokeWidth={1.5}
              d="M12 6v6m0 0v6m0-6h6m0 0h6m-6-6h6m0 0v6"
            />
          </svg>
          <p className="text-gray-600 mb-4">
            {toppings.length === 0
              ? "No toppings yet. Create your first one!"
              : "No toppings match your filters."}
          </p>
          {toppings.length === 0 && (
            <button
              onClick={() => setShowForm(true)}
              className="px-4 py-2 bg-amber-600 text-white rounded-lg hover:bg-amber-700 font-semibold"
            >
              Add First Topping
            </button>
          )}
        </div>
      ) : (
        <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-6">
          {filteredToppings.map((topping) => (
            <div
              key={topping._id}
              className="bg-white rounded-lg shadow hover:shadow-lg transition overflow-hidden"
            >
              <div className="p-6">
                <div className="flex justify-between items-start mb-4">
                  <div className="flex-1">
                    <h3 className="font-bold text-lg text-gray-900">
                      {topping.name}
                    </h3>
                    {topping.description && (
                      <p className="text-sm text-gray-600 mt-1 line-clamp-2">
                        {topping.description}
                      </p>
                    )}
                  </div>
                </div>

                <div className="flex justify-between items-center mb-4">
                  <div>
                    <p className="text-xs text-gray-600 uppercase">Price</p>
                    <p className="text-2xl font-bold text-amber-600">
                      ${topping.price.toFixed(2)}
                    </p>
                  </div>
                  <span className="inline-block px-3 py-1 bg-amber-100 text-amber-700 rounded-full text-xs font-semibold capitalize">
                    {topping.category || "topping"}
                  </span>
                </div>

                <div className="flex gap-2 pt-4 border-t border-gray-200">
                  <button
                    onClick={() => handleEdit(topping)}
                    className="flex-1 px-3 py-2 bg-blue-100 text-blue-700 rounded-lg hover:bg-blue-200 transition font-semibold text-sm"
                  >
                    Edit
                  </button>
                  <button
                    onClick={() => handleDelete(topping._id, topping.name)}
                    className="flex-1 px-3 py-2 bg-red-100 text-red-700 rounded-lg hover:bg-red-200 transition font-semibold text-sm"
                  >
                    Delete
                  </button>
                </div>
              </div>
            </div>
          ))}
        </div>
      )}
    </div>
  );
}
