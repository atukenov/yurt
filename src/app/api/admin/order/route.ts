import { NextResponse } from "next/server";
import { getServerSession } from "next-auth";

import connectDB from "#utils/database/connect";
import { Customers } from "#utils/database/models/customer";
import { Menus } from "#utils/database/models/menu";
import { Orders, TOrder } from "#utils/database/models/order";
import { authOptions } from "#utils/helper/authHelper";
import { CatchNextResponse } from "#utils/helper/common";

export async function GET() {
  try {
    await connectDB();
    const session = await getServerSession(authOptions);
    if (!session) throw { status: 401, message: "Authentication Required" };

    const restaurantID = session?.username;
    const orders =
      (await Orders.find({ restaurantID })
        .populate({ path: "customer", model: Customers })
        .populate({ path: "products.product", model: Menus })
        .lean()) ?? [];

    orders?.forEach?.((order: any) => {
      if (order?.products)
        order.products = order?.products?.map((product: any) => {
          // Spread the populated product (menu) fields into the product object
          const { product: menuData, ...productFields } = product;
          return {
            ...productFields,
            ...menuData, // Menu data is now a plain object due to .lean()
            product: menuData?._id, // Keep the product reference
          };
        });
    });

    return NextResponse.json(orders, {
      headers: {
        "Cache-Control": "no-store, no-cache, must-revalidate, max-age=0",
        Pragma: "no-cache",
        Expires: "0",
      },
    });
  } catch (err) {
    console.log(err);
    return CatchNextResponse(err);
  }
}

export const dynamic = "force-dynamic";
