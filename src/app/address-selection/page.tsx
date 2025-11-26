"use client";

import { useEffect, useState } from "react";
import { useRouter, useSearchParams } from "next/navigation";
import { Button } from "xtreme-ui";

import "./addressSelection.scss";

type Restaurant = {
  _id: string;
  name: string;
  restaurantID: string;
  description?: string;
  avatar?: string;
  addresses: string[];
};

const AddressSelection = () => {
  const router = useRouter();
  const searchParams = useSearchParams();
  const restaurantParam = searchParams.get("restaurant");

  const [step, setStep] = useState<"restaurant" | "address">("restaurant");
  const [restaurants, setRestaurants] = useState<Restaurant[]>([]);
  const [selectedRestaurant, setSelectedRestaurant] = useState<Restaurant | null>(null);
  const [addresses, setAddresses] = useState<string[]>([]);
  const [selectedAddress, setSelectedAddress] = useState<string>("");
  const [searchQuery, setSearchQuery] = useState("");
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    const fetchRestaurants = async () => {
      try {
        const response = await fetch("/api/restaurants");
        
        if (!response.ok) {
          throw new Error("Failed to fetch restaurants");
        }

        const data = await response.json();
        setRestaurants(data.restaurants || []);

        // If restaurant parameter is provided, find it and skip to address selection
        if (restaurantParam) {
          const restaurant = data.restaurants.find(
            (r: Restaurant) => r.restaurantID === restaurantParam
          );
          if (restaurant) {
            setSelectedRestaurant(restaurant);
            setAddresses(restaurant.addresses || []);
            
            // Auto-redirect if exactly one address
            if (restaurant.addresses?.length === 1) {
              const address = restaurant.addresses[0];
              sessionStorage.setItem("selectedAddress", address);
              router.push(`/${restaurantParam}?tab=menu&address=${encodeURIComponent(address)}`);
              return;
            }
            
            setStep("address");
          }
        }

        setLoading(false);
      } catch (err) {
        setError(err instanceof Error ? err.message : "An error occurred");
        setLoading(false);
      }
    };

    fetchRestaurants();
  }, [restaurantParam, router]);

  const filteredRestaurants = restaurants.filter((restaurant) =>
    restaurant.name.toLowerCase().includes(searchQuery.toLowerCase()) ||
    restaurant.restaurantID.toLowerCase().includes(searchQuery.toLowerCase())
  );

  const filteredAddresses = addresses.filter((address) =>
    address.toLowerCase().includes(searchQuery.toLowerCase())
  );

  const handleRestaurantSelect = (restaurant: Restaurant) => {
    setSelectedRestaurant(restaurant);
    setAddresses(restaurant.addresses || []);
    setSearchQuery("");
    
    // Auto-redirect if exactly one address
    if (restaurant.addresses?.length === 1) {
      const address = restaurant.addresses[0];
      sessionStorage.setItem("selectedAddress", address);
      router.push(`/${restaurant.restaurantID}?tab=menu&address=${encodeURIComponent(address)}`);
      return;
    }
    
    setStep("address");
  };

  const handleAddressSelect = (address: string) => {
    setSelectedAddress(address);
  };

  const handleContinue = () => {
    if (selectedAddress && selectedRestaurant) {
      sessionStorage.setItem("selectedAddress", selectedAddress);
      router.push(`/${selectedRestaurant.restaurantID}?tab=menu&address=${encodeURIComponent(selectedAddress)}`);
    }
  };

  const handleBack = () => {
    setStep("restaurant");
    setSelectedRestaurant(null);
    setSelectedAddress("");
    setAddresses([]);
    setSearchQuery("");
  };

  if (loading) {
    return (
      <div className="addressSelection">
        <div className="loadingContainer">
          <p>Loading...</p>
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

  // Step 1: Restaurant Selection
  if (step === "restaurant") {
    if (restaurants.length === 0) {
      return (
        <div className="addressSelection">
          <div className="noAddressesContainer">
            <h2>No Restaurants Available</h2>
            <p>There are no restaurants available at the moment.</p>
            <Button label="Go Back" onClick={() => router.push("/")} />
          </div>
        </div>
      );
    }

    return (
      <div className="addressSelection">
        <div className="container">
          <h1 className="title">Select Restaurant</h1>

          {restaurants.length > 3 && (
            <input
              type="text"
              className="searchInput"
              placeholder="Search restaurants..."
              value={searchQuery}
              onChange={(e) => setSearchQuery(e.target.value)}
            />
          )}

          <div className="restaurantList">
            {filteredRestaurants.map((restaurant) => (
              <div
                key={restaurant._id}
                className="restaurantCard"
                onClick={() => handleRestaurantSelect(restaurant)}
              >
                {restaurant.avatar && (
                  <img
                    src={restaurant.avatar}
                    alt={restaurant.name}
                    className="restaurantAvatar"
                  />
                )}
                <div className="restaurantInfo">
                  <h3 className="restaurantName">{restaurant.name}</h3>
                  {restaurant.description && (
                    <p className="restaurantDescription">{restaurant.description}</p>
                  )}
                  <p className="restaurantAddressCount">
                    {restaurant.addresses?.length || 0} location{restaurant.addresses?.length !== 1 ? 's' : ''}
                  </p>
                </div>
              </div>
            ))}
          </div>

          {filteredRestaurants.length === 0 && (
            <p className="noResults">No restaurants match your search.</p>
          )}

          <div className="actionButtons">
            <Button
              label="Cancel"
              onClick={() => router.push("/")}
              type="secondary"
            />
          </div>
        </div>
      </div>
    );
  }

  // Step 2: Address Selection
  if (addresses.length === 0) {
    return (
      <div className="addressSelection">
        <div className="noAddressesContainer">
          <h2>No Addresses Available</h2>
          <p>This restaurant has no addresses configured yet.</p>
          <Button label="Go Back" onClick={handleBack} />
        </div>
      </div>
    );
  }

  return (
    <div className="addressSelection">
      <div className="container">
        <h1 className="title">Select Location</h1>
        {selectedRestaurant && (
          <p className="restaurantName">{selectedRestaurant.name}</p>
        )}

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
            label="Back"
            onClick={handleBack}
            type="secondary"
          />
        </div>
      </div>
    </div>
  );
};

export default AddressSelection;
