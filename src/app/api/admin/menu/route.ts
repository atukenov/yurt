import { NextResponse } from "next/server";
import { getServerSession } from "next-auth";

import connectDB from "#utils/database/connect";
import { Accounts } from "#utils/database/models/account";
import { Menus, TMenu } from "#utils/database/models/menu";
import { authOptions } from "#utils/helper/authHelper";
import { CatchNextResponse } from "#utils/helper/common";

// Create new menu item
export async function POST(req: Request) {
  try {
    await connectDB();
    const session = await getServerSession(authOptions);

    if (!session) throw { status: 401, message: "Authentication Required" };
    if (session.role !== "admin")
      throw { status: 403, message: "Unauthorized" };

    const data = await req.json();
    const {
      name,
      description,
      category,
      price,
      taxPercent,
      foodType,
      veg,
      image,
      hidden,
    } = data;

    // Validation
    if (!name || name.trim() === "")
      throw { status: 400, message: "Menu item name is required" };
    if (!category || category.trim() === "")
      throw { status: 400, message: "Category is required" };
    if (price === undefined || price === null || isNaN(price))
      throw { status: 400, message: "Valid price is required" };

    // Get restaurant account to verify ownership
    const account = await Accounts.findOne({ username: session.username });
    if (!account)
      throw { status: 404, message: "Restaurant account not found" };

    // Create new menu item
    const newMenuItem = new Menus({
      name: name.trim(),
      restaurantID: account._id,
      description: description?.trim() || "",
      category: category.trim(),
      price: parseFloat(price),
      taxPercent: taxPercent ? parseFloat(taxPercent) : 0,
      foodType: foodType || "non-veg",
      veg: veg || "non-veg",
      image: image?.trim() || "",
      hidden: hidden || false,
    });

    await newMenuItem.save();

    return NextResponse.json(
      {
        status: 200,
        message: "Menu item created successfully",
        data: newMenuItem,
      },
      {
        headers: {
          "Cache-Control": "no-store, no-cache, must-revalidate, max-age=0",
          Pragma: "no-cache",
        },
      }
    );
  } catch (err) {
    console.log(err);
    return CatchNextResponse(err);
  }
}

// Update existing menu item
export async function PUT(req: Request) {
  try {
    await connectDB();
    const session = await getServerSession(authOptions);

    if (!session) throw { status: 401, message: "Authentication Required" };
    if (session.role !== "admin")
      throw { status: 403, message: "Unauthorized" };

    const data = await req.json();
    const {
      id,
      name,
      description,
      category,
      price,
      taxPercent,
      foodType,
      veg,
      image,
      hidden,
    } = data;

    // Validation
    if (!id) throw { status: 400, message: "Menu item ID is required" };
    if (!name || name.trim() === "")
      throw { status: 400, message: "Menu item name is required" };
    if (!category || category.trim() === "")
      throw { status: 400, message: "Category is required" };
    if (price === undefined || price === null || isNaN(price))
      throw { status: 400, message: "Valid price is required" };

    // Get restaurant account to verify ownership
    const account = await Accounts.findOne({ username: session.username });
    if (!account)
      throw { status: 404, message: "Restaurant account not found" };

    // Find and update menu item
    const menuItem = await Menus.findById<TMenu>(id);
    if (!menuItem) throw { status: 404, message: "Menu item not found" };

    // Verify ownership
    if (menuItem.restaurantID.toString() !== account._id.toString()) {
      throw {
        status: 403,
        message: "You do not have permission to edit this menu item",
      };
    }

    // Update fields
    menuItem.name = name.trim();
    menuItem.description = description?.trim() || "";
    menuItem.category = category.trim();
    menuItem.price = parseFloat(price);
    menuItem.taxPercent = taxPercent ? parseFloat(taxPercent) : 0;
    menuItem.foodType = foodType || "non-veg";
    menuItem.veg = veg || "non-veg";
    menuItem.image = image?.trim() || "";
    menuItem.hidden = hidden || false;

    await menuItem.save();

    return NextResponse.json(
      {
        status: 200,
        message: "Menu item updated successfully",
        data: menuItem,
      },
      {
        headers: {
          "Cache-Control": "no-store, no-cache, must-revalidate, max-age=0",
          Pragma: "no-cache",
        },
      }
    );
  } catch (err) {
    console.log(err);
    return CatchNextResponse(err);
  }
}

export const dynamic = "force-dynamic";
