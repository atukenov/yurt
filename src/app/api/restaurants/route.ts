import { NextResponse } from "next/server";

import connectDB from "#utils/database/connect";
import { Profiles } from "#utils/database/models/profile";
import { CatchNextResponse } from "#utils/helper/common";

export async function GET() {
  try {
    await connectDB();

    const restaurants = await Profiles.find(
      {},
      {
        name: 1,
        restaurantID: 1,
        description: 1,
        avatar: 1,
        addresses: 1,
      }
    ).lean();

    return NextResponse.json(
      { status: 200, restaurants },
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
