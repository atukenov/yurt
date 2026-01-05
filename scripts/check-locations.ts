import { connectDB } from "@/lib/mongodb";
import { Location } from "@/models/Location";

async function checkLocations() {
  try {
    await connectDB();

    // Get all locations
    const allLocations = await Location.find();
    console.log("\n=== ALL LOCATIONS ===");
    console.log(`Total: ${allLocations.length}`);
    allLocations.forEach((loc: any) => {
      console.log(`- ${loc.name} (isActive: ${loc.isActive ?? "undefined"})`);
    });

    // Get active locations (strict)
    const activeStrict = await Location.find({ isActive: true });
    console.log(`\n=== ACTIVE (isActive: true) ===`);
    console.log(`Count: ${activeStrict.length}`);

    // Get active locations (loose)
    const activeLoose = await Location.find({
      $or: [{ isActive: true }, { isActive: { $exists: false } }],
    });
    console.log(`\n=== ACTIVE (true or undefined) ===`);
    console.log(`Count: ${activeLoose.length}`);
    activeLoose.forEach((loc: any) => {
      console.log(`- ${loc.name}`);
    });

    // Get inactive locations
    const inactive = await Location.find({ isActive: false });
    console.log(`\n=== INACTIVE (isActive: false) ===`);
    console.log(`Count: ${inactive.length}`);
    inactive.forEach((loc: any) => {
      console.log(`- ${loc.name}`);
    });

    process.exit(0);
  } catch (error) {
    console.error("Error:", error);
    process.exit(1);
  }
}

checkLocations();
