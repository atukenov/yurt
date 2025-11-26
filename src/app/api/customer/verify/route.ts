import { NextResponse } from "next/server";

import connectDB from "#utils/database/connect";
import { Customers } from "#utils/database/models/customer";
import { CatchNextResponse } from "#utils/helper/common";

export async function POST(req: Request) {
  try {
    await connectDB();
    const { phone } = await req.json();

    if (!phone) throw { status: 400, message: "Phone number is required" };

    const customer = (await Customers.findOne({ phone }).lean()) as {
      fname: string;
      lname: string;
      phone: string;
    } | null;

    if (!customer) {
      return NextResponse.json(
        { status: 404, exists: false, message: "Customer not found" },
        { status: 404 }
      );
    }

    return NextResponse.json({
      status: 200,
      exists: true,
      customer: {
        fname: customer.fname,
        lname: customer.lname,
        phone: customer.phone,
      },
    });
  } catch (err) {
    console.log(err);
    return CatchNextResponse(err);
  }
}

export const dynamic = "force-dynamic";
