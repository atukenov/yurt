import { connectDB } from "@/lib/mongodb";
import { Location } from "@/models/Location";
import { MenuItem } from "@/models/MenuItem";
import { Topping } from "@/models/Topping";
import { User } from "@/models/User";
import { coffeeShopData } from "./_data/coffeeShop";

const deleteData = async () => {
  const start = performance.now();
  const models = [
    { model: MenuItem, name: "MenuItems" },
    { model: Topping, name: "Toppings" },
    { model: Location, name: "Locations" },
    { model: User, name: "Users" },
  ];

  const results = await Promise.all(
    models.map(async ({ model, name }) => {
      const res = await model.deleteMany({});
      return { model: name, ...res };
    })
  );

  return {
    processTime: (performance.now() - start) / 1000,
    results,
  };
};

const createData = async () => {
  const { users, menuItems, toppings, locations } = coffeeShopData;
  const start = performance.now();

  const newUsers = await Promise.all(users.map((u) => new User(u).save()));
  const newMenuItems = await Promise.all(
    menuItems.map((m) => new MenuItem(m).save())
  );
  const newToppings = await Promise.all(
    toppings.map((t) => new Topping(t).save())
  );
  const newLocations = await Promise.all(
    locations.map((l) => new Location(l).save())
  );

  return {
    processTime: (performance.now() - start) / 1000,
    users: newUsers,
    menuItems: newMenuItems,
    toppings: newToppings,
    locations: newLocations,
  };
};

export async function GET() {
  await connectDB();
  try {
    const start = performance.now();
    const deleteResult = await deleteData();
    const createResult = await createData();

    const res = {
      totalProcessTime: (performance.now() - start) / 1000,
      delete: deleteResult,
      created: createResult,
    };
    return new Response(JSON.stringify(res, null, 4));
  } catch (err) {
    console.error("Demo data refresh error:", err);
    return new Response(JSON.stringify({ error: String(err) }, null, 4), {
      status: 500,
    });
  }
}
