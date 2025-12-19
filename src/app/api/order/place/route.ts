import { getServerSession } from "next-auth";
import { NextResponse } from "next/server";

import connectDB from "#utils/database/connect";
import { Accounts } from "#utils/database/models/account";
import { Menus, TMenu } from "#utils/database/models/menu";
import { Orders, TOrder, TProduct } from "#utils/database/models/order";
import { authOptions } from "#utils/helper/authHelper";
import { CatchNextResponse } from "#utils/helper/common";
import { notificationManager } from "#utils/helper/notificationManager";

export async function POST(req: Request) {
  try {
    const session = await getServerSession(authOptions);
    const body = await req.json();

    if (!session) throw { status: 401, message: "Authentication Required" };
    if (!body?.products.length)
      throw { status: 400, message: "Can't place order on empty cart" };

    await connectDB();
    const products: TProduct[] = await Promise.all(
      body?.products?.map(async (product: TProduct & { _id: string }) => {
        const menuItem = await Menus.findById<TMenu>(product?._id).lean();

        if (!menuItem)
          throw { status: 404, message: "Ordered product(s) not found." };
        return {
          product: product?._id,
          quantity: product?.quantity,
          price: menuItem?.price,
          tax: ((menuItem?.price * menuItem?.taxPercent) / 100).toFixed(2),
        };
      })
    );

    const restaurantID = session?.restaurant?.username;
    const address = session?.restaurant?.address;
    const customer = session?.customer?._id;
    const order = await Orders.findOne<TOrder>({
      restaurantID,
      customer,
      state: "active",
    });

    if (order) {
      order.products = [...order.products, ...products];
      await order.save();

      return NextResponse.json({
        status: 200,
        message: "Additional items ordered successfully",
      });
    }

    const newOrder = new Orders({
      restaurantID,
      address,
      customer,
      products: products,
    });
    await newOrder.save();

    // Calculate total amount
    const totalAmount = products.reduce((sum, p) => {
      return sum + (parseFloat(p.price) * p.quantity + parseFloat(p.tax));
    }, 0);

    // Get customer name and phone from account
    const customerAccount = await Accounts.findById(customer).lean();
    const customerName = customerAccount?.name || "Customer";
    const customerPhone = customerAccount?.phone || "N/A";

    // Broadcast notification to kitchen
    notificationManager.notifyKitchen(restaurantID, {
      type: "NEW_ORDER",
      orderId: newOrder._id.toString(),
      restaurantID,
      customerName,
      customerPhone,
      itemCount: products.length,
      totalAmount: parseFloat(totalAmount.toFixed(2)),
      address,
      timestamp: Date.now(),
      message: `New order from ${customerName}`,
    });

    return NextResponse.json({
      status: 200,
      message: "Order placed successfully",
    });
  } catch (err) {
    console.log(err);
    return CatchNextResponse(err);
  }
}

export const dynamic = "force-dynamic";
