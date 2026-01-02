# Location Hours Components Documentation

## Overview

Complete documentation for the frontend components used in the location hours & availability system.

---

## LocationAvailabilityDisplay Component

### Purpose

Display current location availability status on the checkout page and other customer-facing pages.

### File Location

`src/components/LocationAvailabilityDisplay.tsx`

### Props

```typescript
interface LocationAvailabilityDisplayProps {
  locationId: string; // MongoDB location ID (required)
  onAvailabilityChange?: (isAvailable: boolean) => void; // Callback when availability changes
}
```

### Exports

```typescript
export function LocationAvailabilityDisplay(
  props: LocationAvailabilityDisplayProps
): JSX.Element;
```

### Features

✅ **Auto-refresh** - Updates every 60 seconds
✅ **Color-coded** - Green for open, red for closed
✅ **Error handling** - Gracefully handles API errors
✅ **Icon indicators** - Uses react-icons for visual feedback
✅ **Next opening time** - Shows when location will reopen

### Usage Examples

#### Basic Usage (Checkout Page)

```typescript
import { LocationAvailabilityDisplay } from "@/components/LocationAvailabilityDisplay";
import { useState } from "react";

export function CheckoutPage() {
  const [isAvailable, setIsAvailable] = useState(true);
  const locationId = "507f1f77bcf86cd799439011";

  return (
    <div>
      <h1>Checkout</h1>

      {/* Show availability status */}
      <LocationAvailabilityDisplay
        locationId={locationId}
        onAvailabilityChange={setIsAvailable}
      />

      {/* Disable button if closed */}
      <button disabled={!isAvailable}>Place Order</button>
    </div>
  );
}
```

#### With Error Messages

```typescript
export function CheckoutPage() {
  const [isAvailable, setIsAvailable] = useState(true);
  const [error, setError] = useState("");

  return (
    <>
      <LocationAvailabilityDisplay
        locationId={locationId}
        onAvailabilityChange={(available) => {
          setIsAvailable(available);
          if (!available) {
            setError(
              "Location is currently closed. Please try again during business hours."
            );
          }
        }}
      />

      {error && <ErrorAlert message={error} />}
    </>
  );
}
```

### Visual Output

#### When Location is OPEN

```
┌─────────────────────────────────────┐
│ ✅ Downtown Cafe                    │
│ 🟢 Open Now                         │
│ Hours: 09:00 - 17:00                │
└─────────────────────────────────────┘
```

#### When Location is CLOSED

```
┌─────────────────────────────────────┐
│ ❌ Downtown Cafe                    │
│ 🔴 Currently Closed                 │
│ Next available: Monday at 09:00     │
└─────────────────────────────────────┘
```

### Internal State

The component manages:

- `availability` - Current availability data
- `loading` - Loading state during initial fetch

### Auto-refresh Behavior

```typescript
useEffect(() => {
  // Fetch immediately
  fetchAvailability();

  // Then every 60 seconds
  const interval = setInterval(fetchAvailability, 60000);

  return () => clearInterval(interval);
}, [locationId, onAvailabilityChange]);
```

### Styling

- **Open Status**:

  - Background: `bg-green-50`
  - Border: `border-green-200`
  - Text: `text-green-800`
  - Icon: Green checkmark

- **Closed Status**:
  - Background: `bg-red-50`
  - Border: `border-red-200`
  - Text: `text-red-800`
  - Icon: Red error icon

---

## LocationHoursManager Component

### Purpose

Admin interface for managing location working hours and holidays.

### File Location

`src/components/admin/LocationHoursManager.tsx`

### Props

```typescript
interface LocationHoursManagerProps {
  locationId: string; // MongoDB location ID (required)
  initialHours?: WorkingHours[]; // Pre-fill schedule
  initialHolidays?: Holiday[]; // Pre-fill holidays
  onSave?: () => void; // Callback after successful save
}
```

### Type Definitions

```typescript
interface WorkingHours {
  day: string; // "Monday", "Tuesday", etc
  open: string; // "09:00" (HH:mm format)
  close: string; // "17:00" (HH:mm format)
}

interface Holiday {
  date: string; // "2024-12-25" (YYYY-MM-DD)
  name: string; // "Christmas"
  isClosed: boolean; // true = closed all day
  customHours?: {
    open: string; // "09:00" if not closed
    close: string; // "17:00" if not closed
  };
}
```

### Features

✅ **Schedule editor** - Visual 7-day schedule with time pickers
✅ **Holiday management** - Add/edit/delete holidays
✅ **Validation** - Validates times and required fields
✅ **Error handling** - Toast notifications for errors
✅ **Admin-only** - Requires admin authentication
✅ **Real-time save** - Updates database immediately

### Usage Example

#### In Location Edit Page

