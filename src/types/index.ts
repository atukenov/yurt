export interface IUser {
  _id: string;
  email: string;
  name: string;
  phone?: string;
  role: "customer" | "admin";
  image?: string;
  createdAt: Date;
  updatedAt: Date;
}

export interface IMenuItem {
  _id: string;
  name: string;
  description?: string;
  category: "hot" | "cold" | "latte" | "cappuccino" | "espresso" | "specialty";
  basePrice: number;
  sizes: Array<{
    size: "small" | "medium" | "large";
    priceModifier: number;
  }>;
  image?: string;
  isAvailable: boolean;
  preparationTime: number;
  createdAt: Date;
  updatedAt: Date;
}

export interface ITopping {
  _id: string;
  name: string;
  price: number;
  category?: "syrup" | "shot" | "milk" | "topping";
  description?: string;
  createdAt: Date;
  updatedAt: Date;
}

export interface ILocation {
  _id: string;
  name: string;
  address: string;
  city: string;
  zipCode: string;
  phone?: string;
  latitude?: number;
  longitude?: number;
  workingHours: Record<string, { open: string; close: string }>;
  isActive: boolean;
  availableMenuItems: string[];
  createdAt: Date;
  updatedAt: Date;
}

export interface IOrderItem {
  menuItem: string | IMenuItem;
  quantity: number;
  size: "small" | "medium" | "large";
  toppings: string[];
  specialInstructions?: string;
  priceAtOrder: number;
}

export interface IOrder {
  _id: string;
  orderNumber: string;
  customer: string | IUser;
  location: string | ILocation;
  items: IOrderItem[];
  status: "pending" | "accepted" | "rejected" | "completed" | "cancelled";
  totalPrice: number;
  estimatedPrepTime?: number;
  rejectionReason?: string;
  rejectionComment?: string;
  paymentStatus: "pending" | "completed" | "failed";
  paymentMethod: "cash" | "card" | "stripe";
  notes?: string;
  createdAt: Date;
  updatedAt: Date;
}

export interface IReview {
  _id: string;
  order: string;
  customer: string | IUser;
  menuItem: string | IMenuItem;
  rating: number;
  comment?: string;
  isApproved?: boolean;
  createdAt: Date;
  updatedAt: Date;
}

export interface INotification {
  _id: string;
  order: string;
  recipient: string;
  type: "order_accepted" | "order_rejected" | "order_completed";
  message?: string;
  read: boolean;
  createdAt: Date;
  updatedAt: Date;
}
