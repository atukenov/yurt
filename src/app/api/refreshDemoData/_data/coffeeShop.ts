import bcrypt from "bcryptjs";

const adminPassword = bcrypt.hashSync("admin123", 10);
const customerPassword = bcrypt.hashSync("customer123", 10);

export const coffeeShopData = {
  users: [
    {
      email: "admin@coffeeshop.com",
      password: adminPassword,
      name: "Admin User",
      phone: "+1-555-0100",
      role: "admin",
      image: "https://i.pravatar.cc/150?img=1",
    },
    {
      email: "john@example.com",
      password: customerPassword,
      name: "John Doe",
      phone: "+1-555-0101",
      role: "customer",
      image: "https://i.pravatar.cc/150?img=2",
    },
    {
      email: "sarah@example.com",
      password: customerPassword,
      name: "Sarah Johnson",
      phone: "+1-555-0102",
      role: "customer",
      image: "https://i.pravatar.cc/150?img=3",
    },
  ],
  menuItems: [
    // Hot Drinks
    {
      name: "Espresso | Эспрессо",
      description:
        "Strong, concentrated coffee | Крепкий, концентрированный кофе",
      category: "espresso",
      basePrice: 1575,
      sizes: [
        { size: "small", priceModifier: 0 },
        { size: "medium", priceModifier: 225 },
        { size: "large", priceModifier: 450 },
      ],
      preparationTime: 3,
      isAvailable: true,
      image:
        "https://b.zmtcdn.com/data/dish_photos/859/8ab5b36ad575355648e552f578675859.jpg",
    },
    {
      name: "Americano | Американо",
      description: "Espresso with hot water | Эспрессо с горячей водой",
      category: "hot",
      basePrice: 1688,
      sizes: [
        { size: "small", priceModifier: 0 },
        { size: "medium", priceModifier: 225 },
        { size: "large", priceModifier: 450 },
      ],
      preparationTime: 4,
      isAvailable: true,
      image:
        "https://b.zmtcdn.com/data/dish_photos/859/8ab5b36ad575355648e552f578675859.jpg",
    },
    {
      name: "Latte | Латте",
      description: "Espresso with steamed milk | Эспрессо с горячим молоком",
      category: "latte",
      basePrice: 2025,
      sizes: [
        { size: "small", priceModifier: 0 },
        { size: "medium", priceModifier: 338 },
        { size: "large", priceModifier: 675 },
      ],
      preparationTime: 5,
      isAvailable: true,
      image:
        "https://b.zmtcdn.com/data/dish_photos/46f/879e3f36c309519bd089ce9a8d13746f.jpg",
    },
    {
      name: "Cappuccino | Капучино",
      description:
        "Espresso, steamed milk, foam | Эспрессо, горячее молоко, пена",
      category: "cappuccino",
      basePrice: 2025,
      sizes: [
        { size: "small", priceModifier: 0 },
        { size: "medium", priceModifier: 338 },
        { size: "large", priceModifier: 675 },
      ],
      preparationTime: 5,
      isAvailable: true,
      image:
        "https://b.zmtcdn.com/data/dish_photos/468/beafebeb370a02fdc9fdd1dc4ac45468.jpg",
    },
    {
      name: "Mocha | Мокко",
      description:
        "Espresso, steamed milk, chocolate | Эспрессо, горячее молоко, шоколад",
      category: "specialty",
      basePrice: 2250,
      sizes: [
        { size: "small", priceModifier: 0 },
        { size: "medium", priceModifier: 338 },
        { size: "large", priceModifier: 675 },
      ],
      preparationTime: 6,
      isAvailable: true,
      image:
        "https://b.zmtcdn.com/data/dish_photos/784/68079cc30be931ca6230c652b55ab784.jpg",
    },
    // Cold Drinks
    {
      name: "Iced Coffee | Холодный кофе",
      description: "Cold brewed coffee over ice | Холодный кофе со льдом",
      category: "cold",
      basePrice: 1575,
      sizes: [
        { size: "small", priceModifier: 0 },
        { size: "medium", priceModifier: 225 },
        { size: "large", priceModifier: 450 },
      ],
      preparationTime: 3,
      isAvailable: true,
      image:
        "https://b.zmtcdn.com/data/dish_photos/15f/f43e4676fbf1e1519a18d93e3340e15f.jpg",
    },
    {
      name: "Iced Latte | Холодный латте",
      description: "Cold espresso with milk | Холодный эспрессо с молоком",
      category: "cold",
      basePrice: 1913,
      sizes: [
        { size: "small", priceModifier: 0 },
        { size: "medium", priceModifier: 225 },
        { size: "large", priceModifier: 450 },
      ],
      preparationTime: 4,
      isAvailable: true,
      image:
        "https://b.zmtcdn.com/data/dish_photos/057/32632f33976aede26beb407156b51057.jpg",
    },
    {
      name: "Cold Brew | Холодная заварка",
      description:
        "Smooth, less acidic cold coffee | Гладкий, менее кислый холодный кофе",
      category: "cold",
      basePrice: 1800,
      sizes: [
        { size: "small", priceModifier: 0 },
        { size: "medium", priceModifier: 225 },
        { size: "large", priceModifier: 450 },
      ],
      preparationTime: 2,
      isAvailable: true,
      image:
        "https://b.zmtcdn.com/data/dish_photos/6da/f088c72f90fecc62b5e139d60d8306da.jpg",
    },
    {
      name: "Iced Cappuccino | Холодный капучино",
      description:
        "Cold espresso with milk and foam | Холодный эспрессо с молоком и пеной",
      category: "cold",
      basePrice: 2138,
      sizes: [
        { size: "small", priceModifier: 0 },
        { size: "medium", priceModifier: 338 },
        { size: "large", priceModifier: 675 },
      ],
      preparationTime: 5,
      isAvailable: true,
      image:
        "https://b.zmtcdn.com/data/dish_photos/059/8c022e30f7482347c6a7248c366d2059.jpg",
    },
  ],
  toppings: [
    // Syrups
    {
      name: "Vanilla Syrup | Ванильный сироп",
      price: 225,
      category: "syrup",
    },
    {
      name: "Caramel Syrup | Карамельный сироп",
      price: 225,
      category: "syrup",
    },
    {
      name: "Hazelnut Syrup | Ореховый сироп",
      price: 225,
      category: "syrup",
    },
    {
      name: "Almond Syrup | Миндальный сироп",
      price: 225,
      category: "syrup",
    },
    {
      name: "Coconut Syrup | Кокосовый сироп",
      price: 225,
      category: "syrup",
    },
    {
      name: "Chocolate Syrup | Шоколадный сироп",
      price: 225,
      category: "syrup",
    },
    {
      name: "Maple Syrup | Кленовый сироп",
      price: 225,
      category: "syrup",
    },
    // Shots
    {
      name: "Extra Shot | Экстра шот",
      price: 350,
      category: "shot",
    },
    {
      name: "Double Shot | Двойной шот",
      price: 450,
      category: "shot",
    },
    // Milk Options
    {
      name: "Oat Milk | Овсяное молоко",
      price: 350,
      category: "milk",
    },
    {
      name: "Almond Milk | Миндальное молоко",
      price: 350,
      category: "milk",
    },
    {
      name: "Soy Milk | Соевое молоко",
      price: 350,
      category: "milk",
    },
    {
      name: "Whole Milk | Цельное молоко",
      price: 225,
      category: "milk",
    },
    {
      name: "Heavy Cream | Жирные сливки",
      price: 350,
      category: "milk",
    },
    // Toppings
    {
      name: "Whipped Cream | Взбитые сливки",
      price: 350,
      category: "topping",
    },
    {
      name: "Cinnamon | Корица",
      price: 100,
      category: "topping",
    },
    {
      name: "Honey | Мед",
      price: 225,
      category: "topping",
    },
    {
      name: "Chocolate Powder | Какао порошок",
      price: 160,
      category: "topping",
    },
    {
      name: "Caramel Drizzle | Карамельная глазурь",
      price: 225,
      category: "topping",
    },
    {
      name: "Chocolate Drizzle | Шоколадная глазурь",
      price: 225,
      category: "topping",
    },
  ],
  locations: [
    {
      name: "Downtown Coffee Shop",
      address: "123 Main Street",
      city: "New York",
      zipCode: "10001",
      phone: "+1-555-0201",
      latitude: 40.7128,
      longitude: -74.006,
      workingHours: {
        monday: { open: "06:00", close: "22:00" },
        tuesday: { open: "06:00", close: "22:00" },
        wednesday: { open: "06:00", close: "22:00" },
        thursday: { open: "06:00", close: "22:00" },
        friday: { open: "06:00", close: "23:00" },
        saturday: { open: "07:00", close: "23:00" },
        sunday: { open: "07:00", close: "21:00" },
      },
      isActive: true,
    },
    {
      name: "Uptown Brew",
      address: "456 Park Avenue",
      city: "New York",
      zipCode: "10022",
      phone: "+1-555-0202",
      latitude: 40.773,
      longitude: -73.9566,
      workingHours: {
        monday: { open: "06:30", close: "20:00" },
        tuesday: { open: "06:30", close: "20:00" },
        wednesday: { open: "06:30", close: "20:00" },
        thursday: { open: "06:30", close: "20:00" },
        friday: { open: "06:30", close: "21:00" },
        saturday: { open: "08:00", close: "21:00" },
        sunday: { open: "08:00", close: "20:00" },
      },
      isActive: true,
    },
    {
      name: "Brooklyn Roastery",
      address: "789 Williamsburg Street",
      city: "Brooklyn",
      zipCode: "11211",
      phone: "+1-555-0203",
      latitude: 40.7081,
      longitude: -73.9565,
      workingHours: {
        monday: { open: "07:00", close: "19:00" },
        tuesday: { open: "07:00", close: "19:00" },
        wednesday: { open: "07:00", close: "19:00" },
        thursday: { open: "07:00", close: "19:00" },
        friday: { open: "07:00", close: "20:00" },
        saturday: { open: "08:00", close: "20:00" },
        sunday: { open: "08:00", close: "18:00" },
      },
      isActive: true,
    },
  ],
};