```typescript
import { LocationHoursManager } from "@/components/admin/LocationHoursManager";
import { useState, useEffect } from "react";

export function LocationEditPage({ locationId }: { locationId: string }) {
  const [location, setLocation] = useState<ILocation | null>(null);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    // Fetch location data
    fetchLocation(locationId)
      .then(setLocation)
      .finally(() => setLoading(false));
  }, [locationId]);

  if (loading) return <Spinner />;

  return (
    <div className="space-y-6">
      <h1>Edit Location: {location?.name}</h1>

      {/* Existing form fields... */}

      {/* Hours Manager Section */}
      <div className="bg-white p-6 rounded-lg">
        <h2 className="text-xl font-bold mb-4">Hours & Holidays</h2>
        <LocationHoursManager
          locationId={locationId}
          initialHours={location?.workingHours}
          initialHolidays={location?.holidays}
          onSave={() => {
            showToast("Hours updated successfully", "success");
            // Optionally refresh location data
            refetchLocation(locationId);
          }}
        />
      </div>
    </div>
  );
}
```

#### As Tab in Admin Dashboard

```typescript
export function LocationTabs() {
  const [activeTab, setActiveTab] = useState<"info" | "hours" | "menu">("info");

  return (
    <>
      {/* Tab buttons */}
      <div className="flex gap-4 mb-6">
        <button
          onClick={() => setActiveTab("info")}
          className={activeTab === "info" ? "active" : ""}
        >
          Basic Info
        </button>
        <button
          onClick={() => setActiveTab("hours")}
          className={activeTab === "hours" ? "active" : ""}
        >
          Hours & Holidays
        </button>
        <button
          onClick={() => setActiveTab("menu")}
          className={activeTab === "menu" ? "active" : ""}
        >
          Menu Items
        </button>
      </div>

      {/* Tab content */}
      {activeTab === "info" && <LocationInfoForm locationId={locationId} />}
      {activeTab === "hours" && (
        <LocationHoursManager
          locationId={locationId}
          initialHours={location.workingHours}
          initialHolidays={location.holidays}
        />
      )}
      {activeTab === "menu" && <LocationMenuManager locationId={locationId} />}
    </>
  );
}
```

### Component Structure

The component consists of three sections:

#### 1. Working Hours Section

```
┌─────────────────────────────────────┐
│ Working Hours                       │
├─────────────────────────────────────┤
│ Monday     [09:00] to [17:00]       │
│ Tuesday    [09:00] to [17:00]       │
│ Wednesday  [09:00] to [17:00]       │
│ Thursday   [09:00] to [17:00]       │
│ Friday     [09:00] to [20:00]       │
│ Saturday   [10:00] to [20:00]       │
│ Sunday     [10:00] to [18:00]       │
└─────────────────────────────────────┘
```

#### 2. Holiday Management Section

```
┌─────────────────────────────────────┐
│ Add Holiday                         │
├─────────────────────────────────────┤
│ Date: [2024-12-25]                  │
│ Name: [Christmas]                   │
│ [✓] Closed all day                  │
│ [+ Add Holiday]                     │
└─────────────────────────────────────┘

Existing Holidays:
┌─────────────────────────────────────┐
│ Christmas              [🗑️ Delete]  │
│ 2024-12-25 - Closed                 │
└─────────────────────────────────────┘
```

#### 3. Save Button

```
┌─────────────────────────────────────┐
│ [Save Hours & Holidays]             │
└─────────────────────────────────────┘
```

### State Management

```typescript
const [hours, setHours] = useState<WorkingHours[]>(initialHours);
const [holidays, setHolidays] = useState<Holiday[]>(initialHolidays);
const [newHolidayDate, setNewHolidayDate] = useState("");
const [newHolidayName, setNewHolidayName] = useState("");
const [newHolidayIsClosed, setNewHolidayIsClosed] = useState(true);
const [loading, setLoading] = useState(false);
```

### Event Handlers

#### Handle Hour Change

```typescript
const handleHourChange = (
  dayIndex: number,
  field: "open" | "close",
  value: string
) => {
  const newHours = [...hours];
  newHours[dayIndex][field] = value;
  setHours(newHours);
};

// Usage
handleHourChange(0, "open", "08:00"); // Monday open
handleHourChange(0, "close", "18:00"); // Monday close
```

#### Handle Add Holiday

```typescript
const handleAddHoliday = () => {
  // Validates fields
  // Creates Holiday object
  // Adds to holidays array
  // Clears form
};
```

#### Handle Delete Holiday

```typescript
const handleDeleteHoliday = (index: number) => {
  setHolidays(holidays.filter((_, i) => i !== index));
};
```

#### Handle Save

```typescript
const handleSave = async () => {
  // 1. Set loading state
  // 2. Call PUT /api/locations/[id]/hours
  // 3. Show success/error toast
  // 4. Call onSave callback if successful
  // 5. Clear loading state
};
```

