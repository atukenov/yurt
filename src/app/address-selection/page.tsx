"use client";

import { useEffect, useState } from "react";
import { useRouter, useSearchParams } from "next/navigation";
import { Button } from "xtreme-ui";

import "./addressSelection.scss";

const AddressSelection = () => {
  const router = useRouter();
  const searchParams = useSearchParams();
  const restaurantId = searchParams.get("restaurant");

  const [addresses, setAddresses] = useState<string[]>([]);
  const [selectedAddress, setSelectedAddress] = useState<string>("");
  const [searchQuery, setSearchQuery] = useState("");
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [restaurantName, setRestaurantName] = useState<string>("");

  useEffect(() => {
    const fetchAddresses = async () => {
      if (!restaurantId) {
        setError("Restaurant ID is required");
        setLoading(false);
        return;
      }

      try {
        const response = await fetch(
          `/api/restaurants/addresses?restaurantId=${restaurantId}`
        );
        
        if (!response.ok) {
          throw new Error("Failed to fetch addresses");
        }

        const data = await response.json();
        setAddresses(data.addresses || []);
        setRestaurantName(data.restaurantName || "");

        // Auto-redirect if exactly one address
        if (data.addresses?.length === 1) {
          const address = data.addresses[0];
          sessionStorage.setItem("selectedAddress", address);
          router.push(`/${restaurantId}?tab=menu&address=${encodeURIComponent(address)}`);
        }

        setLoading(false);
      } catch (err) {
        setError(err instanceof Error ? err.message : "An error occurred");
        setLoading(false);
      }
    };

    fetchAddresses();
  }, [restaurantId, router]);

  const filteredAddresses = addresses.filter((address) =>
    address.toLowerCase().includes(searchQuery.toLowerCase())
  );

  const handleAddressSelect = (address: string) => {
    setSelectedAddress(address);
  };

  const handleContinue = () => {
    if (selectedAddress) {
      sessionStorage.setItem("selectedAddress", selectedAddress);
      router.push(`/${restaurantId}?tab=menu&address=${encodeURIComponent(selectedAddress)}`);
    }
  };

  if (loading) {
    return (
      <div className="addressSelection">
        <div className="loadingContainer">
          <p>Loading addresses...</p>
        </div>
      </div>
    );
  }

  if (error) {
    return (
      <div className="addressSelection">
        <div className="errorContainer">
          <h2>Error</h2>
          <p>{error}</p>
          <Button label="Go Back" onClick={() => router.push("/")} />
        </div>
      </div>
    );
  }

  if (addresses.length === 0) {
    return (
      <div className="addressSelection">
        <div className="noAddressesContainer">
          <h2>No Addresses Available</h2>
          <p>This restaurant has no addresses configured yet.</p>
          <Button label="Go Back" onClick={() => router.push("/")} />
        </div>
      </div>
    );
  }

  return (
    <div className="addressSelection">
      <div className="container">
        <h1 className="title">Select Restaurant Location</h1>
        {restaurantName && <p className="restaurantName">{restaurantName}</p>}

        {addresses.length > 3 && (
          <input
            type="text"
            className="searchInput"
            placeholder="Search addresses..."
            value={searchQuery}
            onChange={(e) => setSearchQuery(e.target.value)}
          />
        )}

        <div className="addressList">
          {filteredAddresses.map((address, index) => (
            <div
              key={index}
              className={`addressCard ${selectedAddress === address ? "selected" : ""}`}
              onClick={() => handleAddressSelect(address)}
            >
              <div className="radioButton">
                {selectedAddress === address && <div className="radioFill" />}
              </div>
              <p className="addressText">{address}</p>
            </div>
          ))}
        </div>

        {filteredAddresses.length === 0 && (
          <p className="noResults">No addresses match your search.</p>
        )}

        <div className="actionButtons">
          <Button
            label="Continue"
            onClick={handleContinue}
            disabled={!selectedAddress}
          />
          <Button
            label="Cancel"
            onClick={() => router.push("/")}
            type="secondary"
          />
        </div>
      </div>
    </div>
  );
};

export default AddressSelection;
