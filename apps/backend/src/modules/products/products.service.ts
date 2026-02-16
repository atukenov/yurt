import { Injectable } from "@nestjs/common";
import { InjectModel } from "@nestjs/mongoose";
import { Model } from "mongoose";
import {
  MenuItem,
  MenuItemDocument,
  Topping,
  ToppingDocument,
} from "../../database/schemas";

@Injectable()
export class ProductsService {
  constructor(
    @InjectModel(MenuItem.name) private menuItemModel: Model<MenuItemDocument>,
    @InjectModel(Topping.name) private toppingModel: Model<ToppingDocument>
  ) {}

  async getMenuItems(category?: string, search?: string) {
    const query: any = { isAvailable: true };
    if (category) query.category = category;
    if (search) query.$text = { $search: search };

    const items = await this.menuItemModel.find(query).limit(50);
    return { items };
  }

  async getToppings() {
    const toppings = await this.toppingModel.find().limit(50);
    return { toppings };
  }
}
