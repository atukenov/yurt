import { z } from "zod";

// Auth Schemas
export const LoginSchema = z.object({
  email: z.string().email("Invalid email address"),
  password: z.string().min(6, "Password must be at least 6 characters"),
});

export const RegisterSchema = z.object({
  name: z.string().min(2, "Name must be at least 2 characters"),
  email: z.string().email("Invalid email address"),
  phone: z.string().min(10, "Phone must be at least 10 digits").optional(),
  password: z.string().min(6, "Password must be at least 6 characters"),
});

// Checkout Schema
export const CheckoutSchema = z.object({
  locationId: z.string().min(1, "Please select a location"),
  paymentMethod: z
    .enum(["cash", "card", "stripe"])
    .refine(
      (method) => ["cash", "card", "stripe"].includes(method),
      "Invalid payment method"
    ),
  notes: z
    .string()
    .max(500, "Notes must be less than 500 characters")
    .optional(),
});

// Menu Item Schema
export const MenuItemSchema = z.object({
  name: z
    .string()
    .min(1, "Item name is required")
    .max(100, "Item name must be less than 100 characters"),
  description: z
    .string()
    .max(500, "Description must be less than 500 characters"),
  basePrice: z.number().positive("Price must be greater than 0"),
  category: z.enum([
    "hot",
    "cold",
    "latte",
    "cappuccino",
    "espresso",
    "specialty",
  ]),
  available: z.boolean().default(true),
});

// Topping Schema
export const ToppingSchema = z.object({
  name: z
    .string()
    .min(1, "Topping name is required")
    .max(50, "Name must be less than 50 characters"),
  price: z.number().nonnegative("Price must be 0 or greater"),
  category: z
    .string()
    .min(1, "Category is required")
    .max(50, "Category must be less than 50 characters"),
});

// Location Schema
export const LocationSchema = z.object({
  name: z
    .string()
    .min(1, "Location name is required")
    .max(100, "Name must be less than 100 characters"),
  address: z
    .string()
    .min(5, "Address must be at least 5 characters")
    .max(200, "Address must be less than 200 characters"),
  phone: z.string().min(10, "Phone must be at least 10 digits"),
  email: z.string().email("Invalid email address"),
});

// Review Schema
export const ReviewSchema = z.object({
  rating: z
    .number()
    .min(1, "Rating must be at least 1")
    .max(5, "Rating must be at most 5"),
  comment: z
    .string()
    .max(1000, "Comment must be less than 1000 characters")
    .optional(),
});

// Order Notes Schema (for admin feedback)
export const OrderNotesSchema = z.object({
  notes: z
    .string()
    .min(1, "Notes are required")
    .max(500, "Notes must be less than 500 characters"),
});

// Password Reset Schema
export const PasswordResetSchema = z.object({
  email: z.string().email("Invalid email address"),
});

export const PasswordUpdateSchema = z
  .object({
    newPassword: z.string().min(6, "Password must be at least 6 characters"),
    confirmPassword: z.string(),
  })
  .refine((data) => data.newPassword === data.confirmPassword, {
    message: "Passwords don't match",
    path: ["confirmPassword"],
  });

// Search Query Schema
export const SearchSchema = z.object({
  query: z.string().max(100, "Search query must be less than 100 characters"),
});

// Filter Schema
export const OrderFilterSchema = z.object({
  status: z
    .enum(["all", "pending", "accepted", "rejected", "completed"])
    .optional(),
  startDate: z.date().optional(),
  endDate: z.date().optional(),
  locationId: z.string().optional(),
});

// Type Exports
export type LoginInput = z.infer<typeof LoginSchema>;
export type RegisterInput = z.infer<typeof RegisterSchema>;
export type CheckoutInput = z.infer<typeof CheckoutSchema>;
export type MenuItemInput = z.infer<typeof MenuItemSchema>;
export type ToppingInput = z.infer<typeof ToppingSchema>;
export type LocationInput = z.infer<typeof LocationSchema>;
export type ReviewInput = z.infer<typeof ReviewSchema>;
export type OrderNotesInput = z.infer<typeof OrderNotesSchema>;
export type PasswordResetInput = z.infer<typeof PasswordResetSchema>;
export type PasswordUpdateInput = z.infer<typeof PasswordUpdateSchema>;
export type SearchInput = z.infer<typeof SearchSchema>;
export type OrderFilterInput = z.infer<typeof OrderFilterSchema>;

// Validation utility function
export function validateFormData<T>(
  schema: z.ZodSchema<T>,
  data: unknown
): { success: boolean; data?: T; errors?: Record<string, string> } {
  try {
    const validated = schema.parse(data);
    return { success: true, data: validated };
  } catch (error) {
    if (error instanceof z.ZodError) {
      const errors: Record<string, string> = {};
      error.issues.forEach((issue) => {
        const path = issue.path.join(".");
        errors[path] = issue.message;
      });
      return { success: false, errors };
    }
    return { success: false, errors: { general: "Validation failed" } };
  }
}
