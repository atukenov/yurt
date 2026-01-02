# Location Availability API Documentation

## Overview

Complete API documentation for the location hours and availability system.

---

## Endpoints

### 1. Check Location Availability

**Endpoint**: `GET /api/locations/[id]/availability`

**Description**: Check if a location is currently open and get availability details.

**Parameters**:

- `id` (path parameter) - MongoDB location ID

**Response** (200 OK):

```typescript
{
  locationId: string;           // MongoDB ID
  name: string;                 // Location name
  isOpen: boolean;              // Currently open?
  openTime?: string;            // Today's open time (HH:mm)
  closeTime?: string;           // Today's close time (HH:mm)
  reason?: string;              // "Open" or reason for closure
  hours: {                       // Today's hours
    open: string;               // HH:mm format
    close: string;              // HH:mm format
  };
  nextAvailable: {
    time: string;               // HH:mm format
    date: string;               // YYYY-MM-DD format
    day: string;                // Day name (Monday, etc)
  } | null;                     // null if open, populated if closed
}
```

**Example Request**:

```bash
curl http://localhost:3001/api/locations/507f1f77bcf86cd799439011/availability
```

**Example Response (Open)**:

```json
{
  "locationId": "507f1f77bcf86cd799439011",
  "name": "Downtown Cafe",
  "isOpen": true,
  "openTime": "09:00",
  "closeTime": "17:00",
  "reason": "Open",
  "hours": {
    "open": "09:00",
    "close": "17:00"
  },
  "nextAvailable": null
}
```

**Example Response (Closed)**:

```json
{
  "locationId": "507f1f77bcf86cd799439011",
  "name": "Downtown Cafe",
  "isOpen": false,
  "openTime": "09:00",
  "closeTime": "17:00",
  "reason": "Location closed on Sunday",
  "hours": {
    "open": "09:00",
    "close": "17:00"
  },
  "nextAvailable": {
    "time": "09:00",
    "date": "2024-01-15",
    "day": "Monday"
  }
}
```

**Error Response** (404):

```json
{
  "error": "Location not found"
}
```

**Usage in Frontend**:

```typescript
// Check if location is open
const res = await fetch(`/api/locations/${locationId}/availability`);
const availability = await res.json();

if (!availability.isOpen) {
  console.log(`Location closed: ${availability.reason}`);
  if (availability.nextAvailable) {
    console.log(
      `Opens ${availability.nextAvailable.day} at ${availability.nextAvailable.time}`
    );
  }
}
```

---

### 2. Update Location Hours (Admin)

**Endpoint**: `PUT /api/locations/[id]/hours`

**Description**: Update location working hours and holidays. Admin-only endpoint.

**Authentication**: Required (admin role)

**Parameters**:

- `id` (path parameter) - MongoDB location ID

**Request Body**:

```typescript
{
  workingHours?: [
    {
      day: string;              // "Monday", "Tuesday", etc
      open: string;             // HH:mm format (e.g., "09:00")
      close: string;            // HH:mm format (e.g., "17:00")
    }
  ];
  holidays?: [
    {
      date: string;             // YYYY-MM-DD format
      name: string;             // Holiday name (e.g., "Christmas")
      isClosed: boolean;        // true = closed all day
      customHours?: {
        open: string;           // HH:mm if not closed
        close: string;          // HH:mm if not closed
      };
    }
  ];
}
```

**Response** (200 OK):

```json
{
  "message": "Location hours updated successfully",
  "location": {
    "_id": "507f1f77bcf86cd799439011",
    "name": "Downtown Cafe",
    "workingHours": [...],
    "holidays": [...]
  }
}
```

**Example Request**:

```bash
curl -X PUT http://localhost:3001/api/locations/507f1f77bcf86cd799439011/hours \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer <token>" \
  -d '{
    "workingHours": [
      {"day": "Monday", "open": "08:00", "close": "18:00"},
      {"day": "Tuesday", "open": "08:00", "close": "18:00"},
      {"day": "Wednesday", "open": "08:00", "close": "18:00"},
      {"day": "Thursday", "open": "08:00", "close": "18:00"},
      {"day": "Friday", "open": "08:00", "close": "20:00"},
      {"day": "Saturday", "open": "10:00", "close": "20:00"},
      {"day": "Sunday", "open": "10:00", "close": "18:00"}
    ],
    "holidays": [
      {
        "date": "2024-12-25",
        "name": "Christmas",
        "isClosed": true
      },
      {
        "date": "2024-11-28",
        "name": "Thanksgiving",
        "isClosed": false,
        "customHours": {
          "open": "09:00",
          "close": "16:00"
        }
      }
    ]
  }'
```

**Error Response** (401):

```json
{
  "error": "Unauthorized"
}
```

**Error Response** (404):

```json
{
  "error": "Location not found"
}
```

**Error Response** (400):

```json
{
  "error": "Invalid request body"
}
```

**Usage in Admin Component**:

```typescript
async function saveLocationHours(
  locationId: string,
  hours: WorkingHours[],
  holidays: Holiday[]
) {
  const response = await fetch(`/api/locations/${locationId}/hours`, {
    method: "PUT",
    headers: {
      "Content-Type": "application/json",
      // Token auto-included by next-auth
    },
    body: JSON.stringify({
      workingHours: hours,
      holidays: holidays,
    }),
  });

  if (!response.ok) {
    const error = await response.json();
    throw new Error(error.error);
  }

  return await response.json();
}
```

