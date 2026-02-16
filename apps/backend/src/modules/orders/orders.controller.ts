import {
  BadRequestException,
  Body,
  Controller,
  Get,
  HttpCode,
  HttpStatus,
  Param,
  Post,
  UseGuards,
} from "@nestjs/common";
import { z } from "zod";
import { CurrentUser } from "../../common/decorators/current-user.decorator";
import { JwtAuthGuard } from "../../common/guards/jwt-auth.guard";
import { OrdersService } from "./orders.service";

const createOrderSchema = z.object({
  locationId: z.string(),
  items: z.array(
    z.object({
      menuItemId: z.string(),
      quantity: z.number().min(1),
      size: z.enum(["small", "medium", "large"]),
      toppings: z.array(z.string()).optional(),
      specialInstructions: z.string().optional(),
    })
  ),
  totalPrice: z.number().min(0),
  paymentMethod: z.enum(["kaspi", "applepay"]).default("kaspi"),
  notes: z.string().optional(),
});

@Controller("orders")
@UseGuards(JwtAuthGuard)
export class OrdersController {
  constructor(private readonly ordersService: OrdersService) {}

  @Post()
  @HttpCode(HttpStatus.CREATED)
  async createOrder(@CurrentUser("id") userId: string, @Body() body: any) {
    const validation = createOrderSchema.safeParse(body);
    if (!validation.success) {
      throw new BadRequestException("Invalid input");
    }
    return this.ordersService.createOrder(userId, validation.data);
  }

  @Get()
  async getOrders(@CurrentUser("id") userId: string) {
    return this.ordersService.getCustomerOrders(userId);
  }

  @Get(":id")
  async getOrder(@Param("id") id: string, @CurrentUser() user: any) {
    return this.ordersService.getOrderById(id, user.id, user.role);
  }
}