### Validation

The component validates:

- Holiday date is not empty
- Holiday name is not empty
- Time format (handled by HTML5 input type="time")
- API response status

### Toast Notifications

Displays toast messages for:

- ✅ "Hours updated successfully" (success)
- ❌ "Please fill in all fields" (error)
- ❌ "Failed to update hours" (error)

### API Integration

#### Save Hours

```typescript
const response = await fetch(`/api/locations/${locationId}/hours`, {
  method: "PUT",
  headers: { "Content-Type": "application/json" },
  body: JSON.stringify({
    workingHours: hours,
    holidays: holidays,
  }),
});
```

---

## Component Composition Example

### Complete Checkout Page with Availability

```typescript
"use client";

import { LocationAvailabilityDisplay } from "@/components/LocationAvailabilityDisplay";
import { useState } from "react";

export default function CheckoutPage() {
  const [locationAvailable, setLocationAvailable] = useState(true);
  const [error, setError] = useState("");

  const handleCheckout = async () => {
    if (!locationAvailable) {
      setError("Location is currently closed");
      return;
    }

    // Proceed with checkout...
    try {
      const response = await fetch("/api/orders", { method: "POST" /* ... */ });

      if (!response.ok) {
        const data = await response.json();
        if (data.isLocationClosed) {
          setError(`Order blocked: ${data.error}`);
          return;
        }
      }

      // Success
    } catch (err) {
      setError(err.message);
    }
  };

  return (
    <div className="max-w-7xl mx-auto p-4">
      <h1>Checkout</h1>

      {/* Location Availability */}
      <LocationAvailabilityDisplay
        locationId="507f1f77bcf86cd799439011"
        onAvailabilityChange={setLocationAvailable}
      />

      {/* Error Message */}
      {error && <div className="alert alert-error mt-4">{error}</div>}

      {/* Order Summary */}
      <div className="mt-6">
        <h2>Order Summary</h2>
        {/* ... */}
      </div>

      {/* Checkout Button */}
      <button
        onClick={handleCheckout}
        disabled={!locationAvailable}
        className="button button-primary mt-4"
      >
        {!locationAvailable ? "Location Closed" : "Place Order"}
      </button>
    </div>
  );
}
```

---

## Best Practices

### 1. Always Show Availability

```typescript
// ✅ Good - Always show availability
<LocationAvailabilityDisplay locationId={locationId} />;

// ❌ Bad - Hiding availability is confusing
{
  showDetails && <LocationAvailabilityDisplay locationId={locationId} />;
}
```

### 2. Disable Checkout When Closed

```typescript
// ✅ Good - Clear feedback
<button disabled={!locationAvailable}>
  {locationAvailable ? "Place Order" : "Location Closed"}
</button>

// ❌ Bad - User doesn't know why button is disabled
<button disabled={!locationAvailable}>Place Order</button>
```

### 3. Handle Location Change

```typescript
// ✅ Good - Re-fetch availability when location changes
<LocationAvailabilityDisplay
  key={locationId} // Force re-mount on location change
  locationId={locationId}
/>;

// ✅ Also good - useEffect dependency
useEffect(() => {
  fetchAvailability();
}, [locationId]);
```

### 4. Integrate Hours Manager in Edit Page

```typescript
// ✅ Good - In its own section
<div className="border-t pt-6 mt-6">
  <h2>Hours & Holidays</h2>
  <LocationHoursManager {...props} />
</div>;

// ✅ Also good - In a tab
{
  activeTab === "hours" && <LocationHoursManager {...props} />;
}
```

---

## Troubleshooting

### Component Not Showing

**Problem**: LocationAvailabilityDisplay shows nothing

```typescript
// ❌ Issue: No location ID
<LocationAvailabilityDisplay locationId="" />

// ✅ Fix: Provide valid location ID
<LocationAvailabilityDisplay locationId={location._id} />
```

### Availability Not Updating

**Problem**: Checkout button stays disabled after availability changes

```typescript
// ❌ Issue: State not updated
const [available, setAvailable] = useState(true);
// Component never calls setAvailable

// ✅ Fix: Pass callback
<LocationAvailabilityDisplay
  locationId={id}
  onAvailabilityChange={setAvailable} // Pass callback
/>;
```

### Hours Not Saving

**Problem**: LocationHoursManager save fails silently

```typescript
// ❌ Issue: No error handling
const response = await fetch(...);
// No error checking

// ✅ Fix: Handle errors
const response = await fetch(...);
if (!response.ok) {
  const error = await response.json();
  showToast(error.error || "Failed to save", "error");
}
```

---

## API Reference

See `LOCATION_HOURS_API_DOCS.md` for complete API documentation.
