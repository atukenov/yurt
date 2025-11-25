import { NextResponse } from "next/server";

import connectDB from "#utils/database/connect";
import { Profiles, TProfile } from "#utils/database/models/profile";
import { CatchNextResponse } from "#utils/helper/common";

export async function GET(req: Request) {
  try {
    const url = new URL(req.url);
    const restaurantId = url.searchParams.get("restaurantId");

    if (!restaurantId) {
      throw { status: 400, message: "Restaurant ID is required" };
    }

    await connectDB();
    const profile = await Profiles.findOne<TProfile>({
      restaurantID: restaurantId,
    }).lean();

    if (!profile) {
      throw {
        status: 404,
        message: `Restaurant with ID: ${restaurantId} not found`,
      };
    }

    return NextResponse.json({
      addresses: profile.addresses || [],
      restaurantName: profile.name,
    });
  } catch (err) {
    console.log(err);
    return CatchNextResponse(err);
  }
}

export const dynamic = "force-dynamic";
