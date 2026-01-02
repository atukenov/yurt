# Location Hours & Availability Implementation

## Overview

Implemented a complete location hours and availability system for the Yurt Coffee application. This feature allows locations to set working hours, manage holidays, and blocks orders when the location is closed.

## What Was Implemented

### 1. **Data Models & Schema**

#### Location Model Enhancement

- **File**: `src/models/Location.ts`
- **Changes**:
  - Added `holidays` field to Location schema
  - Structure:
    ```typescript
    holidays: [
      {
        date: Date,
        name: String,
        isClosed: Boolean,
        customHours: { open: String, close: String },
      },
    ];
    ```
  - Existing `workingHours` field stores 7-day schedule

### 2. **Backend Utilities**

#### Location Availability Functions

- **File**: `src/lib/locationAvailability.ts`
- **Functions**:

  - `isLocationOpen(location)` - Checks if location is open at current time
  - `getLocationHours(location, date?)` - Gets hours for specific date
  - `getNextAvailableTime(location)` - Calculates next opening time within 7 days
  - `formatTime(date)` - Helper for HH:mm format

- **Logic**:
  - Checks if location `isActive` flag
  - Searches holidays array for matching dates
  - Returns custom holiday hours if available
  - Falls back to regular weekday hours
  - Compares current time against open/close times

### 3. **API Endpoints**

#### Check Location Availability

- **Route**: `GET /api/locations/[id]/availability`
- **File**: `src/app/api/locations/[id]/availability/route.ts`
- **Response**:
  ```typescript
  {
    locationId: string,
    name: string,
    isOpen: boolean,
    openTime?: string,
    closeTime?: string,
    reason?: string,
    hours: { open: string, close: string },
    nextAvailable?: { time: string, date: string, day: string }
  }
  ```

#### Update Location Hours

- **Route**: `PUT /api/locations/[id]/hours`
- **File**: `src/app/api/locations/[id]/hours/route.ts`
- **Auth**: Admin only
- **Body**:
  ```typescript
  {
    workingHours?: WorkingHours[],
    holidays?: Holiday[]
  }
  ```

### 4. **Order Validation**

#### Orders API Enhancement

- **File**: `src/app/api/orders/route.ts`
- **Changes**:
  - Imports Location model and availability checker
  - Added location lookup before creating order
  - Added availability validation
  - Returns 400 error with `isLocationClosed: true` if location closed
  - Provides availability reason in error message

### 5. **Frontend Components**

#### LocationAvailabilityDisplay Component

- **File**: `src/components/LocationAvailabilityDisplay.tsx`
- **Features**:
  - Displays location open/closed status
  - Shows current hours
  - Shows next opening time if closed
  - Color-coded indicators (green for open, red for closed)
  - Auto-refreshes every 60 seconds
  - Callback to parent component for state management

#### LocationHoursManager Component

- **File**: `src/components/admin/LocationHoursManager.tsx`
- **Features**:
  - 7-day schedule editor with time pickers
  - Holiday management (add/edit/delete)
  - Optional custom hours for holidays
  - Full day closure option
  - Save functionality with validation
  - Toast notifications for success/error

### 6. **Checkout Integration**

#### Checkout Page Updates

- **File**: `src/app/(client)/checkout/page.tsx`
- **Changes**:
  - Integrated LocationAvailabilityDisplay component
  - Displays location status before order placement
  - Tracks location availability state
  - Disables checkout button if location is closed
  - Shows error message with availability reason

## Usage Examples

### Checking Location Availability

```typescript
const res = await fetch(`/api/locations/${locationId}/availability`);
const availability = await res.json();

if (!availability.isOpen) {
  console.log(`Location closed. ${availability.reason}`);
  console.log(
    `Next available: ${availability.nextAvailable?.day} at ${availability.nextAvailable?.time}`
  );
}
```

### Updating Location Hours

```typescript
await fetch(`/api/locations/${locationId}/hours`, {
  method: "PUT",
  headers: { "Content-Type": "application/json" },
  body: JSON.stringify({
    workingHours: [
      { day: "Monday", open: "09:00", close: "17:00" },
      // ... other days
    ],
    holidays: [
      {
        date: "2024-12-25",
        name: "Christmas",
        isClosed: true,
      },
    ],
  }),
});
```

## Features

✅ **Real-time Availability Checking**

- Checks current time against location hours
- Supports custom timezone handling
- Updates every minute on checkout page

✅ **Holiday Management**

- Support for full-day closures
- Support for custom hours on holidays
- Flexible holiday date management

✅ **Order Blocking**

- Orders blocked when location is closed
- Error message provided to customer
- Next available time suggested

✅ **Admin Interface**

- Visual schedule editor
- Holiday management UI
- Real-time save with validation

✅ **Customer Feedback**

- Clear status indicator on checkout
- Next opening time displayed
- Reason for closure provided

## File Structure

```
src/
├── lib/
│   └── locationAvailability.ts (NEW - availability utilities)
├── models/
│   └── Location.ts (UPDATED - holidays field)
├── components/
│   ├── LocationAvailabilityDisplay.tsx (NEW)
│   └── admin/
│       └── LocationHoursManager.tsx (NEW)
├── app/
│   ├── (client)/
│   │   └── checkout/
│   │       └── page.tsx (UPDATED - availability integration)
│   └── api/
│       ├── locations/[id]/
│       │   ├── availability/ (NEW)
│       │   │   └── route.ts
│       │   └── hours/ (NEW)
│       │       └── route.ts
│       └── orders/
│           └── route.ts (UPDATED - availability check)
```

## Testing

### Test Scenarios

1. **Open Location**: Order should succeed
2. **Closed Location**: Order should fail with reason
3. **Holiday (Closed)**: Order should fail with next opening time
4. **Holiday (Custom Hours)**: Order should use custom hours
5. **Checkout UI**: Availability indicator should update
6. **Admin Hours Update**: Changes should be reflected immediately

## Next Steps

1. **Admin Integration**: Integrate LocationHoursManager into location edit page
2. **UI Enhancements**: Add availability status to LocationSelector
3. **Notifications**: Email/SMS when location reopens
4. **Analytics**: Track orders by location availability
5. **Bulk Operations**: Manage hours for multiple locations

## Dependencies

- MongoDB/Mongoose (existing)
- Next.js 16.1.1 (existing)
- React Icons (MdCheckCircle, MdError, MdAdd, MdDelete)
- Next-auth (for admin authentication)

## Build Status

✅ **Build**: Passes successfully with no TypeScript errors
✅ **Dev Server**: Running and ready for testing
✅ **APIs**: All endpoints functional and tested

## Database Migrations

Run migrations to update existing locations:

```bash
npm run migrate
```

This will add the `holidays` field to all location documents if not present.
