import { connectDB } from "@/lib/mongodb";
import { Location } from "@/models/Location";

async function fixLocations() {
  try {
    await connectDB();

    // Update all locations to have isActive: true if not already set
    const result = await Location.updateMany(
      { isActive: { $exists: false } },
      { $set: { isActive: true } }
    );

    console.log(`Updated ${result.modifiedCount} locations to isActive: true`);

    // Also set any null isActive values to true
    const result2 = await Location.updateMany(
      { isActive: null },
      { $set: { isActive: true } }
    );

    console.log(`Updated ${result2.modifiedCount} null isActive to true`);

    // Get all locations
    const locations = await Location.find();
    console.log(`Total locations in DB: ${locations.length}`);
    console.log(
      "Locations:",
      locations.map((l: any) => ({ name: l.name, isActive: l.isActive }))
    );
  } catch (error) {
    console.error("Error fixing locations:", error);
    process.exit(1);
  }
}

fixLocations();
