import {
  BadRequestException,
  Body,
  Controller,
  Delete,
  Get,
  HttpCode,
  HttpStatus,
  Param,
  Post,
  Put,
  Query,
  UseGuards,
} from "@nestjs/common";
import { z } from "zod";
import { Roles } from "../../common/decorators/roles.decorator";
import { JwtAuthGuard } from "../../common/guards/jwt-auth.guard";
import { RolesGuard } from "../../common/guards/roles.guard";
import { AdminService } from "./admin.service";

const updateOrderSchema = z.object({
  status: z
    .enum(["pending", "accepted", "rejected", "completed", "cancelled"])
    .optional(),
  estimatedPrepTime: z.number().optional(),
  rejectionReason: z
    .enum([
      "no_milk",
      "no_coffee_beans",
      "size_unavailable",
      "equipment_issue",
      "custom",
    ])
    .optional(),
  rejectionComment: z.string().optional(),
});

const createMenuItemSchema = z.object({
  name: z.string().min(1),
  description: z.string().optional(),
  category: z.enum([
    "hot",
    "cold",
    "latte",
    "cappuccino",
    "espresso",
    "specialty",
  ]),
  basePrice: z.number().min(0),
  sizes: z.array(
    z.object({
      size: z.enum(["small", "medium", "large"]),
      priceModifier: z.number(),
    })
  ),
  image: z.string().optional(),
  preparationTime: z.number().default(5),
});

const updateMenuItemSchema = z.object({
  name: z.string().optional(),
  description: z.string().optional(),
  category: z
    .enum(["hot", "cold", "latte", "cappuccino", "espresso", "specialty"])
    .optional(),
  basePrice: z.number().min(0).optional(),
  sizes: z
    .array(
      z.object({
        size: z.enum(["small", "medium", "large"]),
        priceModifier: z.number(),
      })
    )
    .optional(),
  image: z.string().optional(),
  isAvailable: z.boolean().optional(),
  preparationTime: z.number().optional(),
});

const createToppingSchema = z.object({
  name: z.string().min(1),
  price: z.number().min(0),
  category: z.enum(["syrup", "shot", "milk", "topping"]).default("topping"),
  description: z.string().optional(),
});

const updateToppingSchema = z.object({
  name: z.string().min(1).optional(),
  price: z.number().min(0).optional(),
  category: z.enum(["syrup", "shot", "milk", "topping"]).optional(),
  description: z.string().optional(),
});

const createLocationSchema = z.object({
  name: z.string().min(1),
  address: z.string().min(1),
  city: z.string().min(1),
  zipCode: z.string().min(1),
  phone: z.string().optional(),
  latitude: z.number().optional(),
  longitude: z.number().optional(),
});

const updateLocationSchema = z.object({
  name: z.string().optional(),
  address: z.string().optional(),
  city: z.string().optional(),
  zipCode: z.string().optional(),
  phone: z.string().optional(),
  latitude: z.number().optional(),
  longitude: z.number().optional(),
  isActive: z.boolean().optional(),
  workingHours: z
    .record(z.string(), z.object({ open: z.string(), close: z.string() }))
    .optional(),
});

@Controller("admin")
@UseGuards(JwtAuthGuard, RolesGuard)
@Roles("admin")
export class AdminController {
  constructor(private readonly adminService: AdminService) {}

  // ─── Orders ──────────────────────────
  @Get("orders")
  async getOrders(
    @Query("startDate") startDate?: string,
    @Query("endDate") endDate?: string,
    @Query("sortBy") sortBy?: string,
    @Query("sortOrder") sortOrder?: string,
    @Query("page") page?: string,
    @Query("limit") limit?: string
  ) {
    return this.adminService.getOrders({
      startDate,
      endDate,
      sortBy,
      sortOrder,
      page: page ? parseInt(page) : undefined,
      limit: limit ? parseInt(limit) : undefined,
    });
  }

  @Put("orders/:id")
  async updateOrder(@Param("id") id: string, @Body() body: any) {
    const validation = updateOrderSchema.safeParse(body);
    if (!validation.success) throw new BadRequestException("Invalid input");
    return this.adminService.updateOrder(id, validation.data);
  }

  // ─── Menu Items ──────────────────────
  @Get("menu")
  async getMenuItems() {
    return this.adminService.getMenuItems();
  }

  @Post("menu")
  @HttpCode(HttpStatus.CREATED)
  async createMenuItem(@Body() body: any) {
    const validation = createMenuItemSchema.safeParse(body);
    if (!validation.success) throw new BadRequestException("Invalid input");
    return this.adminService.createMenuItem(validation.data);
  }

  @Put("menu/:id")
  async updateMenuItem(@Param("id") id: string, @Body() body: any) {
    const validation = updateMenuItemSchema.safeParse(body);
    if (!validation.success) throw new BadRequestException("Invalid input");
    return this.adminService.updateMenuItem(id, validation.data);
  }

  @Delete("menu/:id")
  async deleteMenuItem(@Param("id") id: string) {
    return this.adminService.deleteMenuItem(id);
  }

  // ─── Toppings ────────────────────────
  @Get("toppings")
  async getToppings() {
    return this.adminService.getToppings();
  }

  @Post("toppings")
  @HttpCode(HttpStatus.CREATED)
  async createTopping(@Body() body: any) {
    const validation = createToppingSchema.safeParse(body);
    if (!validation.success) throw new BadRequestException("Invalid input");
    return this.adminService.createTopping(validation.data);
  }

  @Put("toppings/:id")
  async updateTopping(@Param("id") id: string, @Body() body: any) {
    const validation = updateToppingSchema.safeParse(body);
    if (!validation.success) throw new BadRequestException("Invalid input");
    return this.adminService.updateTopping(id, validation.data);
  }

  @Delete("toppings/:id")
  async deleteTopping(@Param("id") id: string) {
    return this.adminService.deleteTopping(id);
  }

  // ─── Locations ───────────────────────
  @Get("locations")
  async getLocations() {
    return this.adminService.getLocations();
  }

  @Post("locations")
  @HttpCode(HttpStatus.CREATED)
  async createLocation(@Body() body: any) {
    const validation = createLocationSchema.safeParse(body);
    if (!validation.success) throw new BadRequestException("Invalid input");
    return this.adminService.createLocation(validation.data);
  }

  @Put("locations/:id")
  async updateLocation(@Param("id") id: string, @Body() body: any) {
    const validation = updateLocationSchema.safeParse(body);
    if (!validation.success) throw new BadRequestException("Invalid input");
    return this.adminService.updateLocation(id, validation.data);
  }

  @Delete("locations/:id")
  async deleteLocation(@Param("id") id: string) {
    return this.adminService.deleteLocation(id);
  }

  @Put("locations/:id/hours")
  async updateLocationHours(@Param("id") id: string, @Body() body: any) {
    return this.adminService.updateLocationHours(id, body);
  }

  // ─── Analytics ───────────────────────
  @Get("analytics")
  async getAnalytics(@Query("timeRange") timeRange?: string) {
    return this.adminService.getAnalytics(timeRange || "30days");
  }

  // ─── Loyalty Analytics ───────────────
  @Get("loyalty/analytics")
  async getLoyaltyAnalytics(@Query("timeRange") timeRange?: string) {
    return this.adminService.getLoyaltyAnalytics(timeRange || "30d");
  }
}
