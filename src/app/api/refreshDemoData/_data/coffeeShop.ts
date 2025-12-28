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
      name: "Espresso",
      description: "Strong, concentrated coffee",
      category: "espresso",
      basePrice: 3.5,
      sizes: [
        { size: "small", priceModifier: 0 },
        { size: "medium", priceModifier: 0.5 },
        { size: "large", priceModifier: 1 },
      ],
      preparationTime: 3,
      isAvailable: true,
      image:
        "https://b.zmtcdn.com/data/dish_photos/859/8ab5b36ad575355648e552f578675859.jpg",
    },
    {
      name: "Americano",
      description: "Espresso with hot water",
      category: "hot",
      basePrice: 3.75,
      sizes: [
        { size: "small", priceModifier: 0 },
        { size: "medium", priceModifier: 0.5 },
        { size: "large", priceModifier: 1 },
      ],
      preparationTime: 4,
      isAvailable: true,
      image:
        "https://b.zmtcdn.com/data/dish_photos/859/8ab5b36ad575355648e552f578675859.jpg",
    },
    {
      name: "Latte",
      description: "Espresso with steamed milk",
      category: "latte",
      basePrice: 4.5,
      sizes: [
        { size: "small", priceModifier: 0 },
        { size: "medium", priceModifier: 0.75 },
        { size: "large", priceModifier: 1.5 },
      ],
      preparationTime: 5,
      isAvailable: true,
      image:
        "https://b.zmtcdn.com/data/dish_photos/46f/879e3f36c309519bd089ce9a8d13746f.jpg",
    },
    {
      name: "Cappuccino",
      description: "Espresso, steamed milk, foam",
      category: "cappuccino",
      basePrice: 4.5,
      sizes: [
        { size: "small", priceModifier: 0 },
        { size: "medium", priceModifier: 0.75 },
        { size: "large", priceModifier: 1.5 },
      ],
      preparationTime: 5,
      isAvailable: true,
      image:
        "https://b.zmtcdn.com/data/dish_photos/468/beafebeb370a02fdc9fdd1dc4ac45468.jpg",
    },
    {
      name: "Mocha",
      description: "Espresso, steamed milk, chocolate",
      category: "specialty",
      basePrice: 5.0,
      sizes: [
        { size: "small", priceModifier: 0 },
        { size: "medium", priceModifier: 0.75 },
        { size: "large", priceModifier: 1.5 },
      ],
      preparationTime: 6,
      isAvailable: true,
      image:
        "https://b.zmtcdn.com/data/dish_photos/784/68079cc30be931ca6230c652b55ab784.jpg",
    },
    // Cold Drinks
    {
      name: "Iced Coffee",
      description: "Cold brewed coffee over ice",
      category: "cold",
      basePrice: 3.5,
      sizes: [
        { size: "small", priceModifier: 0 },
        { size: "medium", priceModifier: 0.5 },
        { size: "large", priceModifier: 1 },
      ],
      preparationTime: 3,
      isAvailable: true,
      image:
        "https://b.zmtcdn.com/data/dish_photos/15f/f43e4676fbf1e1519a18d93e3340e15f.jpg",
    },
    {
      name: "Iced Latte",
      description: "Cold espresso with milk",
      category: "cold",
      basePrice: 4.25,
      sizes: [
        { size: "small", priceModifier: 0 },
        { size: "medium", priceModifier: 0.5 },
        { size: "large", priceModifier: 1 },
      ],
      preparationTime: 4,
      isAvailable: true,
      image:
        "https://b.zmtcdn.com/data/dish_photos/057/32632f33976aede26beb407156b51057.jpg",
    },
    {
      name: "Cold Brew",
      description: "Smooth, less acidic cold coffee",
      category: "cold",
      basePrice: 4.0,
      sizes: [
        { size: "small", priceModifier: 0 },
        { size: "medium", priceModifier: 0.5 },
        { size: "large", priceModifier: 1 },
      ],
      preparationTime: 2,
      isAvailable: true,
      image:
        "https://b.zmtcdn.com/data/dish_photos/6da/f088c72f90fecc62b5e139d60d8306da.jpg",
    },
    {
      name: "Iced Cappuccino",
      description: "Cold espresso with milk and foam",
      category: "cold",
      basePrice: 4.75,
      sizes: [
        { size: "small", priceModifier: 0 },
        { size: "medium", priceModifier: 0.75 },
        { size: "large", priceModifier: 1.5 },
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
      name: "Vanilla Syrup",
      price: 0.5,
      category: "syrup",
    },
    {
      name: "Caramel Syrup",
      price: 0.5,
      category: "syrup",
    },
    {
      name: "Hazelnut Syrup",
      price: 0.5,
      category: "syrup",
    },
    {
      name: "Almond Syrup",
      price: 0.5,
      category: "syrup",
    },
    {
      name: "Coconut Syrup",
      price: 0.5,
      category: "syrup",
    },
    {
      name: "Chocolate Syrup",
      price: 0.5,
      category: "syrup",
    },
    {
      name: "Maple Syrup",
      price: 0.5,
      category: "syrup",
    },
    // Shots
    {
      name: "Extra Shot",
      price: 0.75,
      category: "shot",
    },
    {
      name: "Double Shot",
      price: 1.0,
      category: "shot",
    },
    // Milk Options
    {
      name: "Oat Milk",
      price: 0.75,
      category: "milk",
    },
    {
      name: "Almond Milk",
      price: 0.75,
      category: "milk",
    },
    {
      name: "Soy Milk",
      price: 0.75,
      category: "milk",
    },
    {
      name: "Whole Milk",
      price: 0.5,
      category: "milk",
    },
    {
      name: "Heavy Cream",
      price: 0.75,
      category: "milk",
    },
    // Toppings
    {
      name: "Whipped Cream",
      price: 0.75,
      category: "topping",
    },
    {
      name: "Cinnamon",
      price: 0.25,
      category: "topping",
    },
    {
      name: "Honey",
      price: 0.5,
      category: "topping",
    },
    {
      name: "Chocolate Powder",
      price: 0.35,
      category: "topping",
    },
    {
      name: "Caramel Drizzle",
      price: 0.5,
      category: "topping",
    },
    {
      name: "Chocolate Drizzle",
      price: 0.5,
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