---

## Location Model Schema

```typescript
interface ILocation {
  _id: ObjectId;
  name: string;
  address: string;
  city: string;
  zipCode: string;
  phone: string;
  coordinates?: {
    latitude: number;
    longitude: number;
  };
  isActive: boolean;
  workingHours: {
    [day: string]: {
      open: string; // HH:mm
      close: string; // HH:mm
    };
  };
  holidays?: [
    {
      date: Date;
      name: string;
      isClosed: boolean;
      customHours?: {
        open: string; // HH:mm
        close: string; // HH:mm
      };
    }
  ];
  createdAt: Date;
  updatedAt: Date;
}
```

---

## Availability Logic

### When is a location considered OPEN?

1. **Location must be active**: `isActive === true`
2. **Check holidays first**:
   - If today is a holiday with `isClosed: true` → CLOSED
   - If today is a holiday with `isClosed: false` and `customHours` → use custom hours
3. **Fall back to regular hours**:
   - Get hours for current day of week
   - Check if current time is between open and close
4. **Return status**:
   - `isOpen: true` if within operating hours
   - `isOpen: false` if outside operating hours
   - `nextAvailable` calculated from next 7 days

### Reason Messages

- `"Open"` - Location is currently open
- `"Location closed on [Day]"` - Location doesn't operate on this day
- `"Location closed for [Holiday]"` - Holiday closure
- `"Location opens [Day] at [Time]"` - Closed now, next opening provided
- `"Location is inactive"` - Location marked as inactive

---

## Integration Examples

### Frontend: Checkout Component

```typescript
import { LocationAvailabilityDisplay } from "@/components/LocationAvailabilityDisplay";

export function CheckoutPage() {
  const [locationAvailable, setLocationAvailable] = useState(true);

  return (
    <>
      <LocationAvailabilityDisplay
        locationId={locationId}
        onAvailabilityChange={setLocationAvailable}
      />

      <button disabled={!locationAvailable} onClick={handleCheckout}>
        Place Order
      </button>
    </>
  );
}
```

### Backend: Order Creation

```typescript
import { isLocationOpen } from "@/lib/locationAvailability";
import { Location } from "@/models/Location";

export async function POST(request: NextRequest) {
  const location = await Location.findById(locationId);
  const availability = isLocationOpen(location);

  if (!availability.isOpen) {
    return Response.json(
      {
        error: availability.reason,
        isLocationClosed: true,
      },
      { status: 400 }
    );
  }

  // Create order...
}
```

### Admin: Update Hours

```typescript
import { LocationHoursManager } from "@/components/admin/LocationHoursManager";

export function LocationEditPage() {
  return (
    <LocationHoursManager
      locationId={locationId}
      initialHours={location.workingHours}
      initialHolidays={location.holidays}
      onSave={() => {
        // Refresh location data
        refetchLocation();
      }}
    />
  );
}
```

---

## Error Handling

### Common HTTP Status Codes

| Code | Meaning      | Example                               |
| ---- | ------------ | ------------------------------------- |
| 200  | Success      | Location found, availability returned |
| 400  | Bad Request  | Invalid time format, missing fields   |
| 401  | Unauthorized | Not authenticated, not admin for PUT  |
| 404  | Not Found    | Location ID doesn't exist             |
| 500  | Server Error | Database error, server issue          |

### Frontend Error Handling

```typescript
try {
  const res = await fetch(`/api/locations/${id}/availability`);

  if (!res.ok) {
    if (res.status === 404) {
      console.error("Location not found");
    } else if (res.status === 500) {
      console.error("Server error, please try again later");
    }
    throw new Error(`HTTP error! status: ${res.status}`);
  }

  const data = await res.json();
  return data;
} catch (error) {
  console.error("Failed to fetch availability:", error);
  // Show fallback UI or error message
}
```

---

## Testing with cURL

### Test if location is open

```bash
# Set location ID
LOCATION_ID="507f1f77bcf86cd799439011"

# Check availability
curl http://localhost:3001/api/locations/$LOCATION_ID/availability

# Pretty print JSON
curl http://localhost:3001/api/locations/$LOCATION_ID/availability | jq .
```

### Test updating hours

```bash
# Requires authentication token
curl -X PUT http://localhost:3001/api/locations/$LOCATION_ID/hours \
  -H "Content-Type: application/json" \
  -d '{
    "workingHours": [
      {"day": "Monday", "open": "09:00", "close": "17:00"}
    ]
  }'
```

---

## Rate Limiting

- No rate limiting currently implemented
- For production, consider adding rate limiting for availability checks
- Recommended: 100 requests per minute per IP

---

## Future Enhancements

- [ ] Timezone support for locations in different zones
- [ ] Bulk operations for updating multiple locations
- [ ] Recurring special hours patterns
- [ ] Break times during the day
- [ ] Partial hour closures
- [ ] Location capacity management
- [ ] Availability analytics

---

## Support

For issues or questions:

1. Check `LOCATION_HOURS_IMPLEMENTATION.md` for technical details
2. Review `src/lib/locationAvailability.ts` for logic
3. Check component source code for implementation examples
