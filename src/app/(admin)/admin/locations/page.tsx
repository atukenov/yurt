"use client";

import { ToastContainer, useToast } from "@/components/Toast";
import { ILocation } from "@/types";
import { useSession } from "next-auth/react";
import { useRouter } from "next/navigation";
import { useEffect, useState } from "react";

interface LocationFormData {
  name: string;
  address: string;
  city: string;
  zipCode: string;
  phone: string;
  latitude?: number;
  longitude?: number;
  isActive: boolean;
  workingHours: {
    [key: string]: { open: string; close: string };
  };
}

export default function AdminLocationsPage() {
  const router = useRouter();
  const { data: session } = useSession({
    required: true,
    onUnauthenticated() {
      router.push("/login");
    },
  });

  const [locations, setLocations] = useState<ILocation[]>([]);
  const [showForm, setShowForm] = useState(false);
  const [editingId, setEditingId] = useState<string | null>(null);
  const [loading, setLoading] = useState(true);
  const [deleteConfirm, setDeleteConfirm] = useState<string | null>(null);
  const { showToast } = useToast();

  const days = [
    "monday",
    "tuesday",
    "wednesday",
    "thursday",
    "friday",
    "saturday",
    "sunday",
  ];

  const [formData, setFormData] = useState<LocationFormData>({
    name: "",
    address: "",
    city: "",
    zipCode: "",
    phone: "",
    latitude: undefined,
    longitude: undefined,
    isActive: true,
    workingHours: {
      monday: { open: "06:00", close: "20:00" },
      tuesday: { open: "06:00", close: "20:00" },
      wednesday: { open: "06:00", close: "20:00" },
      thursday: { open: "06:00", close: "20:00" },
      friday: { open: "06:00", close: "20:00" },
      saturday: { open: "07:00", close: "21:00" },
      sunday: { open: "07:00", close: "21:00" },
    },
  });

  useEffect(() => {
    if (session?.user?.role !== "admin") {
      router.push("/");
    } else {
      fetchLocations();
    }
  }, [session, router]);

  const fetchLocations = async () => {
    try {
      const res = await fetch("/api/admin/locations");
      if (res.ok) {
        const data = await res.json();
        setLocations(data.locations);
      }
    } catch (error) {
      console.error("Error fetching locations:", error);
    } finally {
      setLoading(false);
    }
  };

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();

    try {
      const method = editingId ? "PUT" : "POST";
      const url = editingId
        ? `/api/admin/locations/${editingId}`
        : "/api/admin/locations";

      const res = await fetch(url, {
        method,
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify(formData),
      });

      if (res.ok) {
        showToast(
          editingId
            ? "Location updated successfully!"
            : "Location added successfully!",
          "success"
        );
        fetchLocations();
        setShowForm(false);
        setEditingId(null);
        resetForm();
      } else {
        showToast("Failed to save location", "error");
      }
    } catch (error) {
      console.error("Error saving location:", error);
      showToast("Error saving location", "error");
    }
  };

  const handleDelete = async (id: string) => {
    try {
      const res = await fetch(`/api/admin/locations/${id}`, {
        method: "DELETE",
      });
      if (res.ok) {
        showToast("Location deleted successfully!", "success");
        setDeleteConfirm(null);
        fetchLocations();
      } else {
        showToast("Failed to delete location", "error");
      }
    } catch (error) {
      console.error("Error deleting location:", error);
      showToast("Error deleting location", "error");
    }
  };

  const handleEdit = (location: ILocation) => {
    setEditingId(location._id);
    setFormData({
      name: location.name,
      address: location.address,
      city: location.city,
      zipCode: location.zipCode,
      phone: location.phone || "",
      latitude: location.latitude,
      longitude: location.longitude,
      isActive: location.isActive ?? true,
      workingHours: location.workingHours || {
        monday: { open: "06:00", close: "20:00" },
        tuesday: { open: "06:00", close: "20:00" },
        wednesday: { open: "06:00", close: "20:00" },
        thursday: { open: "06:00", close: "20:00" },
        friday: { open: "06:00", close: "20:00" },
        saturday: { open: "07:00", close: "21:00" },
        sunday: { open: "07:00", close: "21:00" },
      },
    });
    setShowForm(true);
  };

  const resetForm = () => {
    setFormData({
      name: "",
      address: "",
      city: "",
      zipCode: "",
      phone: "",
      latitude: undefined,
      longitude: undefined,
      isActive: true,
      workingHours: {
        monday: { open: "06:00", close: "20:00" },
        tuesday: { open: "06:00", close: "20:00" },
        wednesday: { open: "06:00", close: "20:00" },
        thursday: { open: "06:00", close: "20:00" },
        friday: { open: "06:00", close: "20:00" },
        saturday: { open: "07:00", close: "21:00" },
        sunday: { open: "07:00", close: "21:00" },
      },
    });
  };

  if (session?.user?.role !== "admin") {
    return null;
  }

  return (
    <div className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8 py-8">
      <ToastContainer />

      <div className="flex justify-between items-center mb-8">
        <h1 className="text-3xl font-bold text-gray-900">
          Location Management
        </h1>
        <button
          onClick={() => {
            setShowForm(!showForm);
            setEditingId(null);
            resetForm();
          }}
          className="px-4 py-2 bg-amber-600 text-white rounded-lg hover:bg-amber-700 font-semibold transition"
        >
          {showForm ? "Cancel" : "+ Add Location"}
        </button>
      </div>

      {showForm && (
        <div className="bg-white rounded-lg shadow p-8 mb-8">
          <h2 className="text-2xl font-bold text-gray-900 mb-8">
            {editingId ? "Edit" : "Add New"} Location
          </h2>
          <form onSubmit={handleSubmit} className="space-y-8">
            {/* Basic Information */}
            <div>
              <h3 className="text-lg font-semibold text-gray-900 mb-4">
                Basic Information
              </h3>
              <div className="grid grid-cols-1 md:grid-cols-2 gap-6">
                <div>
                  <label className="block text-sm font-medium text-gray-700 mb-2">
                    Location Name *
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
                    City *
                  </label>
                  <input
                    type="text"
                    required
                    value={formData.city}
                    onChange={(e) =>
                      setFormData({ ...formData, city: e.target.value })
                    }
                    className="w-full px-3 py-2 border border-gray-300 rounded-lg focus:outline-none focus:ring-2 focus:ring-amber-500"
                  />
                </div>

                <div className="md:col-span-2">
                  <label className="block text-sm font-medium text-gray-700 mb-2">
                    Address *
                  </label>
                  <input
                    type="text"
                    required
                    value={formData.address}
                    onChange={(e) =>
                      setFormData({ ...formData, address: e.target.value })
                    }
                    className="w-full px-3 py-2 border border-gray-300 rounded-lg focus:outline-none focus:ring-2 focus:ring-amber-500"
                  />
                </div>

                <div>
                  <label className="block text-sm font-medium text-gray-700 mb-2">
                    Zip Code *
                  </label>
                  <input
                    type="text"
                    required
                    value={formData.zipCode}
                    onChange={(e) =>
                      setFormData({ ...formData, zipCode: e.target.value })
                    }
                    className="w-full px-3 py-2 border border-gray-300 rounded-lg focus:outline-none focus:ring-2 focus:ring-amber-500"
                  />
                </div>

                <div>
                  <label className="block text-sm font-medium text-gray-700 mb-2">
                    Phone
                  </label>
                  <input
                    type="tel"
                    value={formData.phone}
                    onChange={(e) =>
                      setFormData({ ...formData, phone: e.target.value })
                    }
                    className="w-full px-3 py-2 border border-gray-300 rounded-lg focus:outline-none focus:ring-2 focus:ring-amber-500"
                  />
                </div>
              </div>
            </div>

            {/* Working Hours */}
            <div>
              <h3 className="text-lg font-semibold text-gray-900 mb-4">
                Working Hours
              </h3>
              <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
                {days.map((day) => (
                  <div
                    key={day}
                    className="flex items-end gap-3 p-3 bg-gray-50 rounded-lg"
                  >
                    <label className="capitalize font-medium text-gray-700 min-w-20">
                      {day}
                    </label>
                    <input
                      type="time"
                      value={formData.workingHours[day]?.open || "06:00"}
                      onChange={(e) => {
                        setFormData({
                          ...formData,
                          workingHours: {
                            ...formData.workingHours,
                            [day]: {
                              ...formData.workingHours[day],
                              open: e.target.value,
                            },
                          },
                        });
                      }}
                      className="flex-1 px-2 py-1 text-sm border border-gray-300 rounded focus:outline-none focus:ring-2 focus:ring-amber-500"
                    />
                    <span className="text-gray-500">-</span>
                    <input
                      type="time"
                      value={formData.workingHours[day]?.close || "20:00"}
                      onChange={(e) => {
                        setFormData({
                          ...formData,
                          workingHours: {
                            ...formData.workingHours,
                            [day]: {
                              ...formData.workingHours[day],
                              close: e.target.value,
                            },
                          },
                        });
                      }}
                      className="flex-1 px-2 py-1 text-sm border border-gray-300 rounded focus:outline-none focus:ring-2 focus:ring-amber-500"
                    />
                  </div>
                ))}
              </div>
            </div>

            {/* Status */}
            <div className="flex items-center gap-3 p-4 bg-gray-50 rounded-lg">
              <input
                type="checkbox"
                id="isActive"
                checked={formData.isActive}
                onChange={(e) =>
                  setFormData({ ...formData, isActive: e.target.checked })
                }
                className="w-5 h-5 text-amber-600 rounded focus:ring-2 focus:ring-amber-500"
              />
              <label
                htmlFor="isActive"
                className="text-sm font-medium text-gray-700"
              >
                This location is active and accepting orders
              </label>
            </div>

            <div className="flex gap-3">
              <button
                type="submit"
                className="px-6 py-2 bg-amber-600 text-white rounded-lg hover:bg-amber-700 transition font-semibold"
              >
                {editingId ? "Update Location" : "Add Location"}
              </button>
              <button
                type="button"
                onClick={() => {
                  setShowForm(false);
                  setEditingId(null);
                  resetForm();
                }}
                className="px-6 py-2 bg-gray-200 text-gray-700 rounded-lg hover:bg-gray-300 transition font-semibold"
              >
                Cancel
              </button>
            </div>
          </form>
        </div>
      )}

      {loading ? (
        <div className="text-center py-12">
          <p className="text-gray-600">Loading locations...</p>
        </div>
      ) : locations.length === 0 ? (
        <div className="text-center py-12 bg-gray-50 rounded-lg">
          <p className="text-gray-600 text-lg">
            No locations yet. Create your first location!
          </p>
        </div>
      ) : (
        <div className="grid grid-cols-1 md:grid-cols-2 gap-6">
          {locations.map((location) => (
            <div
              key={location._id}
              className="bg-white rounded-lg shadow-md hover:shadow-lg transition overflow-hidden"
            >
              <div className="p-6">
                <div className="flex items-start justify-between mb-4">
                  <h3 className="font-bold text-lg text-gray-900">
                    {location.name}
                  </h3>
                  <span
                    className={`px-3 py-1 rounded-full text-xs font-semibold ${
                      location.isActive
                        ? "bg-green-100 text-green-800"
                        : "bg-gray-100 text-gray-800"
                    }`}
                  >
                    {location.isActive ? "Active" : "Inactive"}
                  </span>
                </div>

                <div className="space-y-3 mb-6">
                  <div>
                    <p className="text-sm font-medium text-gray-600">Address</p>
                    <p className="text-gray-900">{location.address}</p>
                  </div>
                  <div>
                    <p className="text-sm font-medium text-gray-600">
                      City & Zip
                    </p>
                    <p className="text-gray-900">
                      {location.city}, {location.zipCode}
                    </p>
                  </div>
                  {location.phone && (
                    <div>
                      <p className="text-sm font-medium text-gray-600">Phone</p>
                      <p className="text-gray-900">{location.phone}</p>
                    </div>
                  )}
                  {location.workingHours && (
                    <div>
                      <p className="text-sm font-medium text-gray-600 mb-2">
                        Hours
                      </p>
                      <div className="grid grid-cols-2 gap-2 text-sm">
                        {days.map((day) => (
                          <div key={day} className="text-gray-700">
                            <span className="capitalize font-medium">
                              {day}:{" "}
                            </span>
                            {location.workingHours[day]?.open}-
                            {location.workingHours[day]?.close}
                          </div>
                        ))}
                      </div>
                    </div>
                  )}
                </div>

                <div className="flex gap-2 pt-4 border-t border-gray-200">
                  <button
                    onClick={() => handleEdit(location)}
                    className="flex-1 px-3 py-2 bg-blue-100 text-blue-700 rounded-lg hover:bg-blue-200 transition font-semibold text-sm"
                  >
                    Edit
                  </button>
                  <button
                    onClick={() => setDeleteConfirm(location._id)}
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

      {/* Delete Confirmation Modal */}
      {deleteConfirm && (
        <div className="fixed inset-0 bg-black bg-opacity-50 flex items-center justify-center p-4 z-50">
          <div className="bg-white rounded-lg p-6 max-w-sm w-full">
            <h3 className="text-lg font-bold text-gray-900 mb-4">
              Delete Location
            </h3>
            <p className="text-gray-600 mb-6">
              Are you sure you want to delete this location? This action cannot
              be undone.
            </p>
            <div className="flex gap-3">
              <button
                onClick={() => setDeleteConfirm(null)}
                className="flex-1 px-4 py-2 bg-gray-200 text-gray-700 rounded-lg hover:bg-gray-300 transition font-semibold"
              >
                Cancel
              </button>
              <button
                onClick={() => {
                  handleDelete(deleteConfirm);
                  setDeleteConfirm(null);
                }}
                className="flex-1 px-4 py-2 bg-red-600 text-white rounded-lg hover:bg-red-700 transition font-semibold"
              >
                Delete
              </button>
            </div>
          </div>
        </div>
      )}
    </div>
  );
}
