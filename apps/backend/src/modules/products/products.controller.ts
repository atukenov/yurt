import { Controller, Get, Query } from "@nestjs/common";
import { ProductsService } from "./products.service";

@Controller("menu")
export class ProductsController {
  constructor(private readonly productsService: ProductsService) {}

  @Get("items")
  async getMenuItems(
    @Query("category") category?: string,
    @Query("search") search?: string
  ) {
    return this.productsService.getMenuItems(category, search);
  }

  @Get("toppings")
  async getToppings() {
    return this.productsService.getToppings();
  }
}
